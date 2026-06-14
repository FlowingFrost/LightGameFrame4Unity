using UnityEngine;

namespace MusicTogether.DancingBall.SceneOld
{
    public class ClassicBlockDebug : MonoBehaviour, IBlockDebug
    {
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        [SerializeField] private Renderer blockMask;
        
        public void RefreshBlockDisplay(Color color)
        {
            if (blockMask == null) blockMask = GetComponentInChildren<MeshRenderer>();
            var mpb = new MaterialPropertyBlock();
            blockMask.GetPropertyBlock(mpb);
            mpb.SetColor(Color1, color);
            blockMask.SetPropertyBlock(mpb);
        }

        public void DisplayBlockInformation(string info)
        {
            throw new System.NotImplementedException();
        }
    }
}