using UnityEngine;

namespace LightGameFrame.LightAnimation
{
    /// <summary>
    /// 纯时间驱动的动画管理器。
    /// 子类可覆写边界判定以引入触发器等逻辑（如 DancingBall 的 tile 激活检查）。
    /// </summary>
    public class AnimationManager : AnimationManagerBase
    {
        protected override bool ShouldWaitingToPlaying(IAnimationClip clip, double currentTime)
            => currentTime >= clip.BeginTime;

        protected override bool ShouldPlayingToEnded(IAnimationClip clip, double currentTime)
            => currentTime > clip.EndTime;

        protected override bool ShouldEndedToPlaying(IAnimationClip clip, double currentTime)
            => currentTime <= clip.EndTime;

        protected override bool ShouldPlayingToWaiting(IAnimationClip clip, double currentTime)
            => currentTime < clip.BeginTime;
    }
}
