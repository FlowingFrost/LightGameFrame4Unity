namespace MusicTogether.DancingBall.SceneElements
{
    public interface IClickTipObject
    {
        public double BeginTime { get; }
        public double StandardClickTime { get; }
        public double EndTime { get; }
        public void Activate(double beginTime, double standardClickTime, double endTime);
        public void OnClicked(double currentTime);
        public bool UpdateState(double currentTime);
        public void Deactivate();
    }
    
    /// <summary>
    /// 通用动画事件数据（烘焙后供播放读取）
    /// </summary>
    public interface IAnimationData
    {
        /// <summary>
        /// 动画开始时间
        /// </summary>
        double BeginTime { get; }

        /// <summary>
        /// 动画结束时间
        /// </summary>
        double EndTime { get; }
        
        /// <summary>
        /// 动画是否在活动
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 动画进入可播放状态
        /// </summary>
        void OnBegin(double currentTime);

        /// <summary>
        /// 动画结束
        /// </summary>
        void OnEnd(double currentTime);

        /// <summary>
        /// 动画更新（非脚本动画由播放器驱动）
        /// </summary>
        void OnUpdate(double currentTime);
    }

    public interface IEventData
    {
        public int TargetRoadIndex { get; }
        public int TargetBlockIndex { get; }
        public void OnBlockExecute(double currentTime);
    }
}