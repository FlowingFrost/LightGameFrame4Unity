using UnityEngine;
using UnityEngine.Playables;

namespace LightGameFrame.LightAnimation.Timeline
{
    /// <summary>
    /// 最小 PlayableAsset，仅用于让 AnimationTrack 保持活跃。
    /// 放一个 Clip 在 Track 上使其产生 ProcessFrame 回调。
    /// </summary>
    public class AnimationDriverAsset : PlayableAsset
    {
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<AnimationDriverBehaviour>.Create(graph);
        }
    }

    public class AnimationDriverBehaviour : PlayableBehaviour { }
}
