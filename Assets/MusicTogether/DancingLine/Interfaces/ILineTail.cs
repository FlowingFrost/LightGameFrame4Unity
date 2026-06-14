using UnityEngine;

namespace MusicTogether.DancingLine.Interfaces
{
    /// <summary>
    /// 线尾渲染接口
    /// 负责单段线的可视化
    /// </summary>
    public interface ILineTail
    {
        void SetActive(bool active);
        MotionState GetMotionStateAtTime(Vector3 beginPosition, double deltaTime, IDirection direction);
        MotionState UpdateTail(Vector3 beginPosition, double deltaTime, IDirection direction);
        void DeleteTail();
    }
}