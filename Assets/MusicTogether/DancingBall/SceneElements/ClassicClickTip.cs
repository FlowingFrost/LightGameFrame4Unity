using MusicTogether.DancingBall.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicTogether.DancingBall.SceneElements
{
    public class ClassicClickTip : MonoBehaviour, IClickTipObject
    {
        [Title("Resources")]
        [SerializeField] private MeshRenderer[] tapCorners;
        [SerializeField] private MeshRenderer tapEffect;
        [Title("Data")] 
        public float meshAngle;
        public float beginRadius;
        public AnimationCurve circleRadiusCurve;
        public Gradient colorGradient;
        public float fadeTime;
        public AnimationCurve circleFadeCurve,effectFadeCurve;

        [Title("Pre-Processed Data")] 
        public double BeginTime { get; private set; }
        public double StandardClickTime { get; private set; }
        public double EndTime { get; private set; }
        private float angle = 90f;
        private MaterialPropertyBlock propBlock;
        private int colorID;

        [Title("RunTimeData")] 
        private double actualClickTime;
        private Color tapColor;
        private float radius;
        private bool clicked;
        
        
        public void Activate(double beginTime, double standardClickTime, double endTime)
        {
            BeginTime = beginTime;
            StandardClickTime = standardClickTime;
            EndTime = endTime;
            clicked = false;
            actualClickTime = StandardClickTime;
            
            propBlock = new MaterialPropertyBlock();
            colorID = Shader.PropertyToID("_Color");
            tapEffect.GetPropertyBlock(propBlock);
            for (int i = 0; i < tapCorners.Length; i++)
            {
                tapCorners[i].transform.localEulerAngles = new Vector3(0, i*angle, 0);
                tapCorners[i].gameObject.SetActive(true);
            }
            //angle = 360/tapCorners.Length;
        }

        public void OnClicked(double currentTime)
        {
            clicked = true;
            actualClickTime = currentTime;
        }

        public bool UpdateState(double currentTime)
        {
            if (!clicked)
            {
                bool isBehind = currentTime < StandardClickTime;
                double borderValue = isBehind ? BeginTime : EndTime;
                float progress = (float) ((currentTime - borderValue)/ (StandardClickTime - borderValue));
                radius = beginRadius * circleRadiusCurve.Evaluate(progress);
                tapColor = colorGradient.Evaluate(progress);
                propBlock.SetColor(colorID, tapColor);
                for (int i = 0; i < tapCorners.Length; i++)
                {
                    float selfAngle = meshAngle-(i * angle);
                    float angleRad = selfAngle * Mathf.Deg2Rad;
                    tapCorners[i].transform.localPosition = new Vector3(radius * Mathf.Cos(angleRad), 0f, radius * Mathf.Sin(angleRad));
                    tapCorners[i].SetPropertyBlock(propBlock);
                }
                tapColor.a = 0;
                propBlock.SetColor(colorID, tapColor);
                tapEffect.SetPropertyBlock(propBlock);
            }
            else
            {
                float progress = (float)((currentTime - actualClickTime) / (EndTime - actualClickTime));
                tapColor.a = circleFadeCurve.Evaluate(progress)/255;
                propBlock.SetColor(colorID, tapColor);
                for (int i = 0; i < tapCorners.Length; i++)
                {
                    tapCorners[i].SetPropertyBlock(propBlock);
                }
                tapColor.a = effectFadeCurve.Evaluate(progress)/255;
                propBlock.SetColor(colorID, tapColor);
                tapEffect.SetPropertyBlock(propBlock);
            }
            return currentTime <= EndTime;
        }

        public void Deactivate()
        {
            foreach (var t in tapCorners)
                t.gameObject.SetActive(false);
            tapEffect?.gameObject.SetActive(false);
        }
    }
}