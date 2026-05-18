using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace MusicTogether.TimelineControl
{
    [TrackColor(0.9f, 0.6f, 0.2f)]
    [TrackClipType(typeof(TimelineControlAsset))]
    [TrackBindingType(typeof(PlayableDirector))]
    public class TimelineControlTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<TimelineControlMixerBehaviour>.Create(graph, inputCount);
        }
    }

    public class TimelineControlMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var director = playerData as PlayableDirector;
            if (director == null) return;

            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                var inputPlayable = (ScriptPlayable<TimelineControlBehaviour>)playable.GetInput(i);
                var behaviour = inputPlayable.GetBehaviour();
                if (behaviour != null)
                {
                    behaviour.targetDirector = director;
                }
            }
        }
    }
}
