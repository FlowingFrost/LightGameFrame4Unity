using MusicTogether.DancingLine.Interfaces;
using MusicTogether.LevelManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace MusicTogether.DancingLine.TimeLine
{
    [TrackColor(0.9f, 0.3f, 0.2f)] // 红橙色
    [TrackClipType(typeof(LineAsset))]
    [TrackBindingType(typeof(GameObject))] // 绑定到 GameObject
    public class LineTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            // 创建 Mixer
            var mixer = ScriptPlayable<LineMixerBehaviour>.Create(graph, inputCount);
            var mixerBehaviour = mixer.GetBehaviour();
            
            // ✨ 关键优化：在创建时就缓存 ILineComponent 引用，避免每帧查找
            var director = go.GetComponent<PlayableDirector>();
            if (director != null)
            {
                var boundObject = director.GetGenericBinding(this);
                
                // 尝试多种方式获取 ILineComponent
                if (boundObject is GameObject boundGO)
                {
                    mixerBehaviour.cachedLineComponent = boundGO.GetComponent<ILineComponent>();
                    
                    if (mixerBehaviour.cachedLineComponent == null)
                    {
                        Debug.LogWarning($"[LineTrack] GameObject '{boundGO.name}' 没有实现 ILineComponent 接口！");
                    }
                }
                else if (boundObject is Component component)
                {
                    mixerBehaviour.cachedLineComponent = component.GetComponent<ILineComponent>();
                    
                    if (mixerBehaviour.cachedLineComponent == null)
                    {
                        Debug.LogWarning($"[LineTrack] Component '{component.name}' 没有实现 ILineComponent 接口！");
                    }
                }
            }
            
            // 遍历所有 Clip 并注入时间信息
            if (mixerBehaviour.cachedLineComponent != null)
            {
                var controller = mixerBehaviour.cachedLineComponent.Controller;
                var levelManager = director.GetComponent<ILevelManager>();
                foreach (var clip in GetClips())
                {
                    var lineAsset = clip.asset as LineAsset;
                    if (lineAsset != null)
                    {
                        lineAsset.clipStart = clip.start;
                        lineAsset.clipEnd = clip.end;
                        lineAsset.component = mixerBehaviour.cachedLineComponent;
                        lineAsset.controller = controller;
                        lineAsset.levelManager = levelManager;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[LineTrack] Cannot inject controller: cachedLineComponent is null");
            }

            return mixer;
        }
    }
}