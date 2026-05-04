using System.IO;
using System.Reflection;
using UnityEngine;
using Sirenix.OdinInspector;

namespace LightGameFrame.DataManager
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SingletonConfigAttribute : System.Attribute
    {
        public string ResourcePath { get; }
        public string JsonFileName { get; }

        public SingletonConfigAttribute(string resourcePath, string jsonFileName)
        {
            ResourcePath = resourcePath;
            JsonFileName = jsonFileName;
        }
    }

    /// <summary>
    /// 修复版单例ScriptableObject基类
    /// 解决了原版本的所有主要问题
    /// </summary>
    public abstract class SingletonScriptableObject<T> : ScriptableObject where T : SingletonScriptableObject<T>
    {
    private static T _instance;
    private static string TName => typeof(T).Name;
        private static bool _initialized = false;

        [Tooltip("配置版本号。手动递增，JSON override 低于此值时将被忽略。")]
        public int configVersion = 0;
        [HideInInspector]
        public string configUpdatedAt = "";

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (!_initialized)
                {
                    Initialize();
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// 取命名空间最后一个 segment 作为子目录名，避免不同组件同名配置冲突。
        /// LightGameFrame.RoadEditor.AnimationConfig → "RoadEditor"
        /// 无命名空间的类 → null
        /// </summary>
        private static string GetNamespaceFolder()
        {
            string fullName = typeof(T).FullName ?? typeof(T).Name;
            int lastDot = fullName.LastIndexOf('.');
            if (lastDot < 0) return null;

            string ns = fullName.Substring(0, lastDot);
            int secondLastDot = ns.LastIndexOf('.');
            return secondLastDot > 0 ? ns.Substring(secondLastDot + 1) : ns;
        }

        private static string GetResourcePathStatic()
        {
            // 优先使用 Attribute 显式指定路径
            var attr = typeof(T).GetCustomAttribute<SingletonConfigAttribute>();
            if (attr != null) return attr.ResourcePath;

            string folder = GetNamespaceFolder();
            return folder != null ? $"Data/{folder}/{TName}" : $"Data/{TName}";
        }

        private static string GetJsonFileNameStatic()
        {
            // 优先使用 Attribute 显式指定路径
            var attr = typeof(T).GetCustomAttribute<SingletonConfigAttribute>();
            if (attr != null) return attr.JsonFileName;

            string folder = GetNamespaceFolder();
            return folder != null ? $"{folder}/{TName}.json" : $"{TName}.json";
        }
        /// <summary>
        /// 获取JSON保存路径（使用可写的持久化数据路径）
        /// </summary>
        private static string GetJsonSavePath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        /// <summary>
        /// 获取JSON读取路径（优先从持久化路径读取，不存在则从StreamingAssets读取）
        /// </summary>
        private static string GetJsonLoadPath(string fileName)
        {
            string persistentPath = GetJsonSavePath(fileName);
            if (File.Exists(persistentPath))
            {
                return persistentPath;
            }

            return Path.Combine(Application.streamingAssetsPath, fileName);
        }

        /// <summary>
        /// 初始化单例
        /// </summary>
        private static void Initialize()
        {
            if (_initialized) return;

            // 直接使用静态属性获取路径信息，避免创建临时实例
            string resourcePath = GetResourcePathStatic();
            string jsonFileName = GetJsonFileNameStatic();

            // 从Resources加载原始资源（作为默认配置模板）
            T resourceData = Resources.Load<T>(resourcePath);
            
            // 统一创建运行时实例的方式
            if (resourceData != null)
            {
                // 从Resources模板创建副本
                _instance = Instantiate(resourceData);
                Debug.Log($"[{TName}] 从Resources创建实例: {resourcePath}");
            }
            else
            {
                // 创建默认实例
                _instance = CreateInstance<T>();
                Debug.LogWarning($"[{TName}] Resources未找到，创建默认实例: {resourcePath}");
            }

            // 从JSON更新数据（无论哪种方式创建的实例）
            LoadBestJson(_instance, jsonFileName);

            _initialized = true;
            Debug.Log($"[{TName}] 单例初始化完成");
        }
        
        /// <summary>
        /// 从多个 JSON 源（StreamingAssets + persistentDataPath）中选出版本最新、
        /// 且不低于模板 configVersion 的 JSON 应用。
        /// 版本相同则取 updatedAt 更新的那个。
        /// </summary>
        private static void LoadBestJson(T instance, string jsonFileName)
        {
            if (string.IsNullOrEmpty(jsonFileName)) return;

            string streamingPath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
            string persistentPath = GetJsonSavePath(jsonFileName);

            // 收集所有候选 JSON 及其版本信息
            var candidates = new System.Collections.Generic.List<(string path, int version, string date)>();
            int baselineVersion = instance.configVersion;

            if (File.Exists(streamingPath))
            {
                var (v, d) = ReadJsonVersion(streamingPath);
                if (v >= baselineVersion)
                    candidates.Add((streamingPath, v, d));
                else
                    Debug.Log($"[{TName}] StreamingAssets JSON 版本({v})低于模板({baselineVersion})，跳过");
            }

            if (File.Exists(persistentPath))
            {
                var (v, d) = ReadJsonVersion(persistentPath);
                if (v >= baselineVersion)
                    candidates.Add((persistentPath, v, d));
                else
                    Debug.Log($"[{TName}] 持久化 JSON 版本({v})低于模板({baselineVersion})，跳过");
            }

            if (candidates.Count == 0)
            {
                Debug.Log($"[{TName}] 无有效 JSON，使用模板默认配置");
                return;
            }

            // 选最佳：版本最高 → 日期最新
            var best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.version > best.version ||
                    (c.version == best.version && string.CompareOrdinal(c.date, best.date) > 0))
                {
                    best = c;
                }
            }

            try
            {
                string jsonContent = File.ReadAllText(best.path);
                JsonUtility.FromJsonOverwrite(jsonContent, instance);
                Debug.Log($"[{TName}] 配置已应用: {best.path} (v{best.version}, {best.date})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{TName}] JSON 加载失败: {e.Message}");
            }
        }

        /// <summary>
        /// 快速读取 JSON 中的版本信息而不完整反序列化。
        /// </summary>
        private static (int version, string date) ReadJsonVersion(string path)
        {
            string content = File.ReadAllText(path);
            int version = 0;
            string date = "";

            int idx = content.IndexOf("\"configVersion\"");
            if (idx >= 0)
            {
                idx = content.IndexOf(':', idx + 15) + 1;
                while (idx < content.Length && char.IsWhiteSpace(content[idx])) idx++;
                int end = idx;
                while (end < content.Length && char.IsDigit(content[end])) end++;
                int.TryParse(content.Substring(idx, end - idx), out version);
            }

            idx = content.IndexOf("\"configUpdatedAt\"");
            if (idx >= 0)
            {
                idx = content.IndexOf('"', idx + 18) + 1;
                int end = content.IndexOf('"', idx);
                if (end > idx) date = content.Substring(idx, end - idx);
            }

            return (version, date);
        }

        /// <summary>
        /// 保存当前配置到JSON文件（保存到可写的持久化目录）
        /// 保存时自动刷新 configUpdatedAt，但 configVersion 不自增。
        /// </summary>
        public static void SaveToJson()
        {
            if (_instance == null)
            {
                Debug.LogError($"[{TName}] 实例未初始化，无法保存");
                return;
            }

            try
            {
                _instance.configUpdatedAt = System.DateTime.UtcNow.ToString("O");

                string jsonFileName = GetJsonFileNameStatic();
                string jsonPath = GetJsonSavePath(jsonFileName);
                
                // 确保目录存在
                string directory = Path.GetDirectoryName(jsonPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string jsonContent = JsonUtility.ToJson(_instance, true);
                File.WriteAllText(jsonPath, jsonContent);
                Debug.Log($"[{TName}] 配置已保存: {jsonPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{TName}] 保存失败: {e.Message}");
            }
        }

        /// <summary>
        /// 重置为Resources中的默认配置
        /// </summary>
        public static void ResetToDefault()
        {
            if (_instance != null)
            {
                // 在运行时使用安全的销毁方法
                if (Application.isPlaying)
                {
                    Destroy(_instance);
                }
                else
                {
                    DestroyImmediate(_instance);
                }
                _instance = null;
            }
            _initialized = false;
            
            // 重新初始化
            var temp = Instance; // 触发重新初始化
            Debug.Log($"[{TName}] 已重置为默认配置");
        }

        /// <summary>
        /// 删除持久化的JSON文件，下次启动将使用默认配置
        /// </summary>
        public static void DeletePersistedData()
        {
            try
            {
                string jsonFileName = GetJsonFileNameStatic();
                string jsonPath = GetJsonSavePath(jsonFileName);
                if (File.Exists(jsonPath))
                {
                    File.Delete(jsonPath);
                    Debug.Log($"[{TName}] 持久化数据已删除: {jsonPath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{TName}] 删除持久化数据失败: {e.Message}");
            }
        }

        /// <summary>
        /// Editor 中修改原始 .asset 字段时自动触发，将新值同步到运行时副本。
        /// 仅在编辑器运行模式下、且单例已初始化、且当前对象不是副本本身时生效。
        /// </summary>
        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            if (!_initialized || _instance == null) return;
            if (this == _instance) return;

            string json = JsonUtility.ToJson(this);
            JsonUtility.FromJsonOverwrite(json, _instance);
            Debug.Log($"[{TName}] Editor 修改已同步到运行时副本");
#endif
        }

#if UNITY_EDITOR
        [Button("同步到运行时副本", ButtonSizes.Large), PropertyOrder(-1)]
        [InfoBox("将此 .asset 的当前值覆写到内存中的单例实例。", InfoMessageType.Info)]
        private void SyncToRuntimeInstance()
        {
            if (!_initialized || _instance == null)
            {
                Debug.LogWarning($"[{TName}] 单例尚未初始化");
                return;
            }
            if (this == _instance)
            {
                Debug.LogWarning($"[{TName}] 当前查看的已是运行时副本，无需同步");
                return;
            }

            string json = JsonUtility.ToJson(this);
            JsonUtility.FromJsonOverwrite(json, _instance);
            Debug.Log($"[{TName}] ✅ 已同步到运行时副本");
        }
#endif

        /// <summary>
        /// 检查单例是否已初始化
        /// </summary>
        public static bool IsInitialized => _initialized && _instance != null;

        /// <summary>
        /// 手动初始化（可选调用）
        /// </summary>
        public static void EnsureInitialized()
        {
            var temp = Instance; // 触发初始化
        }

        /// <summary>
        /// 获取JSON文件的完整读取路径（用于调试）
        /// </summary>
        public static string GetCurrentJsonPath()
        {
            return GetJsonLoadPath(GetJsonFileNameStatic());
        }

        /// <summary>
        /// 获取JSON文件的保存路径（用于调试）
        /// </summary>
        public static string GetJsonSaveLocation()
        {
            return GetJsonSavePath(GetJsonFileNameStatic());
        }
    }
}