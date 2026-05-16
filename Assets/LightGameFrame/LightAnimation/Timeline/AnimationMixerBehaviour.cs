using UnityEngine.Playables;

namespace LightGameFrame.LightAnimation.Timeline
{
    public class AnimationMixerBehaviour : PlayableBehaviour
    {
        public AnimationManagerBase Manager { get; set; }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (Manager == null) return;
            Manager.Tick(playable.GetTime());
        }
    }
}
