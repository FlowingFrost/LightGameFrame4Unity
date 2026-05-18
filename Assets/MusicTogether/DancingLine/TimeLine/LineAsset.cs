using MusicTogether.DancingLine.Interfaces;
using MusicTogether.LevelManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace MusicTogether.DancingLine.TimeLine
{
    public class LineAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("实现 ILinePool 接口的 MonoBehaviour 组件")]
        public ExposedReference<MonoBehaviour> linePool;
        
        [Tooltip("混合曲线，用于控制多个 Clip 重叠时的混合权重")]
        public AnimationCurve blendCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        public ClipCaps clipCaps => ClipCaps.Blending; // 支持混合
    
        [HideInInspector] public double clipStart;
        [HideInInspector] public double clipEnd;
        [HideInInspector] public ILineComponent component;
        [HideInInspector] public ILineController controller;
        [HideInInspector] public ILevelManager levelManager;
        
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<LineBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            
            var resolvedPool = linePool.Resolve(graph.GetResolver());
            behaviour.linePool = resolvedPool as ILinePool;
            
            if (resolvedPool != null && behaviour.linePool == null)
            {
                Debug.LogWarning($"[LineAsset] 组件 {resolvedPool.name} 没有实现 ILinePool 接口！");
            }
            
            behaviour.clipStart = clipStart;
            behaviour.clipEnd = clipEnd;
            behaviour.blendCurve = blendCurve;
            behaviour.component = component;
            behaviour.lineController = controller;
            behaviour.levelManager = levelManager;
            
            return playable;
        }
    }
}
