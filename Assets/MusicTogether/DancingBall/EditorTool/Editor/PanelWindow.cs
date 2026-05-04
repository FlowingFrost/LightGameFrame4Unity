using System;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Editor
{
    /// <summary>
    /// 泛用 EditorWindow，不需要为每个工具写特化 EditorWindow 子类。
    ///
    /// 使用示例：
    /// <code>
    /// PanelWindow.Show("DancingBall Inspector", new Vector2(520, 360),
    ///     root => new InspectorViewController().Also(ctrl => ctrl.Bind(root)));
    /// </code>
    /// PanelWindow 接管生命周期，窗口关闭时 Dispose Controller。
    /// </summary>
    public class PanelWindow : UnityEditor.EditorWindow
    {
        private IEditorViewController _controller;
        private Func<VisualElement, IEditorViewController> _createController;

        public static PanelWindow Show(string title, Vector2 size,
            Func<VisualElement, IEditorViewController> createController)
        {
            var window = CreateInstance<PanelWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = size;
            window._createController = createController;
            window.Show();
            return window;
        }

        private void CreateGUI()
        {
            if (_createController != null)
            {
                _controller = _createController(rootVisualElement);
                _createController = null; // 释放引用，允许 GC
            }
        }

        private void OnDisable()
        {
            _controller?.Dispose();
            _controller = null;
        }
    }
}
