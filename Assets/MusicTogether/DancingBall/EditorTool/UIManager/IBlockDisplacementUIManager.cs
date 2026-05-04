using System;
using MusicTogether.DancingBall.Data;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.UIManager
{
    public interface IBlockDisplacementUIManager : IDisposable
    {
        VisualElement rootVisualElement { get; }
        void SetData(IBlockDisplacementData data);
        event Action<IBlockDisplacementData> OnDataChanged;
    }
}
