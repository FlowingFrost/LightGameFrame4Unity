using System;

namespace LightGameFrame.Services
{
    /// <summary>
    /// 自动服务标记。
    /// 标记此特性的 Service 类会被 Locator 在启动时自动扫描并注册。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AutoServiceAttribute : Attribute
    {
        /// <summary>
        /// 注册模式，决定该服务在 Play Mode / Edit Mode 的可见性。
        /// </summary>
        public AutoServiceMode Mode { get; set; } = AutoServiceMode.PlayMode;

        /// <summary>
        /// 是否即使场景中已存在也强制创建新的（仅对 Mono 服务有效）。
        /// </summary>
        public bool ForceCreate { get; set; } = false;

        /// <summary>
        /// 是否强制标记为 Mono 服务。null（默认）→ 反射自动判断。
        /// </summary>
        public bool? RequireMono { get; set; } = null;

        public AutoServiceAttribute() { }

        public AutoServiceAttribute(AutoServiceMode mode)
        {
            Mode = mode;
        }
    }

    /// <summary>
    /// 服务注册模式
    /// </summary>
    public enum AutoServiceMode
    {
        /// <summary>
        /// 仅在 Play Mode 注册（默认）
        /// </summary>
        PlayMode,

        /// <summary>
        /// 仅在 Edit Mode 注册
        /// </summary>
        EditorOnly,

        /// <summary>
        /// 同时在 Play Mode 和 Edit Mode 注册
        /// </summary>
        Dual,
    }
}