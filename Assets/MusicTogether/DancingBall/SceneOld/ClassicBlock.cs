using System;
using MusicTogether.DancingBall.Data;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicTogether.DancingBall.SceneOld
{
    public class ClassicBlock : SerializedMonoBehaviour, IBlock
    {
        //外部引用
        public IRoad Road
        {
            get { return _road ??= GetComponentInParent<IRoad>(); }
            private set => _road = value;
        }
        [SerializeField] [ReadOnly] private IRoad _road;

        //本体绑定信息
        public Transform Transform => transform;
        public ITileHolder TileHolder
        {
            get { return _tileHolder ??= GetComponentInChildren<ITileHolder>(); }
            private set => _tileHolder = value;
        }
        [SerializeField] [ReadOnly] private ITileHolder _tileHolder;

        public IBlockDebug BlockDebugDisplay
        {
            get { return _blockDebugDisplay ??= GetComponentInChildren<IBlockDebug>(); }
            private set => _blockDebugDisplay = value;
        }
        [SerializeField] [ReadOnly] private IBlockDebug _blockDebugDisplay;
        //参数
        public int BlockLocalIndex { get => _blockLocalIndex; set => _blockLocalIndex = value; }
        [SerializeField] [ReadOnly] private int _blockLocalIndex = -1;
        //表达式
        public bool IsDataValid => Road != null && BlockLocalIndex >= 0 && BlockLocalIndex < Road.RoadData.BlockCount;
    
#if UNITY_EDITOR
        [Title("Block Data (Preview)")]
        [ShowInInspector, InlineProperty, HideLabel] [ReadOnly]
        private IBlockDisplacementData PreviewBlockDisplacementData
        {
            get
            {
                if (!IsDataValid) return null;
                if (!Road.RoadData.Get_BlockData(BlockLocalIndex, out IBlockDisplacementData blockDisplacementData)) return null;
                return blockDisplacementData;
            }
        }
#endif
        
        //函数
        public void Init(IRoad targetRoad, int blockLocalIndex)
        {
            if (targetRoad == null)
            {
                throw new ArgumentNullException(nameof(targetRoad), "[ClassicBlock.Init] targetRoad 为空，无法初始化 Block。");
            }
            if (blockLocalIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockLocalIndex), blockLocalIndex, "[ClassicBlock.Init] blockLocalIndex 必须 >= 0。");
            }
            Road = targetRoad;
            BlockLocalIndex = blockLocalIndex;
            TileHolder = GetComponentInChildren<ClassicTileHolder>() ?? gameObject.AddComponent<ClassicTileHolder>();
            BlockDebugDisplay = GetComponentInChildren<ClassicBlockDebug>() ?? gameObject.AddComponent<ClassicBlockDebug>();
        }
    }
}