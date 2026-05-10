using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace LightGameFrame.LightAnimation
{
    [Serializable]
    public class ScaleClip : BaseAnimationClip<Transform>
    {
        [Title("Scale Settings")]
        public Vector3 startValue = Vector3.one;
        public Vector3 endValue = Vector3.one;

        [Title("Curves (Per Axis)")]
        public AnimationCurve curveX = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve curveY = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve curveZ = AnimationCurve.Linear(0, 0, 1, 1);

        [HideInInspector, SerializeField]
        private bool _originalCaptured;
        [HideInInspector, SerializeField]
        private Vector3 _originalValue;

        public override void Apply(double progress)
        {
            if (target == null) return;

            float t = (float)Math.Clamp(progress, 0.0, 1.0);
            Vector3 v = new Vector3(
                Mathf.LerpUnclamped(startValue.x, endValue.x, curveX.Evaluate(t)),
                Mathf.LerpUnclamped(startValue.y, endValue.y, curveY.Evaluate(t)),
                Mathf.LerpUnclamped(startValue.z, endValue.z, curveZ.Evaluate(t))
            );

            target.localScale = v;
        }

        public override void Reset()
        {
            if (target == null) return;
            if (!_originalCaptured) return;
            target.localScale = _originalValue;
        }

        public override void CaptureOriginal()
        {
            if (target == null) return;
            _originalValue = target.localScale;
            _originalCaptured = true;
        }
    }
}
