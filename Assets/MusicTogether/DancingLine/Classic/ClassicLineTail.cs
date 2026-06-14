using MusicTogether.DancingLine.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicTogether.DancingLine.Classic
{
    public class ClassicLineTail : SerializedMonoBehaviour, ILineTail
    {
        [SerializeField] protected GameObject lineTailObject;
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        public MotionState GetMotionStateAtTime(Vector3 beginPosition, double deltaTime, IDirection direction)
        {
            return direction.GetLineHeadMotionState(beginPosition, deltaTime);
        }

        public MotionState UpdateTail(Vector3 beginPosition, double deltaTime, IDirection direction)
        {
            return direction.UpdatePosition(beginPosition, deltaTime, lineTailObject.transform);
        }
        
        public void DeleteTail()
        {
            Destroy(gameObject);
            lineTailObject = null;
        }
    }
}