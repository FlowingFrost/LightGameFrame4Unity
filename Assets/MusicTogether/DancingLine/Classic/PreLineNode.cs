using MusicTogether.DancingLine.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicTogether.DancingLine.Classic
{
    public class PreLineNode : SerializedMonoBehaviour, ILineNode
    {
        private IDirection direction;
        private double endTime;
        private Vector3 endPosition;
        private PhysicsState endPhysicsState;
        private Vector3 endVelocity => endPhysicsState.Velocity;
        private Vector3 gravity => endPhysicsState.Gravity;
        public NodeInputType NodeType => NodeInputType.Pre;
        public double BeginTime => double.MinValue;
        public double EndTime => endTime;
        public Vector3? EndDisplacement => null;
        public bool HasLimitedLength => true;
        public IDirection Direction => direction;
        public MotionState CachedBeginMotionState => null;
        public PhysicsState InitialPhysicsState => endPhysicsState;
        public NodeMotionType NodeMotionType => endPhysicsState.NodeMotionType;

        public void InitMotion(IPhysicsDetector physicsDetector, PhysicsState initialPhysicsState)
        {
            endPhysicsState = initialPhysicsState;
        }
        public void SetActive(bool isActive) { }
        public void SetDirection(IDirection newDirection) => direction = newDirection;
        public void SetNodeType(NodeInputType newNodeType) { }
        public void SetBeginTime(double newBeginTime) { }
        public void SetEndTime(double newEndTime) { endTime = newEndTime;}
        public void SetBeginPosition(Vector3 newBeginPosition) => endPosition = newBeginPosition;
        public MotionState GetMotionStateAtTime(double time, bool calculatePhysics = true)
        {
            time = time > endTime ? endTime : time;
            var deltaTime = time - endTime;
            var ms = direction.GetLineHeadMotionState(endPosition, deltaTime);
            if (calculatePhysics)
            {
                switch (NodeMotionType)
                {
                    case NodeMotionType.Falling:
                        ms.ParentSpacePosition += PhysicsHelper.CalculateDisplacement(endVelocity, gravity, (float)deltaTime);
                        break;
                    case NodeMotionType.GroundedToFalling:
                        goto case NodeMotionType.Falling;
                }
            }
            ms.SelfTransform = transform;
            return ms;
        }

        public MotionState UpdateMotionState(double time)
        {
            //time is negative, so it represents the time before the line head reaches the start point, and the line head is moving towards the start point along the direction vector. The end position is determined by the initial velocity and gravity, and the direction vector determines the movement direction of the line head.
            var ms = GetMotionStateAtTime(time);
            return ms;
        }

        public PhysicsState GetPhysicsState(double time)
        {
            time = time > 0 ? 0 : time;
            return new PhysicsState()
            {
                Velocity = PhysicsHelper.CalculateVelocity(endVelocity, gravity, (float)time),
                Gravity = gravity,
                NodeMotionType = endPhysicsState.NodeMotionType
            };
        }
        
        public void DeleteNode() { }
    }
}