using System;
using System.Collections.Generic;
using MusicTogether.DancingBall.Data;
using MusicTogether.DancingBall.EditorTool.UIManager;
using MusicTogether.DancingBall.Scene;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Controller
{
    public class SelectionViewController : IEditorViewController, IShortcutReceiver
    {
        private readonly EditorCenter _editorCenter;
        private SelectionWindowManager _selectionWindowManager;
        private ClassicBlockDisplacementUIManager _classicDisplacementManager;
        private BlockDisplacementDataType _defaultDisplacementType = BlockDisplacementDataType.Classic;
        private IBlock _currentBlock;
        private IBlockDisplacementData _currentDisplacementData;

        private bool _toolEnabled = true;

        private readonly Dictionary<KeyCode, Action> _shortcuts = new();

        public SelectionViewController(EditorCenter editorCenter)
        {
            _editorCenter = editorCenter;

            // 默认快捷键绑定
            _shortcuts[KeyCode.LeftArrow] = () => _editorCenter?.PreviousBlock();
            _shortcuts[KeyCode.RightArrow] = () => _editorCenter?.NextBlock();
        }

        public void SetShortcut(KeyCode key, Action action)
        {
            _shortcuts[key] = action;
        }

        public void OnKeyDown(KeyCode key)
        {
            if (!_toolEnabled) return;
            if (_shortcuts.TryGetValue(key, out var action))
            {
                action?.Invoke();
            }
        }

        public void Bind(VisualElement root)
        {
            _selectionWindowManager = new SelectionWindowManager(root);
            _selectionWindowManager.EnableChanged = enabled => _toolEnabled = enabled;
            _selectionWindowManager.RetryBind = RetryBind;
            _selectionWindowManager.DefaultDisplacementTypeChanged = OnDefaultDisplacementTypeChanged;

            var classicRoot = root.Q<VisualElement>("classic-root");
            if (classicRoot != null)
            {
                _classicDisplacementManager = new ClassicBlockDisplacementUIManager(classicRoot);
                _classicDisplacementManager.DataChanged = OnDisplacementDataChanged;
            }

            BindEditorCenter();
        }

        public void Dispose()
        {
            if (_editorCenter != null)
            {
                _editorCenter.OnSelectionChanged -= _selectionWindowManager.UpdateSelectionInfo;
                _editorCenter.OnBlockSelectionChanged -= OnBlockSelectionChanged;
                _editorCenter.LookAtObject -= LookAt;
            }
            _shortcuts.Clear();
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
            _selectionWindowManager.SetEnabledState(true);
            _selectionWindowManager.SetDefaultDisplacementType(_defaultDisplacementType);
        }

        /// <summary>
        /// 刷新 UI 提示文字（由外部 Host 驱动，如 EditorApplication.update 或 MonoBehaviour.Update）。
        /// </summary>
        public void RefreshHint()
        {
            string hint = _toolEnabled ? "← / → 切换" : "已禁用";
            _selectionWindowManager?.SetHint(hint);
            _selectionWindowManager?.SetEnabledState(_toolEnabled);
        }

        // ---- 事件响应 ----

        private void OnBlockSelectionChanged(IBlock block, IBlockDisplacementData data)
        {
            _currentBlock = block;
            _currentDisplacementData = data;
            RefreshDisplacementPanel();
        }

        private void RefreshDisplacementPanel()
        {
            if (_classicDisplacementManager == null) return;

            if (_currentBlock == null)
            {
                _classicDisplacementManager.SetData(null);
                return;
            }

            if (_currentDisplacementData is ClassicBlockDisplacementData classicData)
            {
                _classicDisplacementManager.SetData(classicData);
                return;
            }

            if (_currentDisplacementData == null)
            {
                _classicDisplacementManager.SetData(
                    CreateDefaultDisplacementData(_currentBlock.BlockLocalIndex) as ClassicBlockDisplacementData);
                return;
            }

            _classicDisplacementManager.SetData(null);
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

            if (TryGetExpandedBounds(go, 3.0f, out var bounds))
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
