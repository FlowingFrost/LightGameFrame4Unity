using UnityEngine;

namespace MusicTogether.DancingLine.Interfaces
{
    /// <summary>
    /// 方向数据接口
    /// 定义了线条移动的方向信息
    /// </summary>
    public interface IDirection
    {
        int ID { get; set; }
        int NextDirectionID { get; set; }
        
        Quaternion Rotation { get; }
        MotionState GetLineHeadMotionState(Vector3 startPoint, double time);
        MotionState UpdatePosition(Vector3 startPoint, double time, Transform lineTailTransform);
    }
}