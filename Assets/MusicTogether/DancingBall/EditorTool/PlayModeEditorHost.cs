using System;
using System.Collections;
using LightGameFrame.Services;
using LightGameFrame.UIDrawer;
using MusicTogether.DancingBall.EditorTool.PlayMode;
using UnityEngine;

namespace MusicTogether.DancingBall.EditorTool
{
    /// <summary>
    /// Play Mode 下的编辑器窗口宿主。
    /// 创建自管理的 PlayMode 窗口（Inspector/Selection/Displacement），
    /// 每个窗口自己负责 UXML 加载、窗口打开和 Controller 生命周期。
    /// </summary>
    [AutoService(Mode = AutoServiceMode.PlayMode)]
    public class PlayModeEditorHost : GameServiceBase<PlayModeEditorHost>
    {
        private PlayModeInspectorWindow _inspector;
        private PlayModeSelectionWindow _selection;
        private PlayModeDisplacementWindow _displacement;

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

        private IEnumerator DelayedOpenWindows(UIDrawManagerService uiManager, EditorCenter editorCenter)
        {
            yield return null;

#if UNITY_EDITOR
            _inspector = new PlayModeInspectorWindow(uiManager, editorCenter);
            _selection = new PlayModeSelectionWindow(uiManager, editorCenter);
            _displacement = new PlayModeDisplacementWindow(uiManager, editorCenter);
#endif
        }

        protected override void OnUpdate()
        {
            _selection?.Controller?.RefreshHint();
            _displacement?.Controller?.RefreshDebugData();
        }

        protected override void OnCleanup()
        {
            _inspector?.Dispose();
            _selection?.Dispose();
            _displacement?.Dispose();
        }
    }
}
