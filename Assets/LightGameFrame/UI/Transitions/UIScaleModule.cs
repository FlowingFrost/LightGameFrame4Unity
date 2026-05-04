using UnityEngine;
using UnityEngine.UIElements;

namespace LightGameFrame.UIDrawer.Transitions
{
    [System.Serializable]
    public sealed class UIScaleModule : UITransitionModule
    {
        [Header("Scale")]
        [SerializeField] private Vector2 from = new Vector2(0.9f, 0.9f);
        [SerializeField] private Vector2 to = Vector2.one;

        protected override void ApplyInternal(VisualElement root, float t)
        {
            var target = root.Q<VisualElement>("Window") ?? root;

            target.style.transformOrigin = new TransformOrigin(
                new Length(50, LengthUnit.Percent),
                new Length(50, LengthUnit.Percent),
                0f
            );

            var value = LerpUnclamped(from, to, t);
            target.style.scale = new Scale(value);

            if (t >= 1f && to == Vector2.one)
            {
                target.style.scale = StyleKeyword.Null;
                target.style.transformOrigin = StyleKeyword.Null;
            }
        }

        private static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t)
        {
            return new Vector2(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t
            );
        }
    }
}
