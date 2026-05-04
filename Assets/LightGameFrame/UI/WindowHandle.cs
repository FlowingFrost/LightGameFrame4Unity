using UnityEngine.UIElements;

namespace LightGameFrame.UIDrawer
{
    public enum WindowState
    {
        Opening,
        Open,
        Closing,
        Closed
    }

    public class WindowHandle
    {
        public string Id { get; }
        public WindowState State { get; internal set; }
        public VisualElement RootVisualElement { get; }
        public UIInterfaceBehaviour Behaviour { get; }
        public string ParentId { get; }
        public bool IsTopLevel => string.IsNullOrEmpty(ParentId);
        public bool HasBehaviour => Behaviour != null;
        public bool IsMinimized { get; internal set; }

        internal WindowHandle(
            string id,
            VisualElement rootVisualElement,
            UIInterfaceBehaviour behaviour,
            string parentId)
        {
            Id = id;
            RootVisualElement = rootVisualElement;
            Behaviour = behaviour;
            ParentId = parentId;
            State = WindowState.Opening;
        }
    }
}
