using UnityEngine;
using UnityEngine.UIElements;

namespace LightGameFrame.UIDrawer.Transitions
{
    [System.Serializable]
    public sealed class UISlideModule : UITransitionModule
    {
        public enum SlideDirection
        {
            None,
            Left,
            Right,
            Up,
            Down
        }

        public enum RelativeBasis
        {
            Screen,
            Root,
            Parent
        }

        [Header("Slide")]
        [Tooltip("使用相对距离：1 代表基准宽/高（Screen/Root/Parent）。")]
        [SerializeField] private bool useRelativeDistance = true;
        [SerializeField] private RelativeBasis relativeBasis = RelativeBasis.Screen;
        [SerializeField] private SlideDirection fromDirection = SlideDirection.Right;
        [SerializeField] private float fromDistance = 320f;
        [SerializeField] private SlideDirection toDirection = SlideDirection.None;
        [SerializeField] private float toDistance = 0f;

        protected override void ApplyInternal(VisualElement root, float t)
        {
            var target = root.Q<VisualElement>("Window") ?? root;

            if (t >= 1f && toDirection == SlideDirection.None && toDistance == 0f)
            {
                target.style.translate = StyleKeyword.Null;
                return;
            }

            var from = DirectionToOffset(target, fromDirection, fromDistance);
            var to = DirectionToOffset(target, toDirection, toDistance);
            var value = LerpUnclamped(from, to, t);
            target.style.translate = new Translate(new Length(value.x, LengthUnit.Pixel), new Length(value.y, LengthUnit.Pixel), 0f);
        }

        private Vector2 DirectionToOffset(VisualElement root, SlideDirection direction, float distance)
        {
            var finalDistance = useRelativeDistance ? distance * GetAxisLength(root, direction) : distance;
            return direction switch
            {
                SlideDirection.Left => new Vector2(-finalDistance, 0f),
                SlideDirection.Right => new Vector2(finalDistance, 0f),
                SlideDirection.Up => new Vector2(0f, -finalDistance),
                SlideDirection.Down => new Vector2(0f, finalDistance),
                _ => Vector2.zero
            };
        }

        private float GetAxisLength(VisualElement root, SlideDirection direction)
        {
            if (relativeBasis == RelativeBasis.Screen)
                return direction is SlideDirection.Up or SlideDirection.Down ? Screen.height : Screen.width;

            var target = ResolveBasisRoot(root);
            var width = target.resolvedStyle.width;
            var height = target.resolvedStyle.height;
            if (width <= 0f) width = Screen.width;
            if (height <= 0f) height = Screen.height;
            return direction is SlideDirection.Up or SlideDirection.Down ? height : width;
        }

        private VisualElement ResolveBasisRoot(VisualElement root)
        {
            return relativeBasis switch
            {
                RelativeBasis.Parent => root?.parent ?? root,
                RelativeBasis.Root => root?.panel?.visualTree ?? root,
                _ => root
            };
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
