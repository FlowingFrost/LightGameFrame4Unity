using UnityEngine;
using UnityEngine.Playables;

namespace MusicTogether.TimelineControl
{
    public class TimelineControlBehaviour : PlayableBehaviour
    {
        public PlayableDirector targetDirector;
        public TimelineControlMode controlMode;
        public MusicTime startTime;
        public MusicTime deltaTime;

        private bool executed;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (targetDirector == null) return;
            if (executed) return;
            executed = true;

            double clipTime = playable.GetTime();
            double targetTime;

            switch (controlMode)
            {
                case TimelineControlMode.JumpTo:
                    targetTime = startTime.ToSeconds();
                    break;

                case TimelineControlMode.FastForward:
                    targetTime = clipTime + deltaTime.ToSeconds();
                    break;

                case TimelineControlMode.FastRewind:
                    targetTime = clipTime - deltaTime.ToSeconds();
                    break;

                default:
                    return;
            }

            double duration = targetDirector.duration;
            targetTime = System.Math.Max(0, System.Math.Min(targetTime, duration > 0 ? duration : targetTime));

            targetDirector.time = targetTime;
            targetDirector.Evaluate();
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            executed = false;
        }
    }
}
