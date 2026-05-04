using System;
using System.Collections.Generic;
using LightGameFrame.Services;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.Player;
using MusicTogether.DancingBall.Scene;
using UnityEngine;

namespace MusicTogether.DancingBall.EditorTool
{
    [AutoService(Mode = AutoServiceMode.Dual)]
    public class EditorCenter : GameServiceBase<EditorCenter>
    {
        public IMap targetMap;
        public int SelectedRoadIndex { get; private set; }
        public int SelectedBlockIndex { get; private set; }
        public IRoad selectedRoad;
        public IBlock selectedBlock;
        public IBlockDisplacementData selectedDisplacementData;
        public BallPlayer player;

        private bool IsRoadIndexOutOfRange => targetMap == null || SelectedRoadIndex < 0 || SelectedRoadIndex >= targetMap.Roads.Count;
        private bool IsBlockIndexOutOfRange => selectedRoad == null || SelectedBlockIndex < 0 || SelectedBlockIndex >= selectedRoad.Blocks.Count;

        public Action<string> SendMessage = Debug.Log;
        public Action<int, int> OnSelectionChanged;
        public Action<IRoad> OnRoadSelectionChanged;
        public Action<IBlock, IBlockDisplacementData> OnBlockSelectionChanged;
        public Action<GameObject> LookAtObject;
        public Action<List<RoadData>> OnRoadListChanged;
        public Action<List<IBlockDisplacementData>> OnBlockDisplacementListChanged;

        public bool IsBound => targetMap != null;
        public bool IsPlayerBound => player != null;

        protected override void OnInitialize()
        {
            TryAutoBind();
        }

        public bool TryAutoBind()
        {
            if (targetMap == null)
                targetMap = FindMapInScene();
            if (player == null)
                player = GameObject.FindObjectOfType<BallPlayer>();

            if (targetMap != null)
                RefreshSelection();

            return IsBound;
        }

        public void Bind(IMap map, BallPlayer player, int roadIndex = 0, int blockIndex = 0)
        {
            targetMap = map;
            this.player = player;
            SelectedRoadIndex = roadIndex;
            SelectedBlockIndex = blockIndex;
            RefreshSelection();
        }

        public void Unbind()
        {
            targetMap = null;
            player = null;
            selectedRoad = null;
            selectedBlock = null;
            selectedDisplacementData = null;
            SelectedRoadIndex = 0;
            SelectedBlockIndex = 0;
        }

        private static IMap FindMapInScene()
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (!go.scene.isLoaded) continue;
                var components = go.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component is IMap map)
                        return map;
                }
            }
            return null;
        }

        public void PreviousBlock()
        {
            SelectedBlockIndex--;
            if (IsBlockIndexOutOfRange) PreviousRoad();
            else RefreshSelection();
        }
        public void NextBlock()
        {
            SelectedBlockIndex++;
            if (IsBlockIndexOutOfRange) NextRoad();
            else RefreshSelection();
        }
        public void PreviousRoad()
        {
            SelectedRoadIndex--;
            SelectedBlockIndex = int.MaxValue;
            RefreshSelection();
        }
        public void NextRoad()
        {
            SelectedRoadIndex++;
            SelectedBlockIndex = 0;
            RefreshSelection();
        }

        public void JumpTo(int roadIndex, int blockIndex = -1)
        {
            SelectedRoadIndex = roadIndex;
            SelectedBlockIndex = blockIndex;
            RefreshSelection();
        }

        public void RefreshSelection()
        {
            OnSelectionChanged?.Invoke(SelectedRoadIndex, SelectedBlockIndex);
            if (targetMap == null)
            {
                SendMessage("Target map is not set.");
                return;
            }

            if (targetMap.Roads == null || targetMap.Roads.Count == 0)
            {
                SendMessage("Target map has no roads.");
                return;
            }

            if (IsRoadIndexOutOfRange)
            {
                SendMessage("Selected road index is out of range.");
                SelectedRoadIndex = SelectedRoadIndex < 0 ? 0 : targetMap.Roads.Count - 1;
            }

            selectedRoad = targetMap.Roads[SelectedRoadIndex];
            OnRoadSelectionChanged?.Invoke(selectedRoad);
            if (selectedRoad == null || selectedRoad.Blocks == null || selectedRoad.Blocks.Count == 0)
            {
                SendMessage("Selected road has no blocks.");
                return;
            }

            if (IsBlockIndexOutOfRange)
            {
                SendMessage("Selected block index is out of range.");
                SelectedBlockIndex = SelectedBlockIndex < 0 ? 0 : selectedRoad.Blocks.Count - 1;
            }

            selectedBlock = selectedRoad.Blocks[SelectedBlockIndex];
            selectedRoad.RoadData.Get_BlockData(selectedBlock.BlockLocalIndex, out selectedDisplacementData);
            OnBlockSelectionChanged?.Invoke(selectedBlock, selectedDisplacementData);
            if (selectedBlock == null)
            {
                SendMessage("Selected block is null.");
                return;
            }

            OnSelectionChanged?.Invoke(SelectedRoadIndex, SelectedBlockIndex);
            LookAtObject?.Invoke(selectedBlock.Transform.gameObject);
        }

        //操作功能
        public void MapRebuildRoadsRequested() { targetMap.RebuildRoads(); RefreshSelection(); }
        public void MapRefreshAllRoadsRequested() { targetMap.RefreshAllRoads(); RefreshSelection(); }

        public bool CreateRoadFromSelection()
        {
            if (targetMap?.SceneData == null) return false;
            var sceneData = targetMap.SceneData;
            var template = selectedRoad?.RoadData;
            string baseName = template?.roadName ?? "Road";
            string newName = GetUniqueRoadName(sceneData, $"{baseName}_New");
            int segmentIndex = template?.targetSegmentIndex ?? 0;
            int noteBegin = template?.noteBeginIndex ?? 0;
            int noteEnd = template?.noteEndIndex ?? noteBegin;

            var created = sceneData.CreateRoadData(newName, segmentIndex, noteBegin, noteEnd);
            if (created == null) return false;
            if (template != null)
            {
                created.loaclPosition = template.loaclPosition;
                created.loaclRotation = template.loaclRotation;
                created.localScale = template.localScale;
            }

            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        public bool CreateRoad(string roadName, int segmentIndex, int noteBegin, int noteEnd)
        {
            if (targetMap?.SceneData == null) return false;
            var sceneData = targetMap.SceneData;
            var finalName = GetUniqueRoadName(sceneData, roadName);
            var created = sceneData.CreateRoadData(finalName, segmentIndex, noteBegin, noteEnd);
            if (created == null) return false;
            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        public bool DuplicateSelectedRoad()
        {
            if (targetMap?.SceneData == null || selectedRoad?.RoadData == null) return false;
            var sceneData = targetMap.SceneData;
            var template = selectedRoad.RoadData;
            string newName = GetUniqueRoadName(sceneData, $"{template.roadName}_Copy");
            var created = sceneData.CreateRoadData(newName, template.targetSegmentIndex, template.noteBeginIndex, template.noteEndIndex);
            if (created == null) return false;

            created.loaclPosition = template.loaclPosition;
            created.loaclRotation = template.loaclRotation;
            created.localScale = template.localScale;

            if (template.blockDisplacementDataList != null)
            {
                created.blockDisplacementDataList = new List<IBlockDisplacementData>();
                foreach (var data in template.blockDisplacementDataList)
                {
                    if (data is ClassicBlockDisplacementData classic)
                    {
                        created.blockDisplacementDataList.Add(new ClassicBlockDisplacementData(classic.BlockIndex_Local)
                        {
                            turnType = classic.turnType,
                            displacementType = classic.displacementType
                        });
                    }
                    else
                    {
                        created.blockDisplacementDataList.Add(data);
                    }
                }
            }

            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        public bool DeleteSelectedRoad()
        {
            if (targetMap?.SceneData == null || selectedRoad?.RoadData == null) return false;
            var sceneData = targetMap.SceneData;
            bool removed = sceneData.RemoveRoadData(selectedRoad.RoadData.roadName);
            if (!removed) return false;

            targetMap.RecoverRoads();
            RefreshSelection();
            OnRoadListChanged?.Invoke(sceneData.roadDataList);
            return true;
        }

        public bool CreateBlockDisplacementDataForSelected(Type dataType = null)
        {
            if (selectedRoad?.RoadData == null || selectedBlock == null) return false;
            int blockLocalIndex = selectedBlock.BlockLocalIndex;
            dataType ??= typeof(ClassicBlockDisplacementData);
            var newData = selectedRoad.RoadData.CreateBlockDisplacementData(blockLocalIndex, dataType);
            if (newData == null) return false;
            selectedRoad.RoadData.AddOrReplace_BlockData(newData);
            selectedRoad.OnBlockDisplacementRuleChanged();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
            return true;
        }

        public bool RemoveBlockDisplacementDataForSelected()
        {
            if (selectedRoad?.RoadData == null || selectedBlock == null) return false;
            int blockLocalIndex = selectedBlock.BlockLocalIndex;
            bool removed = selectedRoad.RoadData.RemoveBlockDisplacementData(blockLocalIndex);
            if (!removed) return false;
            selectedRoad.OnBlockDisplacementRuleChanged();
            RefreshSelection();
            OnBlockDisplacementListChanged?.Invoke(selectedRoad.RoadData.blockDisplacementDataList);
            return true;
        }

        private string GetUniqueRoadName(SceneData sceneData, string baseName)
        {
            if (sceneData == null) return baseName;
            string name = string.IsNullOrWhiteSpace(baseName) ? "Road" : baseName;
            if (sceneData.ValidateRoadNameUnique(name)) return name;

            int suffix = 1;
            while (!sceneData.ValidateRoadNameUnique($"{name}_{suffix}"))
            {
                suffix++;
            }
            return $"{name}_{suffix}";
        }
    }
}
