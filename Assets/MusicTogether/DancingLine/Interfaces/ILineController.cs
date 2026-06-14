namespace MusicTogether.DancingLine.Interfaces
{
    /// <summary>
    /// 线条控制器接口
    /// 负责检测输入并触发方向改变
    /// </summary>
    public interface ILineController
    {
        
        /// <summary>
        /// 注册一个 Pool 及其时间范围到 Controller
        /// </summary>
        /// <param name="pool">要注册的 Pool</param>
        /// <param name="startTime">Clip 开始接受输入数据的时间</param>
        /// <param name="endTime">Clip 结束接受输入数据的时间</param>
        void RegisterPool(ILinePool pool, double startTime, double endTime);
        
        /// <summary>
        /// 注销一个 Pool
        /// </summary>
        void UnregisterPool(ILinePool pool);
    }
}