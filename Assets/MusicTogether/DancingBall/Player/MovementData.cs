using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicTogether.DancingBall.Player
{
    [Serializable]
    public class MovementData
    {
        [HorizontalGroup("1",120)][LabelWidth(90)][SerializeField] private bool needTap;
        [HorizontalGroup("1")][SerializeField] private double time;
        [HorizontalGroup("2",120)][LabelWidth(90)][SerializeField] private float tileThickness;
        [HorizontalGroup("2")][SerializeField] private Transform tileTransform;
        
        public MovementData(bool needTap, double time, Transform tileTransform, float tileThickness)
        {
            NeedTap = needTap;
            Time = time;
            TileTransform = tileTransform;
            TileThickness = tileThickness;
        }

        public bool NeedTap { get => needTap;
            internal set => needTap = value; }
        public double Time { get => time; set => time = value; }
        public Transform TileTransform { get => tileTransform; private set => tileTransform = value; }
        public float TileThickness { get => tileThickness; private set => tileThickness = value; }

        public Vector3 GetPlayerPosition(float ballRadius)
        {
            return TileTransform.TransformPoint(Vector3.up * (tileThickness + ballRadius));
        }

        public Quaternion GetPlayerRotation()
        {
            return TileTransform.rotation;
        }
        
        public Vector3 GetGlobalForwardDirection()
        {
            return TileTransform.TransformVector(Vector3.forward);
        }
    }
}
