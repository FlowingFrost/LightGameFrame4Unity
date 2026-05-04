using System;
using System.Collections.Generic;
using System.Linq;
using MusicTogether.General;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace MusicTogether.DancingBall.Data
{ 
    [Serializable]
    public class RoadData
    {
        public string roadName;
        public int targetSegmentIndex;
        public int noteBeginIndex;
        public int noteEndIndex;
        public int BlockCount => noteEndIndex - noteBeginIndex + 1;
        public Vector3 loaclPosition;
        public Quaternion loaclRotation;
        public Vector3 localScale = Vector3.one;
        [ListDrawerSettings(DefaultExpandedState = false)]
        [OdinSerialize] public List<IBlockDisplacementData> blockDisplacementDataList = new List<IBlockDisplacementData>();

        public RoadData(int roadIndexGlobal, int targetSegmentIndex = 0, int noteBeginIndex = -1, int noteEndIndex = -1)
        {
            //this.roadIndex_Global = roadIndexGlobal;
            this.targetSegmentIndex = targetSegmentIndex;
            this.noteBeginIndex = noteBeginIndex;
            this.noteEndIndex = noteEndIndex;
        }
        
       
        private void Refresh_BlockDisplacementDataList() => blockDisplacementDataList.Sort((a,b)=> a.BlockIndex_Local.CompareTo(b.BlockIndex_Local));
        
        /// <summary>
        /// 获取目标Block的Displacement数据在列表中的位置。
        /// </summary>
        public int Get_BlockDisplacementDataListIndex_ByBlockIndexLocal(int blockIndexLocal) => blockDisplacementDataList.FindIndex(data => data.BlockIndex_Local == blockIndexLocal);
        
        public bool Get_BlockData(int blockLocalIndex, out IBlockDisplacementData data)
        {
            data = null;
            int indexInList = Get_BlockDisplacementDataListIndex_ByBlockIndexLocal(blockLocalIndex);
            if (indexInList >= 0)
            {
                data = blockDisplacementDataList[indexInList];
                return true;
            }
            return false;
        }
        
        private void Set_BlockData(IBlockDisplacementData data)
        {
            int indexInList = Get_BlockDisplacementDataListIndex_ByBlockIndexLocal(data.BlockIndex_Local);
            if (indexInList >= 0) blockDisplacementDataList[indexInList] = data;
            else blockDisplacementDataList.Insert(~indexInList,data);
        }

        /// <summary>
        /// 添加或替换 Block 位移数据。
        /// </summary>
        public bool AddOrReplace_BlockData(IBlockDisplacementData data)
        {
            if (data == null) return false;
            Set_BlockData(data);
            return true;
        }

        public void Remove_BlockData(int blockIndexLocal)
        {
            int indexInList = Get_BlockDisplacementDataListIndex_ByBlockIndexLocal(blockIndexLocal);
            if (indexInList >= 0) blockDisplacementDataList.RemoveAt(indexInList);
        }

        /// <summary>
        /// 创建并写入 Block 位移数据实例。
        /// </summary>
        public IBlockDisplacementData CreateBlockDisplacementData(int blockLocalIndex, Type dataType)
        {
            if (dataType == null) return null;
            if (!typeof(IBlockDisplacementData).IsAssignableFrom(dataType)) return null;
            try
            {
                var instance = Activator.CreateInstance(dataType, blockLocalIndex) as IBlockDisplacementData;
                if (instance != null) Set_BlockData(instance);
                return instance;
            }
            catch (MissingMethodException)
            {
                Debug.LogError($"CreateBlockDisplacementData failed: {dataType.Name} does not have ctor(int blockLocalIndex).");
                return null;
            }
        }

        /// <summary>
        /// 批量平移指定范围内的 BlockDisplacement 索引。
        /// start &lt; end: 平移 [start, end) 范围内的条目。
        /// start >= end: 平移 [start, BlockCount) 范围内的条目。
        /// 新索引 &lt; 0 的条目被跳过（保持原地）。
        /// 目标位置已有条目时覆盖。
        /// </summary>
        public void ShiftBlockDisplacementIndices(int start, int end, int offset)
        {
            if (offset == 0) return;

            int effectiveEnd = start < end ? end : int.MaxValue;

            // 收集范围内的条目（降序排列避免 index 冲突）
            var toShift = blockDisplacementDataList
                .Where(d => d.BlockIndex_Local >= start && d.BlockIndex_Local < effectiveEnd)
                .OrderByDescending(d => d.BlockIndex_Local)
                .ToList();

            var toRemove = new List<IBlockDisplacementData>();
            var toAdd = new List<IBlockDisplacementData>();

            foreach (var data in toShift)
            {
                int newIndex = data.BlockIndex_Local + offset;
                if (newIndex < 0) continue;          // 跳过负值
                if (newIndex == data.BlockIndex_Local) continue; // 无需变更
                toRemove.Add(data);
                toAdd.Add(data.CloneWithNewIndex(newIndex));
            }

            // 批量移除旧条目
            foreach (var data in toRemove)
            {
                Remove_BlockData(data.BlockIndex_Local);
            }
            // 批量写入新条目（覆盖冲突）
            foreach (var data in toAdd)
            {
                AddOrReplace_BlockData(data);
            }
        }

        /// <summary>
        /// 移除 Block 位移数据（封装 Remove_BlockData）。
        /// </summary>
        public bool RemoveBlockDisplacementData(int blockLocalIndex)
        {
            int indexInList = Get_BlockDisplacementDataListIndex_ByBlockIndexLocal(blockLocalIndex);
            if (indexInList < 0) return false;
            Remove_BlockData(blockLocalIndex);
            return true;
        }
    }

    [CreateAssetMenu(menuName = "MusicTogether/DancingBall_SceneData", fileName = "NewSceneData")]
    public class SceneData : SerializedScriptableObject
    {
        public InputNoteData inputNoteData;
        public List<Segemnt> SegmentList => inputNoteData.noteLists;

        //[ListDrawerSettings(CustomAddFunction = nameof(AddRoadData))]
        [NonSerialized][OdinSerialize]public List<RoadData> roadDataList = new List<RoadData>();

        // Note数据操作=================================================================================
        private bool GetSegmentIndex(int targetSegmentIndex, out int segmentListIndex)
        {
            segmentListIndex = -1;
            if (targetSegmentIndex < 0) return false;
            if (targetSegmentIndex < SegmentList.Count)
            {
                if (SegmentList[targetSegmentIndex].Index == targetSegmentIndex)
                {
                    segmentListIndex = targetSegmentIndex;
                    return true;
                }
            }

            segmentListIndex = SegmentList.FindIndex(segment => segment.Index == targetSegmentIndex);
            if (segmentListIndex == -1)
            {
                Debug.LogError($"Segment with index {targetSegmentIndex} not found in SegmentList.");
                return false;
            }

            return true;
        }
        public bool GetSegment(int targetSegmentIndex, out Segemnt segment)
        {
            segment = new Segemnt();
            if (!GetSegmentIndex(targetSegmentIndex, out int segmentListIndex)) return false;
            segment = SegmentList[segmentListIndex];
            return true;
        }

        //Road数据操作==================================================================================
        public bool Exists_RoadData_ByRoadName(string roadName) => roadDataList.Any(rd => rd.roadName == roadName);

        /// <summary>
        /// 检查 RoadName 是否唯一且合法。
        /// </summary>
        public bool ValidateRoadNameUnique(string roadName)
        {
            if (string.IsNullOrWhiteSpace(roadName)) return false;
            return !Exists_RoadData_ByRoadName(roadName);
        }
        
        /// <summary>
        /// 根据Road起始时间排序，按照开始时间排序并生成blockIndex的Global起始。
        /// </summary>
        public void RefreshRoadDataList()
        {
            var sorted = roadDataList
                .Select(rd => new
                {
                    Data = rd,
                    Time = GetSegment(rd.targetSegmentIndex, out var segment) 
                        ? segment.GetNoteTimeAt(rd.noteBeginIndex) 
                        : 0
                })
                .OrderBy(x => x.Time)
                /*.Select((x, i) =>
                {
                    x.Data.roadIndex_Global = i;
                    return x.Data;
                })*/
                .Select((x)=> x.Data)
                .ToList();

            roadDataList.Clear();
            roadDataList.AddRange(sorted);
        }
        
        public int Get_RoadDataIndex_ByRoadName(string roadName) => roadDataList.FindIndex(rd => rd.roadName == roadName);
        
        public bool Get_RoadData_ByRoadName(string roadName, out RoadData roadData)
        {
            roadData = null;
            int index = Get_RoadDataIndex_ByRoadName(roadName);
            if (index >= 0)
            {
                roadData = roadDataList[index];
                return true;
            }
            return false;
        }

        /// <summary>
        /// 替换某一项或在末尾插入新的项(同时修改index)
        /// </summary>
        /// <param name="roadData"></param>
        public void Set_RoadData(RoadData roadData)
        {
            int targetRoadIndexGlobal = Get_RoadDataIndex_ByRoadName(roadData.roadName);
            if (targetRoadIndexGlobal > 0) roadDataList[targetRoadIndexGlobal] = roadData;
            else
            {
                roadDataList.Add(roadData);
                RefreshRoadDataList();
            }
        }

        /// <summary>
        /// 创建并加入新的 RoadData，返回创建结果（失败返回 null）。
        /// </summary>
        public RoadData CreateRoadData(string roadName, int segmentIndex, int noteBegin, int noteEnd)
        {
            if (!ValidateRoadNameUnique(roadName)) return null;
            var roadData = new RoadData(roadDataList.Count, segmentIndex, noteBegin, noteEnd)
            {
                roadName = roadName
            };
            roadDataList.Add(roadData);
            RefreshRoadDataList();
            return roadData;
        }

        /// <summary>
        /// 按名称删除 RoadData。
        /// </summary>
        public bool RemoveRoadData(string roadName)
        {
            int index = Get_RoadDataIndex_ByRoadName(roadName);
            if (index < 0) return false;
            roadDataList.RemoveAt(index);
            RefreshRoadDataList();
            return true;
        }

        /// <summary>
        /// 重命名 RoadData（保证新名称唯一）。
        /// </summary>
        public bool RenameRoadData(string oldName, string newName)
        {
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return false;
            if (!ValidateRoadNameUnique(newName)) return false;
            int index = Get_RoadDataIndex_ByRoadName(oldName);
            if (index < 0) return false;
            roadDataList[index].roadName = newName;
            RefreshRoadDataList();
            return true;
        }

        /// <summary>
        /// 用于JSON存档还原：直接替换列表并重建排序与全局索引。
        /// </summary>
        /// <param name="roads">完整的 RoadData 列表</param>
        public void ApplyRoadDataListFromArchive(List<RoadData> roads)
        {
            roadDataList ??= new List<RoadData>();
            roadDataList.Clear();
            if (roads != null && roads.Count > 0)
            {
                roadDataList.AddRange(roads);
                RefreshRoadDataList();
            }
        }

        //==============Road操作中与Block相关的部分
        //过时的设计：不建议直接由SceneData访问Block数据
        //Block数据操作==================================================================================
        //过时的设计：不建议直接由SceneData访问Block数据
    }
}