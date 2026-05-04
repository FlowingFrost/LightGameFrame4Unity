#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LightGameFrame.Services
{
    /// <summary>
    /// 编辑器服务定位器。在 Edit Mode 下驱动纯 C# 服务，不处理 Mono 服务。
    /// </summary>
    [InitializeOnLoad]
    public class EditorLocator : IServiceLocator
    {
        private static EditorLocator _instance;

        private readonly List<IGameService> _services = new List<IGameService>();
        private readonly List<IUpdateService> _updatableServices = new List<IUpdateService>();

        private bool _isPaused = false;
        private double _lastUpdateTime;

        public static EditorLocator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EditorLocator();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        public static event Action<IGameService> OnServiceRegistered;
        public static event Action<IGameService> OnServiceUnregistered;

        private EditorLocator() { }

        static EditorLocator()
        {
            _instance = new EditorLocator();
            _instance.Initialize();
        }

        private void Initialize()
        {
            // 1. 扫描程序集
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IGameService).IsAssignableFrom(t))
                .Where(t => t.IsDefined(typeof(AutoServiceAttribute), false))
                .ToList();

            if (types.Count == 0) return;

            // 2. 过滤 Mode，跳过 Mono 类型
            var filtered = new List<(Type type, bool isMono)>();
            foreach (var type in types)
            {
                var attr = (AutoServiceAttribute)type.GetCustomAttributes(typeof(AutoServiceAttribute), false)[0];

                if (attr.Mode == AutoServiceMode.PlayMode) continue;

                bool isMono = attr.RequireMono ?? typeof(MonoBehaviour).IsAssignableFrom(type);

                if (isMono)
                {
                    Debug.Log($"[EditorLocator] Skip Mono service {type.Name} (not available in Edit Mode)");
                    continue;
                }

                filtered.Add((type, false));
            }

            if (filtered.Count == 0) return;

            // 3. 创建实例
            var instances = new List<IGameService>();
            foreach (var (type, _) in filtered)
            {
                if (Activator.CreateInstance(type) is IGameService instance)
                    instances.Add(instance);
                else
                    Debug.LogError($"[EditorLocator] Failed to create {type.Name}");
            }

            // 4. 拓扑排序
            var sorted = TopologicalSort(instances);

            // 5. 初始化并注册
            foreach (var service in sorted)
            {
                try
                {
                    service.Initialize();
                    _services.Add(service);
                    if (service is IUpdateService updatable)
                        _updatableServices.Add(updatable);
                    OnServiceRegistered?.Invoke(service);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorLocator] Failed to initialize {service.GetType().Name}: {ex}");
                }
            }

            // 6. 挂驱动
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChange;
        }

        private void OnEditorUpdate()
        {
            if (_isPaused) return;

            double currentTime = EditorApplication.timeSinceStartup;
            float dt = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;

            // 限制最大 dt 防止编辑器卡顿时跳帧太大
            if (dt > 0.5f) dt = 0.5f;

            for (int i = _updatableServices.Count - 1; i >= 0; i--)
            {
                var service = _updatableServices[i];
                if (service != null && service.UpdateEnabled && service.IsInitialized)
                    service.OnUpdate(dt);
            }
        }

        private void OnPlayModeStateChange(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _isPaused = true;
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    _isPaused = false;
                    _lastUpdateTime = EditorApplication.timeSinceStartup;
                    break;
            }
        }

        #region 拓扑排序（与 RuntimeLocator 逻辑一致）

        private static List<IGameService> TopologicalSort(List<IGameService> instances)
        {
            var typeMap = instances.ToDictionary(s => s.GetType());
            var adj = new Dictionary<Type, List<Type>>();
            var inDeg = new Dictionary<Type, int>();

            foreach (var inst in instances)
            {
                var t = inst.GetType();
                adj[t] = new List<Type>();
                inDeg[t] = 0;
            }

            foreach (var inst in instances)
            {
                var t = inst.GetType();
                foreach (var dep in inst.ServiceDependencies)
                {
                    if (!typeMap.ContainsKey(dep))
                    {
                        Debug.LogWarning(
                            $"[EditorLocator] {t.Name} depends on {dep.Name}, " +
                            $"but {dep.Name} is not an [AutoService]. Edge skipped.");
                        continue;
                    }
                    adj[dep].Add(t);
                    inDeg[t]++;
                }
            }

            var queue = new Queue<Type>(instances.Where(s => inDeg[s.GetType()] == 0).Select(s => s.GetType()));
            var sorted = new List<IGameService>();

            while (queue.Count > 0)
            {
                var batch = queue.OrderBy(t => typeMap[t].ServicePriority).ToList();
                queue.Clear();

                foreach (var t in batch)
                {
                    sorted.Add(typeMap[t]);
                    foreach (var dep in adj[t])
                    {
                        inDeg[dep]--;
                        if (inDeg[dep] == 0)
                            queue.Enqueue(dep);
                    }
                }
            }

            if (sorted.Count != instances.Count)
            {
                var cycleNodes = instances
                    .Where(s => inDeg[s.GetType()] > 0)
                    .Select(s => s.GetType().Name);
                Debug.LogError(
                    $"[EditorLocator] Circular dependency detected among: {string.Join(", ", cycleNodes)}");

                foreach (var inst in instances)
                {
                    if (!sorted.Contains(inst))
                        sorted.Add(inst);
                }
            }

            return sorted;
        }

        #endregion

        #region IServiceLocator

        T IServiceLocator.GetService<T>()
        {
            foreach (var service in _services)
            {
                if (service is T typed) return typed;
            }
            return null;
        }

        public void RegisterService(IGameService service)
        {
            if (service == null || _services.Contains(service)) return;

            try
            {
                service.Initialize();
                _services.Add(service);
                if (service is IUpdateService updatable)
                    _updatableServices.Add(updatable);
                OnServiceRegistered?.Invoke(service);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorLocator] Failed to initialize manually registered {service.GetType().Name}: {ex}");
            }
        }

        public void UnregisterService(IGameService service)
        {
            if (service == null) return;

            if (_services.Remove(service))
            {
                if (service is IUpdateService updatable)
                    _updatableServices.Remove(updatable);

                if (service.IsInitialized)
                    service.Cleanup();

                OnServiceUnregistered?.Invoke(service);
            }
        }

        #endregion

        #region 静态便捷方法

        public static T GetService<T>() where T : class, IGameService
        {
            return _instance is IServiceLocator locator ? locator.GetService<T>() : null;
        }

        #endregion
    }
}
#endif
