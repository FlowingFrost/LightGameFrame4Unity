using System;
using System.Collections.Generic;
using System.Linq;
using MusicTogether.DancingBall.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.UIManager
{
    public class ClassicBlockDisplacementUIManager : UIManagerBase, IBlockDisplacementUIManager
    {
        public const string UxmlPath = "Assets/MusicTogether/DancingBall/UI/BlockDisplacementData/Classic.uxml";

        private const string SelectedClass = "db-grid-cell--selected";
        private Label _turnTypeLabel;
        private Label _displacementTypeLabel;
        private VisualElement _turnGrid;
        private VisualElement _displacementGrid;

        private readonly Dictionary<Button, ClassicBlockDisplacementData.TurnType> _turnButtonMap = new();
        private readonly Dictionary<Button, ClassicBlockDisplacementData.DisplacementType> _displacementButtonMap = new();
        private readonly HashSet<Button> _disabledButtons = new();

        private ClassicBlockDisplacementData _currentData;
        private bool _suppressNotify;

        public VisualElement rootVisualElement => Root;
        public event Action<IBlockDisplacementData> OnDataChanged;

        public ClassicBlockDisplacementUIManager(VisualElement root) : base(root)
        {
            BindElements();
        }

        public void Dispose()
        {
            Root?.parent?.Remove(Root);
        }

        public void SetData(IBlockDisplacementData data)
        {
            _currentData = data as ClassicBlockDisplacementData;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (_turnTypeLabel != null)
            {
                _turnTypeLabel.text = _currentData == null ? "当前：未设置" : $"当前：{_currentData.turnType}";
            }

            if (_displacementTypeLabel != null)
            {
                _displacementTypeLabel.text = _currentData == null ? "当前：未设置" : $"当前：{_currentData.displacementType}";
            }

            UpdateSelection(_turnButtonMap, _currentData?.turnType ?? ClassicBlockDisplacementData.TurnType.None);
            UpdateSelection(_displacementButtonMap, _currentData?.displacementType ?? ClassicBlockDisplacementData.DisplacementType.None);
            SetGridEnabled(_currentData != null);
        }

        private void SetGridEnabled(bool enabled)
        {
            foreach (var button in _turnButtonMap.Keys)
            {
                button?.SetEnabled(enabled);
            }

            foreach (var button in _displacementButtonMap.Keys)
            {
                button?.SetEnabled(enabled);
            }

            foreach (var button in _disabledButtons)
            {
                button?.SetEnabled(false);
            }
        }

        private void BindElements()
        {
            if (Root == null)
            {
                Debug.LogWarning("[ClassicBlockDisplacementUIManager] Root is null.");
                return;
            }

            _turnTypeLabel = Root.Q<Label>("classic-turn-type");
            _displacementTypeLabel = Root.Q<Label>("classic-displacement-type");
            _turnGrid = Root.Q<VisualElement>("classic-turn-grid");
            _displacementGrid = Root.Q<VisualElement>("classic-displacement-grid");

            BindTurnButton(1, 1, ClassicBlockDisplacementData.TurnType.None);
            BindTurnButton(1, 0, ClassicBlockDisplacementData.TurnType.Left);
            BindTurnButton(1, 2, ClassicBlockDisplacementData.TurnType.Right);
            BindTurnButton(0, 1, ClassicBlockDisplacementData.TurnType.Jump);

            BindDisplacementButton(1, 1, ClassicBlockDisplacementData.DisplacementType.None);
            BindDisplacementButton(0, 1, ClassicBlockDisplacementData.DisplacementType.Up);
            BindDisplacementButton(2, 1, ClassicBlockDisplacementData.DisplacementType.Down);
            BindDisplacementButton(0, 2, ClassicBlockDisplacementData.DisplacementType.ForwardUp);
            BindDisplacementButton(2, 2, ClassicBlockDisplacementData.DisplacementType.ForwardDown);

            DisableUnmappedButtons(_turnGrid, _turnButtonMap.Keys);
            DisableUnmappedButtons(_displacementGrid, _displacementButtonMap.Keys);

            RefreshDisplay();
        }

        private void BindTurnButton(int row, int col, ClassicBlockDisplacementData.TurnType type)
        {
            var button = Root.Q<Button>($"classic-turn-{row}-{col}");
            if (button == null) return;

            _turnButtonMap[button] = type;
            button.clicked += () => OnTurnTypeSelected(type);
        }

        private void BindDisplacementButton(int row, int col, ClassicBlockDisplacementData.DisplacementType type)
        {
            var button = Root.Q<Button>($"classic-displacement-{row}-{col}");
            if (button == null) return;

            _displacementButtonMap[button] = type;
            button.clicked += () => OnDisplacementTypeSelected(type);
        }

        private void DisableUnmappedButtons(VisualElement grid, ICollection<Button> mappedButtons)
        {
            if (grid == null) return;

            var mappedSet = new HashSet<Button>(mappedButtons);
            foreach (var button in grid.Query<Button>().ToList())
            {
                if (mappedSet.Contains(button)) continue;
                button?.SetEnabled(false);
                if (button != null)
                {
                    _disabledButtons.Add(button);
                }
            }
        }

        private void UpdateSelection<TEnum>(Dictionary<Button, TEnum> mapping, TEnum selected) where TEnum : Enum
        {
            foreach (var pair in mapping)
            {
                if (pair.Key == null) continue;
                bool isSelected = EqualityComparer<TEnum>.Default.Equals(pair.Value, selected);
                pair.Key.EnableInClassList(SelectedClass, isSelected);
            }
        }

        private void OnTurnTypeSelected(ClassicBlockDisplacementData.TurnType type)
        {
            if (_suppressNotify || _currentData == null) return;
            if (_currentData.turnType == type) return;

            var updated = new ClassicBlockDisplacementData(_currentData.BlockIndex_Local)
            {
                turnType = type,
                displacementType = _currentData.displacementType
            };

            _suppressNotify = true;
            _currentData = updated;
            RefreshDisplay();
            _suppressNotify = false;

            OnDataChanged?.Invoke(updated);
            ClearFocus();
        }

        private void OnDisplacementTypeSelected(ClassicBlockDisplacementData.DisplacementType type)
        {
            if (_suppressNotify || _currentData == null) return;
            if (_currentData.displacementType == type) return;

            var updated = new ClassicBlockDisplacementData(_currentData.BlockIndex_Local)
            {
                turnType = _currentData.turnType,
                displacementType = type
            };

            _suppressNotify = true;
            _currentData = updated;
            RefreshDisplay();
            _suppressNotify = false;

            OnDataChanged?.Invoke(updated);
            ClearFocus();
        }

        private void ClearFocus()
        {
            var focused = Root?.panel?.focusController?.focusedElement;
            focused?.Blur();
        }
    }
}
