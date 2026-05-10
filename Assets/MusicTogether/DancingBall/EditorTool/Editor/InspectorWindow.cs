using LightGameFrame.Services;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Editor
{
    /// <summary>
    /// Inspector 面板窗口。
    /// 窗口自己负责创建 Controller 和加载 UXML，不受 Domain Reload 影响。
    /// </summary>
    public class InspectorWindow : EditorWindow
    {
        [SerializeField] private string _uxmlPath = "Assets/MusicTogether/DancingBall/UI/InspectorWindow.uxml";

        private IEditorViewController _controller;
        private EditorCenter _editorCenter;

        [MenuItem("MusicTogether/DancingBall/Inspector")]
        public static void ShowWindow()
        {
            var window = GetWindow<InspectorWindow>();
            window.titleContent = new GUIContent("DancingBall Editor");
            window.minSize = new Vector2(520, 360);
            window.Show();
        }

        private void CreateGUI()
        {
            _editorCenter = EditorLocator.GetService<EditorCenter>();
            if (_editorCenter == null)
            {
                rootVisualElement.Add(new Label("EditorCenter not found."));
                return;
            }

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(_uxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[InspectorWindow] UXML not found at path: {_uxmlPath}");
                rootVisualElement.Add(new Label($"UXML not found: {_uxmlPath}"));
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

            _controller = new InspectorViewController(_editorCenter);
            _controller.Bind(rootVisualElement);

            // Host 负责 Editor 特有的窗口创建（RoadCreateWindow）
            var ctrl = (InspectorViewController)_controller;
            ctrl.RoadCreateDialogRequested = () =>
            {
                RoadCreateWindow.ShowWindow(_editorCenter.selectedRoad,
                    (name, seg, begin, end) => ctrl.OnRoadCreated(name, seg, begin, end));
            };
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_editorCenter == null) return;
            _editorCenter.ClaimInput(this);
            if (_editorCenter.ProcessKey(evt.keyCode, this))
                evt.StopPropagation();
        }

        private void OnDisable()
        {
            _editorCenter?.ReleaseInput(this);
            _controller?.Dispose();
            _controller = null;
        }
    }
}
