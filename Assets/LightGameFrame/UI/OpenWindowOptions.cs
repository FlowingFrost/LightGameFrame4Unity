using System.Collections.Generic;
using UnityEngine;

namespace LightGameFrame.UIDrawer
{
    public class OpenWindowOptions
    {
        public string WindowId { get; set; }
        public bool Focus { get; set; } = true;
        public bool PlayTransition { get; set; } = true;

        public UITransition SelfEnterOverride { get; set; }
        public UITransition PreviousCoverOverride { get; set; }

        public string ParentWindowId { get; set; }

        public Vector2? WindowPosition { get; set; }
        public Vector2? WindowSize { get; set; }

        public WindowChromeOptions WindowChrome { get; set; }
    }

    public class WindowChromeOptions
    {
        public bool Enabled { get; set; } = true;
        public string Title { get; set; }
        public bool EnableDrag { get; set; } = true;
        public bool EnableResize { get; set; } = true;
        public bool EnableToolBar { get; set; } = true;
        public bool ClampToParent { get; set; } = true;
        public Vector2 MinSize { get; set; } = new Vector2(320f, 240f);
        public bool EnableFullscreenAnimation { get; set; } = true;
        public float FullscreenTransitionDuration { get; set; } = 0.2f;
        public AnimationCurve FullscreenTransitionCurve { get; set; } = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public List<WindowToolbarButton> ToolBarButtons { get; set; } = new List<WindowToolbarButton>();
    }

    public class CloseWindowOptions
    {
        public bool PlayTransition { get; set; } = true;
        public bool CloseRootFamily { get; set; } = false;

        public UITransition ExitOverride { get; set; }
        public UITransition NextUncoverOverride { get; set; }
    }

    public class MinimizeWindowOptions
    {
        public bool PlayTransition { get; set; } = true;
        public UITransition ExitOverride { get; set; }
    }

    public class RestoreWindowOptions
    {
        public bool PlayTransition { get; set; } = true;
        public bool Focus { get; set; } = true;
        public UITransition EnterOverride { get; set; }
    }
}
