using System;
using System.Collections.Generic;
using LightGameFrame.Services;
using LightGameFrame.UIDrawer;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool
{
    /// <summary>
    /// Play Mode 下的编辑器窗口宿主。
    /// 自动创建 Inspector、Selection、Displacement 等编辑窗口，
    /// 通过 UIDrawManagerService 管理窗口生命周期。
    /// </summary>
    [AutoService(Mode = AutoServiceMode.PlayMode)]
    public class PlayModeEditorHost : GameServiceBase<PlayModeEditorHost>
    {
        private readonly List<IEditorViewController> _controllers = new();
        private SelectionViewController _selectionCtrl;
        private DisplacementViewController _displacementCtrl;

        private static readonly string[] UxmlPaths =
        {
            "Assets/MusicTogether/DancingBall/UI/InspectorWindow.uxml",
            "Assets/MusicTogether/DancingBall/UI/SelectionWindow.uxml",
            "Assets/MusicTogether/DancingBall/UI/DisplacementOverlay.uxml",
        };

        private static readonly (string id, Vector2 size)[] WindowConfigs =
        {
            ("DancingBall_Inspector",  new Vector2(520, 360)),
            ("DancingBall_Selection",  new Vector2(360, 200)),
            ("DancingBall_Displacement", new Vector2(360, 180)),
        };

        public override Type[] ServiceDependencies => new[]
        {
            typeof(EditorCenter),
            typeof(UIDrawManagerService),
        };

        public override int ServicePriority => 30;

        protected override void OnInitialize()
        {
#if UNITY_EDITOR
            var editorCenter = RuntimeLocator.GetService<EditorCenter>();
            if (editorCenter == null)
            {
                Debug.LogError("[PlayModeEditorHost] EditorCenter not available.");
                return;
            }

            var uiManager = UIDrawManagerService.Instance;
            if (uiManager == null)
            {
                Debug.LogError("[PlayModeEditorHost] UIDrawManagerService not available.");
                return;
            }

            // 延迟一帧确保 UI 系统完全就绪
            uiManager.StartCoroutine(DelayedOpenWindows(uiManager, editorCenter));
#endif
        }

        private System.Collections.IEnumerator DelayedOpenWindows(UIDrawManagerService uiManager, EditorCenter editorCenter)
        {
            yield return null;

#if UNITY_EDITOR
            for (int i = 0; i < UxmlPaths.Length; i++)
            {
                var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPaths[i]);
                if (uxml == null)
                {
                    Debug.LogWarning($"[PlayModeEditorHost] UXML not found: {UxmlPaths[i]}");
                    continue;
                }

                var (id, size) = WindowConfigs[i];
                var options = new OpenWindowOptions
                {
                    WindowId = id,
                    Focus = false,
                    PlayTransition = false,
                    WindowSize = size,
                };

                var handle = uiManager.Open(uxml, options);
                if (handle == null) continue;

                // 创建对应的 Controller
                IEditorViewController ctrl = i switch
                {
                    0 => new InspectorViewController(editorCenter),
                    1 => new SelectionViewController(editorCenter),
                    2 => new DisplacementViewController(editorCenter),
                    _ => null,
                };

                if (ctrl != null)
                {
                    ctrl.Bind(handle.RootVisualElement);
                    _controllers.Add(ctrl);

                    if (ctrl is SelectionViewController sv) _selectionCtrl = sv;
                    if (ctrl is DisplacementViewController dv) _displacementCtrl = dv;
                }
            }
#endif
        }

        protected override void OnUpdate()
        {
            _selectionCtrl?.RefreshHint();
            _displacementCtrl?.RefreshDebugData();
        }

        protected override void OnCleanup()
        {
            foreach (var ctrl in _controllers)
                ctrl.Dispose();
            _controllers.Clear();
            _selectionCtrl = null;
            _displacementCtrl = null;
        }
    }
}
