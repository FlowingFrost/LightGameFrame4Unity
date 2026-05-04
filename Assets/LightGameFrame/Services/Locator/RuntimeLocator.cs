using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LightGameFrame.Services
{
    /// <summary>
    /// 运行时服务定位器。MonoBehaviour 单例，在 Play Mode 下驱动所有服务。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class RuntimeLocator : MonoBehaviour, IServiceLocator
    {
        #region 单例

        private static RuntimeLocator _instance;
        private static readonly object _lock = new object();

        public static RuntimeLocator Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = FindObjectOfType<RuntimeLocator>();
                            if (_instance == null)
                            {
                                var go = new GameObject("[RuntimeLocator]");
                                _instance = go.AddComponent<RuntimeLocator>();
                                DontDestroyOnLoad(go);
                            }
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region 事件

        public static event Action<IGameService> OnServiceRegistered;
        public static event Action<IGameService> OnServiceUnregistered;

        #endregion

        #region 状态

        /// <summary>
        /// 所有已注册的服务
        /// </summary>
        private readonly List<IGameService> _services = new List<IGameService>();

        /// <summary>
        /// 需要 Update 驱动缓存
        /// </summary>
        private readonly List<IUpdateService> _updatableServices = new List<IUpdateService>();

        /// <summary>
        /// Awake 阶段积压的注册（Locator 未就绪时）
        /// </summary>
        private readonly List<IGameService> _pendingServices = new List<IGameService>();

        private bool _autoServicesInitialized = false;

        /// <summary>
        /// Locator 创建前的早期注册积累（静态，不依赖实例）
        /// </summary>
        private static List<IGameService> _preInitQueue = new List<IGameService>();

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                gameObject.name = "[RuntimeLocator]";
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // 1. 初始化 [AutoService] 服务
            InitializeAutoServices();

            // 2. 刷新 Locator 创建前积压的注册（来自场景 Mono 的 Awake）
            foreach (var service in _preInitQueue)
            {
                TryRegisterAndInitialize(service);
            }
            _preInitQueue.Clear();

            _autoServicesInitialized = true;
        }

        private void Update()
        {
            // 处理 pending 队列（Awake 阶段积压，或手动注册但 Locator 未就绪的）
            if (_pendingServices.Count > 0)
            {
                var batch = _pendingServices.OrderBy(s => s.ServicePriority).ToArray();
                _pendingServices.Clear();
                foreach (var service in batch)
                {
                    TryRegisterAndInitialize(service);
                }
            }

            // 驱动 IUpdateService
            float dt = Time.deltaTime;
            for (int i = _updatableServices.Count - 1; i >= 0; i--)
            {
                var s = _updatableServices[i];
                if (s != null && s.UpdateEnabled && s.IsInitialized)
                    s.OnUpdate(dt);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                CleanupAll();
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            CleanupAll();
        }

        #endregion

        #region AutoService 扫描与初始化

        private void InitializeAutoServices()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IGameService).IsAssignableFrom(t))
                .Where(t => t.IsDefined(typeof(AutoServiceAttribute), false))
                .ToList();

            if (types.Count == 0) return;

            // 1. 过滤 Mode（EditorOnly 在运行时跳过）
            var filtered = types.Where(t =>
            {
                var attr = (AutoServiceAttribute)t.GetCustomAttributes(typeof(AutoServiceAttribute), false)[0];
                return attr.Mode != AutoServiceMode.EditorOnly;
            }).ToList();

            if (filtered.Count == 0) return;

            // 2. 创建实例
            var instances = new List<IGameService>();
            foreach (var type in filtered)
            {
                var instance = CreateInstance(type);
                if (instance != null)
                    instances.Add(instance);
            }

            if (instances.Count == 0) return;

            // 3. 拓扑排序
            var sorted = TopologicalSort(instances);

            // 4. 初始化并注册
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
                    Debug.LogError($"[RuntimeLocator] Failed to initialize {service.GetType().Name}: {ex}");
                }
            }
        }

        private IGameService CreateInstance(Type type)
        {
            var attr = (AutoServiceAttribute)type.GetCustomAttributes(typeof(AutoServiceAttribute), false)[0];
            bool requireMono = attr.RequireMono ?? typeof(MonoBehaviour).IsAssignableFrom(type);

            if (requireMono)
            {
                if (!attr.ForceCreate)
                {
                    var existing = FindObjectOfType(type) as IGameService;
                    if (existing != null) return existing;
                }

                var go = new GameObject(type.Name);
                go.transform.SetParent(transform);
                return go.AddComponent(type) as IGameService;
            }
            else
            {
                if (Activator.CreateInstance(type) is IGameService instance)
                    return instance;

                Debug.LogError($"[RuntimeLocator] Failed to create pure service {type.Name}");
                return null;
            }
        }

        #endregion

        #region 拓扑排序

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
                            $"[RuntimeLocator] {t.Name} depends on {dep.Name}, " +
                            $"but it is not registered as [AutoService]. Edge skipped.");
                        continue;
                    }
                    adj[dep].Add(t);
                    inDeg[t]++;
                }
            }

            var queue = new Queue<Type>(instances.Where(s => inDeg[s.GetType()] == 0).Select(s => s.GetType()));
            var sorted = new List<IGameService>();

            // Kahn 拓扑排序，同层按优先级排序
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
                    $"[RuntimeLocator] Circular dependency detected among: {string.Join(", ", cycleNodes)}");

                // 将环中节点追加到尾部，尽可能运行已有服务
                foreach (var inst in instances)
                {
                    if (!sorted.Contains(inst))
                        sorted.Add(inst);
                }
            }

            return sorted;
        }

        #endregion

        #region 内部注册逻辑

        private void TryRegisterAndInitialize(IGameService service)
        {
            if (_services.Contains(service)) return;

            _services.Add(service);
            if (service is IUpdateService updatable)
                _updatableServices.Add(updatable);

            try
            {
                service.Initialize();
                OnServiceRegistered?.Invoke(service);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RuntimeLocator] Failed to initialize {service.GetType().Name}: {ex}");
            }
        }

        #endregion

        #region IServiceLocator

        // GetService 用显式接口实现，避免与静态 GetService<T>() 签名冲突
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
            if (service == null) return;
            if (_services.Contains(service) || _pendingServices.Contains(service)) return;

            if (_autoServicesInitialized)
            {
                TryRegisterAndInitialize(service);
            }
            else
            {
                _pendingServices.Add(service);
            }

            if (service is MonoBehaviour mb)
                mb.transform.SetParent(transform);
        }

        public void UnregisterService(IGameService service)
        {
            if (service == null) return;

            _pendingServices.Remove(service);

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

        #region 静态注册方法（供 MonoServiceBase 调用，处理 Locator 未就绪的情况）

        /// <summary>
        /// 静态注册入口。如果 Locator 尚未创建，先积累到预初始化队列。
        /// </summary>
        public static void Register(IGameService service)
        {
            if (service == null) return;

            if (_instance != null)
            {
                _instance.RegisterService(service);
            }
            else
            {
                _preInitQueue.Add(service);

                // 整理层级（等 Locator 创建后统一处理）
            }
        }

        /// <summary>
        /// 静态注销入口。
        /// </summary>
        public static void Unregister(IGameService service)
        {
            _instance?.UnregisterService(service);
        }

        #endregion

        #region 批量操作

        public void CleanupAll()
        {
            _pendingServices.Clear();

            for (int i = _services.Count - 1; i >= 0; i--)
            {
                var service = _services[i];
                if (service is IUpdateService updatable)
                    _updatableServices.Remove(updatable);

                if (service.IsInitialized)
                    service.Cleanup();

                _services.RemoveAt(i);
            }
        }

        #endregion

        #region 静态便捷方法

        /// <summary>
        /// 获取已注册的服务。
        /// </summary>
        public static T GetService<T>() where T : class, IGameService
        {
            return _instance is IServiceLocator locator ? locator.GetService<T>() : null;
        }

        /// <summary>
        /// 获取或创建纯 C# 服务（懒加载）。仅对 GameServiceBase 生效。
        /// </summary>
        public static T GetOrCreateService<T>() where T : GameServiceBase<T>, new()
        {
            var locator = Instance;
            if (locator == null) return null;

            var sl = (IServiceLocator)locator;
            var existing = sl.GetService<T>();
            if (existing != null) return existing;

            foreach (var s in locator._pendingServices)
            {
                if (s is T target) return target;
            }

            var service = new T();
            locator.RegisterService(service);
            return service;
        }

        #endregion
    }
}