using System;
using System.Collections.Generic;
using System.Linq;
using MusicTogether.DancingBall.EditorTool.UIManager;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.SceneOld;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Editor
{
    public class RoadCreateWindow : UnityEditor.EditorWindow
    {
        private const string UxmlPath = "Assets/MusicTogether/DancingBall/UI/RoadCreateWindow.uxml";
        private Action<string, string, int, int> onCreate;
        private IRoad templateRoad;
        private RoadCreateWindowManager _windowManager;

        public static void ShowWindow(IRoad template, Action<string, string, int, int> onCreate)
        {
            var window = CreateInstance<RoadCreateWindow>();
            window.titleContent = new GUIContent("Create Road");
            window.minSize = new Vector2(360, 260);
            window.onCreate = onCreate;
            window.templateRoad = template;
            window.ShowUtility();
        }

        private void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[RoadCreateWindow] UXML not found at path: {UxmlPath}");
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            _windowManager = new RoadCreateWindowManager(rootVisualElement);
            _windowManager.CreateRequested = OnCreateRequested;
            _windowManager.CancelRequested = Close;

            var data = templateRoad?.RoadData;
            var sceneData = templateRoad?.Map?.SceneData;
            _windowManager.SetSegmentOptions(GetSegmentDisplayNames(sceneData), GetSegmentNames(sceneData));
            _windowManager.SetDefaults(
                data == null ? "Road_New" : $"{data.roadName}_New",
                data?.targetSegmentName ?? "",
                data?.noteBeginIndex ?? 0,
                data?.noteEndIndex ?? 0);
        }

        private void OnCreateRequested(string roadName, string segmentName, int noteBegin, int noteEnd)
        {
            onCreate?.Invoke(roadName, segmentName, noteBegin, noteEnd);
            Close();
        }

        private static List<string> GetSegmentDisplayNames(SceneData sceneData)
        {
            var result = new List<string>();
            if (sceneData?.SamplingSegments == null) return result;
            for (int i = 0; i < sceneData.SamplingSegments.Count; i++)
            {
                var seg = sceneData.SamplingSegments[i];
                var displayName = string.IsNullOrWhiteSpace(seg.name) ? "Unnamed" : seg.name;
                result.Add($"{i} | {displayName}");
            }
            return result;
        }

        private static List<string> GetSegmentNames(SceneData sceneData)
        {
            var result = new List<string>();
            if (sceneData?.SamplingSegments == null) return result;
            foreach (var seg in sceneData.SamplingSegments)
            {
                result.Add(seg.name);
            }
            return result;
        }
    }
}
