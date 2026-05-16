using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LightGameFrame.LightAnimation.Timeline
{
    [TrackColor(0.2f, 0.8f, 0.4f)]
    [TrackBindingType(typeof(AnimationManagerBase))]
    [TrackClipType(typeof(AnimationDriverAsset))]
    public class AnimationTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<AnimationMixerBehaviour>.Create(graph, inputCount);
            var behaviour = mixer.GetBehaviour();

            var director = go.GetComponent<PlayableDirector>();
            if (director != null)
                behaviour.Manager = director.GetGenericBinding(this) as AnimationManagerBase;

            return mixer;
        }
    }
}
