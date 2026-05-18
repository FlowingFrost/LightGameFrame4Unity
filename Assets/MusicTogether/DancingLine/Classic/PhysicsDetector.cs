using System;
using JetBrains.Annotations;
using MusicTogether.DancingLine.Interfaces;
using UnityEngine;
using UnityEngine.Events;
using LightGameFrame.Services;
using Sirenix.OdinInspector;
using UnityEngine.Serialization;

namespace MusicTogether.DancingLine.Classic
{
    public class PhysicsDetector : MonoBehaviour , IPhysicsDetector
    {
        [SerializeField] internal BoxCollider lineHeadCollider;
        [SerializeField] internal float groundBottomCheckBottomShrinkDistance = 0.05f;//直接使用底面作为起点会因为平面与起点贴合而被忽视，因此使用一个微小的距离将起点向上移动，避免起点与地面贴合
        [SerializeField] internal float groundTopCheckTopShrinkDistance = 0.05f;//上面同理，避免与地面贴合
        [SerializeField] internal float groundTopCheckBottomShrinkDistance = 0.00f;
        [SerializeField] internal float groundCheckDistance = 0.2f;
        [SerializeField] internal float groundFindDistance = 2f;
        [SerializeField] internal LayerMask groundLayer;
        [SerializeField] internal bool debug = false;
        
        private DebugDrawService _debugDraw;
        
        private void Awake()
        {
            _debugDraw = DebugDrawService.Instance;
        }
        
        internal Vector3[] DownRayOriginDisplacement
        {
            get
            {
                if (lineHeadColliderCache != lineHeadCollider || downRayOriginDisplacementCache == null || downRayOriginDisplacementCache.Length != 4)
                {
                    lineHeadColliderCache = lineHeadCollider;
                    Vector3 colliderSize = lineHeadCollider.size;
                    downRayOriginDisplacementCache = new Vector3[4];
                    for(int i = -1; i <= 1; i+=2)
                    {
                        for(int j = -1; j <= 1; j+=2)
                        {
                            downRayOriginDisplacementCache[(i+1)/2 + j+1] = new Vector3(i * colliderSize.x / 2, -colliderSize.y/2, j * colliderSize.z/2);
                        }
                    }
                }
                return downRayOriginDisplacementCache;
            }
        }
        internal Vector3[] TopRayOriginDisplacement
        {
            get
            {
                if (lineHeadColliderCache != lineHeadCollider || topRayOriginDisplacementCache == null || topRayOriginDisplacementCache.Length != 4)
                {
                    lineHeadColliderCache = lineHeadCollider;
                    Vector3 colliderSize = lineHeadCollider.size;
                    topRayOriginDisplacementCache = new Vector3[4];
                    for(int i = -1; i <= 1; i+=2)
                    {
                        for(int j = -1; j <= 1; j+=2)
                        {
                            topRayOriginDisplacementCache[(i+1)/2 + j+1] = new Vector3(i * colliderSize.x / 2, colliderSize.y/2, j * colliderSize.z/2);
                        }
                    }
                }
                return topRayOriginDisplacementCache;
            }
        }
        internal BoxCollider lineHeadColliderCache;
        internal Vector3[] downRayOriginDisplacementCache;
        internal Vector3[] topRayOriginDisplacementCache;

        /// <summary>
        /// 针对盒碰撞器的射线检测，使用四条射线分别从盒碰撞器的四个角向下检测，以提高检测的可靠性。
        /// </summary>
        /// <param name="objPosition"></param>
        /// <param name="direction"></param>
        /// <param name="distance"></param>
        /// <param name="fallPoint">注意：返回的位置不是检测到地面上的点，而是盒碰撞器与地面贴合时，其中心所在的位置</param>
        /// <param name="detectFromTop"></param>
        /// <returns></returns>
        internal bool BoxColliderRaycast(MotionState ms, float distance, out Vector3 fallPoint,
            bool detectFromTop = false)
        {
            var direction = -ms.WorldSpaceUpDirection;
            Vector3[] displacements = detectFromTop ? TopRayOriginDisplacement : DownRayOriginDisplacement;
            Vector3 shrink = detectFromTop ? Vector3.down * groundTopCheckTopShrinkDistance : Vector3.up * groundBottomCheckBottomShrinkDistance;//调整起点位置，避免与地面贴合
            
            // 防穿模机制：检测所有点位，取最近的碰撞点
            bool hasHit = false;
            float minDistance = float.MaxValue;
            Vector3 closestFallPoint = Vector3.zero;
            int closestIndex = -1;
            
            for (int i = 0; i < displacements.Length; i++)
            {
                var origin = ms.ObjectPosToWorld(displacements[i] + shrink);
                Ray ray = new Ray(origin, direction);
                if (Physics.Raycast(ray, out var hitInfo, distance, groundLayer))
                {
                    if (hitInfo.distance < minDistance)
                    {
                        minDistance = hitInfo.distance;
                        closestFallPoint = hitInfo.point - ms.ObjectVecToWorld(DownRayOriginDisplacement[i]); //调整为物体中心位置，等效为 hitInfo.point - (origin - objPosition)
                        closestIndex = i;
                        hasHit = true;
                    }
                    if (debug) Debug.DrawLine(origin, origin + direction * distance, Color.yellow);
                }
                else
                {
                    if (debug) Debug.DrawLine(origin, origin + direction * distance, Color.red);
                }
            }
            
            if (hasHit)
            {
                fallPoint = closestFallPoint;
                if (debug) _debugDraw.DrawBox(fallPoint, Vector3.one, ms.WorldSpaceRotation, Color.green, DrawMode.Once);
                return true;
            }
            fallPoint = Vector3.zero;
            return false;
        }

        /// <summary>
        /// 检测是否着地，同时检查是否被地面贯穿，若贯穿则返回物体需要向上移动的位移。
        /// </summary>
        /// <param name="lineHeadMotionState"></param>
        /// <param name="parentSpaceProjectedDisplacement"></param>
        /// <param name="findingMode"></param>
        /// <returns></returns>
        public bool IsGrounded(MotionState lineHeadMotionState, out Vector3? parentSpaceProjectedDisplacement, bool findingMode = false)
        {
            Vector3 worldSpaceUpDirection = lineHeadMotionState.WorldSpaceUpDirection;
            Vector3 worldSpacePosition = lineHeadMotionState.WorldSpacePosition;
            
            parentSpaceProjectedDisplacement = null;
            //先检测碰撞器中间的区域
            var distance = lineHeadCollider.size.y - groundTopCheckBottomShrinkDistance -
                           groundTopCheckTopShrinkDistance;
            if (BoxColliderRaycast(lineHeadMotionState,distance, out var fallPoint, true))
            {
                var parentSpaceDisplacement = lineHeadMotionState.WorldVecToParent(fallPoint - worldSpacePosition);
                parentSpaceProjectedDisplacement = Vector3.Project(parentSpaceDisplacement, lineHeadMotionState.ParentSpaceUpDirection);//物体需要向上移动的距离
                
                // 绘制：上坡时的向上位移（永久）
                if (parentSpaceDisplacement.magnitude > Mathf.Epsilon)
                {
                    if (debug && _debugDraw != null && parentSpaceProjectedDisplacement.HasValue)
                    {
                        var worldDisplacement = lineHeadMotionState.ParentVecToWorld(parentSpaceProjectedDisplacement.Value);
                        _debugDraw.DrawArrow(worldSpacePosition, worldSpacePosition + worldDisplacement, Color.yellow, DrawMode.Permanent, 0.2f, 20f);
                        _debugDraw.DrawBox(worldSpacePosition, lineHeadCollider.size, lineHeadMotionState.WorldSpaceRotation, Color.yellow, DrawMode.Permanent);
                    }
                }
            }
            //再测碰撞器底面以下的区域
            if (BoxColliderRaycast(lineHeadMotionState, groundCheckDistance, out _)) return true;
            
            

            // 绘制：离地时的位置和地面射线（永久）
            if (!findingMode && debug && _debugDraw != null)
            {
                _debugDraw.DrawBox(worldSpacePosition, lineHeadCollider.size, lineHeadMotionState.WorldSpaceRotation, Color.red, DrawMode.Permanent);
                _debugDraw.DrawRay(worldSpacePosition-worldSpaceUpDirection * lineHeadCollider.size.y/2, -worldSpaceUpDirection * groundCheckDistance, Color.red, DrawMode.Permanent);
            }

            return false;
        }
        
        /// <summary>
        /// 计算质点的自由落体运动。在调用之前应以被计算对象为参考系，为所有变量做垂直方向的投影。
        /// </summary>
        internal bool SolveMotionTime(float currentProjectedVelocity, float projectedGravity, float currentMovementime, float h0, out MotionCalculationResult result)
        {
            result = new MotionCalculationResult();
            float a = 0.5f * projectedGravity;
            float b = currentProjectedVelocity;
            float c = -h0;
            
            if (Mathf.Abs(a) < Mathf.Epsilon) // 重力投影为0，匀速运动
            {
                if (Mathf.Abs(b) < Mathf.Epsilon) // 静止
                    return false;
        
                result.T1 = result.T2 = -c / b;
                return b != 0;
            }
            
            float discriminant = b * b - 4 * a * c;
            float sol1 = 0, sol2 = 0;
            
            result.Delta = discriminant;
            if (discriminant < 0)
            {
                return false;
            }
            if (Mathf.Approximately(discriminant, 0f)) // 处理浮点数精度问题
            {
                sol1 = sol2 = -b/(2*a);
                return false;
            }
            
            float sqrtDisc = Mathf.Sqrt(discriminant);

            sol1 = (-b - sqrtDisc) / (2 * a);
            sol2 = (-b + sqrtDisc) / (2 * a);

            result.T1 = sol1;
            result.T2 = sol2;
            result.FinalT = Mathf.Max(sol1, sol2);
            return true;
        }

        /// <summary>
        /// 以当前物体为参考系，计算落地时间。注：由于物体的 位置 和 旋转由 direction 的函数决定，同时为了做预测工作，不直接使用物体Transform做转换。
        /// </summary>
        public bool GetLandingPoint(MotionState initialMotionState, PhysicsState initialPhysicsState,
            float currentMoveTime, IDirection direction, [CanBeNull] out MotionCalculationResult result, bool verifyResult = true)
        {
            Transform selfTransform = initialMotionState.SelfTransform;
            MotionState cachedMS = initialMotionState;
            
            Vector3 parentSpaceBeginPosition = initialMotionState.ParentSpacePosition;
            Vector3 parentSpaceBeginVelocity = initialPhysicsState.Velocity;
            Vector3 parentSpaceGravity = initialPhysicsState.Gravity;
            Vector3 objectSpaceUp = Vector3.up; // (0,1,0)
            
            
            void UpdateMotion(float t)
            {
                cachedMS = direction.GetLineHeadMotionState(parentSpaceBeginPosition, t);
                cachedMS.ParentSpacePosition += ParentSpaceMovementDisplacement(t);//应该加入重力
                cachedMS.SelfTransform = selfTransform;
            }

            Vector3 ParentSpaceMovementDisplacement(float t) => PhysicsHelper.CalculateDisplacement(parentSpaceBeginVelocity, parentSpaceGravity, t);
            Vector3 ParentSpaceVelocity(float t) => parentSpaceBeginVelocity + parentSpaceGravity * t;
            Vector3 ObjectSpaceVelocity(float t) => cachedMS.ParentVecToObject(ParentSpaceVelocity(t));
            Vector3 objectSpaceGravity = cachedMS.ParentVecToObject(parentSpaceGravity); // 转到物体空间
            
            //Vector3 WorldSpaceTotalPosition(float t) => cachedMS.WorldSpacePosition + cachedMS.ParentVecToWorld(ParentSpaceMovementDisplacement(t));
            
            

            float ObjectSpaceUpProjection(Vector3 dir) => Vector3.Dot(objectSpaceUp, dir);
            //第一次检测：物体当前位置进行地面探测
            //[true]正常检测1：  delta > 0 t2 >= 0 验证落点成功 正常的降落或者跳跃
            //[false]正常检测2： delta > 0 t2 >= 0 验证落点失败 说明落地点被地面边际或者其它情况所覆盖，无法着地。属正常情况，可视为第一次检测未检出，让先继续运动即可
            //[true]特殊情况1： delta > 0 t2 < 0, t2 + currentDeltaTime > 0，错过了落地 可以选择倒退回到落地时的时间。
            //[false]特殊情况2：delta > 0 t2 < 0, t2 + currentDeltaTime < 0，此时有几种情况a.线头刚刚在上台阶导致下底面低于平面高度. b.线头降落时正好错过了它前方的平面，正在逐渐嵌入其中。
            //[false]特殊情况3： delta = 0 该节点一开始是着地的，或者是情况2的特例。
            //[false]特殊情况4：delta < 0 与情况2相同。
            //情况 234 统一按上拉线头至地面处理

            UpdateMotion(currentMoveTime);
            var currentGlobalPosition = cachedMS.WorldSpacePosition;
            var currentGlobalUpDirection = cachedMS.ObjectVecToWorld(objectSpaceUp);
            
            if (BoxColliderRaycast(cachedMS, groundFindDistance, out var landingPoint, true))
            //if (BoxColliderRaycast(currentGlobalPosition, -currentGlobalUpDirection, groundFindDistance, out var landingPoint, true))
            {
                //找到地面
                var objectSpaceFallingDisplacement = cachedMS.WorldPosToObject(landingPoint);
                float h = ObjectSpaceUpProjection(objectSpaceFallingDisplacement);
                float v0 = ObjectSpaceUpProjection(ObjectSpaceVelocity(currentMoveTime));
                float g = ObjectSpaceUpProjection(objectSpaceGravity);
                if (SolveMotionTime(v0, g, currentMoveTime,
                        h, out result))
                {
                    if (!verifyResult) return true;
                    //delta > 0
                    var totalMoveTime = currentMoveTime + result.FinalT;
                    if (totalMoveTime > 0)
                    {
                        //找到了落地位置(正常检测/特殊情况1)，可行。接下来完成验证
                        UpdateMotion(totalMoveTime);
                        var landingGlobalPosition = cachedMS.WorldSpacePosition;
                        var landingGlobalUpDirection = cachedMS.ObjectVecToWorld(objectSpaceUp);
                        
                        if (BoxColliderRaycast(cachedMS, groundCheckDistance, out _))
                        //if (BoxColliderRaycast(landingGlobalPosition, -landingGlobalUpDirection, groundCheckDistance, out _))
                        {
                            //正常检测1/特殊情况1 验证通过，落地点存在
                            
                            // 绘制：落地点和轨迹（永久）
                            if (debug && _debugDraw != null)
                            {
                                _debugDraw.DrawBox(landingGlobalPosition, lineHeadCollider.size, cachedMS.WorldSpaceRotation, Color.green, DrawMode.Permanent);
                                _debugDraw.DrawLine(currentGlobalPosition, landingGlobalPosition, Color.cyan, DrawMode.Permanent);
                                _debugDraw.DrawSphere(landingGlobalPosition, 0.1f, Color.green, DrawMode.Permanent);
                            }
                            
                            return true;
                        }
                        else
                        {
                            //正常检测2 未检出重点地面，说明此时地面早已到达边际，或者其它情况。
                            if (debug) _debugDraw.DrawBox(landingGlobalPosition, lineHeadCollider.size, cachedMS.WorldSpaceRotation, Color.red, DrawMode.Permanent);
                            UpdateMotion(currentMoveTime);//退回到发起检测时的状态
                            result = null;
                            return false;
                        }
                    }
                }
                //无法计算时间/验证未通过，特殊情况 234
                //在发起检测的原始位置计算上移向量。由于物体可能存在旋转，直接使用全局向量可能会导致不准确，因此先将全局向量转换到物体空间进行投影，再转换回局部。
                if (!verifyResult) return false;
                var projectedDisplacement =Vector3.Project(cachedMS.ObjectVecToParent(objectSpaceFallingDisplacement), cachedMS.ParentSpaceUpDirection);
                result.Displacement = projectedDisplacement;
                
                // 绘制：需要修正的位移（永久）
                if (debug && _debugDraw != null)
                {
                    var worldDisplacement = cachedMS.ParentVecToWorld(projectedDisplacement);
                    _debugDraw.DrawArrow(currentGlobalPosition, currentGlobalPosition + worldDisplacement, Color.magenta, DrawMode.Permanent, 0.2f, 20f);
                    _debugDraw.DrawBox(currentGlobalPosition + worldDisplacement, lineHeadCollider.size, cachedMS.WorldSpaceRotation, Color.magenta, DrawMode.Permanent);
                }
                
                return false;
                
            }
            //未找到任何地面
            result = null;
            return false;
        }
    }
}