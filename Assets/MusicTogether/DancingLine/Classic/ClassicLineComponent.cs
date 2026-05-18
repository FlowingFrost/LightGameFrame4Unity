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
        
        /*public void Turn()
        {
            debugInfo += $"[{LevelState.ToString()}] Turn input received at time {time} in state {LevelState}\n";
            debugText.text = debugInfo;
            switch (LevelState)
            {
                case LevelState.Playing:
                    pool.AddNode(NodeInputType.Turn, time);
                    break;
                case LevelState.Previewing:
                    levelManager.SetLevelState(LevelState.Playing);
                    pool.ClearNodesAfterTime(time);
                    //goto case LevelState.Playing;
                    pool.AddNode(NodeInputType.Turn, time);
                    break;
                default:
                    break;        
            }
        }*/
        
        /*public void ClearNodesAfterTime(double? time)
        {
            pool.ClearNodesAfterTime(time);
        }

        public void ClearNodesAfterNow()
        {
            pool.ClearNodesAfterTime(time);
        }*/
        
        /*public void Move()
        {
            var currentMotion = pool.CurrentMotionState;//UpdatePool(time);
            lineHeadTransform.position = currentMotion.WorldSpacePosition;
            lineHeadTransform.rotation = currentMotion.WorldSpaceRotation;
        }*/
        
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
        
        //生命周期
        /*public virtual void AwakeUnion()
        {
            // 先取消订阅，防止重复订阅
            controller.OnInputDetected -= Turn;
            controller.OnInputDetected += Turn;
            //levelManager.OnLevelStateChanged += OnLevelStateChanged;
        }

        public void StartUnion(double startTime = 0d)
        {
            cachedBeginTime = startTime;
            //var currentMotion = pool.Init();
            //transform.localPosition = currentMotion.Position;
            //lineHeadTransform.localRotation = currentMotion.Rotation;
        }

        public virtual void UpdateUnion(double currentTime)
        {
            Move();
        }*/

        private void Update()
        {
            //Move();
        }
    }
}