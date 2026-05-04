using System;

namespace LightGameFrame.Services
{
    /// <summary>
    /// 纯 C# 服务基类。不依赖 MonoBehaviour，可在 GameServiceBase Edit Mode 和 Play Mode 下独立运行。
    /// </summary>
    /// <typeparam name="T">具体的服务类型</typeparam>
    public abstract class GameServiceBase<T> : IUpdateService where T : GameServiceBase<T>, new()
    {
        public bool IsInitialized { get; private set; }
        public bool UpdateEnabled { get; set; } = true;

        /// <summary>
        /// 服务优先级，默认 100。子类可重写。
        /// 只对同层无依赖关系的服务生效（依赖优先于优先级）。
        /// </summary>
        public virtual int ServicePriority => 100;

        /// <summary>
        /// 显式声明的服务依赖类型列表。
        /// 纯 C# 服务不应声明对 Mono 服务的依赖。
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

        /// <summary>
        /// 初始化时调用
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// 每帧更新时调用（由 Locator 驱动）
        /// </summary>
        protected virtual void OnUpdate() { }

        /// <summary>
        /// 清理时调用
        /// </summary>
        protected virtual void OnCleanup() { }
    }
}