using System;
using MusicTogether.DancingLine.Interfaces;
using MusicTogether.LevelManagement;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MusicTogether.DancingLine.Classic
{
    public class ClassicLineComponent : SerializedMonoBehaviour, ILineComponent
    {
        //绑定信息
        [SerializeField]protected ILevelManager levelManager;
        //[SerializeField]protected ILinePool pool;
        [SerializeField]protected ILineController controller;
        [SerializeField]protected Transform lineHeadTransform;

        //临时数据
        internal double cachedBeginTime;
        //运行参数
        protected double time => levelManager.LevelTime;
        public LevelState LevelState => levelManager.CurrentLevelState;

        [SerializeField] internal TextMeshProUGUI debugText;
        internal string debugInfo;
        //API
        public Transform Transform => lineHeadTransform;
        public ILineController Controller => controller;
        
        public void UpdatePosition(MotionState motionState)
        {
            if (motionState == null)
            {
                debugInfo += $"[{LevelState}] UpdatePosition: Received null MotionState at time {time}\n";
                if(debugText != null) debugText.text = debugInfo;
                return;
            }
            
            lineHeadTransform.position = motionState.WorldSpacePosition;
            lineHeadTransform.rotation = motionState.WorldSpaceRotation;
            
            debugInfo += $"[{LevelState}] UpdatePosition: Pos={motionState.ParentSpacePosition}, Rot={motionState.ParentSpaceRotation.eulerAngles} at time {time}\n";
            if(debugText != null) debugText.text = debugInfo;
        }
    }
}