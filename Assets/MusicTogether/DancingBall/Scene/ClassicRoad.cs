using System;
using System.Collections.Generic;
using System.Linq;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.EditorTool;
using MusicTogether.DancingBall.Player;
using MusicTogether.DancingBall.SceneElements;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;

namespace MusicTogether.DancingBall.Scene
{
    public class ClassicRoad : SerializedMonoBehaviour, IRoad
    {
        //外部引用
        public IMap Map
        {
            get { return _map ??= GetComponentInParent<IMap>(); }
            private set => _map = value;
        }
        [SerializeField] [ReadOnly] private IMap _map;
        
        private RoadData cachedRoadData;
        [SerializeField] [ReadOnly] private string targetRoadDataName;
        public RoadData RoadData
        {
            get
            {
                if (cachedRoadData != null && Map.SceneData.roadDataList.Contains(cachedRoadData)) return cachedRoadData;
                if (Map.SceneData.Get_RoadData_ByRoadName(targetRoadDataName, out cachedRoadData)) return cachedRoadData;
                Map.OnRoadDataMissing(this);
                return null;
            }
            private set
            {
                cachedRoadData = value;
                targetRoadDataName = value.roadName;
            }
        }
        public string RoadName => targetRoadDataName;
        private EditorConfig EditorConfig => EditorConfig.Config;
        
        //本体绑定信息
        public Transform Transform => transform;
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private List<IBlock> blocks = new List<IBlock>();
        public List<IBlock> Blocks => blocks;
        //预生成信息
        [SerializeField] private double roadBeginTime, roadEndTime;
        [SerializeField] private double singleBlockDuration;
        public double RoadBeginTime => roadBeginTime;
        public double RoadEndTime => roadEndTime;
        [SerializeField] [ReadOnly] private List<MovementData> movementDatum = new List<MovementData>();
        [SerializeField] [ReadOnly] private List<IAnimationData> animationEventDatum = new List<IAnimationData>();
        public List<MovementData> MovementDatum => movementDatum;
        public List<IAnimationData> AnimationEventDatum => animationEventDatum;
        //运行数据
        [SerializeField] [ReadOnly] private int dirtyLevel;
        //表达式
        public bool IsDataValid
        {
            get
            {
                if (Map == null)
                {
                    return false;
                }
                if (RoadData == null)
                {
                    return false;
                }
                return true;
            }
        }
        
        //函数
        public void Init(IMap map, RoadData roadData, GameObject blockPrefab)
        {
            Map = map;
            RoadData = roadData;
            this.blockPrefab = blockPrefab;
        }


        #region Road_Operations //操作功能
            //物体操作================================================================================================

            [Button("重建Block列表")]
            //Dirty Level = 4
            public void RebuildBlocks()
            {
                if (!IsDataValid)
                {
                    throw new InvalidOperationException("[ClassicMap.RebuildRoads] SceneData 为空，无法重建 Road。");
                }
                blocks.Clear();
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
                if (dirtyLevel <= 4) dirtyLevel = 3;
                RecoverBlocks();
            }
            /// <summary>
            /// 暂未优化该函数，仅仅完成了错误处理
            /// </summary>
            [Button("清理重复和无效Block，添加缺失Block，更新Block位置")]
            //Dirty Level = 3
            public void RecoverBlocks()
            {
                if (!IsDataValid)
                {
                    throw new InvalidOperationException($"[ClassicRoad.RefreshRoadBlocks] 数据无效，Map={Map != null}, CachedRoadData={cachedRoadData != null}, RoadName={RoadName}");
                }

                int blockCount = Mathf.Max(0, RoadData.BlockCount);

                // 去重：保留 blockLocalIndex 最小的
                var duplicates = blocks
                    .GroupBy(b => b.BlockLocalIndex)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.OrderBy(b => b.BlockLocalIndex).Skip(1))
                    .ToList();
                if (duplicates.Count > 0)
                {
                    RemoveBlocks(duplicates);
                }

                int formerCount = blocks.Count;
                if (formerCount < blockCount)
                {
                    CreateBlocks(formerCount, blockCount - formerCount);
                }
                else if (formerCount > blockCount)
                {
                    var toRemove = blocks.Where(b => b.BlockLocalIndex >= blockCount).ToList();
                    RemoveBlocks(toRemove);
                }

                if (blocks.Count == 0 && blockCount > 0)
                {
                    throw new InvalidOperationException($"[ClassicRoad.RefreshRoadBlocks] 期望创建 {blockCount} 个 Block 但结果为 0。请检查 blockPrefab、CreateBlock 以及 RoadData.BlockCount。RoadName={RoadName}");
                }

                // 重新赋值 localIndex 按顺序
                var sorted = blocks.OrderBy(b => b.BlockLocalIndex).ToList();
                blocks.Clear();
                blocks.AddRange(sorted);
                for (int i = 0; i < blocks.Count; i++)
                {
                    blocks[i].BlockLocalIndex = i;
                }
                if (dirtyLevel <= 3) dirtyLevel = 2;
                OnBlockDisplacementRuleChanged();
            }

            /// <summary>
            /// 暂未优化该函数，仅仅完成了错误处理
            /// </summary>
            [Button("更新Block位置")]
            //Dirty Level = 2
            public void OnBlockDisplacementRuleChanged()
            {
                if (!IsDataValid)
                {
                    throw new InvalidOperationException($"[ClassicRoad.OnBlockDisplacementRuleChanged] 数据无效，Map={Map != null}, CachedRoadData={cachedRoadData != null}, RoadName={RoadName}");
                }
                int blockCount = Mathf.Max(0, RoadData.BlockCount);
                var sortedBlocks = blocks
                    .Where(b => b.BlockLocalIndex >= 0 && b.BlockLocalIndex < blockCount)
                    .OrderBy(b => b.BlockLocalIndex)
                    .ToList();
                if (sortedBlocks.Count == 0)
                {
                    throw new InvalidOperationException($"[ClassicRoad.OnBlockDisplacementRuleChanged] 没有有效 Block 可用于位移规则，blocks={blocks.Count}, BlockCount={blockCount}, RoadName={RoadName}");
                }

                // 分组：遇到具有位移规则的块时开启新组
                var groups = new List<List<IBlock>>();
                var currentGroup = new List<IBlock>();
                groups.Add(currentGroup);

                foreach (var block in sortedBlocks)
                {
                    bool isNewGroupStart = currentGroup.Count > 0 && RoadData.blockDisplacementDataList.Exists(b => b.BlockIndex_Local == block.BlockLocalIndex);
                    if (isNewGroupStart)
                    {
                        currentGroup.Add(block);
                        currentGroup = new List<IBlock> { block };
                        groups.Add(currentGroup);
                    }
                    else
                    {
                        currentGroup.Add(block);
                    }
                }

                foreach (var group in groups)
                {
                    if (group.Count == 0) continue;
                    var rootBlock = group[0];
                    if (!RoadData.Get_BlockData(rootBlock.BlockLocalIndex, out var blockData) || blockData == null) continue;
                    blockData.ApplyDisplacementRule(group);
                }

                if (dirtyLevel <= 2) dirtyLevel = 1;
                RefreshBlockInfoDisplay();
            }
            [Button("更新Block显示信息")]
            //Dirty Level = 1
            public void RefreshBlockInfoDisplay()
            {
                if (!IsDataValid)
                {
                    throw new InvalidOperationException($"[ClassicRoad.RefreshBlockInfoDisplay] 数据无效，Map={Map != null}, CachedRoadData={cachedRoadData != null}, RoadName={RoadName}");
                }
                if (blocks == null || blocks.Count == 0)
                {
                    throw new InvalidOperationException($"[ClassicRoad.RefreshBlockInfoDisplay] blocks 为空，无法刷新显示。RoadName={RoadName}");
                }

                foreach (var block in blocks)
                {
                    var color = GetBlockColor(RoadData, block.BlockLocalIndex);
                    block.BlockDebugDisplay?.RefreshBlockDisplay(color);
                }

                if (dirtyLevel <= 1) dirtyLevel = 0;
            }

            //数据操作================================================================================================
            //Road级别
            [Button("更改Note起始序号，重建Block列表")]
            public void ModifyNoteBeginIndex(int newBeginIndex)
            {
                if (!IsDataValid) return;
                RoadData.noteBeginIndex = newBeginIndex;
                RecoverBlocks();//Map.EditManager?.OnRoadBlockCountChanged(EditorTool.EditorActionContext.ForRoad(Map, GetRoadIndex()));
            }
            [Button("更改Note结束序号，重建Block列表")]
            public void ModifyNoteEndIndex(int newEndIndex)
            {
                if (!IsDataValid) return;
                RoadData.noteEndIndex = newEndIndex;
                RecoverBlocks();//Map.EditManager?.OnRoadBlockCountChanged(EditorTool.EditorActionContext.ForRoad(Map, GetRoadIndex()));
            }
            [Button("更改Note起止序号，重建Block列表")]
            public void ModifyNoteRange(int newBeginIndex, int newEndIndex)
            {
                if (!IsDataValid) return;
                RoadData.noteBeginIndex = newBeginIndex;
                RoadData.noteEndIndex = newEndIndex;
                RecoverBlocks();//Map.EditManager?.OnRoadBlockCountChanged(EditorTool.EditorActionContext.ForRoad(Map, GetRoadIndex()));
            }
            [Button("更改目标Segment")]
            public void ModifyTargetSegmentIndex(int newSegmentIndex)
            {
                if (!IsDataValid) return;
                RoadData.targetSegmentIndex = newSegmentIndex;
                RecoverBlocks();//Map.EditManager?.OnRoadBlockCountChanged(EditorTool.EditorActionContext.ForRoad(Map, GetRoadIndex()));
            }
            [Button("更改目标Road数据名称")]
            public void ModifyTargetRoadDataName(string newName)
            {
                if (!IsDataValid) return;
                RoadData.roadName = newName;
                targetRoadDataName = newName;
            }
            [Button("保存transform信息")]
            public void SaveTransformData()
            {
                if (!IsDataValid) return;
                RoadData.loaclPosition = Transform.localPosition;
                RoadData.loaclRotation = Transform.localRotation;
                RoadData.localScale = Transform.localScale;
            }

            [Button("确保Block位移数据")]
            public bool EnsureBlockDisplacementData(int blockLocalIndex, IBlockDisplacementData data)
            {
                if (!IsDataValid) return false;
                if (data == null) return false;
                RoadData.AddOrReplace_BlockData(data);
                OnBlockDisplacementRuleChanged();
                return true;
            }

            [Button("移除Block位移数据")]
            public bool RemoveBlockDisplacementData(int blockLocalIndex)
            {
                if (!IsDataValid) return false;
                int indexInList = RoadData.Get_BlockDisplacementDataListIndex_ByBlockIndexLocal(blockLocalIndex);
                if (indexInList < 0) return false;
                RoadData.Remove_BlockData(blockLocalIndex);
                OnBlockDisplacementRuleChanged();
                return true;
            }
            
            //预处理数据
            [Button("生成Block MovementData（测试）")]
            public void GenerateBlockMovementData()
            {
                if (!Map.SceneData.GetSegment(RoadData.targetSegmentIndex, out var targetSegment)) throw new Exception("找不到目标Segment，无法生成Block MovementData");
                movementDatum.Clear();
                singleBlockDuration = targetSegment.GetNoteTimeAt(1);
                foreach (var block in blocks)
                {
                    double blockTime = targetSegment.GetNoteTimeAt(RoadData.noteBeginIndex + block.BlockLocalIndex);
                    bool blockNeedTap = targetSegment.notes.Contains(RoadData.noteBeginIndex + block.BlockLocalIndex);
                    movementDatum.AddRange(block.TileHolder.GetTileMovementDatum(blockTime, singleBlockDuration, blockNeedTap));
                }
                bool movementDatumHasData = movementDatum.Count > 0;
                if (!movementDatumHasData) throw new Exception("生成的MovementData列表为空，请检查Block.TileHolder.GetTileMovementDatum的实现");
                
                roadBeginTime = movementDatum.First().Time;
                roadEndTime = movementDatum.Last().Time;
            }
        #endregion


        //操作功能

        //数据获取
        private int GetRoadIndex()
        {
            if (Map == null) return -1;
            return Map.Roads.IndexOf(this);
        }
        private int GetRoadDataIndex()
        {
            if (Map?.SceneData == null || RoadData == null) return -1;
            return Map.SceneData.roadDataList.IndexOf(RoadData);
        }
        private Color GetBlockColor(RoadData roadData, int blockLocalIndex)
        {
            if (!IsDataValid) return EditorConfig.problemBlockColor;
            bool hasRule = roadData.blockDisplacementDataList.Exists(b => b.BlockIndex_Local == blockLocalIndex);
            bool hasTap = Map.SceneData.GetSegment(roadData.targetSegmentIndex, out var segemnt) ? segemnt.notes.Contains(roadData.noteBeginIndex + blockLocalIndex) : false;
            if (hasTap)
                return hasRule
                    ? EditorConfig.tapBlockWithDisplacementColor
                    : EditorConfig.tapBlockWithoutDisplacementColor;
            return hasRule ? EditorConfig.normalBlockWithDisplacementColor : EditorConfig.normalBlockColor;
        }
        public List<MovementData> GetBlockMovementDatum(int blockBeginIndex)
        {
            return new List<MovementData>();
        }
        //数据操作
        public void ModifyDisplacementData(int blockLocalIndex, IBlockDisplacementData newDisplacementData)
        {
            if (!IsDataValid)
            {
                throw new InvalidOperationException($"[ClassicRoad.ModifyDisplacementData] 数据无效，Map={Map != null}, CachedRoadData={cachedRoadData != null}, RoadName={RoadName}, BlockLocalIndex={blockLocalIndex}");
            }
            if (newDisplacementData == null)
            {
                throw new ArgumentNullException(nameof(newDisplacementData), $"[ClassicRoad.ModifyDisplacementData] newDisplacementData 为空。RoadName={RoadName}, BlockLocalIndex={blockLocalIndex}");
            }
            RoadData.AddOrReplace_BlockData(newDisplacementData);
            OnBlockDisplacementRuleChanged();
        }
        //地图操作
        /// <summary>
        /// 从prefab创建Block。未预装Block脚本时自动添加ClassicBlock
        /// </summary>
        public IBlock CreateBlock(int blockLocalIndex)
        {
            if (!IsDataValid)
            {
                throw new InvalidOperationException($"[ClassicRoad.CreateBlock] 数据无效，Map={Map != null}, CachedRoadData={cachedRoadData != null}, RoadName={RoadName}, BlockLocalIndex={blockLocalIndex}");
            }
            if (blocks.Exists(b => b.BlockLocalIndex == blockLocalIndex))
            {
                throw new InvalidOperationException($"[ClassicRoad.CreateBlock] Block with local index {blockLocalIndex} already exists in the road. RoadName={RoadName}");
            }
            
            var parent = Transform;

            if (blockPrefab == null)
            {
                throw new InvalidOperationException($"[ClassicRoad.CreateBlock] Block prefab does not exist. RoadName={RoadName}, BlockLocalIndex={blockLocalIndex}");
            }

            GameObject blockObj = Instantiate(blockPrefab, parent, false);
            blockObj.name = $"Block_{blockLocalIndex}";

            blockObj.transform.localPosition = Vector3.zero;
            blockObj.transform.localRotation = Quaternion.identity;

            if (!blockObj.TryGetComponent<IBlock>(out var block))
            {
                block = blockObj.AddComponent<ClassicBlock>();
            }

            block.Init(this, blockLocalIndex);

            blocks.Add(block);
            return block;
        }
        public List<IBlock> CreateBlocks(IEnumerable<int> index)
        {
            var list = new List<IBlock>();
            foreach (var i in index)
            {
                if (blocks.Exists(b => b.BlockLocalIndex == i)) continue;
                var block = CreateBlock(i);
                if (block != null) list.Add(block);
            }
            return list;
        }
        public List<IBlock> CreateBlocks(int indexBegin, int count)
        {
            var indexList = new List<int>();
            for (int i = 0; i < count; i++)
            {
                indexList.Add(indexBegin + i);
            }
            return CreateBlocks(indexList);
        }
        public void RemoveBlocks(List<IBlock> blocksToRemove)
        {
            foreach (var block in blocksToRemove)
            {
                if (block == null) continue;
                blocks.Remove(block);
                if (block is MonoBehaviour blockBehaviour)
                {
                    DestroyImmediate(blockBehaviour.gameObject);
                }
            }
        }
    }
}