using LightGameFrame.Services;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Editor
{
    [Overlay(typeof(SceneView), "Block Editor", defaultDisplay: true)]
    public class SelectionOverlay : Overlay
    {
        private const string SelectionUxmlPath = "Assets/MusicTogether/DancingBall/UI/SelectionWindow.uxml";
        private SelectionViewController _ctrl;

        private bool _toolEnabled = true;
        private int _controlId = -1;

        private EditorApplication.CallbackFunction _updateCallback;

        public override void OnCreated()
        {
            base.OnCreated();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public override void OnWillBeDestroyed()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (_updateCallback != null)
            {
                EditorApplication.update -= _updateCallback;
                _updateCallback = null;
            }
            _ctrl?.Dispose();
            base.OnWillBeDestroyed();
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SelectionUxmlPath);
            if (visualTree == null)
            {
                root.Add(new Label($"UXML not found: {SelectionUxmlPath}"));
                return root;
            }

            visualTree.CloneTree(root);

            var editorCenter = EditorLocator.GetService<EditorCenter>();
            _ctrl = new SelectionViewController(editorCenter);
            _ctrl.Bind(root);

            // Controller 内部已默认注册 ←/→ 快捷键
            _updateCallback = () => _ctrl?.RefreshHint();
            EditorApplication.update += _updateCallback;

            return root;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (Event.current.type == EventType.Layout)
            {
                _controlId = GUIUtility.GetControlID(FocusType.Keyboard);
                HandleUtility.AddDefaultControl(_controlId);
            }

            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (!_toolEnabled) return;

            _ctrl?.OnKeyDown(e.keyCode);
            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow)
                e.Use();
        }
    }
}
