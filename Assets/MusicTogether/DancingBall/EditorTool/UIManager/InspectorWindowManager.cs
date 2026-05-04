using System;
using System.Collections.Generic;
using MusicTogether.DancingBall.Data;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.UIManager
{
    public class InspectorWindowManager : UIManagerBase
    {
        //Map
        private VisualElement _mapCommonContainer;//map-common-container
            private Button _mapRebuildRoadsButton;//map-rebuild-roads
            private Button _mapRefreshAllRoadsButton;//map-refresh-all-roads
            //map-road-list-section
                //road-toolbar
                    private Button _roadCreateButton;
                    private Button _roadDeleteButton;
                    private Button _roadDuplicateButton;
                    private Button _roadRefreshButton;
                private ListView _roadListView;
                private Label _roadListEmptyLabel;
        private VisualElement _mapDerivedContainer;
        private VisualElement _mapMissBindingContainer;
            private Button _mapMissBindingButton;
            
        //Road
        private VisualElement _roadCommonContainer;
            //road-detail-form
                    private TextField _roadTargetDataNameField;
                    private Button _roadModifyTargetDataNameButton;
                private DropdownField _roadSegmentNameField;
                    private IntegerField _roadNoteRangeBeginField;
                    private IntegerField _roadNoteRangeEndField;
                private Button _roadModifyNoteRangeButton;
            private Button _roadRefreshRoadBlocksButton;
            private Button _roadUpdateBlockTransformButton;
            private Button _roadRefreshBlockDisplayButton;
            //block-displacement-list-section
                //block-displacement-toolbar
                    private Button _blockDisplacementCreateButton;
                    private Button _blockDisplacementDeleteButton;
                    private Button _blockDisplacementApplyBatchButton;
                private ListView _blockDisplacementListView;
                private Label _blockDisplacementEmptyLabel;
        private VisualElement _roadDerivedContainer;
        private VisualElement _roadMissBindingContainer;
        
        //Block
        private VisualElement _blockCommonContainer;
        private VisualElement _blockDerivedContainer;
            private VisualElement _blockDisplacementCreateSection;
                private EnumField _blockDisplacementDataTypeField;
                private Button _blockDisplacementCreateCurrentButton;
            private VisualElement _blockDisplacementDetailSection;
                //classic-block-displacement-options
                    private EnumField _classicBlockTurnTypeField;
                    private Button _classicBlockApplyTurnTypeButton;
                    private EnumField _classicBlockDisplacementTypeField;
                    private Button _classicBlockApplyDisplacementTypeButton;
                private Button _blockDisplacementDeleteCurrentButton;
            private VisualElement _blockDisplacementShiftSection;
                private IntegerField _blockDisplacementShiftStartField;
                private IntegerField _blockDisplacementShiftEndField;
                private Button _blockDisplacementShiftMinusButton;
                private Button _blockDisplacementShiftPlusButton;
        private VisualElement _blockMissBindingContainer;

        

        private List<RoadData> _roadListCache = new List<RoadData>();
        private List<IBlockDisplacementData> _blockDisplacementListCache = new List<IBlockDisplacementData>();

        private VisualElement _bindedView;
        private VisualElement _unbindedView;
        private Button _retryButton;

        public Action MapMissBindingRetryRequested { get; set; }
        public Action MapRebuildRoadsRequested { get; set; }
        public Action MapRefreshAllRoadsRequested { get; set; }

        public Action RoadCreateRequested { get; set; }
        public Action RoadDeleteRequested { get; set; }
        public Action RoadDuplicateRequested { get; set; }
        public Action RoadRefreshRequested { get; set; }
        public Action<int> RoadListSelectionChanged { get; set; }

        public Action RoadRefreshBlocksRequested { get; set; }
        public Action RoadUpdateBlockTransformRequested { get; set; }
        public Action RoadRefreshBlockDisplayRequested { get; set; }
        public Action<int> RoadModifyTargetSegmentRequested { get; set; }
        public Action<int, int> RoadModifyNoteRangeRequested { get; set; }
        public Action<string> RoadModifyTargetDataNameRequested { get; set; }

        public Action<Enum> BlockDisplacementDataTypeChanged { get; set; }
        public Action<Enum> ClassicBlockApplyTurnTypeRequested { get; set; }
        public Action<Enum> ClassicBlockApplyDisplacementTypeRequested { get; set; }

        public Action BlockDisplacementCreateRequested { get; set; }
        public Action BlockDisplacementDeleteRequested { get; set; }
        public Action BlockDisplacementApplyBatchRequested { get; set; }
        public Action<int> BlockDisplacementSelectionChanged { get; set; }

        public Action<int, int, int> BlockDisplacementShiftRequested { get; set; }

        public Action RetryBind { get; set; }

        public InspectorWindowManager(VisualElement root) : base(root)
        {
            BindElements();
        }

        public void SetMapContainersVisibility(bool commonVisible, bool derivedVisible, bool missBindingVisible)
        {
            if (_mapCommonContainer != null) _mapCommonContainer.style.display = commonVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_mapDerivedContainer != null) _mapDerivedContainer.style.display = derivedVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_mapMissBindingContainer != null) _mapMissBindingContainer.style.display = missBindingVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetRoadContainersVisibility(bool commonVisible, bool derivedVisible, bool missBindingVisible)
        {
            if (_roadCommonContainer != null) _roadCommonContainer.style.display = commonVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_roadDerivedContainer != null) _roadDerivedContainer.style.display = derivedVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_roadMissBindingContainer != null) _roadMissBindingContainer.style.display = missBindingVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetBlockContainersVisibility(bool commonVisible, bool derivedVisible, bool missBindingVisible)
        {
            if (_blockCommonContainer != null) _blockCommonContainer.style.display = commonVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_blockDerivedContainer != null) _blockDerivedContainer.style.display = derivedVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_blockMissBindingContainer != null) _blockMissBindingContainer.style.display = missBindingVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetBlockDisplacementCreateVisible(bool visible)
        {
            if (_blockDisplacementCreateSection != null)
            {
                _blockDisplacementCreateSection.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetBlockDisplacementDetailVisible(bool visible)
        {
            if (_blockDisplacementDetailSection != null)
            {
                _blockDisplacementDetailSection.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetBlockDisplacementShiftVisible(bool visible)
        {
            if (_blockDisplacementShiftSection != null)
            {
                _blockDisplacementShiftSection.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public (int start, int end) GetBlockDisplacementShiftRange()
        {
            int start = _blockDisplacementShiftStartField?.value ?? 0;
            int end = _blockDisplacementShiftEndField?.value ?? 0;
            return (start, end);
        }

        public void SetBindedViewVisible(bool isBinded)
        {
            if (_bindedView != null)
            {
                _bindedView.style.display = isBinded ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_unbindedView != null)
            {
                _unbindedView.style.display = isBinded ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private readonly List<int> _roadSegmentOptionIndices = new List<int>();
        private readonly Dictionary<int, string> _roadSegmentNameMap = new Dictionary<int, string>();
        private bool _isUpdatingSegmentOptions;

        public void SetRoadNoteRange(int begin, int end)
        {
            _roadNoteRangeBeginField?.SetValueWithoutNotify(begin);
            _roadNoteRangeEndField?.SetValueWithoutNotify(end);
        }

        public void SetRoadSegmentOptions(IReadOnlyList<string> displayNames, IReadOnlyList<int> segmentIndices, int selectedSegmentIndex)
        {
            if (_roadSegmentNameField == null) return;
            _isUpdatingSegmentOptions = true;
            _roadSegmentNameMap.Clear();
            _roadSegmentOptionIndices.Clear();

            if (displayNames != null && segmentIndices != null)
            {
                int count = Mathf.Min(displayNames.Count, segmentIndices.Count);
                for (int i = 0; i < count; i++)
                {
                    _roadSegmentOptionIndices.Add(segmentIndices[i]);
                    _roadSegmentNameMap[segmentIndices[i]] = displayNames[i];
                }
                _roadSegmentNameField.choices = new List<string>(displayNames);
            }
            else
            {
                _roadSegmentNameField.choices = new List<string>();
            }

            int targetOptionIndex = _roadSegmentOptionIndices.IndexOf(selectedSegmentIndex);
            if (targetOptionIndex >= 0 && targetOptionIndex < _roadSegmentNameField.choices.Count)
            {
                _roadSegmentNameField.SetValueWithoutNotify(_roadSegmentNameField.choices[targetOptionIndex]);
                _roadSegmentNameField.index = targetOptionIndex;
            }
            else if (_roadSegmentNameField.choices.Count > 0)
            {
                _roadSegmentNameField.SetValueWithoutNotify(_roadSegmentNameField.choices[0]);
                _roadSegmentNameField.index = 0;
            }
            else
            {
                _roadSegmentNameField.SetValueWithoutNotify(string.Empty);
                _roadSegmentNameField.index = -1;
            }
            _isUpdatingSegmentOptions = false;
        }

        private string GetSegmentDisplayName(int segmentIndex)
        {
            if (_roadSegmentNameMap.TryGetValue(segmentIndex, out var name))
            {
                return name;
            }

            return $"{segmentIndex} | Unnamed";
        }

        public void SetRoadTargetDataName(string dataName)
        {
            _roadTargetDataNameField?.SetValueWithoutNotify(dataName ?? string.Empty);
        }

        public void BindRoadList(IReadOnlyList<RoadData> roads, int selectedIndex)
        {
            _roadListCache = roads == null ? new List<RoadData>() : new List<RoadData>(roads);
            if (_roadListView == null) return;
            _roadListView.itemsSource = _roadListCache;
            _roadListView.Rebuild();
            if (_roadListEmptyLabel != null)
            {
                _roadListEmptyLabel.style.display = _roadListCache.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (selectedIndex >= 0 && selectedIndex < _roadListCache.Count)
            {
                _roadListView.SetSelectionWithoutNotify(new[] { selectedIndex });
            }
            else
            {
                _roadListView.SetSelectionWithoutNotify(Array.Empty<int>());
            }
        }

        public void BindBlockDisplacementList(IReadOnlyList<IBlockDisplacementData> displacementList, int selectedBlockLocalIndex)
        {
            _blockDisplacementListCache = displacementList == null
                ? new List<IBlockDisplacementData>()
                : new List<IBlockDisplacementData>(displacementList);

            if (_blockDisplacementListView == null) return;
            _blockDisplacementListView.itemsSource = _blockDisplacementListCache;
            _blockDisplacementListView.Rebuild();
            if (_blockDisplacementEmptyLabel != null)
            {
                _blockDisplacementEmptyLabel.style.display = _blockDisplacementListCache.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            int selectionIndex = _blockDisplacementListCache.FindIndex(data => data.BlockIndex_Local == selectedBlockLocalIndex);
            if (selectionIndex >= 0)
            {
                _blockDisplacementListView.SetSelectionWithoutNotify(new[] { selectionIndex });
            }
            else
            {
                _blockDisplacementListView.SetSelectionWithoutNotify(Array.Empty<int>());
            }
        }

        public BlockDisplacementDataType GetSelectedDisplacementDataType()
        {
            if (_blockDisplacementDataTypeField?.value is BlockDisplacementDataType type)
            {
                return type;
            }
            return BlockDisplacementDataType.Classic;
        }

        public List<int> GetSelectedBlockDisplacementIndices()
        {
            var result = new List<int>();
            if (_blockDisplacementListView == null || _blockDisplacementListCache == null) return result;
            foreach (var index in _blockDisplacementListView.selectedIndices)
            {
                if (index >= 0 && index < _blockDisplacementListCache.Count)
                {
                    result.Add(_blockDisplacementListCache[index].BlockIndex_Local);
                }
            }
            return result;
        }

        public void SetClassicBlockTurnType(Enum type)
        {
            if (type == null)
            {
                return;
            }

            _classicBlockTurnTypeField?.SetValueWithoutNotify(type);
        }

        public void SetClassicBlockDisplacementType(Enum type)
        {
            if (type == null)
            {
                return;
            }

            _classicBlockDisplacementTypeField?.SetValueWithoutNotify(type);
        }

        private void BindElements()
        {
            if (Root == null)
            {
                Debug.LogWarning("[EditorWindowManager] Root is null.");
                return;
            }

            _mapCommonContainer = Root.Q<VisualElement>("map-common-container");
            _mapDerivedContainer = Root.Q<VisualElement>("map-derived-container");
            _mapMissBindingContainer = Root.Q<VisualElement>("map-missbinding-container");

            _roadCommonContainer = Root.Q<VisualElement>("road-common-container");
            _roadDerivedContainer = Root.Q<VisualElement>("road-derived-container");
            _roadMissBindingContainer = Root.Q<VisualElement>("road-missbinding-container");

            _blockCommonContainer = Root.Q<VisualElement>("block-common-container");
            _blockDerivedContainer = Root.Q<VisualElement>("block-derived-container");
            _blockMissBindingContainer = Root.Q<VisualElement>("block-missbinding-container");

            _bindedView = Root.Q<VisualElement>("binded-view");
            _unbindedView = Root.Q<VisualElement>("unbinded-view");
            _retryButton = Root.Q<Button>("editor-retry");

            _mapMissBindingButton = Root.Q<Button>("map-missbinding-retry");
            _mapRebuildRoadsButton = Root.Q<Button>("map-rebuild-roads");
            _mapRefreshAllRoadsButton = Root.Q<Button>("map-refresh-all-roads");

            _roadListView = Root.Q<ListView>("road-list-view");
            _roadListEmptyLabel = Root.Q<Label>("road-list-empty");
            _roadCreateButton = Root.Q<Button>("road-create");
            _roadDeleteButton = Root.Q<Button>("road-delete");
            _roadDuplicateButton = Root.Q<Button>("road-duplicate");
            _roadRefreshButton = Root.Q<Button>("road-refresh");

            _roadRefreshRoadBlocksButton = Root.Q<Button>("road-refresh-road-blocks");
            _roadUpdateBlockTransformButton = Root.Q<Button>("road-update-block-transform");
            _roadRefreshBlockDisplayButton = Root.Q<Button>("road-refresh-block-display");
            _roadSegmentNameField = Root.Q<DropdownField>("road-detail-segment");
            _roadNoteRangeBeginField = Root.Q<IntegerField>("road-note-range-begin");
            _roadNoteRangeEndField = Root.Q<IntegerField>("road-note-range-end");
            _roadModifyNoteRangeButton = Root.Q<Button>("road-modify-note-range");
            _roadTargetDataNameField = Root.Q<TextField>("road-target-data-name");
            _roadModifyTargetDataNameButton = Root.Q<Button>("road-modify-target-data-name");

            _blockDisplacementDataTypeField = Root.Q<EnumField>("block-displacement-data-type");
            _classicBlockTurnTypeField = Root.Q<EnumField>("classic-block-turn-type");
            _classicBlockApplyTurnTypeButton = Root.Q<Button>("classic-block-apply-turn-type");
            _classicBlockDisplacementTypeField = Root.Q<EnumField>("classic-block-displacement-type");
            _classicBlockApplyDisplacementTypeButton = Root.Q<Button>("classic-block-apply-displacement-type");
            _blockDisplacementCreateSection = Root.Q<VisualElement>("block-displacement-create-section");
            _blockDisplacementDetailSection = Root.Q<VisualElement>("block-displacement-detail");
            _blockDisplacementCreateCurrentButton = Root.Q<Button>("block-displacement-create-current");
            _blockDisplacementDeleteCurrentButton = Root.Q<Button>("block-displacement-delete-current");

            _blockDisplacementShiftSection = Root.Q<VisualElement>("block-displacement-shift-section");
            _blockDisplacementShiftStartField = Root.Q<IntegerField>("block-displacement-shift-start");
            _blockDisplacementShiftEndField = Root.Q<IntegerField>("block-displacement-shift-end");
            _blockDisplacementShiftMinusButton = Root.Q<Button>("block-displacement-shift-minus");
            _blockDisplacementShiftPlusButton = Root.Q<Button>("block-displacement-shift-plus");

            _blockDisplacementListView = Root.Q<ListView>("block-displacement-list-view");
            _blockDisplacementEmptyLabel = Root.Q<Label>("block-displacement-list-empty");
            _blockDisplacementCreateButton = Root.Q<Button>("block-displacement-create");
            _blockDisplacementDeleteButton = Root.Q<Button>("block-displacement-delete");
            _blockDisplacementApplyBatchButton = Root.Q<Button>("block-displacement-apply-batch");

            if (_mapMissBindingButton != null)
            {
                _mapMissBindingButton.clicked += () => MapMissBindingRetryRequested?.Invoke();
            }
            if (_mapRebuildRoadsButton != null)
            {
                _mapRebuildRoadsButton.clicked += () => MapRebuildRoadsRequested?.Invoke();
            }

            if (_mapRefreshAllRoadsButton != null)
            {
                _mapRefreshAllRoadsButton.clicked += () => MapRefreshAllRoadsRequested?.Invoke();
            }

            if (_roadCreateButton != null)
            {
                _roadCreateButton.clicked += () => RoadCreateRequested?.Invoke();
            }

            if (_roadDeleteButton != null)
            {
                _roadDeleteButton.clicked += () => RoadDeleteRequested?.Invoke();
            }

            if (_roadDuplicateButton != null)
            {
                _roadDuplicateButton.clicked += () => RoadDuplicateRequested?.Invoke();
            }

            if (_roadRefreshButton != null)
            {
                _roadRefreshButton.clicked += () => RoadRefreshRequested?.Invoke();
            }

            if (_roadRefreshRoadBlocksButton != null)
            {
                _roadRefreshRoadBlocksButton.clicked += () => RoadRefreshBlocksRequested?.Invoke();
            }

            if (_roadUpdateBlockTransformButton != null)
            {
                _roadUpdateBlockTransformButton.clicked += () => RoadUpdateBlockTransformRequested?.Invoke();
            }

            if (_roadRefreshBlockDisplayButton != null)
            {
                _roadRefreshBlockDisplayButton.clicked += () => RoadRefreshBlockDisplayRequested?.Invoke();
            }

            if (_roadSegmentNameField != null)
            {
                _roadSegmentNameField.RegisterValueChangedCallback(_ =>
                {
                    if (_isUpdatingSegmentOptions) return;
                    int selectedIndex = _roadSegmentNameField.index;
                    if (selectedIndex >= 0 && selectedIndex < _roadSegmentOptionIndices.Count)
                    {
                        RoadModifyTargetSegmentRequested?.Invoke(_roadSegmentOptionIndices[selectedIndex]);
                    }
                });
            }

            if (_roadModifyNoteRangeButton != null)
            {
                _roadModifyNoteRangeButton.clicked += () => RoadModifyNoteRangeRequested?.Invoke(
                    _roadNoteRangeBeginField?.value ?? 0,
                    _roadNoteRangeEndField?.value ?? 0);
            }

            if (_roadModifyTargetDataNameButton != null)
            {
                _roadModifyTargetDataNameButton.clicked += () => RoadModifyTargetDataNameRequested?.Invoke(_roadTargetDataNameField?.value ?? string.Empty);
            }

            if (_blockDisplacementDataTypeField != null)
            {
                _blockDisplacementDataTypeField.Init(BlockDisplacementDataType.Classic);
                _blockDisplacementDataTypeField.SetValueWithoutNotify(BlockDisplacementDataType.Classic);
                _blockDisplacementDataTypeField.RegisterValueChangedCallback(evt => BlockDisplacementDataTypeChanged?.Invoke(evt.newValue));
            }

            if (_classicBlockApplyTurnTypeButton != null)
            {
                _classicBlockApplyTurnTypeButton.clicked += () => ClassicBlockApplyTurnTypeRequested?.Invoke(_classicBlockTurnTypeField?.value);
            }

            if (_classicBlockApplyDisplacementTypeButton != null)
            {
                _classicBlockApplyDisplacementTypeButton.clicked += () => ClassicBlockApplyDisplacementTypeRequested?.Invoke(_classicBlockDisplacementTypeField?.value);
            }

            if (_roadListView != null)
            {
                _roadListView.selectionType = SelectionType.Single;
                _roadListView.fixedItemHeight = 22;
                _roadListView.makeItem = () =>
                {
                    var label = new Label();
                    label.AddToClassList("db-list-row");
                    return label;
                };
                _roadListView.bindItem = (element, i) =>
                {
                    if (element is Label label && i >= 0 && i < _roadListCache.Count)
                    {
                        var road = _roadListCache[i];
                        label.text = $"{i}. {road.roadName} | {GetSegmentDisplayName(road.targetSegmentIndex)} | Note {road.noteBeginIndex}-{road.noteEndIndex} | Block {road.BlockCount}";
                    }
                };
                _roadListView.selectionChanged += _ =>
                {
                    if (_roadListView.selectedIndex >= 0)
                    {
                        RoadListSelectionChanged?.Invoke(_roadListView.selectedIndex);
                    }
                };
            }

            if (_blockDisplacementListView != null)
            {
                _blockDisplacementListView.selectionType = SelectionType.Multiple;
                _blockDisplacementListView.fixedItemHeight = 22;
                _blockDisplacementListView.makeItem = () =>
                {
                    var label = new Label();
                    label.AddToClassList("db-list-row");
                    return label;
                };
                _blockDisplacementListView.bindItem = (element, i) =>
                {
                    if (element is Label label && i >= 0 && i < _blockDisplacementListCache.Count)
                    {
                        var data = _blockDisplacementListCache[i];
                        label.text = $"Block {data.BlockIndex_Local} | {data.GetType().Name}";
                    }
                };
                _blockDisplacementListView.selectionChanged += _ =>
                {
                    if (_blockDisplacementListView.selectedIndex < 0 || _blockDisplacementListCache.Count == 0) return;
                    var data = _blockDisplacementListCache[Mathf.Clamp(_blockDisplacementListView.selectedIndex, 0, _blockDisplacementListCache.Count - 1)];
                    BlockDisplacementSelectionChanged?.Invoke(data.BlockIndex_Local);
                };
            }

            if (_blockDisplacementCreateButton != null)
            {
                _blockDisplacementCreateButton.clicked += () => BlockDisplacementCreateRequested?.Invoke();
            }

            if (_blockDisplacementCreateCurrentButton != null)
            {
                _blockDisplacementCreateCurrentButton.clicked += () => BlockDisplacementCreateRequested?.Invoke();
            }

            if (_blockDisplacementDeleteButton != null)
            {
                _blockDisplacementDeleteButton.clicked += () => BlockDisplacementDeleteRequested?.Invoke();
            }

            if (_blockDisplacementDeleteCurrentButton != null)
            {
                _blockDisplacementDeleteCurrentButton.clicked += () => BlockDisplacementDeleteRequested?.Invoke();
            }

            if (_blockDisplacementApplyBatchButton != null)
            {
                _blockDisplacementApplyBatchButton.clicked += () => BlockDisplacementApplyBatchRequested?.Invoke();
            }

            if (_blockDisplacementShiftMinusButton != null)
            {
                _blockDisplacementShiftMinusButton.clicked += () =>
                {
                    var (start, end) = GetBlockDisplacementShiftRange();
                    BlockDisplacementShiftRequested?.Invoke(start, end, -1);
                };
            }

            if (_blockDisplacementShiftPlusButton != null)
            {
                _blockDisplacementShiftPlusButton.clicked += () =>
                {
                    var (start, end) = GetBlockDisplacementShiftRange();
                    BlockDisplacementShiftRequested?.Invoke(start, end, 1);
                };
            }

            if (_retryButton != null)
            {
                _retryButton.clicked += () => RetryBind?.Invoke();
            }

        }
    }
}
