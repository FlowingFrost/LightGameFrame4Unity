using System;
using LightGameFrame.UIDrawer;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.PlayMode
{
    /// <summary>
    /// Play Mode 下的 Selection 浮层窗口。
    /// 自己加载 UXML、打开窗口、创建 Controller，遵守"窗口唤醒 Controller"的原则。
    /// </summary>
    public class PlayModeSelectionWindow : IDisposable
    {
        private static readonly string UxmlPath = "Assets/MusicTogether/DancingBall/UI/SelectionWindow.uxml";
        private const string WindowId = "DancingBall_Selection";
        private static readonly UnityEngine.Vector2 WindowSize = new(360, 200);

        private SelectionViewController _controller;
        private WindowHandle _handle;

        public SelectionViewController Controller => _controller;

        public PlayModeSelectionWindow(UIDrawManagerService uiManager, EditorCenter editorCenter)
        {
#if UNITY_EDITOR
            var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                UnityEngine.Debug.LogError($"[PlayModeSelectionWindow] UXML not found: {UxmlPath}");
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

            _controller = new SelectionViewController(editorCenter);
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
