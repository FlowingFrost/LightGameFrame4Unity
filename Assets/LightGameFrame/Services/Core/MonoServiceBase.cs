using System;
using UnityEngine;

namespace LightGameFrame.Services
{
    /// <summary>
    /// Mono 服务基类。保留 MonoBehaviour，供需要 Gizmos / GUI / 协程 / 物理回调等的服务使用。
    /// 仅在 Play Mode 由 RuntimeLocator 管理，EditorLocator 不处理。
    /// </summary>
    /// <typeparam name="T">具体的服务类型</typeparam>
    public abstract class MonoServiceBase<T> : MonoBehaviour, IUpdateService where T : MonoServiceBase<T>
    {
        public bool IsInitialized { get; private set; }
        public bool UpdateEnabled { get; set; } = true;

        /// <summary>
        /// 服务优先级，默认 100。子类可重写。
        /// </summary>
        public virtual int ServicePriority => 100;

        /// <summary>
        /// 显式声明的服务依赖类型列表。
        /// </summary>
        public virtual Type[] ServiceDependencies => Array.Empty<Type>();

        public void Initialize()
        {
            if (IsInitialized) return;
            OnInitialize();
            IsInitialized = true;
        }

        public void OnUpdate(float deltaTime)
        {
            if (!IsInitialized || !UpdateEnabled) return;
            OnUpdate();
        }

        public void Cleanup()
        {
            if (!IsInitialized) return;
            OnCleanup();
            IsInitialized = false;
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnUpdate() { }
        protected virtual void OnCleanup() { }

        /// <summary>
        /// 非 [AutoService] 方式挂载到场景的 Mono 服务通过 Awake 自行注册。
        /// [AutoService] 方式创建的 Mono 服务由 RuntimeLocator 统一管理，不依赖此回调。
        /// </summary>
        protected virtual void Awake()
        {
            RuntimeLocator.Register(this);
        }

        protected virtual void OnDestroy()
        {
            if (IsInitialized)
            {
                RuntimeLocator.Unregister(this);
            }
        }
    }
}