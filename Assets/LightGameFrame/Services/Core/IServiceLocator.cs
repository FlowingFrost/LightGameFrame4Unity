namespace LightGameFrame.Services
{
    /// <summary>
    /// 服务定位器接口。RuntimeLocator 和 EditorLocator 各自实现。
    /// </summary>
    public interface IServiceLocator
    {
        /// <summary>
        /// 获取已注册的服务实例
        /// </summary>
        T GetService<T>() where T : class, IGameService;

        /// <summary>
        /// 注册服务（手动注册，非 AutoService）
        /// </summary>
        void RegisterService(IGameService service);

        /// <summary>
        /// 注销服务
        /// </summary>
        void UnregisterService(IGameService service);
    }
}