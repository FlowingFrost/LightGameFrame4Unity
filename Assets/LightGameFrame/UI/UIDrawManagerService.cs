using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LightGameFrame.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightGameFrame.UIDrawer
{
    /// <summary>
    /// UI 窗口管理器。Play Mode 下管理窗口的创建、层级、焦点、动画与生命周期。
    /// </summary>
    [AutoService(Mode = AutoServiceMode.PlayMode)]
    public class UIDrawManagerService : MonoServiceBase<UIDrawManagerService>
    {
        [Header("Sorting")]
        [SerializeField] private bool _useSiblingIndexOrdering = true;

        [Header("Interaction")]
        [SerializeField] private bool _disableCoveredInput = true;
        [SerializeField] private bool _focusOnPointerDown = true;

        private readonly Dictionary<string, WindowHandle> _windows = new Dictionary<string, WindowHandle>();
        private readonly List<string> _rootStack = new List<string>();

        private UIDocument _rootDocument;
        private VisualElement _rootVisualContainer;

        private UITransition _cachedDefaultTransition;

        public static UIDrawManagerService Instance { get; private set; }

        public override int ServicePriority => 80;

        // ===== Public API =====

        public bool IsOpen(string windowId)
            => !string.IsNullOrEmpty(windowId) && _windows.ContainsKey(windowId);

        public bool IsMinimized(string windowId)
            => !string.IsNullOrEmpty(windowId) && _windows.TryGetValue(windowId, out var h) && h.IsMinimized;

        /// <summary>
        /// 打开纯 UI 窗口（无 Behaviour）。
        /// </summary>
        public WindowHandle Open(VisualTreeAsset uxml, OpenWindowOptions options = null)
        {
            if (uxml == null)
            {
                Debug.LogWarning("[UIDrawManager] Open(uxml) failed: uxml is null.");
                return null;
            }

            options ??= new OpenWindowOptions();
            var rootVE = uxml.Instantiate();
            if (rootVE == null)
            {
                Debug.LogWarning("[UIDrawManager] Open(uxml) failed: Instantiate returned null.");
                return null;
            }

            var windowId = EnsureUniqueWindowId(options.WindowId, null);
            var parentHandle = !string.IsNullOrEmpty(options.ParentWindowId)
                ? _windows.GetValueOrDefault(options.ParentWindowId)
                : null;

            WindowHandle handle;
            var isTopLevel = parentHandle == null;

            if (isTopLevel)
            {
                EnsureRootContainer();
                _rootVisualContainer.Add(rootVE);
                handle = new WindowHandle(windowId, rootVE, null, null);
            }
            else
            {
                var parentContent = parentHandle.RootVisualElement;
                parentContent.Add(rootVE);
                handle = new WindowHandle(windowId, rootVE, null, parentHandle.Id);
            }

            // Stretch to parent
            rootVE.StretchToParentSize();
            rootVE.pickingMode = PickingMode.Ignore;

            RegisterWindow(handle, options);

            if (options.Focus)
                Focus(handle.Id);
            else
                UpdateRootInteractable();

            if (options.PlayTransition && options.SelfEnterOverride != null)
                StartCoroutine(PlayEnterTransition(handle, options.SelfEnterOverride));

            return handle;
        }

        /// <summary>
        /// 打开 Prefab 窗口（含 Behaviour）。
        /// </summary>
        public WindowHandle Open(GameObject prefab, OpenWindowOptions options = null)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[UIDrawManager] Open(prefab) failed: prefab is null.");
                return null;
            }

            options ??= new OpenWindowOptions();

            var instance = Instantiate(prefab);
            var behaviour = instance.GetComponent<UIInterfaceBehaviour>();
            if (behaviour == null)
            {
                Debug.LogWarning("[UIDrawManager] Open(prefab) failed: UIInterfaceBehaviour missing.");
                Destroy(instance);
                return null;
            }

            if (!behaviour.TryCreateEmbeddedRoot(out var rootVE))
            {
                Debug.LogWarning("[UIDrawManager] Open(prefab) failed: cannot create embedded root.");
                Destroy(instance);
                return null;
            }

            var windowId = EnsureUniqueWindowId(options.WindowId, instance);

            var parentHandle = !string.IsNullOrEmpty(options.ParentWindowId)
                ? _windows.GetValueOrDefault(options.ParentWindowId)
                : null;

            var previousTopRootId = GetTopRootId();
            WindowHandle handle;

            if (parentHandle == null)
            {
                EnsureRootContainer();
                instance.transform.SetParent(_rootDocument.transform, false);
                _rootVisualContainer.Add(rootVE);
                handle = new WindowHandle(windowId, rootVE, behaviour, null);
            }
            else
            {
                instance.transform.SetParent(parentHandle.Behaviour?.transform, false);
                parentHandle.RootVisualElement.Add(rootVE);
                handle = new WindowHandle(windowId, rootVE, behaviour, parentHandle.Id);
            }

            rootVE.pickingMode = PickingMode.Ignore;
            rootVE.StretchToParentSize();
            if (rootVE.childCount > 0)
                rootVE[0].StretchToParentSize();

            ApplyDefaultTransitionIfNeeded(behaviour);
            ApplyWindowChromeOptions(behaviour, options);

            // Deferred size/position
            rootVE.schedule.Execute(() =>
            {
                ApplyWindowSize(behaviour, options, parentHandle);
                ApplyWindowPosition(behaviour, options, parentHandle);
            }).ExecuteLater(0);

            RegisterWindow(handle, options);

            if (options.Focus)
                Focus(handle.Id);
            else
                UpdateRootInteractable();

            // Animate
            if (handle.IsTopLevel && options.PlayTransition)
            {
                StartCoroutine(PlayOpenTransitions(handle, previousTopRootId, options));
            }
            else if (!handle.IsTopLevel && options.PlayTransition)
            {
                StartCoroutine(PlayChildEnterTransition(handle, options));
            }

            return handle;
        }

        public void Close(string windowId, CloseWindowOptions options = null)
        {
            if (string.IsNullOrEmpty(windowId)) return;
            if (!_windows.TryGetValue(windowId, out var handle)) return;

            options ??= new CloseWindowOptions();

            if (!options.CloseRootFamily)
            {
                if (options.PlayTransition)
                {
                    StartCoroutine(PlaySubtreeCloseTransition(handle, options));
                    return;
                }
                var subtreeIds = CollectSubtreeWindowIds(handle.Id);
                foreach (var id in subtreeIds)
                    RemoveWindow(id);
                ApplyRootSorting();
                UpdateRootInteractable();
                return;
            }

            if (handle.IsTopLevel && options.PlayTransition)
            {
                StartCoroutine(PlayCloseTransitions(windowId, options));
                return;
            }

            var rootId = GetRootId(windowId);
            var relatedWindows = _windows.Values
                .Where(w => GetRootId(w.Id) == rootId)
                .Select(w => w.Id)
                .ToList();
            foreach (var id in relatedWindows)
                RemoveWindow(id);
            ApplyRootSorting();
            UpdateRootInteractable();
        }

        public void Close(WindowHandle handle, CloseWindowOptions options = null)
        {
            if (handle == null) return;
            Close(handle.Id, options);
        }

        public void Focus(string windowId)
        {
            if (string.IsNullOrEmpty(windowId)) return;
            if (!_windows.TryGetValue(windowId, out var handle) || handle.IsMinimized) return;

            var rootId = GetRootId(windowId);
            if (!string.IsNullOrEmpty(rootId))
            {
                _rootStack.Remove(rootId);
                _rootStack.Add(rootId);
            }

            if (!handle.IsTopLevel && handle.RootVisualElement != null)
                handle.RootVisualElement.BringToFront();

            ApplyRootSorting();
            UpdateRootInteractable();
        }

        public void Focus(WindowHandle handle)
        {
            if (handle == null) return;
            Focus(handle.Id);
        }

        public void Minimize(string windowId, MinimizeWindowOptions options = null)
        {
            if (string.IsNullOrEmpty(windowId)) return;
            if (!_windows.TryGetValue(windowId, out var handle) || handle.IsMinimized) return;

            options ??= new MinimizeWindowOptions();
            if (options.PlayTransition)
            {
                StartCoroutine(PlayMinimizeTransition(handle, options));
                return;
            }
            ApplyMinimizedState(handle);
            ApplyRootSorting();
            UpdateRootInteractable();
        }

        public void Restore(string windowId, RestoreWindowOptions options = null)
        {
            if (string.IsNullOrEmpty(windowId)) return;
            if (!_windows.TryGetValue(windowId, out var handle) || !handle.IsMinimized) return;

            options ??= new RestoreWindowOptions();
            if (options.PlayTransition)
            {
                StartCoroutine(PlayRestoreTransition(handle, options));
                return;
            }
            ApplyRestoredState(handle, options.Focus);
            ApplyRootSorting();
            UpdateRootInteractable();
        }

        // ===== Behaviour-based overloads (for WindowChrome) =====

        public bool IsMinimized(UIInterfaceBehaviour behaviour)
        {
            if (behaviour == null) return false;
            return TryGetHandle(behaviour, out var handle) && handle.IsMinimized;
        }

        public void Close(UIInterfaceBehaviour behaviour, CloseWindowOptions options = null)
        {
            if (behaviour == null) return;
            if (TryGetHandle(behaviour, out var handle))
                Close(handle.Id, options);
        }

        public void Focus(UIInterfaceBehaviour behaviour)
        {
            if (behaviour == null) return;
            if (TryGetHandle(behaviour, out var handle))
                Focus(handle.Id);
        }

        public void Minimize(UIInterfaceBehaviour behaviour, MinimizeWindowOptions options = null)
        {
            if (behaviour == null) return;
            if (TryGetHandle(behaviour, out var handle))
                Minimize(handle.Id, options);
        }

        public void Restore(UIInterfaceBehaviour behaviour, RestoreWindowOptions options = null)
        {
            if (behaviour == null) return;
            if (TryGetHandle(behaviour, out var handle))
                Restore(handle.Id, options);
        }

        private bool TryGetHandle(UIInterfaceBehaviour behaviour, out WindowHandle handle)
        {
            foreach (var kv in _windows)
            {
                if (kv.Value.Behaviour == behaviour)
                {
                    handle = kv.Value;
                    return true;
                }
            }
            handle = null;
            return false;
        }

        // ===== Lifecycle =====

        protected override void OnInitialize()
        {
            Instance = this;
            EnsureRootContainer();
        }

        protected override void OnCleanup()
        {
            Instance = null;
            var openIds = _windows.Keys.ToList();
            foreach (var id in openIds)
                RemoveWindow(id);

            if (_rootDocument != null)
            {
                Destroy(_rootDocument.gameObject);
                _rootDocument = null;
                _rootVisualContainer = null;
            }
        }

        // ===== Root Container =====

        private void EnsureRootContainer()
        {
            if (_rootDocument != null) return;

            var rootObject = new GameObject("UIRoot");
            rootObject.transform.SetParent(transform, false);
            _rootDocument = rootObject.GetComponent<UIDocument>();
            if (_rootDocument == null)
                _rootDocument = rootObject.AddComponent<UIDocument>();

            // Apply PanelSettings from config
            var config = DataManager.UIConfig.Config;
            if (config?.panelSettings != null)
                _rootDocument.panelSettings = config.panelSettings;
            else
                Debug.LogWarning("[UIDrawManager] UIConfig.panelSettings is not set.");

            _rootVisualContainer = new VisualElement { name = "UIRootContainer" };
            _rootVisualContainer.pickingMode = PickingMode.Ignore;
            _rootVisualContainer.style.position = Position.Relative;
            _rootVisualContainer.StretchToParentSize();
            _rootDocument.rootVisualElement.Add(_rootVisualContainer);
        }

        // ===== Window Registration =====

        private void RegisterWindow(WindowHandle handle, OpenWindowOptions options)
        {
            _windows.Add(handle.Id, handle);
            handle.State = WindowState.Opening;

            if (handle.IsTopLevel)
            {
                _rootStack.Remove(handle.Id);
                _rootStack.Add(handle.Id);
                ApplyRootSorting();
            }

            RegisterFocusHandler(handle);
        }

        private void RemoveWindow(string windowId)
        {
            if (!_windows.TryGetValue(windowId, out var handle)) return;

            if (handle.IsTopLevel)
                _rootStack.Remove(handle.Id);

            handle.State = WindowState.Closed;

            if (handle.RootVisualElement != null)
                handle.RootVisualElement.RemoveFromHierarchy();

            _windows.Remove(windowId);

            if (handle.Behaviour != null)
                Destroy(handle.Behaviour.gameObject);
        }

        // ===== Sorting & Interactability =====

        private void ApplyRootSorting()
        {
            if (!_useSiblingIndexOrdering || _rootVisualContainer == null) return;

            for (var i = 0; i < _rootStack.Count; i++)
            {
                if (!_windows.TryGetValue(_rootStack[i], out var handle)) continue;
                if (handle.IsMinimized || handle.RootVisualElement == null) continue;

                handle.RootVisualElement.RemoveFromHierarchy();
                _rootVisualContainer.Insert(i, handle.RootVisualElement);
            }
        }

        private void UpdateRootInteractable()
        {
            if (!_disableCoveredInput)
            {
                foreach (var handle in _windows.Values)
                {
                    if (!handle.IsTopLevel || handle.IsMinimized) continue;
                    handle.Behaviour?.SetInteractable(true);
                }
                return;
            }

            if (_focusOnPointerDown)
            {
                foreach (var handle in _windows.Values)
                {
                    if (!handle.IsTopLevel || handle.IsMinimized) continue;
                    handle.Behaviour?.SetInteractable(true);
                }
                return;
            }

            var topRootId = _rootStack.Count > 0 ? _rootStack[_rootStack.Count - 1] : null;
            foreach (var handle in _windows.Values)
            {
                if (!handle.IsTopLevel || handle.IsMinimized) continue;
                handle.Behaviour?.SetInteractable(handle.Id == topRootId);
            }
        }

        // ===== Minimize / Restore =====

        private void ApplyMinimizedState(WindowHandle handle)
        {
            if (handle == null) return;

            if (handle.IsTopLevel)
                _rootStack.Remove(handle.Id);

            handle.IsMinimized = true;
            handle.Behaviour?.SetInteractable(false);
            if (handle.RootVisualElement != null)
                handle.RootVisualElement.style.display = DisplayStyle.None;
        }

        private void ApplyRestoredState(WindowHandle handle, bool focus)
        {
            if (handle == null) return;

            handle.IsMinimized = false;
            if (handle.RootVisualElement != null)
                handle.RootVisualElement.style.display = DisplayStyle.Flex;

            if (handle.IsTopLevel)
            {
                _rootStack.Remove(handle.Id);
                _rootStack.Add(handle.Id);
            }
            else if (handle.RootVisualElement != null)
            {
                handle.RootVisualElement.BringToFront();
            }

            if (focus) Focus(handle.Id);
            else UpdateRootInteractable();
        }

        // ===== Focus Handler =====

        private void RegisterFocusHandler(WindowHandle handle)
        {
            if (!_focusOnPointerDown || handle.RootVisualElement == null) return;
            var targetId = handle.IsTopLevel ? handle.Id : (GetRootId(handle.Id) ?? handle.Id);
            handle.RootVisualElement.RegisterCallback<PointerDownEvent>(_ => Focus(targetId));
        }

        // ===== Window Options =====

        private void ApplyDefaultTransitionIfNeeded(UIInterfaceBehaviour behaviour)
        {
            if (behaviour == null || behaviour.Transition != null) return;

            var transition = GetDefaultTransition();
            if (transition != null)
                behaviour.SetTransition(transition);
        }

        private UITransition GetDefaultTransition()
        {
            if (_cachedDefaultTransition != null) return _cachedDefaultTransition;

            var config = DataManager.UIConfig.Config;
            if (config == null || string.IsNullOrEmpty(config.defaultUITransitionResourcePath)) return null;

            _cachedDefaultTransition = Resources.Load<UITransition>(config.defaultUITransitionResourcePath);
            if (_cachedDefaultTransition == null)
                Debug.LogWarning($"[UIDrawManager] Default UITransition not found at: {config.defaultUITransitionResourcePath}");

            return _cachedDefaultTransition;
        }

        private void ApplyWindowChromeOptions(UIInterfaceBehaviour behaviour, OpenWindowOptions options)
        {
            if (behaviour == null || options?.WindowChrome == null || !options.WindowChrome.Enabled) return;

            var chrome = behaviour.GetComponent<WindowChrome>();
            if (chrome == null)
                chrome = behaviour.gameObject.AddComponent<WindowChrome>();

            var o = options.WindowChrome;
            chrome.Configure(o.Title, o.EnableDrag, o.EnableResize, o.EnableToolBar,
                o.ClampToParent, o.MinSize, o.ToolBarButtons,
                o.EnableFullscreenAnimation, o.FullscreenTransitionDuration, o.FullscreenTransitionCurve);
        }

        private void ApplyWindowSize(UIInterfaceBehaviour behaviour, OpenWindowOptions options, WindowHandle parentHandle)
        {
            if (behaviour == null || options?.WindowSize == null) return;
            var root = behaviour.RootVisualElement;
            if (root == null) return;

            var windowRoot = root.Q<VisualElement>("Window") ?? root;
            var size = options.WindowSize.Value;

            if (parentHandle != null)
            {
                var parentContent = parentHandle.RootVisualElement;
                if (parentContent != null)
                {
                    var pw = parentContent.resolvedStyle.width;
                    var ph = parentContent.resolvedStyle.height;
                    if (float.IsNaN(pw) || pw <= 0f) pw = parentContent.layout.width;
                    if (float.IsNaN(ph) || ph <= 0f) ph = parentContent.layout.height;
                    if (!float.IsNaN(pw) && pw > 0f && size.x > pw) size.x = pw;
                    if (!float.IsNaN(ph) && ph > 0f && size.y > ph) size.y = ph;
                }
            }

            if (size.x > 0f) windowRoot.style.width = size.x;
            if (size.y > 0f) windowRoot.style.height = size.y;
        }

        private void ApplyWindowPosition(UIInterfaceBehaviour behaviour, OpenWindowOptions options, WindowHandle parentHandle)
        {
            if (behaviour == null || options?.WindowPosition == null) return;
            var root = behaviour.RootVisualElement;
            if (root == null) return;

            var windowRoot = root.Q<VisualElement>("Window") ?? root;
            var position = options.WindowPosition.Value;

            if (parentHandle != null)
            {
                var parentContent = parentHandle.RootVisualElement;
                if (parentContent != null)
                {
                    var pw = parentContent.resolvedStyle.width;
                    var ph = parentContent.resolvedStyle.height;
                    if (float.IsNaN(pw) || pw <= 0f) pw = parentContent.layout.width;
                    if (float.IsNaN(ph) || ph <= 0f) ph = parentContent.layout.height;
                    if (!float.IsNaN(pw) && pw > 0f)
                        position.x = Mathf.Clamp(position.x, 0f, pw - windowRoot.resolvedStyle.width);
                    if (!float.IsNaN(ph) && ph > 0f)
                        position.y = Mathf.Clamp(position.y, 0f, ph - windowRoot.resolvedStyle.height);
                }
            }

            windowRoot.style.left = position.x;
            windowRoot.style.top = position.y;
        }

        // ===== Transition Coroutines =====

        private IEnumerator PlayEnterTransition(WindowHandle handle, UITransition transition)
        {
            if (transition == null || handle.RootVisualElement == null) yield break;
            yield return StartCoroutine(transition.PlayEnter(handle.RootVisualElement));
            handle.State = WindowState.Open;
        }

        private IEnumerator PlayOpenTransitions(WindowHandle handle, string previousTopRootId, OpenWindowOptions options)
        {
            if (_disableCoveredInput)
                SetAllRootInteractable(false);

            // Cover previous top window
            if (!string.IsNullOrEmpty(previousTopRootId) && _windows.TryGetValue(previousTopRootId, out var prev))
            {
                var cover = options.PreviousCoverOverride ?? prev.Behaviour?.Transition;
                if (cover != null && prev.RootVisualElement != null)
                    yield return StartCoroutine(cover.PlayCover(prev.RootVisualElement));
            }

            // Enter current window
            var enter = options.SelfEnterOverride ?? handle.Behaviour?.Transition;
            if (enter != null && handle.RootVisualElement != null)
                yield return StartCoroutine(enter.PlayEnter(handle.RootVisualElement));

            handle.State = WindowState.Open;
            UpdateRootInteractable();
        }

        private IEnumerator PlayChildEnterTransition(WindowHandle handle, OpenWindowOptions options)
        {
            var enter = options.SelfEnterOverride ?? handle.Behaviour?.Transition;
            if (enter != null && handle.RootVisualElement != null)
                yield return StartCoroutine(enter.PlayEnter(handle.RootVisualElement));
            handle.State = WindowState.Open;
        }

        private IEnumerator PlaySubtreeCloseTransition(WindowHandle handle, CloseWindowOptions options)
        {
            handle.State = WindowState.Closing;
            var exit = options.ExitOverride ?? handle.Behaviour?.Transition;
            if (exit != null && handle.RootVisualElement != null)
                yield return StartCoroutine(exit.PlayExit(handle.RootVisualElement));

            var subtreeIds = CollectSubtreeWindowIds(handle.Id);
            foreach (var id in subtreeIds)
                RemoveWindow(id);
            ApplyRootSorting();
            UpdateRootInteractable();
        }

        private IEnumerator PlayCloseTransitions(string windowId, CloseWindowOptions options)
        {
            if (!_windows.TryGetValue(windowId, out var handle)) yield break;
            handle.State = WindowState.Closing;

            if (_disableCoveredInput)
                SetAllRootInteractable(false);

            var exit = options.ExitOverride ?? handle.Behaviour?.Transition;
            if (exit != null && handle.RootVisualElement != null)
                yield return StartCoroutine(exit.PlayExit(handle.RootVisualElement));

            var rootId = GetRootId(windowId);
            var related = _windows.Values
                .Where(w => GetRootId(w.Id) == rootId)
                .Select(w => w.Id)
                .ToList();

            foreach (var id in related)
                RemoveWindow(id);

            ApplyRootSorting();

            var nextTopId = GetTopRootId();
            if (!string.IsNullOrEmpty(nextTopId) && _windows.TryGetValue(nextTopId, out var next))
            {
                var uncover = options.NextUncoverOverride ?? next.Behaviour?.Transition;
                if (uncover != null && next.RootVisualElement != null)
                    yield return StartCoroutine(uncover.PlayUncover(next.RootVisualElement));
            }

            UpdateRootInteractable();
        }

        private IEnumerator PlayMinimizeTransition(WindowHandle handle, MinimizeWindowOptions options)
        {
            if (handle == null) yield break;
            var exit = options.ExitOverride ?? handle.Behaviour?.Transition;
            if (exit != null && handle.RootVisualElement != null)
                yield return StartCoroutine(exit.PlayExit(handle.RootVisualElement));

            ApplyMinimizedState(handle);
            ApplyRootSorting();
            UpdateRootInteractable();
        }

        private IEnumerator PlayRestoreTransition(WindowHandle handle, RestoreWindowOptions options)
        {
            if (handle == null) yield break;

            handle.IsMinimized = false;
            if (handle.RootVisualElement != null)
                handle.RootVisualElement.style.display = DisplayStyle.Flex;

            if (handle.IsTopLevel)
            {
                _rootStack.Remove(handle.Id);
                _rootStack.Add(handle.Id);
                ApplyRootSorting();
            }
            else if (handle.RootVisualElement != null)
            {
                handle.RootVisualElement.BringToFront();
            }

            var enter = options.EnterOverride ?? handle.Behaviour?.Transition;
            if (enter != null && enter.HasEnterModules && handle.RootVisualElement != null)
                yield return StartCoroutine(enter.PlayEnter(handle.RootVisualElement));
            else
                ResetTransitionStyles(handle.Behaviour);

            if (options.Focus) Focus(handle.Id);
            else UpdateRootInteractable();
        }

        // ===== Helpers =====

        private void SetAllRootInteractable(bool interactable)
        {
            foreach (var handle in _windows.Values)
            {
                if (!handle.IsTopLevel || handle.IsMinimized) continue;
                handle.Behaviour?.SetInteractable(interactable);
            }
        }

        private void ResetTransitionStyles(UIInterfaceBehaviour behaviour)
        {
            if (behaviour == null) return;
            var root = behaviour.RootVisualElement;
            if (root == null) return;
            var target = root.Q<VisualElement>("Window") ?? root;
            target.style.opacity = StyleKeyword.Null;
            target.style.translate = StyleKeyword.Null;
            target.style.scale = StyleKeyword.Null;
            target.style.transformOrigin = StyleKeyword.Null;
        }

        private string EnsureUniqueWindowId(string baseId, GameObject instance)
        {
            var id = baseId;
            if (string.IsNullOrEmpty(id) && instance != null)
                id = instance.name?.Replace("(Clone)", string.Empty).Trim();
            if (string.IsNullOrEmpty(id))
                id = instance != null ? instance.GetInstanceID().ToString() : System.Guid.NewGuid().ToString("N");
            if (!_windows.ContainsKey(id)) return id;

            var suffix = 1;
            var candidate = $"{id}#{suffix}";
            while (_windows.ContainsKey(candidate))
            {
                suffix++;
                candidate = $"{id}#{suffix}";
            }
            Debug.LogWarning($"[UIDrawManager] WindowId '{id}' already exists. Using '{candidate}'.");
            return candidate;
        }

        private string GetTopRootId()
            => _rootStack.Count > 0 ? _rootStack[_rootStack.Count - 1] : null;

        private string GetRootId(string windowId)
        {
            if (!_windows.TryGetValue(windowId, out var handle)) return null;
            var current = handle;
            while (current != null && !current.IsTopLevel)
            {
                if (string.IsNullOrEmpty(current.ParentId)) break;
                if (!_windows.TryGetValue(current.ParentId, out current)) break;
            }
            return current?.Id;
        }

        private List<string> CollectSubtreeWindowIds(string rootId)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(rootId)) return results;

            var queue = new Queue<string>();
            queue.Enqueue(rootId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (results.Contains(current)) continue;
                if (!_windows.ContainsKey(current)) continue;

                results.Add(current);
                foreach (var handle in _windows.Values)
                {
                    if (handle.ParentId == current)
                        queue.Enqueue(handle.Id);
                }
            }

            return results;
        }
    }
}
