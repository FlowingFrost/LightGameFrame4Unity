using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightGameFrame.UIDrawer
{
    [CreateAssetMenu(menuName = "LightGameFrame/UI/Transition", fileName = "UITransitionProfile")]
    public sealed class UITransition : ScriptableObject
    {
        [System.Serializable]
        public class Phase
        {
            public bool enabled = true;
            public float duration = 0.2f;
            [SerializeReference] public List<UITransitionModule> modules = new List<UITransitionModule>();
        }

        [Header("Phases")]
        [SerializeField] private Phase enter = new Phase();
        [SerializeField] private Phase exit = new Phase();
        [SerializeField] private Phase cover = new Phase();
        [SerializeField] private Phase uncover = new Phase();

        public bool HasEnterModules => enter != null && enter.enabled && enter.modules != null && enter.modules.Any(m => m != null);

        public IEnumerator PlayEnter(VisualElement root) => PlayPhase(root, enter);
        public IEnumerator PlayExit(VisualElement root) => PlayPhase(root, exit);
        public IEnumerator PlayCover(VisualElement root) => PlayPhase(root, cover);
        public IEnumerator PlayUncover(VisualElement root) => PlayPhase(root, uncover);

        private static IEnumerator PlayPhase(VisualElement root, Phase phase)
        {
            if (root == null || phase == null || !phase.enabled) yield break;

            var modules = phase.modules?.Where(m => m != null).ToList();
            if (modules == null || modules.Count == 0) yield break;

            var duration = Mathf.Max(0f, phase.duration);
            if (duration <= 0f)
            {
                foreach (var module in modules)
                    module.Apply(root, 1f);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                foreach (var module in modules)
                    module.Apply(root, t);
                yield return null;
            }

            foreach (var module in modules)
                module.Apply(root, 1f);
        }
    }

    [System.Serializable]
    public abstract class UITransitionModule
    {
        [SerializeField] private AnimationCurve ease = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public void Apply(VisualElement root, float normalizedTime)
        {
            if (root == null) return;
            var t = ease == null ? normalizedTime : ease.Evaluate(normalizedTime);
            ApplyInternal(root, t);
        }

        protected abstract void ApplyInternal(VisualElement root, float t);
    }
}
