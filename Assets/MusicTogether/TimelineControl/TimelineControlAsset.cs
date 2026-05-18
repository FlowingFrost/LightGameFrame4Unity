using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace MusicTogether.TimelineControl
{
    [Serializable]
    public class TimelineControlAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("控制模式：跳转 / 快进 / 快退")]
        public TimelineControlMode controlMode;

        [Tooltip("跳转目标时间（仅 JumpTo 模式）")]
        public MusicTime startTime = new MusicTime();

        [Tooltip("快进/快退的时间增量")]
        public MusicTime deltaTime = new MusicTime();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<TimelineControlBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();

            behaviour.controlMode = controlMode;
            behaviour.startTime = startTime;
            behaviour.deltaTime = deltaTime;

            return playable;
        }
    }
}
