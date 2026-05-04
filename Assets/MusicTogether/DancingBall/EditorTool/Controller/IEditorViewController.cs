using System;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Controller
{
    /// <summary>
    /// Controller 接口：桥接 EditorCenter 与 UIManager。
    /// 纯 C# 实现，不依赖任何 Editor API，可在 Play Mode 和 Editor Mode 下共用。
    /// </summary>
    public interface IEditorViewController : IDisposable
    {
        /// <summary>
        /// 绑定到 VisualElement 根节点。
        /// 内部创建 UIManager、订阅 EditorCenter 事件、挂接 Action 回调。
        /// </summary>
        void Bind(VisualElement root);
    }
}
