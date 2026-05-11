using System;
using System.Collections.Generic;
using System.Linq;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.EditorTool.UIManager;
using MusicTogether.DancingBall.Scene;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Controller
{
    public class InspectorViewController : IEditorViewController
    {
        private readonly EditorCenter _editorCenter;
        private InspectorWindowManager _windowManager;
        private IBlockDisplacementUIManager _currentDisplacementUI;
        private VisualElement _displacementContainer;

        /// <summary>
        /// Host 在此事件中打开创建道路的对话框（如 Editor 下的 RoadCreateWindow）。
        /// 对话框确认后应调用 <see cref="OnRoadCreated"/>。
        /// </summary>
        public Action RoadCreateDialogRequested { get; set; }

        public InspectorViewController(EditorCenter editorCenter)
        {
            _editorCenter = editorCenter;
        }

        public void Bind(VisualElement root)
        {
            _windowManager = new InspectorWindowManager(root);
            _displacementContainer = root.Q<VisualElement>("block-displacement-container");
            _currentDisplacementUI?.Dispose();
            _currentDisplacementUI = null;

            _windowManager.RetryBind = RetryBind;
            BindEditorCenter();
        }

        public void Dispose()
        {
            _currentDisplacementUI?.Dispose();
            if (_editorCenter != null)
            {
                _editorCenter.OnRoadSelectionChanged -= OnRoadSelected;
                _editorCenter.OnBlockSelectionChanged -= OnBlockSelected;
            }
        }

        private void RetryBind()
        {
            if (_editorCenter != null)
            {
                SubscribeEditorEvents();
                _editorCenter.TryAutoBind();
            }
            BindEditorCenter();
        }

        private void SubscribeEditorEvents()
        {
            _editorCenter.OnRoadSelectionChanged += OnRoadSelected;
            _editorCenter.OnBlockSelectionChanged += OnBlockSelected;
        }

        private void BindEditorCenter()
        {
            if (_editorCenter == null)
            {
                _windowManager.SetBindedViewVisible(false);
                return;
            }

            SubscribeEditorEvents();

            _windowManager.MapRebuildRoadsRequested = MapRebuildRoadsRequested;
            _windowManager.MapRefreshAllRoadsRequested = MapRefreshAllRoadsRequested;
            _windowManager.RoadRefreshBlocksRequested = RoadRefreshBlocksRequested;
            _windowManager.RoadUpdateBlockTransformRequested = RoadUpdateBlockTransformRequested;
            _windowManager.RoadRefreshBlockDisplayRequested = RoadRefreshBlockDisplayRequested;
            _windowManager.RoadModifyTargetSegmentRequested = RoadModifyTargetSegmentRequested;
            _windowManager.RoadModifyNoteRangeRequested = RoadModifyNoteRangeRequested;
            _windowManager.RoadModifyTargetDataNameRequested = RoadModifyTargetDataNameRequested;
            _windowManager.RoadSaveTransformRequested = RoadSaveTransformRequested;
            _windowManager.RoadListSelectionChanged = RoadListSelectionChanged;
            _windowManager.BlockDisplacementSelectionChanged = BlockDisplacementSelectionChanged;
            _windowManager.RoadRefreshRequested = RoadRefreshRequested;
            _windowManager.BlockDisplacementApplyBatchRequested = BlockDisplacementApplyBatchRequested;
            _windowManager.BlockDisplacementShiftRequested = BlockDisplacementShiftRequested;
            _windowManager.RoadCreateRequested = RoadCreateRequested;
            _windowManager.RoadDeleteRequested = RoadDeleteRequested;
            _windowManager.RoadDuplicateRequested = RoadDuplicateRequested;
            _windowManager.BlockDisplacementCreateRequested = BlockDisplacementCreateRequested;
            _windowManager.BlockDisplacementDeleteRequested = BlockDisplacementDeleteRequested;
            _windowManager.RoadTruncateRequested = RoadTruncateRequested;
            _windowManager.RoadTruncateAndCreateRequested = RoadTruncateAndCreateRequested;
            _windowManager.RoadContinueCreateRequested = RoadContinueCreateRequested;
            _windowManager.RoadGenerateMovementDataRequested = RoadGenerateMovementDataRequested;
            _windowManager.MapGenerateMovementDataRequested = MapGenerateMovementDataRequested;

            _windowManager.RetryBind = RetryBind;
            _windowManager.MapMissBindingRetryRequested = RetryBind;

            _windowManager.SetBindedViewVisible(true);
            if (VerifyMap())
            {
                _windowManager.SetMapContainersVisibility(true, false, false);
                _windowManager.BindRoadList(_editorCenter.targetMap.SceneData.roadDataList, _editorCenter.SelectedRoadIndex);
                RefreshMapInfo();
            }
            OnRoadSelected(_editorCenter.selectedRoad);
            OnBlockSelected(_editorCenter.selectedBlock, _editorCenter.selectedDisplacementData);
        }

        // ---- 验证辅助 ----

        private bool VerifyMap()
        {
            if (_editorCenter.targetMap == null)
            {
                _windowManager.SetMapContainersVisibility(false, false, true);
                return false;
            }
            return true;
        }

        private bool VerifyRoad()
        {
            if (_editorCenter.selectedRoad == null)
            {
                _windowManager.SetRoadContainersVisibility(false, false, true);
                return false;
            }
            return true;
        }

        private bool VerifyBlock()
        {
            if (_editorCenter.selectedBlock == null)
            {
                _windowManager.SetBlockContainersVisibility(false, false, true);
                return false;
            }
            return true;
        }

        // ---- 信息刷新 ----

        private void RefreshMapInfo()
        {
            var map = _editorCenter?.targetMap;
            _windowManager.SetMapIsDataValid(map != null && map.SceneData != null);
            _windowManager.SetMapSceneDataStatus(map?.SceneData != null);
            _windowManager.SetMapRoadCount(map?.Roads?.Count ?? 0);
            int movementCount = 0;
            if (map?.Roads != null)
            {
                foreach (var road in map.Roads)
                {
                    movementCount += road?.MovementDatum?.Count ?? 0;
                }
            }
            _windowManager.SetMapMovementDataCount(movementCount);
        }

        private void RefreshRoadInfo(IRoad road)
        {
            if (road == null) return;
            _windowManager.SetRoadIsDataValid(road.IsDataValid);
            _windowManager.SetRoadMapName(GetSafeMapName(road.Map));
            _windowManager.SetRoadPregenData(
                road.RoadBeginTime,
                road.RoadEndTime,
                0,
                road.MovementDatum?.Count ?? 0,
                road.AnimationEventDatum?.Count ?? 0);
        }

        private void RoadSaveTransformRequested()
        {
            if (!VerifyRoad()) return;
            _editorCenter.selectedRoad.SaveTransformData();
            _editorCenter.SendMessage("Transform 已保存到 RoadData。");
        }

        private void RoadContinueCreateRequested()
        {
            if (!VerifyRoad()) return;
            if (_editorCenter.ContinueCreateRoad())
            {
                _windowManager.BindRoadList(_editorCenter.targetMap.SceneData.roadDataList, _editorCenter.SelectedRoadIndex);
                _editorCenter.SendMessage("已从当前 Road 末尾创建新 Road。");
            }
            else
            {
                _editorCenter.SendMessage("继续创建失败，请检查 Road 数据。");
            }
        }

        private void RoadTruncateRequested()
        {
            if (!VerifyBlock()) return;
            if (_editorCenter.TruncateRoadAtSelectedBlock())
            {
                _windowManager.BindBlockDisplacementList(
                    _editorCenter.selectedRoad.RoadData.blockDisplacementDataList,
                    _editorCenter.SelectedBlockIndex);
                _windowManager.BindRoadList(_editorCenter.targetMap.SceneData.roadDataList, _editorCenter.SelectedRoadIndex);
                RefreshRoadInfo(_editorCenter.selectedRoad);
                _editorCenter.SendMessage("Road 已截断。");
            }
            else
            {
                _editorCenter.SendMessage("截断失败，选中的 Block 可能已在 Road 末尾。");
            }
        }

        private void RoadTruncateAndCreateRequested()
        {
            if (!VerifyBlock()) return;
            if (_editorCenter.TruncateAndCreateRoad())
            {
                _windowManager.BindBlockDisplacementList(
                    _editorCenter.selectedRoad.RoadData.blockDisplacementDataList,
                    _editorCenter.SelectedBlockIndex);
                _windowManager.BindRoadList(_editorCenter.targetMap.SceneData.roadDataList, _editorCenter.SelectedRoadIndex);
                RefreshRoadInfo(_editorCenter.selectedRoad);
                RefreshMapInfo();
                _editorCenter.SendMessage("已截断 Road 并创建新 Road 承接剩余部分。");
            }
            else
            {
                _editorCenter.SendMessage("截断并创建失败，选中的 Block 可能已在 Road 末尾。");
            }
        }

        private void RoadGenerateMovementDataRequested()
        {
            if (!VerifyRoad()) return;
            try
            {
                _editorCenter.selectedRoad.GenerateBlockMovementData();
                RefreshRoadInfo(_editorCenter.selectedRoad);
                _editorCenter.SendMessage("Block MovementData 已生成。");
            }
            catch (Exception ex)
            {
                _editorCenter.SendMessage($"生成 MovementData 失败：{ex.Message}");
            }
        }

        private void MapGenerateMovementDataRequested()
        {
            if (!VerifyMap()) return;
            try
            {
                _editorCenter.targetMap.GenerateMovementData();
                RefreshMapInfo();
                _editorCenter.SendMessage("所有 Road 的 MovementData 已生成。");
            }
            catch (Exception ex)
            {
                _editorCenter.SendMessage($"生成 MovementData 失败：{ex.Message}");
            }
        }

        // ---- 事件响应 ----

        private void OnRoadSelected(IRoad road)
        {
            if (!VerifyRoad()) return;
            _windowManager.SetRoadContainersVisibility(true, false, false);
            _windowManager.SetRoadNoteRange(road.RoadData.noteBeginIndex, road.RoadData.noteEndIndex);
            _windowManager.SetRoadTargetDataName(road.RoadData.roadName);
            _windowManager.SetRoadSegmentOptions(
                GetSegmentDisplayNames(_editorCenter.targetMap?.SceneData),
                GetSegmentIndices(_editorCenter.targetMap?.SceneData),
                road.RoadData.targetSegmentIndex);
            _windowManager.BindRoadList(_editorCenter.targetMap.SceneData.roadDataList, _editorCenter.SelectedRoadIndex);
            _windowManager.BindBlockDisplacementList(road.RoadData.blockDisplacementDataList, _editorCenter.SelectedBlockIndex);
            _windowManager.SetBlockDisplacementShiftVisible(true);
            _windowManager.SetRoadTruncateButtonsEnabled(false);
            RefreshRoadInfo(road);
            RefreshMapInfo();
        }

        private void OnBlockSelected(IBlock block, IBlockDisplacementData displacementData)
        {
            if (!VerifyBlock()) return;
            _windowManager.SetBlockContainersVisibility(true, true, false);
            _windowManager.BindBlockDisplacementList(
                _editorCenter.selectedRoad.RoadData.blockDisplacementDataList,
                block.BlockLocalIndex);

            // 更新 Block 通用信息
            _windowManager.SetBlockLocalIndex(block.BlockLocalIndex);
            _windowManager.SetBlockRoadName(_editorCenter.selectedRoad?.RoadData?.roadName ?? "未知");
            _windowManager.SetBlockIsDataValid(block.IsDataValid);
            _windowManager.SetBlockTileHolderStatus(block.TileHolder != null);
            _windowManager.SetBlockDebugStatus(block.BlockDebugDisplay != null);

            // 更新截断按钮状态与文本
            _windowManager.SetRoadTruncateButtonsEnabled(true);
            _windowManager.SetRoadTruncateButtonText(block.BlockLocalIndex,
                _editorCenter.selectedRoad?.RoadData?.noteBeginIndex ?? 0);

            if (displacementData == null)
            {
                _windowManager.SetBlockDisplacementCreateVisible(true);
                _windowManager.SetBlockDisplacementDetailVisible(false);
                _windowManager.SetBlockDisplacementSummary("无位移数据");
                ClearDisplacementUI();
            }
            else
            {
                _windowManager.SetBlockDisplacementCreateVisible(false);
                _windowManager.SetBlockDisplacementDetailVisible(true);
                _windowManager.SetBlockDisplacementSummary(FormatDisplacementSummary(displacementData));
                ShowDisplacementUI(displacementData);
            }
        }

        private static string FormatDisplacementSummary(IBlockDisplacementData data)
        {
            if (data == null) return "无位移数据";
            if (data is ClassicBlockDisplacementData classic)
            {
                return $"Turn: {classic.turnType} | Displacement: {classic.displacementType}";
            }
            return $"{data.GetType().Name} (Index: {data.BlockIndex_Local})";
        }

        private void ClearDisplacementUI()
        {
            _currentDisplacementUI?.Dispose();
            _currentDisplacementUI = null;
            _displacementContainer?.Clear();
        }

        private void ShowDisplacementUI(IBlockDisplacementData data)
        {
            ClearDisplacementUI();
            if (data == null || !BlockDisplacementUIFactory.HasCreator(data)) return;

            _currentDisplacementUI = BlockDisplacementUIFactory.Create(_displacementContainer, data);
            if (_currentDisplacementUI != null)
            {
                _currentDisplacementUI.SetData(data);
                _currentDisplacementUI.OnDataChanged += OnDisplacementDataChanged;
            }
        }

        // ---- 功能按钮 ----

        private void MapRebuildRoadsRequested()
        {
            if (!VerifyMap()) return;
            _editorCenter.MapRebuildRoadsRequested();
        }

        private void MapRefreshAllRoadsRequested()
        {
            if (!VerifyMap()) return;
            _editorCenter.MapRefreshAllRoadsRequested();
        }

        private void RoadRefreshRequested()
        {
            if (!VerifyMap()) return;
            _editorCenter.targetMap.SceneData.RefreshRoadDataList();
            _editorCenter.RefreshSelection();
        }

        private void RoadListSelectionChanged(int roadIndex)
        {
            if (!VerifyMap()) return;
            _editorCenter.JumpTo(roadIndex);
        }

        private void BlockDisplacementSelectionChanged(int blockLocalIndex)
        {
            if (!VerifyRoad()) return;
            _editorCenter.JumpTo(_editorCenter.SelectedRoadIndex, blockLocalIndex);
        }

        private void BlockDisplacementApplyBatchRequested()
        {
            if (!VerifyRoad()) return;
            var selectedData = _editorCenter.selectedDisplacementData;
            if (selectedData == null) return;

            var targetIndices = _windowManager.GetSelectedBlockDisplacementIndices();
            if (targetIndices.Count == 0) return;

            foreach (var blockLocalIndex in targetIndices)
            {
                IBlockDisplacementData newData;
                if (selectedData is ClassicBlockDisplacementData classicData)
                {
                    var clone = new ClassicBlockDisplacementData(blockLocalIndex)
                    {
                        turnType = classicData.turnType,
                        displacementType = classicData.displacementType
                    };
                    newData = clone;
                }
                else
                {
                    newData = _editorCenter.selectedRoad.RoadData.CreateBlockDisplacementData(
                        blockLocalIndex, selectedData.GetType());
                }

                if (newData != null)
                {
                    _editorCenter.selectedRoad.RoadData.AddOrReplace_BlockData(newData);
                }
            }

            _editorCenter.selectedRoad.OnBlockDisplacementRuleChanged();
            _windowManager.BindBlockDisplacementList(
                _editorCenter.selectedRoad.RoadData.blockDisplacementDataList,
                _editorCenter.SelectedBlockIndex);
        }

        private void BlockDisplacementShiftRequested(int start, int end, int offset)
        {
            if (!VerifyRoad()) return;
            if (_editorCenter.selectedRoad?.RoadData == null) return;
            _editorCenter.ShiftBlockDisplacementIndices(start, end, offset);
            _windowManager.BindBlockDisplacementList(
                _editorCenter.selectedRoad.RoadData.blockDisplacementDataList,
                _editorCenter.SelectedBlockIndex);
        }

        private void RoadCreateRequested()
        {
            if (!VerifyMap()) return;
            RoadCreateDialogRequested?.Invoke();
        }

        /// <summary>
        /// 由 Host 在创建道路对话框确认后调用。
        /// </summary>
        public void OnRoadCreated(string roadName, int segmentIndex, int noteBegin, int noteEnd)
        {
            if (!VerifyMap()) return;
            _editorCenter.CreateRoad(roadName, segmentIndex, noteBegin, noteEnd);
            _windowManager.BindRoadList(
                _editorCenter.targetMap.SceneData.roadDataList,
                _editorCenter.SelectedRoadIndex);
        }

        private void RoadDeleteRequested()
        {
            if (!VerifyMap()) return;
            _editorCenter.DeleteSelectedRoad();
            _windowManager.BindRoadList(_editorCenter.targetMap.SceneData.roadDataList, _editorCenter.SelectedRoadIndex);
        }

        private void RoadDuplicateRequested()
        {
            if (!VerifyMap()) return;
            _editorCenter.DuplicateSelectedRoad();
            _windowManager.BindRoadList(_editorCenter.targetMap.SceneData.roadDataList, _editorCenter.SelectedRoadIndex);
        }

        private void BlockDisplacementCreateRequested()
        {
            if (!VerifyRoad()) return;
            var selectedType = _windowManager.GetSelectedDisplacementDataType();
            var dataType = selectedType switch
            {
                BlockDisplacementDataType.Classic => typeof(ClassicBlockDisplacementData),
                _ => typeof(ClassicBlockDisplacementData)
            };
            _editorCenter.CreateBlockDisplacementDataForSelected(dataType);
            _windowManager.BindBlockDisplacementList(
                _editorCenter.selectedRoad.RoadData.blockDisplacementDataList,
                _editorCenter.SelectedBlockIndex);
        }

        private void BlockDisplacementDeleteRequested()
        {
            if (!VerifyRoad()) return;
            _editorCenter.RemoveBlockDisplacementDataForSelected();
            _windowManager.BindBlockDisplacementList(
                _editorCenter.selectedRoad.RoadData.blockDisplacementDataList,
                _editorCenter.SelectedBlockIndex);
        }

        private void RoadRefreshBlocksRequested()
        {
            if (!VerifyRoad()) return;
            _editorCenter.selectedRoad.RebuildBlocks();
        }

        private void RoadUpdateBlockTransformRequested()
        {
            if (!VerifyRoad()) return;
            _editorCenter.selectedRoad.OnBlockDisplacementRuleChanged();
        }

        private void RoadRefreshBlockDisplayRequested()
        {
            if (!VerifyRoad()) return;
            _editorCenter.selectedRoad.RefreshBlockInfoDisplay();
        }

        private void RoadModifyTargetSegmentRequested(int segmentIndex)
        {
            if (!VerifyRoad()) return;
            _editorCenter.selectedRoad.ModifyTargetSegmentName(segmentIndex);
        }

        private void RoadModifyNoteRangeRequested(int begin, int end)
        {
            if (!VerifyRoad()) return;
            _editorCenter.selectedRoad.ModifyNoteRange(begin, end);
        }

        private void RoadModifyTargetDataNameRequested(string value)
        {
            if (!VerifyRoad()) return;
            _editorCenter.selectedRoad.ModifyTargetRoadDataName(value);
        }

        private void OnDisplacementDataChanged(IBlockDisplacementData data)
        {
            if (!VerifyRoad() || data == null) return;
            _editorCenter.selectedRoad.ModifyDisplacementData(data.BlockIndex_Local, data);
            _editorCenter.RefreshSelection();
            _windowManager.BindBlockDisplacementList(
                _editorCenter.selectedRoad.RoadData.blockDisplacementDataList,
                _editorCenter.SelectedBlockIndex);
        }

        // ---- 工具方法 ----

        private static List<string> GetSegmentDisplayNames(SceneData sceneData)
        {
            var result = new List<string>();
            if (sceneData?.SegmentList == null) return result;
            foreach (var segment in sceneData.SegmentList.OrderBy(seg => seg.Index))
            {
                var displayName = string.IsNullOrWhiteSpace(segment.name) ? "Unnamed" : segment.name;
                result.Add($"{segment.Index} | {displayName}");
            }
            return result;
        }

        private static List<int> GetSegmentIndices(SceneData sceneData)
        {
            var result = new List<int>();
            if (sceneData?.SegmentList == null) return result;
            foreach (var segment in sceneData.SegmentList.OrderBy(seg => seg.Index))
            {
                result.Add(segment.Index);
            }
            return result;
        }

        private static string GetSafeMapName(IMap map)
        {
            if (map == null) return "运行时对象";
            if (map is UnityEngine.Object unityObj && unityObj == null) return "(已销毁)";
            if (map is UnityEngine.Object namedObj) return namedObj.name;
            return map.GetType().Name;
        }
    }
}
