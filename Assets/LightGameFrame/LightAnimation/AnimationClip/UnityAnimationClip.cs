using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace LightGameFrame.LightAnimation
{
    /// <summary>
    /// 包装 Unity AnimationClip 资源，支持复杂层级和骨骼动画。
    /// 通过 SampleAnimation 逐帧采样，天然支持 scrubbing 和倒带。
    /// 速度控制：调整 BeginTime/EndTime 使 Duration != clip.length 即可变速。
    /// </summary>
    [Serializable]
    public class UnityAnimationClip : IAnimationClip
    {
        [Title("Animation")]
        [SerializeField] private AnimationClip clip;
        [SerializeField] private GameObject target;

        [Title("Time Range")]
        [SerializeField] private double beginTime;
        [SerializeField] private double endTime;

        public double BeginTime => beginTime;
        public double EndTime => endTime;
        public double Duration => endTime - beginTime;
        public bool IsActive { get; set; }

        public AnimationClip Clip => clip;
        public GameObject Target => target;

        public UnityAnimationClip() { }

        public UnityAnimationClip(AnimationClip clip, GameObject target, double beginTime, double endTime)
        {
            this.clip = clip;
            this.target = target;
            this.beginTime = beginTime;
            this.endTime = endTime;
        }

        public void Apply(double progress)
        {
            if (clip == null || target == null) return;

            float clipTime = (float)(Math.Clamp(progress, 0.0, 1.0) * clip.length);
            clip.SampleAnimation(target, clipTime);
        }

        public void Reset()
        {
            if (clip == null || target == null) return;
            clip.SampleAnimation(target, 0f);
        }

        public void CaptureOriginal()
        {
            // SampleAnimation(target, 0) 即原始姿态，无需额外存储
        }
    }
}
