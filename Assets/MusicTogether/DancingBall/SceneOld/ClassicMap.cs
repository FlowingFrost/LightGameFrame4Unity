using System;
using System.Collections.Generic;
using System.Linq;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MusicTogether.DancingBall.SceneOld
{
    public class ClassicMap : SerializedMonoBehaviour, IMap
    {
        //外部引用
        [Header("References")]
        //[SerializeField] private EditManager editManager;
        //[SerializeField] private Factory factory;
        [SerializeField] private SceneData sceneData;
        [SerializeField] private GameObject roadPrefab;
        [SerializeField] private GameObject blockPrefab;
        public SceneData SceneData => sceneData;
        //本体绑定信息
        public Transform Transform => transform;
        [SerializeField] private readonly List<IRoad> roads = new List<IRoad>();
        public List<IRoad> Roads => roads;
        [SerializeField] private readonly List<MovementData> movementDataList = new List<MovementData>();
        
        //运行数据
        [SerializeField] [ReadOnly] private bool dirty;
        
        //表达式
        public bool IsDataValid
        {
            get
            {
                if (sceneData == null)
                {
                    return false;
                }

                return true;
            }
        }

        public void Init()
        {
        }

        #region Map_Operations //操作功能
        //地图操作
            [Button("重建所有Road")]
            public void RebuildRoads()
            {
                if (!IsDataValid)
                {
                    throw new InvalidOperationException("[ClassicMap.RebuildRoads] SceneData 为空，无法重建 Road。");
                }
                roads.Clear();
                foreach (Transform children in transform)
                {
                    DestroyImmediate(children.gameObject);
                }
                RecoverRoads();
            }
            [Button("清理重复和无效Road，添加缺失Road")]
            public void RecoverRoads()
            {
                if (!IsDataValid)
                {
                    throw new InvalidOperationException("[ClassicMap.RecoverRoads] SceneData 为空，无法恢复 Road。");
                }
                var roadDataList = sceneData.roadDataList;

                // 清理已销毁的 Road（DestroyImmediate 后接口引用无法被 == null 检测）
                roads.RemoveAll(r => r is UnityEngine.Object obj && obj == null);

                // 去重：保留 blocks 最多的
                var duplicateRoads = roads
                    .GroupBy(r => r.RoadData?.roadName)
                    .Where(g => g.Key != null && g.Count() > 1)
                    .SelectMany(g => g.OrderByDescending(r => r.Blocks.Count).Skip(1))
                    .ToList();
                RemoveRoads(duplicateRoads);

                // 移除无效 road（RoadData 为空，或 RoadData 已不在 SceneData 中）
                var roadToRemove = roads.Where(r => r.RoadData == null || !SceneData.roadDataList.Contains(r.RoadData)).ToList();
                RemoveRoads(roadToRemove);
                
                //移除不在roads列表中的road（丢失绑定的子物体）
                var missbindingRoadToRemove = GetComponentsInChildren<IRoad>().Where(r => !roads.Contains(r)).ToList();
                RemoveRoads(missbindingRoadToRemove);
                
                // 添加缺失 road
                var roadToAdd = roadDataList
                    .Where(d => !roads.Exists(r => r.RoadData != null && r.RoadData.roadName == d.roadName))
                    .ToList();
                AddRoads(roadToAdd);
                RefreshAllRoads();
            }
            [Button("刷新所有Road，重建Block列表")]
            public void RefreshAllRoads()
            {
                if (!IsDataValid)
                {
                    throw new InvalidOperationException("[ClassicMap.RefreshAllRoads] SceneData 为空，无法刷新 Road。");
                }
                
                for (int i = 0; i < roads.Count; i++)
                {
                    var road = roads[i];
                    road.Init(this, sceneData.roadDataList.Find(r => r.roadName == road.RoadName), blockPrefab);
                    road.Transform.localPosition = road.RoadData.loaclPosition;
                    road.Transform.localRotation = road.RoadData.loaclRotation;
                    road.Transform.localScale = road.RoadData.localScale;
                    road.RecoverBlocks();
                }
            }
        //预处理运行数据
            [Button("生成所有Road的MovementData")]
            public void GenerateMovementData()
            {
                foreach (var road in roads)
                {
                    road.GenerateBlockMovementData();
                }
            }
        #endregion
        //地图操作
        /// <summary>
        /// 从prefab或空物体创建Road。未预装Road脚本时自动添加ClassicRoad
        /// </summary>
        private IRoad CreateRoad(RoadData roadData)
        {
            //数据校验：空校验与重复校验
            if (!IsDataValid) return null;
            if (roads.Exists(r => r.RoadData != null && r.RoadData.roadName == roadData.roadName))
            {
                Debug.LogError($"Road with name {roadData.roadName} already exists in the map.");
                return null;
            }

            var parent = Transform;

            GameObject roadObj = roadPrefab != null
                ? Instantiate(roadPrefab, parent, false)
                : new GameObject();
            roadObj.name = string.IsNullOrEmpty(roadData.roadName) ? "Road" : $"Road_{roadData.roadName}";
            if (roadPrefab == null && parent != null)
            {
                roadObj.transform.SetParent(parent);
            }

            if (!roadObj.TryGetComponent<IRoad>(out var road))
            {
                road = roadObj.AddComponent<ClassicRoad>();
            }

            road.Init(this, roadData, blockPrefab);
            roads.Add(road);
            return road;
        }
        public List<IRoad> CreateRoads(List<RoadData> roadDataList)
        {
            var list = new List<IRoad>();
            if (roadDataList == null) return list;
            foreach (var roadData in roadDataList)
            {
                var road = CreateRoad(roadData);
                if (road != null) list.Add(road);
            }
            return list;
        }
        public void AddRoads(List<RoadData> roadDataToAdd)
        {
            foreach (var roadData in roadDataToAdd)
            {
                var newRoad = CreateRoad(roadData);
                if (newRoad != null && !Roads.Contains(newRoad)) Roads.Add(newRoad);
            }
        }
        public void RemoveRoads(List<IRoad> roadsToRemove)
        {
            foreach (var road in roadsToRemove)
            {
                Roads.Remove(road);
                if (road is MonoBehaviour roadBehaviour)
                {
                    DestroyImmediate(roadBehaviour.gameObject);
                }
            }
        }
        public void OnRoadDataMissing(IRoad road)
        {
            DestroyImmediate(road.Transform.gameObject);
            RecoverRoads();
        }
    }
}
