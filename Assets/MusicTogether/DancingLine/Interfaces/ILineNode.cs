using System;
using JetBrains.Annotations;
using UnityEngine;

namespace MusicTogether.DancingLine.Interfaces
{
    /// <summary>
    /// 线条节点接口
    /// 表示一个时间点的输入和对应的线段
    /// </summary>
    public interface ILineNode
    {
        public NodeInputType NodeType { get; }
        public double BeginTime { get; }
        public double EndTime { get; }
        public Vector3? EndDisplacement { get; }
        public bool HasLimitedLength { get; }
        public IDirection Direction { get; }
        public MotionState CachedBeginMotionState { get; }
        public PhysicsState InitialPhysicsState { get; }
        public NodeMotionType NodeMotionType { get;}
        
        void InitMotion(IPhysicsDetector physicsDetector, PhysicsState initialPhysicsState);
        
        void SetActive(bool isActive);
        void SetDirection(IDirection newDirection);
        void SetNodeType(NodeInputType newNodeType);
        void SetBeginTime(double newBeginTime);
        void SetEndTime(double newEndTime);//这两个之后需要改成属性访问器
        void SetBeginPosition(Vector3 newBeginPosition);
        
        MotionState GetMotionStateAtTime(double time, bool calculatePhysics = true);
        MotionState UpdateMotionState(double time);
        PhysicsState GetPhysicsState(double time);
        
        void DeleteNode();
    }
}