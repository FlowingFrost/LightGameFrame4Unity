namespace LightGameFrame.LightAnimation
{
    public interface IAnimationClip
    {
        double BeginTime { get; }
        double EndTime { get; }
        double Duration { get; }
        bool IsActive { get; set; }

        void Apply(double progress);
        void Reset();
        void CaptureOriginal();
    }
}
