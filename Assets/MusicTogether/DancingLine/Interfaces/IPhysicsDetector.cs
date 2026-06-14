using System;
using JetBrains.Annotations;
using UnityEngine;

namespace MusicTogether.DancingLine.Interfaces
{
    /// <summary>
    /// 物理检测接口
    /// 负责检测线条与地面的碰撞
    /// </summary>
    public interface IPhysicsDetector
    {
        bool IsGrounded(MotionState lineHeadMotionState, out Vector3? parentSpaceProjectedDisplacement, bool findingMode = false);

        bool GetLandingPoint(MotionState initialMotionState, PhysicsState initialPhysicsState, float currentMoveTime,
            IDirection direction, [CanBeNull] out MotionCalculationResult result, bool verifyResult = true);
    }
}