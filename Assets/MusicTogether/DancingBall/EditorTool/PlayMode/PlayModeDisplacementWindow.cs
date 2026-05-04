using System;
using LightGameFrame.UIDrawer;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.PlayMode
{
    /// <summary>
    /// Play Mode 下的 Displacement 浮层窗口。
    /// 自己加载 UXML、打开窗口、创建 Controller，遵守"窗口唤醒 Controller"的原则。
    /// </summary>
    public class PlayModeDisplacementWindow : IDisposable
    {
        private static readonly string UxmlPath = "Assets/MusicTogether/DancingBall/UI/DisplacementOverlay.uxml";
        private const string WindowId = "DancingBall_Displacement";
        private static readonly UnityEngine.Vector2 WindowSize = new(360, 180);

        private DisplacementViewController _controller;
        private WindowHandle _handle;

        public DisplacementViewController Controller => _controller;

        public PlayModeDisplacementWindow(UIDrawManagerService uiManager, EditorCenter editorCenter)
        {
#if UNITY_EDITOR
            var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                UnityEngine.Debug.LogError($"[PlayModeDisplacementWindow] UXML not found: {UxmlPath}");
                return;
            }

            var options = new OpenWindowOptions
            {
                WindowId = WindowId,
                Focus = false,
                PlayTransition = false,
                WindowSize = WindowSize,
            };

            _handle = uiManager.Open(uxml, options);
            if (_handle == null) return;

            _controller = new DisplacementViewController(editorCenter);
            _controller.Bind(_handle.RootVisualElement);
#endif
        }

        public void Dispose()
        {
            _controller?.Dispose();
            _controller = null;
            _handle = null;
        }
    }
}
