using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace LightGameFrame.LightAnimation
{
    [Serializable]
    public class ColorClip : BaseAnimationClip<MeshRenderer>
    {
        [Title("Color")]
        public Gradient colorGradient = new Gradient();

        private MaterialPropertyBlock _propBlock;
        private static readonly int ColorID = Shader.PropertyToID("_Color");

        [HideInInspector, SerializeField]
        private bool _originalCaptured;
        [HideInInspector, SerializeField]
        private Color _originalColor;

        public override void Apply(double progress)
        {
            if (target == null) return;

            float t = (float)Math.Clamp(progress, 0.0, 1.0);
            Color c = colorGradient.Evaluate(t);

            _propBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(ColorID, c);
            target.SetPropertyBlock(_propBlock);
        }

        public override void Reset()
        {
            if (target == null) return;
            if (!_originalCaptured) return;

            _propBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(ColorID, _originalColor);
            target.SetPropertyBlock(_propBlock);
        }

        public override void CaptureOriginal()
        {
            if (target == null) return;
            _propBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_propBlock);
            _originalColor = _propBlock.GetColor(ColorID);
            _originalCaptured = true;
        }
    }
}
