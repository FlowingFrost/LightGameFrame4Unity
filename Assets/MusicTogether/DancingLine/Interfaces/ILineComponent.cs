using MusicTogether.LevelManagement;
using UnityEngine;

namespace MusicTogether.DancingLine.Interfaces
{
    public interface ILineComponent// : ILevelUnion
    {
        Transform Transform { get; }
        //ILinePool Pool { get; }
        ILineController Controller { get; }
        //IPhysicsDetector PhysicsDetector { get; }
        LevelState LevelState { get; }
        //IDirection CurrentDirection { get; }
        //Vector3 Gravity { get; }
        
        //bool GetDirectionByID(int targetID, out IDirection direction);
        //bool SetCurrentDirection(int targetID);
        //void Move();
      
        //void Turn(int? newDirectionID);//([CanBeNull] IDirection direction);
        //void OnGroundedChanged(bool grounded, Vector3 groundPoint);
        //void SetCurrentMotionType(MotionType motionType);
        //void OnGravityChanged(Vector3 newGravity);  //void Turn();
        //void ClearNodesAfterNow();
        
        /// <summary>
        /// 通过 Timeline 的 LineTrack 更新线头位置
        /// 当多个 Pool Clip 重叠时，会接收混合后的 MotionState
        /// </summary>
        /// <param name="motionState">混合后的运动状态</param>
        void UpdatePosition(MotionState motionState);
    }
}