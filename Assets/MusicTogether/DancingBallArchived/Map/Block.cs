//using MusicTogether.LiteAnimation;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicTogether.DancingBallArchived.Map
{
    public class Block : MonoBehaviour
    {
        [Title("Resources")]
        public Transform anchor;
        public Transform downNode,forwardNode,backNode;
        //public Transform upNode, leftNode, rightNode;
        //[Title("Data")] 
        //public AnimationManager animation;
        void Awake()
        {
        }

        public void HideAll()
        {
            downNode.gameObject.SetActive(false);
            forwardNode.gameObject.SetActive(false);
            backNode.gameObject.SetActive(false);
            //upNode.gameObject.SetActive(false);
            //leftNode.gameObject.SetActive(false);
            //rightNode.gameObject.SetActive(false);
        }
    }
}
