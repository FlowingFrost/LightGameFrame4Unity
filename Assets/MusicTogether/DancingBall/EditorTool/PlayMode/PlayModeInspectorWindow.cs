using System;
using LightGameFrame.UIDrawer;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.PlayMode
{
    /// <summary>
    /// Play Mode 下的 Inspector 面板窗口。
    /// 自己加载 UXML、打开窗口、创建 Controller，遵守"窗口唤醒 Controller"的原则。
    /// </summary>
    public class PlayModeInspectorWindow : IDisposable
    {
        private static readonly string UxmlPath = "Assets/MusicTogether/DancingBall/UI/InspectorWindow.uxml";
        private const string WindowId = "DancingBall_Inspector";
        private static readonly UnityEngine.Vector2 WindowSize = new(520, 360);

        private InspectorViewController _controller;
        private WindowHandle _handle;

        public InspectorViewController Controller => _controller;

        public PlayModeInspectorWindow(UIDrawManagerService uiManager, EditorCenter editorCenter)
        {
#if UNITY_EDITOR
            var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                UnityEngine.Debug.LogError($"[PlayModeInspectorWindow] UXML not found: {UxmlPath}");
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

            _controller = new InspectorViewController(editorCenter);
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
