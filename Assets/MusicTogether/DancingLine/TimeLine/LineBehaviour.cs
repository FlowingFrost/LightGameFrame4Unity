using MusicTogether.DancingLine.Interfaces;
using MusicTogether.LevelManagement;
using UnityEngine;
using UnityEngine.Playables;

namespace MusicTogether.DancingLine.TimeLine
{
    public class LineBehaviour : PlayableBehaviour
    {
        public ILineComponent component;
        public ILineController lineController;
        public ILinePool linePool;
        public ILevelManager levelManager;
        public double clipStart;
        public double clipEnd;
        public AnimationCurve blendCurve;
    
        private bool isInitialized;
        
        /// <summary>
        /// 当前的 MotionState，用于混合
        /// </summary>
        public MotionState CurrentMotionState { get; private set; }
        
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (linePool == null) return;
            
            //linePool.AwakeUnion();
            linePool.Init(component, clipStart);
            if (lineController != null)
            {
                lineController.RegisterPool(linePool, linePool.BeginTime, clipEnd);
            }
            
            isInitialized = false;
        }
        
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (linePool == null) return;
        
            if (!isInitialized)
            {
                linePool.StartUnion(clipStart);
                isInitialized = true;
            }
        
            // 计算当前绝对时间（Clip 内相对时间 + Clip 起始时间）
            double localTime = playable.GetTime();
            double globalTime = localTime + clipStart;

            if (levelManager != null && levelManager.IsEditorPreviewing)
            {
                linePool.EditorPreviewUpdate(globalTime);
            }
            else
            {
                linePool.UpdateUnion(globalTime);
                CurrentMotionState = linePool.UpdatePool(globalTime);
            }
        }
        
        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (lineController != null && linePool != null)
            {
                lineController.UnregisterPool(linePool);
            }
            
            isInitialized = false;
        }
    }
}