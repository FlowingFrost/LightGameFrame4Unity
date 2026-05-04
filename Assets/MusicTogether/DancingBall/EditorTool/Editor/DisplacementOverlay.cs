using LightGameFrame.Services;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Editor
{
    [Overlay(typeof(SceneView), "Displacement Debug", defaultDisplay: true)]
    public class DisplacementOverlay : Overlay
    {
        private const string DisplacementUxmlPath = "Assets/MusicTogether/DancingBall/UI/DisplacementOverlay.uxml";
        private DisplacementViewController _ctrl;
        private VisualElement _root;
        private EditorApplication.CallbackFunction _updateCallback;

        public override void OnCreated()
        {
            base.OnCreated();
            _updateCallback = RefreshUI;
            EditorApplication.update += _updateCallback;
        }

        public override void OnWillBeDestroyed()
        {
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
            _root = new VisualElement();
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DisplacementUxmlPath);
            if (visualTree == null)
            {
                _root.Add(new Label($"UXML not found: {DisplacementUxmlPath}"));
                return _root;
            }

            visualTree.CloneTree(_root);

            var editorCenter = EditorLocator.GetService<EditorCenter>();
            _ctrl = new DisplacementViewController(editorCenter);
            _ctrl.Bind(_root);

            return _root;
        }

        private void RefreshUI()
        {
            if (_root?.panel == null)
            {
                if (_updateCallback != null)
                {
                    EditorApplication.update -= _updateCallback;
                    _updateCallback = null;
                }
                return;
            }

            _ctrl?.RefreshDebugData();
        }
    }
}
