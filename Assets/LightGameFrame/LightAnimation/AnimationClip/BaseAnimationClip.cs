using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace LightGameFrame.LightAnimation
{
    [Serializable]
    public abstract class BaseAnimationClip<T> : IAnimationClip where T : class
    {
        [HideInInspector] public double beginTime;
        [HideInInspector] public double endTime;

        [Title("Target")]
        [SerializeField] protected T target;

        [Title("Curve")]
        [SerializeField] protected AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

        public double BeginTime => beginTime;
        public double EndTime => endTime;
        public double Duration => endTime - beginTime;
        public bool IsActive { get; set; }

        protected float Evaluate(double progress)
        {
            double clamped = Math.Clamp(progress, 0.0, 1.0);
            return curve.Evaluate((float)clamped);
        }

        public abstract void Apply(double progress);
        public abstract void Reset();
        public virtual void CaptureOriginal() { }
    }
}
