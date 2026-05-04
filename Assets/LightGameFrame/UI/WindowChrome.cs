using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightGameFrame.UIDrawer
{
    public class WindowToolbarButton
    {
        public WindowToolbarButton() { }

        public WindowToolbarButton(string name, Action onClick, string tooltip = null)
        {
            Name = name;
            OnClick = onClick;
            Tooltip = tooltip;
        }

        public string Name { get; set; }
        public string Tooltip { get; set; }
        public Action OnClick { get; set; }
    }

    [RequireComponent(typeof(UIInterfaceBehaviour))]
    public class WindowChrome : MonoBehaviour
    {
        private static DataManager.UIConfig UIConfig => DataManager.UIConfig.Instance;

        [Header("Behavior")]
        private bool _enableDrag = true;
        private bool _enableResize = true;
        private bool _enableToolBar = true;
        private bool _clampToParent = true;
        private Vector2 _minSize = new Vector2(320f, 240f);
        private string _titleText;
        private List<WindowToolbarButton> _toolBarButtons = new();

        private UIInterfaceBehaviour _behaviour;
        private VisualElement _root;
        private VisualElement _windowRoot;
        private VisualElement _titleBar;
        private VisualElement _toolBar;
        private VisualElement _toolBarButtonsContainer;
        private Button _minimizeButton;
        private Button _closeButton;
        private Button _fullScreenButton;
        private Label _titleLabel;

        private bool _dragging;
        private bool _resizing;
        private int _activePointerId = -1;
        private Vector2 _pointerStart;
        private Vector2 _windowStartPos;
        private Vector2 _windowStartSize;
        private VisualElement _activeResizeHandle;
        private bool _isFullscreen;
        private Vector2 _restorePosition;
        private Vector2 _restoreSize;
        private Coroutine _fullscreenAnimation;
        private Coroutine _clampAnimation;

        private enum SnapState { None, Full, Left, Right }
        private SnapState _snapState;
        private SnapState _pendingSnapState;

        private VisualElement _aeroSnapRoot;
        private VisualElement _aeroSnapContainer;
        private VisualElement _aeroPreviewWindow;
        private VisualElement _aeroFeedback;
        private bool _aeroSnapActive;
        private bool _aeroSnapVisible;
        private Coroutine _aeroSnapAnimation;
        private bool _aeroSnapDestroyOnHide;

        [System.Flags]
        private enum ResizeEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }
        private ResizeEdge _activeEdge;

        private readonly List<VisualElement> _registeredResizeHandles = new List<VisualElement>();

        private void Awake()
        {
            _behaviour = GetComponent<UIInterfaceBehaviour>();
        }

        private void OnEnable()
        {
            if (_behaviour == null) return;
            _root = _behaviour.RootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[WindowChrome] RootVisualElement is null.");
                return;
            }

            _windowRoot = _root.Q<VisualElement>("Window") ?? _root;
            _titleBar = _windowRoot.Q<VisualElement>(UIConfig.elements.titleBar);
            _toolBar = _windowRoot.Q<VisualElement>(UIConfig.elements.toolBar);
            _toolBarButtonsContainer = _windowRoot.Q<VisualElement>(UIConfig.elements.toolBarButtons);
            _minimizeButton = _windowRoot.Q<Button>(UIConfig.elements.minimizeButton);
            _closeButton = _windowRoot.Q<Button>(UIConfig.elements.closeButton);
            _fullScreenButton = _windowRoot.Q<Button>(UIConfig.elements.fullScreenButton);
            _titleLabel = _windowRoot.Q<Label>(UIConfig.elements.titleLabel);

            ApplyToolBarVisibility();
            ApplyToolBarButtons();
            ApplyTitle();

            if (_windowRoot != null)
                _windowRoot.style.position = Position.Absolute;

            RegisterDrag();
            RegisterResizeHandles();

            if (_closeButton != null)
            {
                _closeButton.AddToClassList("window-command-button-close");
                _closeButton.clicked += OnCloseButtonClicked;
                _closeButton.text = UIConfig.buttonText.close;
            }

            if (_fullScreenButton != null)
            {
                _fullScreenButton.clicked += OnFullScreenButtonClicked;
                UpdateFullScreenButtonText();
            }

            if (_minimizeButton != null)
            {
                _minimizeButton.clicked += OnMinimizeButtonClicked;
                _minimizeButton.text = UIConfig.buttonText.minimize;
            }
        }

        private void OnDisable()
        {
            UnregisterDrag();
            UnregisterResizeHandles();

            if (_closeButton != null)
                _closeButton.clicked -= OnCloseButtonClicked;
            if (_minimizeButton != null)
                _minimizeButton.clicked -= OnMinimizeButtonClicked;
            if (_fullScreenButton != null)
                _fullScreenButton.clicked -= OnFullScreenButtonClicked;

            TearDownAeroSnap();
        }

        // ===== Service Access =====

        private UIDrawManagerService GetService() => UIDrawManagerService.Instance;

        // ===== Button Handlers =====

        private void OnCloseButtonClicked()
        {
            var svc = GetService();
            if (svc == null) return;
            svc.Close(_behaviour, new CloseWindowOptions { PlayTransition = true, CloseRootFamily = false });
        }

        private void OnMinimizeButtonClicked()
        {
            var svc = GetService();
            if (svc == null) return;

            if (svc.IsMinimized(_behaviour))
                svc.Restore(_behaviour, new RestoreWindowOptions { PlayTransition = true, Focus = true });
            else
                svc.Minimize(_behaviour, new MinimizeWindowOptions { PlayTransition = true });
        }

        private void OnFullScreenButtonClicked()
        {
            if (_windowRoot == null) return;
            if (!_isFullscreen) EnterFullscreen();
            else ExitFullscreen();
        }

        private void FocusSelf()
        {
            var svc = GetService();
            if (svc == null) return;
            svc.Focus(_behaviour);
        }

        // ===== Fullscreen =====

        private void EnterFullscreen()
        {
            var startRect = GetCurrentWindowRect();
            if (_snapState != SnapState.Left && _snapState != SnapState.Right)
            {
                _restorePosition = new Vector2(startRect.x, startRect.y);
                _restoreSize = new Vector2(startRect.width, startRect.height);
            }

            var parentSize = GetParentSize();
            StartFullscreenAnimation(startRect, new Rect(0f, 0f, parentSize.x, parentSize.y), true);
        }

        private void ExitFullscreen()
        {
            var startRect = GetCurrentWindowRect();
            StartFullscreenAnimation(startRect, new Rect(_restorePosition.x, _restorePosition.y, _restoreSize.x, _restoreSize.y), false);
        }

        private void StartFullscreenAnimation(Rect start, Rect target, bool fullscreen)
        {
            _isFullscreen = fullscreen;
            _snapState = fullscreen ? SnapState.Full : SnapState.None;
            UpdateFullScreenButtonText();

            if (!UIConfig.fullscreenAnimation.enabled || UIConfig.fullscreenAnimation.transitionDuration <= 0f)
            {
                ApplyWindowRectFinal(target);
                return;
            }

            if (_fullscreenAnimation != null) StopCoroutine(_fullscreenAnimation);
            _fullscreenAnimation = StartCoroutine(AnimateWindowRect(start, target));
        }

        private void UpdateFullScreenButtonText()
        {
            if (_fullScreenButton == null) return;
            _fullScreenButton.text = _isFullscreen ? UIConfig.buttonText.restore : UIConfig.buttonText.fullScreen;
        }

        private IEnumerator AnimateWindowRect(Rect start, Rect target)
        {
            var elapsed = 0f;
            var duration = UIConfig.fullscreenAnimation.transitionDuration;
            var curve = UIConfig.fullscreenAnimation.transitionCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = Mathf.Clamp01(curve.Evaluate(t));
                ApplyWindowRectAbsolute(new Rect(
                    Mathf.Lerp(start.x, target.x, eased),
                    Mathf.Lerp(start.y, target.y, eased),
                    Mathf.Lerp(start.width, target.width, eased),
                    Mathf.Lerp(start.height, target.height, eased)));
                yield return null;
            }

            ApplyWindowRectFinal(target);
            _fullscreenAnimation = null;
        }

        private void ApplyWindowRectAbsolute(Rect rect)
        {
            if (_windowRoot == null) return;
            _windowRoot.style.position = Position.Absolute;
            _windowRoot.style.left = rect.x;
            _windowRoot.style.top = rect.y;
            _windowRoot.style.right = StyleKeyword.Auto;
            _windowRoot.style.bottom = StyleKeyword.Auto;
            _windowRoot.style.width = rect.width;
            _windowRoot.style.height = rect.height;
        }

        private void ApplyWindowRectFinal(Rect rect)
        {
            if (_windowRoot == null) return;

            var parentSize = GetParentSize();
            var isFullWidth = Mathf.Approximately(rect.width, parentSize.x);
            var isFullHeight = Mathf.Approximately(rect.height, parentSize.y);
            var isLeftAligned = Mathf.Approximately(rect.x, 0f);
            var isTopAligned = Mathf.Approximately(rect.y, 0f);
            var isRightAligned = Mathf.Approximately(rect.x + rect.width, parentSize.x);

            if (isFullWidth && isFullHeight && isLeftAligned && isTopAligned)
            {
                _windowRoot.style.position = Position.Absolute;
                _windowRoot.style.left = 0f;
                _windowRoot.style.top = 0f;
                _windowRoot.style.right = 0f;
                _windowRoot.style.bottom = 0f;
                _windowRoot.style.width = StyleKeyword.Auto;
                _windowRoot.style.height = StyleKeyword.Auto;
            }
            else if (isFullHeight && isTopAligned && (isLeftAligned || isRightAligned))
            {
                _windowRoot.style.position = Position.Absolute;
                _windowRoot.style.left = isLeftAligned ? 0f : new StyleLength(StyleKeyword.Auto);
                _windowRoot.style.right = isRightAligned ? 0f : new StyleLength(StyleKeyword.Auto);
                _windowRoot.style.top = 0f;
                _windowRoot.style.bottom = 0f;
                _windowRoot.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
                _windowRoot.style.height = StyleKeyword.Auto;
            }
            else
            {
                _windowRoot.style.position = Position.Absolute;
                _windowRoot.style.left = rect.x;
                _windowRoot.style.top = rect.y;
                _windowRoot.style.right = StyleKeyword.Auto;
                _windowRoot.style.bottom = StyleKeyword.Auto;
                _windowRoot.style.width = rect.width;
                _windowRoot.style.height = rect.height;
            }
        }

        // ===== Drag =====

        private void RegisterDrag()
        {
            if (!_enableDrag || _titleBar == null) return;

            _titleBar.RegisterCallback<PointerDownEvent>(OnDragPointerDown);
            _titleBar.RegisterCallback<PointerMoveEvent>(OnDragPointerMove);
            _titleBar.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _titleBar.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        private void UnregisterDrag()
        {
            if (_titleBar == null) return;
            _titleBar.UnregisterCallback<PointerDownEvent>(OnDragPointerDown);
            _titleBar.UnregisterCallback<PointerMoveEvent>(OnDragPointerMove);
            _titleBar.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _titleBar.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        private void OnDragPointerDown(PointerDownEvent evt)
        {
            if (!_enableDrag || _windowRoot == null || _activePointerId != -1) return;

            FocusSelf();

            _dragging = true;
            _activePointerId = evt.pointerId;
            _pointerStart = evt.position;

            if (_isFullscreen || _snapState == SnapState.Left || _snapState == SnapState.Right)
            {
                var currentSize = GetWindowSize();
                var currentPos = GetWindowPosition();
                var parent = _windowRoot.parent ?? _root;
                var local = parent?.WorldToLocal(evt.position) ?? (Vector2)evt.position;

                var relX = currentSize.x > 0 ? (local.x - currentPos.x) / currentSize.x : 0.5f;
                var relY = currentSize.y > 0 ? (local.y - currentPos.y) / currentSize.y : 0.5f;

                var restoredSize = _restoreSize;
                _windowStartPos = new Vector2(local.x - restoredSize.x * relX, local.y - restoredSize.y * relY);

                RestoreFromSnapWithTarget(_windowStartPos);
            }
            else
            {
                _windowStartPos = GetWindowPosition();
            }

            _titleBar?.CapturePointer(_activePointerId);
            evt.StopPropagation();
        }

        private void OnDragPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || evt.pointerId != _activePointerId || _windowRoot == null) return;

            var delta = (Vector2)evt.position - _pointerStart;
            var newPos = _windowStartPos + delta;

            _windowRoot.style.left = newPos.x;
            _windowRoot.style.top = newPos.y;

            UpdateAeroSnapPreview(evt.position);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_activePointerId == -1 || evt.pointerId != _activePointerId) return;

            if (_dragging)
            {
                _dragging = false;
                _titleBar?.ReleasePointer(_activePointerId);

                if (UIConfig.windowClamp.enabled && _clampToParent && !_isFullscreen && _snapState == SnapState.None)
                {
                    var currentPos = GetWindowPosition();
                    var clamped = ClampPosition(currentPos);
                    if (!Mathf.Approximately(currentPos.x, clamped.x) || !Mathf.Approximately(currentPos.y, clamped.y))
                        StartClampAnimation(currentPos, clamped);
                }
            }

            if (_resizing)
            {
                _resizing = false;
                _activeResizeHandle?.ReleasePointer(_activePointerId);
                _activeEdge = ResizeEdge.None;
                _activeResizeHandle = null;
            }

            _activePointerId = -1;
            TryApplyAeroSnap();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (_activePointerId == -1 || evt.pointerId != _activePointerId) return;

            if (_dragging)
            {
                _dragging = false;
                _titleBar?.ReleasePointer(_activePointerId);

                if (UIConfig.windowClamp.enabled && _clampToParent && !_isFullscreen && _snapState == SnapState.None)
                {
                    var currentPos = GetWindowPosition();
                    var clamped = ClampPosition(currentPos);
                    if (!Mathf.Approximately(currentPos.x, clamped.x) || !Mathf.Approximately(currentPos.y, clamped.y))
                        StartClampAnimation(currentPos, clamped);
                }
            }

            if (_resizing)
            {
                _resizing = false;
                _activeResizeHandle?.ReleasePointer(_activePointerId);
                _activeEdge = ResizeEdge.None;
                _activeResizeHandle = null;
            }

            _activePointerId = -1;
            ResetAeroSnap();
        }

        // ===== Resize =====

        private void RegisterEdgeResize(VisualElement handle, ResizeEdge edge)
        {
            if (!_enableResize || handle == null) return;
            handle.userData = edge;
            handle.RegisterCallback<PointerDownEvent>(OnEdgeResizeDown);
            handle.RegisterCallback<PointerMoveEvent>(OnEdgeResizeMove);
            handle.RegisterCallback<PointerUpEvent>(OnPointerUp);
            handle.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            _registeredResizeHandles.Add(handle);
        }

        private void UnregisterEdgeResize(VisualElement handle)
        {
            if (handle == null) return;
            handle.UnregisterCallback<PointerDownEvent>(OnEdgeResizeDown);
            handle.UnregisterCallback<PointerMoveEvent>(OnEdgeResizeMove);
            handle.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            handle.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        private void RegisterResizeHandles()
        {
            _registeredResizeHandles.Clear();
            RegisterResizeHandle(UIConfig.resizeHandles.top, ResizeEdge.Top);
            RegisterResizeHandle(UIConfig.resizeHandles.right, ResizeEdge.Right);
            RegisterResizeHandle(UIConfig.resizeHandles.bottom, ResizeEdge.Bottom);
            RegisterResizeHandle(UIConfig.resizeHandles.left, ResizeEdge.Left);
            RegisterResizeHandle(UIConfig.resizeHandles.topLeft, ResizeEdge.Top | ResizeEdge.Left);
            RegisterResizeHandle(UIConfig.resizeHandles.topRight, ResizeEdge.Top | ResizeEdge.Right);
            RegisterResizeHandle(UIConfig.resizeHandles.bottomLeft, ResizeEdge.Bottom | ResizeEdge.Left);
            RegisterResizeHandleWithFallback(UIConfig.resizeHandles.bottomRight, UIConfig.resizeHandles.handle, ResizeEdge.Bottom | ResizeEdge.Right);
        }

        private void RegisterResizeHandle(string name, ResizeEdge edge)
        {
            if (_windowRoot == null || string.IsNullOrEmpty(name)) return;
            var handle = _windowRoot.Q<VisualElement>(name);
            if (handle != null) RegisterEdgeResize(handle, edge);
        }

        private void RegisterResizeHandleWithFallback(string primary, string fallback, ResizeEdge edge)
        {
            if (_windowRoot == null) return;
            var handle = !string.IsNullOrEmpty(primary) ? _windowRoot.Q<VisualElement>(primary) : null;
            if (handle == null && !string.IsNullOrEmpty(fallback))
                handle = _windowRoot.Q<VisualElement>(fallback);
            if (handle != null) RegisterEdgeResize(handle, edge);
        }

        private void UnregisterResizeHandles()
        {
            foreach (var h in _registeredResizeHandles)
                UnregisterEdgeResize(h);
            _registeredResizeHandles.Clear();
        }

        private void OnEdgeResizeDown(PointerDownEvent evt)
        {
            if (!_enableResize || _windowRoot == null || _activePointerId != -1) return;
            FocusSelf();

            if (evt.currentTarget is VisualElement ve && ve.userData is ResizeEdge edge)
                _activeEdge = edge;
            else return;

            _resizing = true;
            _activePointerId = evt.pointerId;
            _pointerStart = evt.position;
            _windowStartSize = GetWindowSize();
            _windowStartPos = GetWindowPosition();

            _activeResizeHandle = evt.currentTarget as VisualElement;
            _activeResizeHandle?.CapturePointer(_activePointerId);
            evt.StopPropagation();
        }

        private void OnEdgeResizeMove(PointerMoveEvent evt)
        {
            if (!_resizing || _activeEdge == ResizeEdge.None || evt.pointerId != _activePointerId || _windowRoot == null) return;

            var delta = (Vector2)evt.position - _pointerStart;
            var pos = _windowStartPos;
            var size = _windowStartSize;

            if (_activeEdge.HasFlag(ResizeEdge.Left)) { pos.x += delta.x; size.x -= delta.x; }
            if (_activeEdge.HasFlag(ResizeEdge.Right)) size.x += delta.x;
            if (_activeEdge.HasFlag(ResizeEdge.Top)) { pos.y += delta.y; size.y -= delta.y; }
            if (_activeEdge.HasFlag(ResizeEdge.Bottom)) size.y += delta.y;

            ApplyMinSize(ref pos, ref size);
            if (_clampToParent) ClampResizeToParent(ref pos, ref size);

            _windowRoot.style.left = pos.x;
            _windowRoot.style.top = pos.y;
            _windowRoot.style.width = size.x;
            _windowRoot.style.height = size.y;
        }

        // ===== Aero Snap =====

        private void SetupAeroSnap()
        {
            if (!UIConfig.aeroSnap.enabled || _windowRoot == null || _aeroSnapRoot != null) return;

            var parent = _windowRoot.parent ?? _root;
            if (parent == null) return;

            var visualTree = Resources.Load<VisualTreeAsset>(UIConfig.aeroSnap.resourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"[WindowChrome] AeroSnap VTA not found: {UIConfig.aeroSnap.resourcePath}");
                return;
            }

            _aeroSnapRoot = visualTree.Instantiate();
            if (_aeroSnapRoot == null) return;

            _aeroSnapRoot.pickingMode = PickingMode.Ignore;

            var styleSheet = Resources.Load<StyleSheet>(UIConfig.aeroSnap.stylePath);
            if (styleSheet != null) _aeroSnapRoot.styleSheets.Add(styleSheet);

            _aeroSnapRoot.style.position = Position.Absolute;
            _aeroSnapRoot.style.left = 0f;
            _aeroSnapRoot.style.top = 0f;
            _aeroSnapRoot.style.right = 0f;
            _aeroSnapRoot.style.bottom = 0f;

            parent.Add(_aeroSnapRoot);

            _aeroSnapContainer = _aeroSnapRoot.Q<VisualElement>("Container") ?? _aeroSnapRoot;
            _aeroPreviewWindow = _aeroSnapRoot.Q<VisualElement>("PreviewWindow");
            _aeroFeedback = _aeroSnapRoot.Q<VisualElement>("FeedbackAnimation");

            if (_aeroSnapContainer != null) _aeroSnapContainer.pickingMode = PickingMode.Ignore;
            if (_aeroPreviewWindow != null) _aeroPreviewWindow.pickingMode = PickingMode.Ignore;
            if (_aeroFeedback != null) _aeroFeedback.pickingMode = PickingMode.Ignore;

            SetAeroSnapVisible(false);
        }

        private bool EnsureAeroSnapOverlay()
        {
            if (!UIConfig.aeroSnap.enabled || _windowRoot == null) return false;
            if (_aeroSnapRoot != null) return true;
            SetupAeroSnap();
            return _aeroSnapRoot != null;
        }

        private void TearDownAeroSnap()
        {
            ResetAeroSnap();
            if (_aeroSnapRoot != null)
            {
                _aeroSnapRoot.RemoveFromHierarchy();
                _aeroSnapRoot = null;
                _aeroSnapContainer = null;
                _aeroPreviewWindow = null;
            }
        }

        private void UpdateAeroSnapPreview(Vector2 pointerPosition)
        {
            if (!UIConfig.aeroSnap.enabled || _isFullscreen || _windowRoot == null) return;
            var parent = _windowRoot.parent ?? _root;
            if (parent == null) return;

            var local = parent.WorldToLocal(pointerPosition);
            var parentSize = GetAeroSnapParentSize(parent);
            var inTop = local.y <= UIConfig.aeroSnap.snapThreshold;
            var inLeft = local.x <= UIConfig.aeroSnap.snapThreshold;
            var inRight = local.x >= parentSize.x - UIConfig.aeroSnap.snapThreshold;

            if (inTop)
            {
                if (!EnsureAeroSnapOverlay()) return;
                _aeroSnapActive = true;
                _pendingSnapState = SnapState.Full;
                SetAeroSnapVisible(true);
                ApplyPreviewRect(parentSize, SnapState.Full);
                UpdateFeedbackPosition(local, parentSize, SnapState.Full);
            }
            else if (inLeft || inRight)
            {
                if (!EnsureAeroSnapOverlay()) return;
                _aeroSnapActive = true;
                _pendingSnapState = inLeft ? SnapState.Left : SnapState.Right;
                SetAeroSnapVisible(true);
                ApplyPreviewRect(parentSize, _pendingSnapState);
                UpdateFeedbackPosition(local, parentSize, _pendingSnapState);
            }
            else if (_aeroSnapActive)
            {
                ResetAeroSnap();
            }
        }

        private void ApplyPreviewRect(Vector2 parentSize, SnapState state)
        {
            if (_aeroPreviewWindow == null) return;
            _aeroPreviewWindow.style.position = Position.Absolute;
            _aeroPreviewWindow.style.top = 0f;
            _aeroPreviewWindow.style.bottom = 0f;

            if (state == SnapState.Full)
            {
                _aeroPreviewWindow.style.left = 0f;
                _aeroPreviewWindow.style.right = 0f;
                _aeroPreviewWindow.style.width = StyleKeyword.Auto;
                _aeroPreviewWindow.style.height = StyleKeyword.Auto;
            }
            else if (state == SnapState.Left)
            {
                _aeroPreviewWindow.style.left = 0f;
                _aeroPreviewWindow.style.right = StyleKeyword.Auto;
                _aeroPreviewWindow.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
                _aeroPreviewWindow.style.height = StyleKeyword.Auto;
            }
            else if (state == SnapState.Right)
            {
                _aeroPreviewWindow.style.left = StyleKeyword.Auto;
                _aeroPreviewWindow.style.right = 0f;
                _aeroPreviewWindow.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
                _aeroPreviewWindow.style.height = StyleKeyword.Auto;
            }
        }

        private void TryApplyAeroSnap()
        {
            if (!UIConfig.aeroSnap.enabled || !_aeroSnapActive) return;
            var target = _pendingSnapState;
            ResetAeroSnap();

            switch (target)
            {
                case SnapState.Left: EnterHalfSnap(SnapState.Left); break;
                case SnapState.Right: EnterHalfSnap(SnapState.Right); break;
                default:
                    if (!_isFullscreen) EnterFullscreen();
                    break;
            }
        }

        private void ResetAeroSnap()
        {
            _aeroSnapActive = false;
            _pendingSnapState = SnapState.None;
            SetAeroSnapVisible(false, true);
        }

        private void SetAeroSnapVisible(bool visible, bool destroyAfterHide = false)
        {
            if (visible && !EnsureAeroSnapOverlay()) return;
            if (_aeroSnapRoot == null || _aeroSnapVisible == visible) return;

            _aeroSnapVisible = visible;
            _aeroSnapDestroyOnHide = destroyAfterHide;

            if (_aeroSnapAnimation != null)
            {
                StopCoroutine(_aeroSnapAnimation);
                _aeroSnapAnimation = null;
            }
            _aeroSnapAnimation = StartCoroutine(AnimateAeroSnap(visible));
        }

        private IEnumerator AnimateAeroSnap(bool show)
        {
            if (_aeroSnapRoot == null) yield break;
            if (show) _aeroSnapRoot.style.display = DisplayStyle.Flex;

            var duration = Mathf.Max(0f, UIConfig.aeroSnap.previewDuration);
            var curve = UIConfig.aeroSnap.previewCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            if (duration <= 0f)
            {
                ApplyAeroSnapAnimation(show, 1f, show ? 0f : 1f, show ? 1f : 0f);
                if (!show)
                {
                    _aeroSnapRoot.style.display = DisplayStyle.None;
                    if (_aeroSnapDestroyOnHide) DestroyAeroSnapOverlay();
                }
                yield break;
            }

            var elapsed = 0f;
            var start = show ? 0f : 1f;
            var end = show ? 1f : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                ApplyAeroSnapAnimation(show, curve.Evaluate(t), start, end);
                yield return null;
            }

            ApplyAeroSnapAnimation(show, 1f, start, end);
            if (!show)
            {
                _aeroSnapRoot.style.display = DisplayStyle.None;
                if (_aeroSnapDestroyOnHide) DestroyAeroSnapOverlay();
            }
            _aeroSnapAnimation = null;
        }

        private void DestroyAeroSnapOverlay()
        {
            _aeroSnapDestroyOnHide = false;
            if (_aeroSnapRoot != null) _aeroSnapRoot.RemoveFromHierarchy();
            _aeroSnapRoot = null;
            _aeroSnapContainer = null;
            _aeroPreviewWindow = null;
            _aeroFeedback = null;
        }

        private void ApplyAeroSnapAnimation(bool show, float eased, float start, float end)
        {
            var value = Mathf.Lerp(start, end, eased);
            if (_aeroPreviewWindow != null)
            {
                _aeroPreviewWindow.style.scale = new Scale(new Vector3(value, value, 1f));
                _aeroPreviewWindow.style.opacity = value;
            }
            if (_aeroFeedback != null)
            {
                if (show)
                {
                    var fs = eased <= 0.5f
                        ? Mathf.Lerp(0f, 1f, eased * 2f)
                        : Mathf.Lerp(1f, UIConfig.aeroSnap.feedbackExpandScale, (eased - 0.5f) * 2f);
                    var fo = eased <= 0.5f
                        ? Mathf.Lerp(0f, 1f, eased * 2f)
                        : Mathf.Lerp(1f, 0f, (eased - 0.5f) * 2f);
                    _aeroFeedback.style.scale = new Scale(new Vector3(fs, fs, 1f));
                    _aeroFeedback.style.opacity = fo;
                }
                else
                {
                    _aeroFeedback.style.opacity = 0f;
                }
            }
        }

        private Vector2 GetAeroSnapParentSize(VisualElement parent)
        {
            if (parent != null)
            {
                var w = parent.resolvedStyle.width;
                var h = parent.resolvedStyle.height;
                if (w <= 0f) w = parent.layout.width;
                if (h <= 0f) h = parent.layout.height;
                if (w > 0f && h > 0f) return new Vector2(w, h);
            }
            return new Vector2(Screen.width, Screen.height);
        }

        private void UpdateFeedbackPosition(Vector2 local, Vector2 parentSize, SnapState targetState)
        {
            if (_aeroFeedback == null) return;
            var fw = GetElementSize(_aeroFeedback, true, 50f);
            var fh = GetElementSize(_aeroFeedback, false, 50f);

            float x, y;
            if (targetState == SnapState.Full)
            {
                x = Mathf.Clamp(local.x - fw * 0.5f, 0f, Mathf.Max(0f, parentSize.x - fw));
                y = -fh * 0.5f;
            }
            else if (targetState == SnapState.Left)
            {
                x = -fw * 0.5f;
                y = Mathf.Clamp(local.y - fh * 0.5f, 0f, Mathf.Max(0f, parentSize.y - fh));
            }
            else if (targetState == SnapState.Right)
            {
                x = parentSize.x - fw * 0.5f;
                y = Mathf.Clamp(local.y - fh * 0.5f, 0f, Mathf.Max(0f, parentSize.y - fh));
            }
            else return;

            _aeroFeedback.style.left = x;
            _aeroFeedback.style.top = y;
        }

        private static float GetElementSize(VisualElement el, bool width, float fallback)
        {
            if (el == null) return fallback;
            var s = width ? el.resolvedStyle.width : el.resolvedStyle.height;
            if (s <= 0f) s = width ? el.layout.width : el.layout.height;
            return s > 0f ? s : fallback;
        }

        private void EnterHalfSnap(SnapState state)
        {
            if (state != SnapState.Left && state != SnapState.Right) return;

            var startRect = GetCurrentWindowRect();
            _restorePosition = new Vector2(startRect.x, startRect.y);
            _restoreSize = new Vector2(startRect.width, startRect.height);

            var parentSize = GetParentSize();
            var w = parentSize.x * 0.5f;
            StartHalfSnapAnimation(startRect, new Rect(state == SnapState.Right ? w : 0f, 0f, w, parentSize.y), state);
        }

        private void StartHalfSnapAnimation(Rect start, Rect target, SnapState state)
        {
            _isFullscreen = false;
            _snapState = state;
            UpdateFullScreenButtonText();

            if (!UIConfig.fullscreenAnimation.enabled || UIConfig.fullscreenAnimation.transitionDuration <= 0f)
            {
                ApplyWindowRectFinal(target);
                return;
            }

            if (_fullscreenAnimation != null) StopCoroutine(_fullscreenAnimation);
            _fullscreenAnimation = StartCoroutine(AnimateWindowRect(start, target));
        }

        private void RestoreFromSnapWithTarget(Vector2 targetPosition)
        {
            if (_isFullscreen)
            {
                ExitFullscreen();
            }
            else if (_snapState == SnapState.Left || _snapState == SnapState.Right)
            {
                var startRect = GetCurrentWindowRect();
                var targetRect = new Rect(targetPosition.x, targetPosition.y, _restoreSize.x, _restoreSize.y);
                _snapState = SnapState.None;
                _pendingSnapState = SnapState.None;
                UpdateFullScreenButtonText();

                if (UIConfig.fullscreenAnimation.enabled && UIConfig.fullscreenAnimation.transitionDuration > 0f)
                {
                    if (_fullscreenAnimation != null) StopCoroutine(_fullscreenAnimation);
                    _fullscreenAnimation = StartCoroutine(AnimateWindowRect(startRect, targetRect));
                }
                else
                {
                    ApplyWindowRectFinal(targetRect);
                }
                return;
            }

            _snapState = SnapState.None;
            _pendingSnapState = SnapState.None;
            UpdateFullScreenButtonText();
        }

        // ===== Geometry Helpers =====

        private void ApplyMinSize(ref Vector2 pos, ref Vector2 size)
        {
            if (size.x < _minSize.x)
            {
                if (_activeEdge.HasFlag(ResizeEdge.Left)) pos.x -= _minSize.x - size.x;
                size.x = _minSize.x;
            }
            if (size.y < _minSize.y)
            {
                if (_activeEdge.HasFlag(ResizeEdge.Top)) pos.y -= _minSize.y - size.y;
                size.y = _minSize.y;
            }
        }

        private void ClampResizeToParent(ref Vector2 pos, ref Vector2 size)
        {
            var parentSize = GetParentSize();
            if (pos.x < 0f) { if (_activeEdge.HasFlag(ResizeEdge.Left)) size.x += pos.x; pos.x = 0f; }
            if (pos.y < 0f) { if (_activeEdge.HasFlag(ResizeEdge.Top)) size.y += pos.y; pos.y = 0f; }
            if (pos.x + size.x > parentSize.x) { if (_activeEdge.HasFlag(ResizeEdge.Right)) size.x = parentSize.x - pos.x; else pos.x = parentSize.x - size.x; }
            if (pos.y + size.y > parentSize.y) { if (_activeEdge.HasFlag(ResizeEdge.Bottom)) size.y = parentSize.y - pos.y; else pos.y = parentSize.y - size.y; }
            size.x = Mathf.Max(_minSize.x, size.x);
            size.y = Mathf.Max(_minSize.y, size.y);
        }

        private Vector2 ClampPosition(Vector2 pos)
        {
            var parentSize = GetParentSize();
            var size = GetWindowSize();
            return new Vector2(
                Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, parentSize.x - size.x)),
                Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, parentSize.y - size.y)));
        }

        private void StartClampAnimation(Vector2 from, Vector2 to)
        {
            if (_clampAnimation != null) StopCoroutine(_clampAnimation);
            _clampAnimation = StartCoroutine(AnimateClampPosition(from, to));
        }

        private IEnumerator AnimateClampPosition(Vector2 from, Vector2 to)
        {
            if (_windowRoot == null) yield break;
            var duration = UIConfig.windowClamp.duration;
            var curve = UIConfig.windowClamp.curve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            if (duration <= 0f)
            {
                _windowRoot.style.left = to.x;
                _windowRoot.style.top = to.y;
                _clampAnimation = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = Mathf.Clamp01(curve.Evaluate(t));
                var p = Vector2.Lerp(from, to, eased);
                _windowRoot.style.left = p.x;
                _windowRoot.style.top = p.y;
                yield return null;
            }

            _windowRoot.style.left = to.x;
            _windowRoot.style.top = to.y;
            _clampAnimation = null;
        }

        private Vector2 GetWindowPosition()
        {
            if (_windowRoot == null) return Vector2.zero;
            var l = _windowRoot.resolvedStyle.left;
            var t = _windowRoot.resolvedStyle.top;
            if (float.IsNaN(l) || float.IsNaN(t) || (l == 0f && t == 0f))
                return new Vector2(_windowRoot.layout.x, _windowRoot.layout.y);
            return new Vector2(l, t);
        }

        private Vector2 GetWindowSize()
        {
            if (_windowRoot == null) return Vector2.zero;
            return new Vector2(_windowRoot.resolvedStyle.width, _windowRoot.resolvedStyle.height);
        }

        private Rect GetCurrentWindowRect()
        {
            if (_windowRoot == null) return new Rect(0, 0, 800, 600);

            if (_snapState != SnapState.None)
            {
                var l = _windowRoot.layout;
                return new Rect(l.x, l.y, l.width, l.height);
            }

            var pos = GetWindowPosition();
            var size = GetWindowSize();
            return new Rect(pos.x, pos.y, size.x, size.y);
        }

        private Vector2 GetParentSize()
        {
            var parent = _windowRoot?.parent;
            if (parent == null) parent = _root;

            if (parent != null)
            {
                var pTrans = _behaviour?.transform?.parent;
                if (pTrans != null)
                {
                    var pBehaviour = pTrans.GetComponent<UIInterfaceBehaviour>();
                    if (pBehaviour?.ContentRoot != null)
                    {
                        var cr = pBehaviour.ContentRoot;
                        var w = cr.resolvedStyle.width;
                        var h = cr.resolvedStyle.height;
                        if (float.IsNaN(w) || w <= 0f) w = cr.layout.width;
                        if (float.IsNaN(h) || h <= 0f) h = cr.layout.height;
                        if (w > 0f && h > 0f) return new Vector2(w, h);
                    }
                }

                var pw = parent.resolvedStyle.width;
                var ph = parent.resolvedStyle.height;
                if (pw > 0f && ph > 0f) return new Vector2(pw, ph);
            }

            return new Vector2(Screen.width, Screen.height);
        }

        // ===== Config apply =====

        private void ApplyToolBarVisibility()
        {
            if (_toolBar == null) return;
            _toolBar.style.display = _enableToolBar ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyToolBarButtons()
        {
            if (_toolBar == null) return;

            if (_toolBarButtonsContainer == null)
            {
                _toolBarButtonsContainer = _toolBar.Q<VisualElement>(UIConfig.elements.toolBarButtons);
                if (_toolBarButtonsContainer == null)
                {
                    _toolBarButtonsContainer = new VisualElement { name = UIConfig.elements.toolBarButtons };
                    _toolBarButtonsContainer.AddToClassList("toolbar-buttons");
                    _toolBar.Add(_toolBarButtonsContainer);
                }
            }

            _toolBarButtonsContainer.Clear();
            if (_toolBarButtons == null || _toolBarButtons.Count == 0) return;

            foreach (var btn in _toolBarButtons)
            {
                if (string.IsNullOrEmpty(btn.Name)) continue;
                var button = new Button { text = btn.Name };
                button.AddToClassList("toolbar-button");
                if (!string.IsNullOrEmpty(btn.Tooltip)) button.tooltip = btn.Tooltip;
                if (btn.OnClick != null) button.clicked += btn.OnClick;
                _toolBarButtonsContainer.Add(button);
            }
        }

        private void ApplyTitle()
        {
            if (_titleLabel == null || string.IsNullOrEmpty(_titleText)) return;
            _titleLabel.text = _titleText;
        }

        // ===== Public Configure =====

        public void Configure(string title, bool dragEnabled, bool resizeEnabled, bool toolBarEnabled,
            bool clampEnabled, Vector2 minimumSize, IReadOnlyList<WindowToolbarButton> buttons,
            bool fullscreenAnimationEnabled, float transitionDuration, AnimationCurve transitionCurve)
        {
            _titleText = title;
            _enableDrag = dragEnabled;
            _enableResize = resizeEnabled;
            _enableToolBar = toolBarEnabled;
            _clampToParent = clampEnabled;
            _minSize = minimumSize;
            _toolBarButtons = buttons != null ? new List<WindowToolbarButton>(buttons) : new List<WindowToolbarButton>();

            ApplyToolBarVisibility();
            ApplyToolBarButtons();
            ApplyTitle();
        }

        public void Configure(string title, bool dragEnabled, bool resizeEnabled, bool toolBarEnabled,
            bool clampEnabled, Vector2 minimumSize,
            bool fullscreenAnimationEnabled, float transitionDuration, AnimationCurve transitionCurve)
        {
            Configure(title, dragEnabled, resizeEnabled, toolBarEnabled, clampEnabled, minimumSize,
                null, fullscreenAnimationEnabled, transitionDuration, transitionCurve);
        }
    }
}
