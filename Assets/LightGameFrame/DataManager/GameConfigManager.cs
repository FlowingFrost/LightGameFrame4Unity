using UnityEngine;

namespace LightGameFrame.DataManager
{
    /// <summary>
    /// 游戏配置ScriptableObject - 单例版本
    /// 支持跨场景访问，自动初始化，无需手动添加到场景
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "DataManager/Game Config")]
    public class GameConfig : SingletonScriptableObject<GameConfig>
    {
        public static GameConfig Config => Instance;

        public GeneralSettings general = new();
        public AudioSettings audio = new();
        public GraphicsSettings graphics = new();
        public InterfaceSettings ui = new();
        public MarkdownSettings markdown = new();

        [System.Serializable]
        public class GeneralSettings
        {
            [Header("General")]
            public string gameName = "My Unity Game";
            public string version = "1.0.0";
            public bool enableDebugMode = false;
        }

        [System.Serializable]
        public class AudioSettings
        {
            [Header("Audio")]
            [Range(0f, 1f)] public float master = 1.0f;
            [Range(0f, 1f)] public float music = 0.8f;
            [Range(0f, 1f)] public float sfx = 1.0f;
        }

        [System.Serializable]
        public class GraphicsSettings
        {
            [Header("Graphics")]
            public int targetFrameRate = 60;
            public bool enableVSync = true;
            public int qualityLevel = 2;
        }

        [System.Serializable]
        public class InterfaceSettings
        {
            [Header("Interface")]
            public string defaultLanguage = "zh-CN";
            public bool showFPS = false;
            public Color uiThemeColor = Color.blue;
        }

        [System.Serializable]
        public class MarkdownSettings
        {
            [Header("Markdown")]
            [Tooltip("#~###### 标题等级对应的字号，从1级到6级。长度不足6时使用最后一个值填充。")]
            public int[] headerSizes = new int[] { 24, 20, 18, 16, 14, 12 };
        }

        #region 单例ScriptableObject实现

        void OnEnable()
        {
            if (Application.isPlaying && IsInitialized)
            {
                ApplyGameSettings();
            }
        }

        public static void ApplyGameSettings()
        {
            if (Instance == null) return;

            var g = Instance.graphics;
            Application.targetFrameRate = g.targetFrameRate;
            QualitySettings.vSyncCount = g.enableVSync ? 1 : 0;
            QualitySettings.SetQualityLevel(g.qualityLevel);

            Debug.Log($"游戏设置已应用 - FPS: {g.targetFrameRate}, 质量: {g.qualityLevel}");
        }

        public static void SetVolume(float master, float music, float sfx)
        {
            if (Instance == null) return;
            var a = Instance.audio;
            a.master = Mathf.Clamp01(master);
            a.music = Mathf.Clamp01(music);
            a.sfx = Mathf.Clamp01(sfx);
        }

        public static void ToggleDebugMode()
        {
            if (Instance == null) return;
            Instance.general.enableDebugMode = !Instance.general.enableDebugMode;
            Debug.Log($"调试模式: {(Instance.general.enableDebugMode ? "开启" : "关闭")}");
        }

        public static string GetConfigSummary()
        {
            if (Instance == null) return "配置未初始化";
            var g = Instance.general;
            return $"游戏配置: {g.gameName} v{g.version}, " +
                   $"调试模式: {(g.enableDebugMode ? "开启" : "关闭")}";
        }

        #endregion
    }
}