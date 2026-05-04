using UnityEngine;
using UnityEngine.UIElements;

namespace LightGameFrame.DataManager
{
    public class UIConfig : SingletonScriptableObject<UIConfig>
    {
        public static UIConfig Config => Instance;

        [Header("Panel & Prefab")]
        public PanelSettings panelSettings;
        public GameObject windowFramePrefab;
        public string defaultUITransitionResourcePath = "Transitions/DefaultUI";

        public WindowElements elements = new();
        public WindowButtonText buttonText = new();
        public ResizeHandles resizeHandles = new();
        public FullscreenAnimation fullscreenAnimation = new();
        public WindowClamp windowClamp = new();
        public AeroSnap aeroSnap = new();

        [System.Serializable]
        public class WindowElements
        {
            public string titleBar = "TitleBar";
            public string toolBar = "ToolBar";
            public string toolBarButtons = "ToolBarButtons";
            public string minimizeButton = "MinimizeButton";
            public string closeButton = "CloseButton";
            public string fullScreenButton = "FullScreenButton";
            public string titleLabel = "TitleLabel";
        }

        [System.Serializable]
        public class WindowButtonText
        {
            public string close = "×";
            public string minimize = "−";
            public string restore = "◻";
            public string fullScreen = "⛶";
        }

        [System.Serializable]
        public class ResizeHandles
        {
            public string top = "ResizeHandle_Top";
            public string right = "ResizeHandle_Right";
            public string bottom = "ResizeHandle_Bottom";
            public string left = "ResizeHandle_Left";
            public string topLeft = "ResizeHandle_TopLeft";
            public string topRight = "ResizeHandle_TopRight";
            public string bottomLeft = "ResizeHandle_BottomLeft";
            public string bottomRight = "ResizeHandle_BottomRight";
            public string handle = "ResizeHandle";
        }

        [System.Serializable]
        public class FullscreenAnimation
        {
            public bool enabled = true;
            public float transitionDuration = 0.2f;
            public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        [System.Serializable]
        public class WindowClamp
        {
            public bool enabled = true;
            public float duration = 0.15f;
            public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        [System.Serializable]
        public class AeroSnap
        {
            public bool enabled = true;
            public float snapThreshold = 40f;
            public string resourcePath = "UI/AeroSnapOverlay";
            public string stylePath = "UI/AeroSnapStyles";
            public float previewDuration = 0.2f;
            public AnimationCurve previewCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public float feedbackExpandScale = 1.5f;
        }
    }
}
