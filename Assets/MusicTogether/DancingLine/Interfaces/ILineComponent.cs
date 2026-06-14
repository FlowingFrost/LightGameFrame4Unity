using MusicTogether.LevelManagement;
using UnityEngine;

namespace MusicTogether.DancingLine.Interfaces
{
    public interface ILineComponent// : ILevelUnion
    {
        Transform Transform { get; }
        ILineController Controller { get; }
        LevelState LevelState { get; }
        
        /// <summary>
        /// 通过 Timeline 的 LineTrack 更新线头位置
        /// 当多个 Pool Clip 重叠时，会接收混合后的 MotionState
        /// </summary>
        /// <param name="motionState">混合后的运动状态</param>
        void UpdatePosition(MotionState motionState);
    }
}