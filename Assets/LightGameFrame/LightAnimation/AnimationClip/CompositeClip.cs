using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace LightGameFrame.LightAnimation
{
    [Serializable]
    public class CompositeClip : IAnimationClip
    {
        [SerializeReference, ListDrawerSettings(DefaultExpandedState = true)]
        public List<IAnimationClip> children = new List<IAnimationClip>();

        public double BeginTime => children.Count > 0 ? children.Min(c => c.BeginTime) : 0;
        public double EndTime => children.Count > 0 ? children.Max(c => c.EndTime) : 0;
        public double Duration => EndTime - BeginTime;
        public bool IsActive { get; set; }

        public void Apply(double progress)
        {
            foreach (var child in children)
            {
                if (child.IsActive)
                    child.Apply(progress);
            }
        }

        public void Reset()
        {
            foreach (var child in children)
                child.Reset();
        }

        public void CaptureOriginal()
        {
            foreach (var child in children)
                child.CaptureOriginal();
        }
    }
}
