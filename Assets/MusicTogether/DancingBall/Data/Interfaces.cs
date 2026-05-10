using System;
using System.Collections.Generic;
using MusicTogether.DancingBall.Scene;
using UnityEngine;

namespace MusicTogether.DancingBall.Data
{
    /// <summary>
    /// 用于声明当前节点的方块位移规则。没有数据则使用前一个，有则使用这里的数据。
    /// </summary>
    public interface IBlockDisplacementData
    {
        public int BlockIndex_Local { get; }
        //public bool HasDisplacementRule { get; } 取消这个设计
        public void ApplyDisplacementRule(List<IBlock> targetBlocks);
        public int GetBlockIndexDelta();
        public IBlockDisplacementData CloneWithNewIndex(int newBlockLocalIndex);
    }
    
    public interface ITileStyleData
    {
        public GameObject TilePrefab { get; }
    }
}