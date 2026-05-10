using System;
using System.Collections.Generic;
using System.Linq;
using MusicTogether.DancingBall.Scene;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace MusicTogether.DancingBall.Data
{
    
    [Serializable]
    public class ClassicBlockDisplacementData : IBlockDisplacementData
    {
        public enum TurnType { None, Left, Right, Jump }
        public enum DisplacementType { None, Up, Down, ForwardUp, ForwardDown }
        
        [OdinSerialize]public int BlockIndex_Local { get; private set; }
        [OdinSerialize]public TurnType turnType;
        [OdinSerialize]public DisplacementType displacementType;
        
        public bool HasDisplacementRule => turnType != TurnType.None && displacementType != DisplacementType.None;

        private ClassicBlockDisplacementData() { }
        public ClassicBlockDisplacementData(int BlockLocalIndex)
        {
            this.BlockIndex_Local = BlockLocalIndex;
            this.turnType = TurnType.None;
            this.displacementType = DisplacementType.None;
        }

        /// <summary>
        /// 单个 Block 的放置数据（纯数据，不持有场景对象引用）。
        /// Block所有的拐弯模式：(TurnType + DisplacementType) 的组合
        /// 1.需要点击转向：
        /// Left/Right + None：在当前水平面内分别向左/右转90度。
        /// Left/right + Up/Down：左右转90°+上下转90°
        /// Jump + None：跳跃，继续向前但是空出2个位置
        /// 2.不需要点击
        /// None + Up/Down：向上/下转90°
        /// None + ForwardUp/ForwardDown：向上/下转45°(并不真正旋转45°，而是按照切比雪夫距离在斜对角上移动1单位)
        /// 其余组合不合法，视为 None + None（正常向前）
        /// </summary>
        public void ApplyDisplacementRule(List<IBlock> targetBlocks)
        {
            targetBlocks.Sort((a, b) => a.BlockLocalIndex.CompareTo(b.BlockLocalIndex));
            var rootBlock = targetBlocks[0];
            int rootBlockLocalIndex = rootBlock.BlockLocalIndex;
            Vector3 startPosition = rootBlock.Transform.localPosition;
            Quaternion previousRotation = rootBlock.Transform.localRotation;
            
            int displacementID = (int)turnType * 10 + (int)displacementType;
            Quaternion turnRotation = turnType switch
            {
                TurnType.Left => Quaternion.Euler(0, -90, 0),
                TurnType.Right => Quaternion.Euler(0, 90, 0),
                _ => Quaternion.identity
            };
            Quaternion displacementRotation = displacementType switch
            {
                DisplacementType.Up => Quaternion.Euler(-90, 0, 0),
                DisplacementType.Down => Quaternion.Euler(90, 0, 0),
                _ => Quaternion.identity
            };

            turnRotation *= displacementRotation;
            
            var transformList = targetBlocks.ConvertAll(b => b.Transform);
            switch (displacementID)
            {
                //1* 2* 左转右转 3* 跳跃
                //*0 平路 *1 *2 垂直向上/下 *3 *4 斜向上/下
                case 10: goto BottomTileWithNormalDisplacement;
                case 20: goto BottomTileWithNormalDisplacement;
                case 11: goto Upward;
                case 21: goto Upward;
                case 12: goto Downward;
                case 22: goto Downward;
                case 13: goto UpwardStair;
                case 23: goto UpwardStair;
                case 14: goto DownStair;
                case 24: goto DownStair;
                case 30: goto Jump;
                case 01: goto Upward;
                case 02: goto Downward;
                case 03: goto UpwardStair;
                case 04: goto DownStair;


                Upward:
                    ApplyTile(new List<ITileHolder> { rootBlock.TileHolder }, false, true);
                    ApplyBottomTile(targetBlocks.Skip(1).Select(b => b.TileHolder).ToList());
                    ApplyLineDisplacement(transformList, startPosition, previousRotation, turnRotation);
                    break;
                Downward:
                    ApplyBottomTile(targetBlocks.Select(b => b.TileHolder).ToList());
                    ApplyDownwardDisplacement(transformList, startPosition, previousRotation, turnRotation);
                    break;
                Jump:
                    ApplyBottomTile(targetBlocks.Select(b => b.TileHolder).ToList());
                    var emptyHolders = targetBlocks
                        .Where(b => b.BlockLocalIndex > rootBlockLocalIndex && b.BlockLocalIndex <= rootBlockLocalIndex + 1)
                        .Select(b => b.TileHolder).ToList();
                    ApplyEmptyTile(emptyHolders);
                    ApplyLineDisplacement(transformList, startPosition, previousRotation, Quaternion.identity);
                    break;
                UpwardStair:
                    var holdersUS = targetBlocks.Select(b => b.TileHolder).ToList();
                    ApplyTile(holdersUS, true, false);
                    ApplyBottomTile(new List<ITileHolder> { holdersUS.Last() });
                    ApplyStairDisplacement(transformList, startPosition, previousRotation * turnRotation, true);
                    break;
                DownStair:
                    var holdersDS = targetBlocks.Select(b => b.TileHolder).ToList();
                    ApplyTile(holdersDS, false, true);
                    ApplyBottomTile(new List<ITileHolder> { holdersDS.First() });
                    ApplyStairDisplacement(transformList, startPosition, previousRotation * turnRotation, false);
                    break;
                BottomTileWithNormalDisplacement:
                    ApplyBottomTile(targetBlocks.Select(b => b.TileHolder).ToList());
                    goto NormalDisplacement;
                SkipFirstThenBottomTileWithNormalDisplacement:
                    ApplyBottomTile(targetBlocks.Skip(1).Select(b => b.TileHolder).ToList());
                    goto NormalDisplacement;
                NormalDisplacement:
                    ApplyLineDisplacement(transformList, startPosition, previousRotation, turnRotation);
                    break;
                default:
                    ApplyLineDisplacement(transformList, startPosition, previousRotation, Quaternion.identity);
                    ApplyBottomTile(targetBlocks.Skip(1).Select(b => b.TileHolder).ToList());
                    break;
            }
        }
        
        private void ApplyLineDisplacement(List<Transform> blockTransforms, Vector3 startPosition, Quaternion previousRotation, Quaternion deltaRotation)
        {
            var rotation = previousRotation * deltaRotation;
            for (int i = 0; i < blockTransforms.Count; i++)
            {
                var t = blockTransforms[i];
                t.localPosition = startPosition + rotation * Vector3.forward * i;
                t.localRotation = rotation;
            }
        }

        private void ApplyDownwardDisplacement(List<Transform> blockTransforms, Vector3 startPosition, Quaternion previousRotation, Quaternion deltaRotation)
        {
            blockTransforms[0].localPosition = startPosition;
            blockTransforms[0].localRotation = previousRotation;
            var rotation = previousRotation * deltaRotation;
            for (int i = 1; i < blockTransforms.Count; i++)
            {
                var t = blockTransforms[i];
                t.localPosition = startPosition + rotation * (Vector3.up + Vector3.forward * i);
                t.localRotation = rotation;
            }
        }
        private void ApplyStairDisplacement(List<Transform> blockTransforms, Vector3 startPosition, Quaternion rotation, bool isUpward)
        {
            for (int i = 0; i < blockTransforms.Count; i++)
            {
                var t = blockTransforms[i];
                t.localPosition = startPosition + rotation * (Vector3.forward + (isUpward ? Vector3.up : Vector3.down)) * i;
                t.localRotation = rotation;
            }
        }

        private void ApplyTile(List<ITileHolder> TileHolders, bool forward, bool backward)
        {
            if (TileHolders == null || TileHolders.Count == 0) return;
            foreach (var TileHolder in TileHolders)
            {
                TileHolder.SetTileActive(forward, backward);
            }
        }

        private void ApplyBottomTile(List<ITileHolder> TileHolders) => ApplyTile(TileHolders, false, false);
        private void ApplyEmptyTile(List<ITileHolder> TileHolders) => ApplyTile(TileHolders, false, false);

        public IBlockDisplacementData CloneWithNewIndex(int newBlockLocalIndex)
        {
            return new ClassicBlockDisplacementData(newBlockLocalIndex)
            {
                turnType = this.turnType,
                displacementType = this.displacementType
            };
        }

        public int GetBlockIndexDelta()
        {
            if (displacementType == DisplacementType.Down) return 0;
            return 1;
        }
    }
}