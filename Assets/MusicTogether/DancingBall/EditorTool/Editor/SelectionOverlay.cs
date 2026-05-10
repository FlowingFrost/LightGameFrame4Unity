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
        private EditorCenter _editorCenter;

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
            _editorCenter?.ReleaseInput(this);
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

            _editorCenter = EditorLocator.GetService<EditorCenter>();
            _editorCenter?.ClaimInput(this);

            _ctrl = new SelectionViewController(_editorCenter);
            _ctrl.LoadShortcutsFromConfig();
            _ctrl.Bind(root);

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

            if (_editorCenter != null && _editorCenter.ProcessKey(e.keyCode, this))
                e.Use();
        }
    }
}
