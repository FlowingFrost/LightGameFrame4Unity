using System;
using JetBrains.Annotations;
using MusicTogether.DancingLine.Interfaces;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace MusicTogether.DancingLine.Classic
{
    //升级需求 1.Motion需要SelfTransform值填入 2.物理检测新增上台阶处理 3.落地检测新增延后计算（当前实际已经开始降落了）4.Pool需要读取CachedEndDisplacement并应用
    public class ClassicLineNode : SerializedMonoBehaviour, ILineNode
    {
        [SerializeField]protected ILineTail tail;
        protected NodeInputType nodeType;
        protected double beginTime;
        protected double endTime;
        internal Vector3? CachedEndDisplacement = null;
        protected bool hasLimitedLength = false;
        protected IDirection direction;
        protected IPhysicsDetector physicsDetector;
        
        protected bool hasInitialized = false;
        protected double? cachedTime = null;
        protected Vector3 cachedBeginPosition => cachedBeginMotionState.ParentSpacePosition;
        protected MotionState cachedBeginMotionState = new MotionState();
        protected PhysicsState cachedPhysicsState;
        protected NodeMotionType CachedNodeNodeMotionType => cachedPhysicsState.NodeMotionType;
        protected Vector3 cachedBeginVelocity => cachedPhysicsState.Velocity;
        protected Vector3 cachedGravity => cachedPhysicsState.Gravity;
        
        public NodeInputType NodeType => nodeType;
        public double BeginTime => beginTime;
        public double EndTime => endTime;
        
        public Vector3? EndDisplacement => CachedEndDisplacement;
        public bool HasLimitedLength => hasLimitedLength;
        public IDirection Direction => direction;
        public MotionState CachedBeginMotionState => cachedBeginMotionState;
        public PhysicsState InitialPhysicsState => cachedPhysicsState;
        public NodeMotionType NodeMotionType => CachedNodeNodeMotionType;
        
        //Debug
        [SerializeField] internal string debugInfo;

        public void InitMotion(IPhysicsDetector physicsDetector, PhysicsState initialPhysicsState)
        {
            this.physicsDetector = physicsDetector;
            this.cachedPhysicsState = initialPhysicsState;
            cachedBeginMotionState.SelfTransform = transform;
            
            hasInitialized = false;
        }

        public void SetActive(bool isActive)
        {
            tail.SetActive(isActive);
        }
        
        public void SetNodeType(NodeInputType newNodeType)
        {
            this.nodeType = newNodeType;
        }
        
        public void SetDirection(IDirection newDirection)
        {
            this.direction = newDirection;
        }

        public void SetBeginTime(double newBeginTime)
        {
            this.beginTime = newBeginTime;
        }
        
        public void SetEndTime(double newEndTime)
        {
            this.endTime = newEndTime;
        }

        public void SetBeginPosition(Vector3 newBeginPosition)
        {
            cachedBeginMotionState.ParentSpacePosition = newBeginPosition;
        }

        public MotionState GetMotionStateAtTime(double time, bool calculatePhysics = true)
        {
            var deltaTime = (float)(time - beginTime);

            var headMotion = tail.GetMotionStateAtTime(cachedBeginPosition, deltaTime, direction);
            headMotion.SelfTransform = transform;
            
            
            
            
            //此处未实现物理计算
            
            
            
            
            
            return headMotion;
        }
        public MotionState UpdateMotionState(double time)
        {
            cachedTime = time;
            if (hasLimitedLength && cachedTime > endTime)
            {
                cachedTime = endTime;
            }
            var deltaTime = (float)(cachedTime.Value - beginTime);

            var headMotion = tail.UpdateTail(cachedBeginPosition, deltaTime, direction);
            headMotion.SelfTransform = transform;

            //处理物理逻辑
            switch (CachedNodeNodeMotionType)
            {
                case NodeMotionType.Grounded:
                    if(!physicsDetector.IsGrounded(headMotion, out var displacement))
                    {
                        if (!hasInitialized)
                        {
                            cachedPhysicsState.NodeMotionType = NodeMotionType.Falling;
                            cachedPhysicsState.Velocity = Vector3.zero;
                        }
                        else
                        {
                            cachedPhysicsState.NodeMotionType = NodeMotionType.GroundedToFalling;
                            hasLimitedLength = true;
                            endTime = time;
                        }
                    }
                    else if (displacement.HasValue)
                    {
                        hasLimitedLength = true;
                        endTime = time;
                        CachedEndDisplacement = displacement;
                    }
                    break;
                case NodeMotionType.Falling:
                    if (!hasInitialized)
                    {
                        if (physicsDetector.IsGrounded(headMotion, out var displacement1))
                        {
                            cachedPhysicsState.NodeMotionType = NodeMotionType.Grounded;
                            cachedPhysicsState.Velocity = Vector3.zero;
                            break;
                        }
                    }
                    var deltaH = PhysicsHelper.CalculateDisplacement(cachedBeginVelocity, cachedGravity, deltaTime);
                    headMotion.ParentSpacePosition += deltaH;
                    //需要估算落地位置
                    if (physicsDetector.GetLandingPoint(cachedBeginMotionState, cachedPhysicsState, deltaTime, direction, out var result))
                    {
                        cachedPhysicsState.NodeMotionType = NodeMotionType.FallingToGrounded;
                        hasLimitedLength = true;
                        endTime = time + result.FinalT;
                    }
                    else if (result != null)
                    {
                        cachedPhysicsState.NodeMotionType = NodeMotionType.FallingToGrounded;
                        hasLimitedLength = true;
                        endTime = time + result.FinalT;
                        CachedEndDisplacement = result.Displacement;
                    }
                    break;
                case NodeMotionType.FallingToGrounded:
                    var deltaH2 = PhysicsHelper.CalculateDisplacement(cachedBeginVelocity, cachedGravity, deltaTime);
                    headMotion.ParentSpacePosition += deltaH2;
                    if (physicsDetector.IsGrounded(headMotion, out var displacement2, true))
                    {
                        /*增强优化：检测到自己下方存在地面，与之前第一次求解出运动结果的计算进行对照验证。如果相同则跳过，如果不同就重新发起落点寻找
                        if (physicsDetector.GetLandingPoint(cachedMotionState, cachedPhysicsState, deltaTime, direction, out var result2, false) && result2 != null)
                        {
                            var cachedEndTime = beginTime + deltaTime + result2.FinalT;
                            if (endTime - cachedEndTime > 0.1f)//如果落地点检测结果和实际落地点相差过大，说明之前的落地点检测结果不准确，需要修正
                            {
                                cachedPhysicsState.NodeMotionType = MotionType.Falling;
                                goto case MotionType.Falling;
                            }
                        }*/
                    }
                    else if (displacement2.HasValue)
                    {
                        //cachedPhysicsState.NodeMotionType = MotionType.Grounded;
                        hasLimitedLength = true;
                        endTime = time;
                        CachedEndDisplacement = displacement2;
                    }
                    break;
            }
            //if (hasLimitedLength && cachedTime >= endTime) onNodeEnd?.Invoke(this);
            if (!hasInitialized)
            {
                switch (CachedNodeNodeMotionType)
                {
                    case NodeMotionType.Grounded:
                        tail.SetActive(true);
                        break;
                    default:
                        tail.SetActive(false);
                        break;
                }

                hasInitialized = true;
            }
            SetDebugInfo();
            
            return headMotion;
        }
        
        public PhysicsState GetPhysicsState(double time)
        {
            var deltaTime = (float)(time - beginTime);
            switch (CachedNodeNodeMotionType)
            {
                case NodeMotionType.Grounded:
                    return new PhysicsState(){Velocity = Vector3.zero, Gravity = cachedGravity, NodeMotionType = NodeMotionType.Grounded};
                case NodeMotionType.GroundedToFalling://当前节点刚失去地面接触，开始自由落体
                    if (time < endTime) goto case NodeMotionType.Grounded;//没有完全离地前仍然按照贴地计算
                    return new PhysicsState(){Velocity = Vector3.zero, Gravity = cachedGravity, NodeMotionType = NodeMotionType.Falling};
                case NodeMotionType.Falling://当前节点降落中
                    return new PhysicsState()
                    {
                        Velocity = PhysicsHelper.CalculateVelocity(cachedBeginVelocity, cachedGravity, deltaTime),
                        Gravity = cachedGravity,
                        NodeMotionType = NodeMotionType.Falling
                    };
                case NodeMotionType.FallingToGrounded://当前节点成功找到落地地点
                    if (time < endTime) goto case NodeMotionType.Falling;//没有完全落地前仍然按照自由落体计算
                    deltaTime = (float)(endTime - beginTime);
                    return new PhysicsState()
                    {
                        Velocity = PhysicsHelper.CalculateVelocity(cachedBeginVelocity, cachedGravity, deltaTime),
                        Gravity = cachedGravity,
                        NodeMotionType = NodeMotionType.Grounded
                    };
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public void DeleteNode()
        {
            tail.DeleteTail();
            Destroy(gameObject);
        }
        
        internal void SetDebugInfo()
        {
            debugInfo = "";
            debugInfo += $"NodeType: {NodeType}\n";
            debugInfo += $"BeginTime: {BeginTime}, BeginPosition: {cachedBeginPosition}\n";
            debugInfo += $"Direction: id = {direction.ID}, nextID = {direction.NextDirectionID}\n";
        }
    }
}