using LightGameFrame.Services;
using MusicTogether.DancingBall.EditorTool.Controller;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Editor
{
    /// <summary>
    /// Inspector 面板入口。实际逻辑委托给 InspectorViewController，
    /// 窗口生命周期由 PanelWindow 管理。
    /// </summary>
    public static class InspectorWindow
    {
        private const string UxmlPath = "Assets/MusicTogether/DancingBall/UI/InspectorWindow.uxml";

        [MenuItem("MusicTogether/DancingBall/Inspector")]
        public static void ShowWindow()
        {
            var editorCenter = EditorLocator.GetService<EditorCenter>();

            PanelWindow.Show("DancingBall Editor", new Vector2(520, 360), root =>
            {
                var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
                if (visualTree == null)
                {
                    Debug.LogError($"[DancingBallEditorWindow] UXML not found at path: {UxmlPath}");
                    root.Add(new Label($"UXML not found: {UxmlPath}"));
                    return null;
                }

                visualTree.CloneTree(root);
                var ctrl = new InspectorViewController(editorCenter);
                ctrl.Bind(root);

                // Host 负责 Editor 特有的窗口创建（RoadCreateWindow）
                ctrl.RoadCreateDialogRequested = () =>
                {
                    RoadCreateWindow.ShowWindow(editorCenter.selectedRoad,
                        (name, seg, begin, end) => ctrl.OnRoadCreated(name, seg, begin, end));
                };

                return ctrl;
            });
        }
    }
}
