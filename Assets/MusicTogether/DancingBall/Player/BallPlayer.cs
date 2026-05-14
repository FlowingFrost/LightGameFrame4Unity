using System.Collections.Generic;
using MusicTogether.DancingBall.Scene;
using MusicTogether.DancingBall.SceneElements;
using MusicTogether.LevelManagement;
using Sirenix.OdinInspector;
using UnityEngine;

//设计思路：
//切换到某个road时，road内的data按照begin时间（现在没有声明动画，暂时使用clickTime + duration*2）顺序排列

namespace MusicTogether.DancingBall.Player
{
    public class BallPlayer : SerializedMonoBehaviour
    {
        public enum PlayerState { }

        public struct DisplacementDebugData
        {
            public bool HasData;
            public float DeltaTimeRatio;
            public float CorrectionT;
            public Vector3 StandardDisplacement;
            public Vector3 ActualDisplacement;
            public Vector3 ActualOnStandardVector;
            public Vector3 ActualOnOrthogonalVector;
            public Vector3 PreviousDataPosition;
            public Vector3 CurrentDataPosition;
            public Vector3 PreviousMotionPosition;
        }
        private const float StandardDisplacementSqrEpsilon = 1e-6f;

        [SerializeField] private ILevelManager levelManager;
        private double Time => levelManager.LevelTime;
        [SerializeField] private IMap map;
        [SerializeField] private float ballRadius;
        [SerializeField] private AnimationCurve motionCorrectionCurve;
        
        private int currentRoadIndex = 0;
        private int currentDataIndex = 0;
        
        private IRoad CurrentRoad => map.Roads[currentRoadIndex];
        private MovementData CurrentData => CurrentRoad.MovementDatum[currentDataIndex];
        private MovementData previousData, nextData;
        private double CurrentDataTime => CurrentData.Time;
        private double PreviousDataTime => previousData.Time;
        private bool IsRoadIndexOutOfRange(int roadIndex) => roadIndex < 0 || roadIndex >= map.Roads.Count;
        private bool IsDataIndexOutOfCurrentRange(int dataIndex) => dataIndex < 0 || dataIndex >= CurrentRoad.MovementDatum.Count;
        private bool IsDataIndexOutOfRange(int roadIndex, int dataIndex)
        {
            if (IsRoadIndexOutOfRange(roadIndex))
                return true;
            if (dataIndex < 0 || dataIndex >= map.Roads[roadIndex].MovementDatum.Count)
                return true;
            return false;
        }
        
        private double previousMotionPointTime;
        private Vector3 previousMotionPointPosition;
        private Quaternion previousMotionPointRotation;

        private DisplacementDebugData _debugData;

        public bool TryGetDebugData(out DisplacementDebugData data)
        {
            data = _debugData;
            return _debugData.HasData;
        }

        private bool DetectInput() => Input.GetMouseButtonDown(0);

        protected virtual void RecordPreviousMotionPoint()
        {
            previousMotionPointTime = Time;
            previousMotionPointPosition = transform.position;
            previousMotionPointRotation = transform.GetChild(0).rotation;
        }
        private bool GetPreviousData(out MovementData data) => GetPreviousData(1, out data);

        private bool GetPreviousData(int step, out MovementData data)
        {
            data = null;
            if (step <= 0)
            {
                data = CurrentData;
                return data != null;
            }

            int roadIndex = currentRoadIndex;
            int dataIndex = currentDataIndex;

            for (int i = 0; i < step; i++)
            {
                if (dataIndex - 1 < 0)
                {
                    roadIndex--;
                    if (IsRoadIndexOutOfRange(roadIndex))
                        return false;
                    dataIndex = map.Roads[roadIndex].MovementDatum.Count - 1;
                }
                else
                {
                    dataIndex--;
                }
            }

            if (IsDataIndexOutOfRange(roadIndex, dataIndex))
                return false;

            data = map.Roads[roadIndex].MovementDatum[dataIndex];
            return true;
        }
        private bool GotoPreviousData()
        {
            if (IsDataIndexOutOfCurrentRange(currentDataIndex - 1))
            {
                if (IsRoadIndexOutOfRange(currentRoadIndex - 1))
                    return false;
                else
                {
                    nextData = CurrentData;
                    GetPreviousData(out previousData);
                    
                    currentRoadIndex--;
                    currentDataIndex = map.Roads[currentRoadIndex].MovementDatum.Count - 1;
                    //transform.SetParent(CurrentRoad.Transform);
                    transform.position = CurrentData.GetPlayerPosition(ballRadius);
                    transform.GetChild(0).rotation = CurrentData.GetPlayerRotation();
                    RecordPreviousMotionPoint();
                    return true;
                }
            }
            nextData = CurrentData;
            GetPreviousData(out previousData);
            
            currentDataIndex--;
            RecordPreviousMotionPoint();
            return true;
        }

        private bool GetNextData(out MovementData data) => GetNextData(1, out data);

        private bool GetNextData(int step, out MovementData data)
        {
            data = null;
            if (step <= 0)
            {
                data = CurrentData;
                return data != null;
            }

            int roadIndex = currentRoadIndex;
            int dataIndex = currentDataIndex;

            for (int i = 0; i < step; i++)
            {
                if (dataIndex + 1 >= map.Roads[roadIndex].MovementDatum.Count)
                {
                    roadIndex++;
                    if (IsRoadIndexOutOfRange(roadIndex))
                        return false;
                    dataIndex = 0;
                }
                else
                {
                    dataIndex++;
                }
            }

            if (IsDataIndexOutOfRange(roadIndex, dataIndex))
                return false;

            data = map.Roads[roadIndex].MovementDatum[dataIndex];
            return true;
        }
        private bool GotoNextData()
        {
            if (IsDataIndexOutOfCurrentRange(currentDataIndex + 1))
            {
                if (IsRoadIndexOutOfRange(currentRoadIndex + 1))
                    return false;
                else
                {
                    previousData = CurrentData;
                    GetNextData(out nextData);
                    
                    currentRoadIndex++;
                    currentDataIndex = 0;
                    //transform.SetParent(CurrentRoad.Transform);

                    transform.position = CurrentData.GetPlayerPosition(ballRadius);
                    transform.GetChild(0).rotation = CurrentData.GetPlayerRotation();
                    RecordPreviousMotionPoint();
                    //GotoNextData();//传送到第0，立刻开始前往第1
                    return true;
                }
            }
            previousData = CurrentData;
            GetNextData(out nextData);
            
            currentDataIndex++;
            RecordPreviousMotionPoint();
            return true;
        }

        private void SetPlayerTransformOnPlaying(double currentTime)
        {
            //if (transform.parent != CurrentRoad.Transform)
                //transform.SetParent(CurrentRoad.Transform);
            double previousTime = previousMotionPointTime;
            double motionTimeLength = 0;
            Vector3 previousPosition = previousMotionPointPosition;
            Quaternion previousRotation = previousMotionPointRotation;
            

            Vector3 nextPosition = Vector3.zero;
            Quaternion nextRotation = Quaternion.identity;
            if (CurrentData != null)
            {
                motionTimeLength = CurrentDataTime - previousTime;
                nextPosition = CurrentData.GetPlayerPosition(ballRadius);
                nextRotation = CurrentData.GetPlayerRotation();
            }

            float deltaTimeRatio = motionTimeLength == 0
                ? 1f
                : (float)((currentTime - previousTime) / motionTimeLength);
            Vector3 currentPosition = Vector3.LerpUnclamped(previousPosition, nextPosition, deltaTimeRatio);
            float deltaTimeRatioClamped = Mathf.Clamp01(deltaTimeRatio);
            
            
            DisplacementDebugData? debugData = null;
            
            //GetPreviousData(out previousData);
            if (previousData != null && CurrentData != null)
            {
                nextRotation = CurrentData.NeedTap ? previousData.GetPlayerRotation() : CurrentData.GetPlayerRotation();

                Vector3 previousDataPosition = previousData.GetPlayerPosition(ballRadius);
                Vector3 currentDataPosition = CurrentData.GetPlayerPosition(ballRadius);
                Vector3 standardDisplacement = currentDataPosition - previousDataPosition;
                Vector3 actualDisplacement = currentDataPosition - previousPosition;

                if (standardDisplacement.sqrMagnitude > StandardDisplacementSqrEpsilon)
                {
                    Vector3 actualDisplacementOnStandardDirection = Vector3.Project(actualDisplacement, standardDisplacement);
                    Vector3 actualDisplacementOnOrthogonalDirection = actualDisplacement - actualDisplacementOnStandardDirection;
                    currentPosition = previousPosition +
                                      actualDisplacementOnStandardDirection * deltaTimeRatio +
                                      actualDisplacementOnOrthogonalDirection *
                                      motionCorrectionCurve.Evaluate(deltaTimeRatioClamped);
                    //currentRotation = Quaternion.Lerp(previousRotation, previousData.GetPlayerRotation(), motionCorrectionCurve.Evaluate(deltaTimeRatioClamped));
                    
        

                    debugData = new DisplacementDebugData
                    {
                        HasData = true,
                        DeltaTimeRatio = deltaTimeRatio,
                        CorrectionT = deltaTimeRatioClamped,
                        StandardDisplacement = standardDisplacement,
                        ActualDisplacement = actualDisplacement,
                        ActualOnStandardVector = actualDisplacementOnStandardDirection,
                        ActualOnOrthogonalVector = actualDisplacementOnOrthogonalDirection,
                        PreviousDataPosition = previousDataPosition,
                        CurrentDataPosition = currentDataPosition,
                        PreviousMotionPosition = previousMotionPointPosition
                    };
                }
                else
                {
                    debugData = new DisplacementDebugData
                    {
                        HasData = false,
                        StandardDisplacement = standardDisplacement,
                        ActualDisplacement = actualDisplacement,
                        DeltaTimeRatio = deltaTimeRatio,
                        CorrectionT = deltaTimeRatioClamped,
                        PreviousDataPosition = previousDataPosition,
                        CurrentDataPosition = currentDataPosition,
                        PreviousMotionPosition = previousMotionPointPosition
                    };
                }
            }
            else if (currentRoadIndex != 0 || currentDataIndex != 0)
            {
                Debug.LogError("错误的播放状态");
            }
            UpdateDebugData(debugData);
            
            Quaternion currentRotation = Quaternion.Lerp(previousRotation, nextRotation, motionCorrectionCurve.Evaluate(deltaTimeRatioClamped));
            
            //deltaTimeRatio = Mathf.Clamp01(deltaTimeRatio);
            
            
            transform.position = currentPosition;
            transform.GetChild(0).rotation = currentRotation;
        }
        private void SetPlayerTransformOnPreview(double currentTime)
        {
            double previousTime = 0;
            Vector3 previousPosition = Vector3.zero;
            Quaternion previousRotation = Quaternion.identity;
            if (previousData != null)
            {
                previousTime = PreviousDataTime;
                previousPosition = previousData.GetPlayerPosition(ballRadius);
                previousRotation = previousData.GetPlayerRotation();
            }
            double motionTimeLength = 0;
            Vector3 nextPosition = Vector3.zero;
            Quaternion nextRotation = Quaternion.identity;
            if (CurrentData != null)
            {
                motionTimeLength = CurrentDataTime - previousTime;
                nextPosition = CurrentData.GetPlayerPosition(ballRadius);
                nextRotation = CurrentData.GetPlayerRotation();
            }
            
            float deltaTimeRatio = motionTimeLength == 0
                ? 1f
                : (float)((currentTime - previousTime) / motionTimeLength);
            Vector3 currentPosition = Vector3.LerpUnclamped(previousPosition, nextPosition, deltaTimeRatio);
            Quaternion currentRotation = Quaternion.Lerp(previousRotation, nextRotation, deltaTimeRatio);
            transform.position = currentPosition;
            transform.GetChild(0).rotation = currentRotation;
        }

        private void UpdateMovementDataOnPlaying(double currentTime)
        {
            if (CurrentData.NeedTap)
            {
                if (DetectInput())
                {
                    if (previousData == null || PreviousDataTime < currentTime)
                    {
                        GotoNextData();
                    }
                    //previousMotionPointTime = PreviousDataTime;
                }
                else if (currentDataIndex == 0)//第0项不可设为需要点击。
                {
                    GotoNextData();
                }
                /*else if (currentTime > nextData.Time)
                {
                    GotoNextData();
                }*/
            }

            //超时切换
            else if (currentTime > CurrentDataTime)
            {
                while (currentTime > CurrentDataTime && !CurrentData.NeedTap)// && (!CurrentData.NeedTap || DetectInput()) 
                {
                    if (!GotoNextData()) break;
                }
            }
            /*else if (PreviousDataTime > currentTime)
            {
                while (PreviousDataTime > currentTime)
                {
                    if (!GotoPreviousData()) break;
                }
            }*/
        }
        private void UpdateMovementDataOnPreview(double currentTime)
        {
            if (currentTime > CurrentDataTime)
            {
                while (currentTime > CurrentDataTime)
                {
                    if (!GotoNextData()) break;
                }
            }
            else if (PreviousDataTime > currentTime)
            {
                while (PreviousDataTime > currentTime)
                {
                    if (!GotoPreviousData()) break;
                }
            }
        }
        
        public void Update()
        {
            switch (levelManager.CurrentLevelState)
            {
                case LevelState.Playing:
                    UpdateMovementDataOnPlaying(Time);
                    SetPlayerTransformOnPlaying(Time);
                    break;
                case LevelState.Previewing:
                    UpdateMovementDataOnPreview(Time);
                    SetPlayerTransformOnPreview(Time);
                    break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (map == null || map.Roads == null || map.Roads.Count == 0)
                return;
            if (IsRoadIndexOutOfRange(currentRoadIndex) || IsDataIndexOutOfCurrentRange(currentDataIndex))
                return;

            MovementData currentData = CurrentData;
            if (currentData == null)
                return;

            bool hasPreviousData = GetPreviousData(out var gizmoPreviousData);
            Vector3 previousDataPosition = hasPreviousData
                ? gizmoPreviousData.GetPlayerPosition(ballRadius)
                : previousMotionPointPosition;
            Vector3 currentDataPosition = currentData.GetPlayerPosition(ballRadius);
            Vector3 previousMotionPosition = previousMotionPointPosition;

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(previousDataPosition, ballRadius * 0.1f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(currentDataPosition, ballRadius * 0.1f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(previousDataPosition, currentDataPosition);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(previousMotionPosition, currentDataPosition);

            float directionLength = ballRadius;
            float arrowHeadLength = ballRadius * 0.35f;
            float arrowHeadAngle = 25f;

            if (hasPreviousData)
            {
                Quaternion previousRotation = gizmoPreviousData.GetPlayerRotation();
                Vector3 previousForward = previousRotation * Vector3.forward;
                Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.9f);
                Vector3 previousArrowEnd = previousDataPosition + previousForward * directionLength;
                Gizmos.DrawLine(previousDataPosition, previousArrowEnd);
                Vector3 previousArrowLeft = Quaternion.AngleAxis(180f + arrowHeadAngle, previousRotation * Vector3.up) * previousForward;
                Vector3 previousArrowRight = Quaternion.AngleAxis(180f - arrowHeadAngle, previousRotation * Vector3.up) * previousForward;
                Gizmos.DrawLine(previousArrowEnd, previousArrowEnd + previousArrowLeft * arrowHeadLength);
                Gizmos.DrawLine(previousArrowEnd, previousArrowEnd + previousArrowRight * arrowHeadLength);
            }

            Quaternion currentRotation = currentData.GetPlayerRotation();
            Vector3 currentForward = currentRotation * Vector3.forward;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Vector3 currentArrowEnd = currentDataPosition + currentForward * directionLength;
            Gizmos.DrawLine(currentDataPosition, currentArrowEnd);
            Vector3 currentArrowLeft = Quaternion.AngleAxis(180f + arrowHeadAngle, currentRotation * Vector3.up) * currentForward;
            Vector3 currentArrowRight = Quaternion.AngleAxis(180f - arrowHeadAngle, currentRotation * Vector3.up) * currentForward;
            Gizmos.DrawLine(currentArrowEnd, currentArrowEnd + currentArrowLeft * arrowHeadLength);
            Gizmos.DrawLine(currentArrowEnd, currentArrowEnd + currentArrowRight * arrowHeadLength);

            Vector3 standardDisplacement = currentDataPosition - previousDataPosition;
            if (standardDisplacement.sqrMagnitude > StandardDisplacementSqrEpsilon)
            {
                Vector3 actualDisplacement = currentDataPosition - previousMotionPosition;
                Vector3 actualDisplacementOnStandard = Vector3.Project(actualDisplacement, standardDisplacement);
                Vector3 actualDisplacementOnOrthogonal = actualDisplacement - actualDisplacementOnStandard;

                Gizmos.color = Color.blue;
                Gizmos.DrawLine(previousMotionPosition + actualDisplacementOnOrthogonal, previousMotionPosition + actualDisplacementOnOrthogonal + actualDisplacementOnStandard);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(previousMotionPosition, previousMotionPosition + actualDisplacementOnOrthogonal);
            }
        }

        private void UpdateDebugData(DisplacementDebugData? debugData)
        {
            _debugData = debugData ?? new DisplacementDebugData { HasData = false };
        }
    }
}