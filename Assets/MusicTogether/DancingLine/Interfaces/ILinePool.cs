using System.Collections.Generic;
using MusicTogether.LevelManagement;
using UnityEngine;

namespace MusicTogether.DancingLine.Interfaces
{
    /// <summary>
    /// 线条池接口
    /// 管理所有节点并计算当前位置
    /// </summary>
    public interface ILinePool : ILevelUnion
    {
        double BeginTime { get; }
        MotionState CurrentMotionState { get; }
        IDirection CurrentDirection { get; }
        int CurrentNodeIndex { get; }
        ILineNode CurrentNode { get; }
        bool IsEmpty { get; }
        IReadOnlyList<ILineNode> LineNodes { get; }
        
        ILineNode AddNode(NodeInputType nodeType, double time, bool isPending = true);
        
        void ClearNodesAfterTime(double? time);
        void Init(ILineComponent lineComponent,double time);
        MotionState UpdatePool(double time);
        void EditorPreviewUpdate(double time);
    }
}