using MusicTogether.DancingBall.EditorTool.UIManager;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Editor
{
    public class SettingsWindow : UnityEditor.EditorWindow
    {
        private const string UxmlPath = "Assets/MusicTogether/DancingBall/UI/SettingsWindow.uxml";
        private SettingsWindowManager _windowManager;

        [MenuItem("MusicTogether/DancingBall/Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<SettingsWindow>("DancingBall Settings");
            window.minSize = new Vector2(520, 360);
        }

        private void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[DancingBallSettingsWindow] UXML not found at path: {UxmlPath}");
                return;
            }

            var commonStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/MusicTogether/DancingBall/UI/DancingBallEditor_Common.uss");
            if (commonStyle != null)
                rootVisualElement.styleSheets.Add(commonStyle);

            visualTree.CloneTree(rootVisualElement);

            _windowManager = new SettingsWindowManager(rootVisualElement);
            _windowManager.LoadShortcutSettings(EditorShortcutConfig.Config);
        }
    }
}
