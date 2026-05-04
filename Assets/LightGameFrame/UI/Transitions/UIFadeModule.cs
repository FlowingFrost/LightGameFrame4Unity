using UnityEngine;
using UnityEngine.UIElements;

namespace LightGameFrame.UIDrawer.Transitions
{
    [System.Serializable]
    public sealed class UIFadeModule : UITransitionModule
    {
        [Header("Fade")]
        [SerializeField] private float from = 0f;
        [SerializeField] private float to = 1f;

        protected override void ApplyInternal(VisualElement root, float t)
        {
            var target = root.Q<VisualElement>("Window") ?? root;
            var value = LerpUnclamped(from, to, t);
            target.style.opacity = value;

            if (t >= 1f && to == 1f)
                target.style.opacity = StyleKeyword.Null;
        }

        private static float LerpUnclamped(float a, float b, float t)
            => a + (b - a) * t;
    }
}
