using System;

namespace LightGameFrame.Services
{
    /// <summary>
    /// 基础服务接口
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// 服务优先级 (数值越小越先初始化)
        /// </summary>
        int ServicePriority { get; }

        /// <summary>
        /// 服务是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 显式声明的服务依赖类型列表，用于拓扑排序。
        /// 纯 C# 服务不应依赖 Mono 服务。
        /// </summary>
        Type[] ServiceDependencies { get; }

        /// <summary>
        /// 初始化服务
        /// </summary>
        void Initialize();

        /// <summary>
        /// 清理服务
        /// </summary>
        void Cleanup();
    }

    /// <summary>
    /// 需要每帧更新的服务接口
    /// </summary>
    public interface IUpdateService : IGameService
    {
        /// <summary>
        /// 是否启用更新
        /// </summary>
        bool UpdateEnabled { get; }

        /// <summary>
        /// 每帧更新逻辑
        /// </summary>
        void OnUpdate(float deltaTime);
    }
}
