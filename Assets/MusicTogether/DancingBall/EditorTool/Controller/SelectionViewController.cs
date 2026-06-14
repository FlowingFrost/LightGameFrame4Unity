using System;
using System.Collections.Generic;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.EditorTool.UIManager;
using MusicTogether.DancingBall.SceneOld;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Controller
{
    public class SelectionViewController : IEditorViewController, IShortcutReceiver
    {
        private readonly EditorCenter _editorCenter;
        private SelectionWindowManager _selectionWindowManager;
        private IBlockDisplacementUIManager _currentDisplacementUI;
        private VisualElement _displacementContainer;
        private BlockDisplacementDataType _defaultDisplacementType = BlockDisplacementDataType.Classic;
        private IBlock _currentBlock;
        private IBlockDisplacementData _currentDisplacementData;

        private bool _hadExistingData;

        public SelectionViewController(EditorCenter editorCenter)
        {
            _editorCenter = editorCenter;
        }

        public void LoadShortcutsFromConfig()
        {
            _editorCenter?.Dispatcher?.LoadFromConfig();
        }

        public void SetShortcut(KeyCode key, Action action)
        {
            // 快捷键现已统一由 EditorShortcutDispatcher 管理，此方法保留仅为接口兼容。
        }

        public void OnKeyDown(KeyCode key)
        {
            if (_editorCenter != null && !_editorCenter.IsInputEnabled) return;
            _editorCenter?.ProcessKey(key, this);
        }

        public void Bind(VisualElement root)
        {
            _selectionWindowManager = new SelectionWindowManager(root);
            _selectionWindowManager.EnableChanged = enabled => { if (_editorCenter != null) _editorCenter.IsInputEnabled = enabled; };

            _selectionWindowManager.RetryBind = RetryBind;
            _selectionWindowManager.DefaultDisplacementTypeChanged = OnDefaultDisplacementTypeChanged;

            _displacementContainer = root.Q<VisualElement>("block-displacement-container");

            BindEditorCenter();
        }

        public void Dispose()
        {
            _currentDisplacementUI?.Dispose();
            if (_editorCenter != null)
            {
                _editorCenter.OnSelectionChanged -= _selectionWindowManager.UpdateSelectionInfo;
                _editorCenter.OnBlockSelectionChanged -= OnBlockSelectionChanged;
                _editorCenter.LookAtObject -= LookAt;
                _editorCenter.OnRoadSwitchCompleted -= OnRoadSwitchCompleted;
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
            _editorCenter.OnSelectionChanged += _selectionWindowManager.UpdateSelectionInfo;
            _editorCenter.OnBlockSelectionChanged += OnBlockSelectionChanged;
            _editorCenter.LookAtObject += LookAt;
            _editorCenter.OnRoadSwitchCompleted += OnRoadSwitchCompleted;
        }

        private void BindEditorCenter()
        {
            if (_editorCenter == null)
            {
                _selectionWindowManager.SetBindedViewVisible(false);
                return;
            }

            SubscribeEditorEvents();

            _selectionWindowManager.JumpTo = (roadIndex, blockIndex) => _editorCenter.JumpTo(roadIndex, blockIndex);
            _selectionWindowManager.SetBindedViewVisible(true);
            _selectionWindowManager.SetEnabledState(_editorCenter?.IsInputEnabled ?? true);
            _selectionWindowManager.SetDefaultDisplacementType(_defaultDisplacementType);
        }

        /// <summary>
        /// 刷新 UI 提示文字（由外部 Host 驱动，如 EditorApplication.update 或 MonoBehaviour.Update）。
        /// </summary>
        public void RefreshHint()
        {
            bool enabled = _editorCenter?.IsInputEnabled ?? true;
            string hint = enabled
                ? _editorCenter?.Dispatcher?.HintText ?? "← / → 切换"
                : "已禁用";
            _selectionWindowManager?.SetHint(hint);
            _selectionWindowManager?.SetEnabledState(enabled);
        }

        // ---- 事件响应 ----

        private void OnRoadSwitchCompleted()
        {
        }

        private void OnBlockSelectionChanged(IBlock block, IBlockDisplacementData data)
        {
            bool blockChanged = _currentBlock != block;
            _currentBlock = block;
            _currentDisplacementData = data;

            if (blockChanged)
            {
                _hadExistingData = data != null;
            }

            RefreshDisplacementPanel();
        }

        private void RefreshDisplacementPanel()
        {
            _currentDisplacementUI?.Dispose();
            _currentDisplacementUI = null;
            _displacementContainer?.Clear();

            if (_currentBlock == null) return;

            var dataToShow = _currentDisplacementData
                ?? CreateDefaultDisplacementData(_currentBlock.BlockLocalIndex);

            if (dataToShow != null && BlockDisplacementUIFactory.HasCreator(dataToShow))
            {
                _currentDisplacementUI = BlockDisplacementUIFactory.Create(_displacementContainer, dataToShow);
                if (_currentDisplacementUI != null)
                {
                    _currentDisplacementUI.SetData(dataToShow);
                    _currentDisplacementUI.OnDataChanged += OnDisplacementDataChanged;
                }
            }
        }

        private IBlockDisplacementData CreateDefaultDisplacementData(int blockLocalIndex)
        {
            return _defaultDisplacementType switch
            {
                BlockDisplacementDataType.Classic => new ClassicBlockDisplacementData(blockLocalIndex),
                _ => new ClassicBlockDisplacementData(blockLocalIndex)
            };
        }

        private void OnDefaultDisplacementTypeChanged(Enum value)
        {
            if (value is BlockDisplacementDataType type)
            {
                _defaultDisplacementType = type;
                if (_currentDisplacementData == null)
                {
                    RefreshDisplacementPanel();
                }
            }
        }

        private void OnDisplacementDataChanged(IBlockDisplacementData data)
        {
            if (data == null || _editorCenter?.selectedRoad == null) return;

            if (!_hadExistingData && data is ClassicBlockDisplacementData classic
                && classic.turnType == ClassicBlockDisplacementData.TurnType.None
                && classic.displacementType == ClassicBlockDisplacementData.DisplacementType.None)
            {
                return;
            }

            _editorCenter.selectedRoad.ModifyDisplacementData(data.BlockIndex_Local, data);
            _editorCenter.RefreshSelection();
        }

        // ---- LookAt ----

        public void LookAt(GameObject go)
        {
            if (go == null) return;

#if UNITY_EDITOR
            UnityEditor.Selection.activeGameObject = go;
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            if (TryGetExpandedBounds(go, 7.0f, out var bounds))
            {
                sceneView.Frame(bounds, false);
            }
            else
            {
                sceneView.FrameSelected();
            }
#endif
        }

        private static bool TryGetExpandedBounds(GameObject target, float expandMultiplier, out Bounds bounds)
        {
            bounds = new Bounds();
            if (target == null) return false;

            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (expandMultiplier > 1f)
            {
                bounds.Expand(bounds.size * (expandMultiplier - 1f));
            }
            return true;
        }
    }
}
