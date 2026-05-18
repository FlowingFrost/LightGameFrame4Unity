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
        
        //void AddNode(double time, MotionType nodeType);
        //void Init(double time, IDirection direction, MotionType nodeType);
        //void AddNodeByBeginPoint(Vector3 beginPoint, IDirection direction, MotionType nodeType);
        //void AddNodeByBeginTime(double time, IDirection direction, MotionType nodeType, Vector3? beginVelocity = null, Vector3? acceleration = null);//极易混淆，注意区分
        ILineNode AddNode(NodeInputType nodeType, double time, bool isPending = true);
        //ILineNode AddNode(NodeInputType nodeType, double time, IDirection direction, PhysicsState physicsState);
        
        void ClearNodesAfterTime(double? time);
        //void GetPosition(double time, out Vector3 position, out Vector3 velocity);
        void Init(ILineComponent lineComponent,double time);
        MotionState UpdatePool(double time);
        void EditorPreviewUpdate(double time);
    }
}