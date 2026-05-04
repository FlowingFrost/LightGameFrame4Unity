using System;
using UnityEngine;

namespace MusicTogether.DancingBall.EditorTool.Controller
{
    /// <summary>
    /// 需要键盘输入的 Controller 实现此接口。
    /// Host 只负责转发原始 KeyCode，不处理业务逻辑。
    /// </summary>
    public interface IShortcutReceiver
    {
        /// <summary>
        /// 注册快捷键绑定。
        /// </summary>
        void SetShortcut(KeyCode key, Action action);

        /// <summary>
        /// 由 Host 在检测到按键时调用。
        /// </summary>
        void OnKeyDown(KeyCode key);
    }
}
