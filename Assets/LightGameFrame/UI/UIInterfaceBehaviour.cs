using UnityEngine;
using UnityEngine.UIElements;

namespace LightGameFrame.UIDrawer
{
    public class UIInterfaceBehaviour : MonoBehaviour
    {
        [Header("UI Toolkit")]
        [SerializeField] private VisualTreeAsset embeddedVisualTree;
        [SerializeField] private string contentRootName = "";
        [SerializeField] private bool stretchEmbeddedRoot = true;

        [Header("Transition")]
        [SerializeField] private UITransition transition;

        private VisualElement _embeddedRoot;

        public UITransition Transition => transition;

        public void SetTransition(UITransition newTransition)
        {
            transition = newTransition;
        }

        public VisualElement RootVisualElement => _embeddedRoot;

        public void ConfigureEmbeddedVisualTree(VisualTreeAsset visualTreeAsset, string contentRoot = "", bool stretchRoot = true)
        {
            embeddedVisualTree = visualTreeAsset;
            contentRootName = contentRoot ?? string.Empty;
            stretchEmbeddedRoot = stretchRoot;
        }

        public VisualElement ContentRoot
        {
            get
            {
                var root = RootVisualElement;
                if (root == null) return null;
                if (string.IsNullOrEmpty(contentRootName)) return root;
                return root.Q<VisualElement>(contentRootName) ?? root;
            }
        }

        public bool TryCreateEmbeddedRoot(out VisualElement root)
        {
            root = null;
            if (embeddedVisualTree == null) return false;

            _embeddedRoot = embeddedVisualTree.Instantiate();
            root = _embeddedRoot;
            return true;
        }

        public void SetInteractable(bool value)
        {
            var root = RootVisualElement;
            if (root == null) return;
            root.SetEnabled(value);
        }
    }
}
