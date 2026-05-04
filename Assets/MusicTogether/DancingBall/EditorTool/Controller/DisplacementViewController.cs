using System;
using MusicTogether.DancingBall.EditorTool.UIManager;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.Controller
{
    public class DisplacementViewController : IEditorViewController
    {
        private readonly EditorCenter _editorCenter;
        private DisplacementOverlayManager _overlayManager;
        private bool _toolEnabled = true;

        public DisplacementViewController(EditorCenter editorCenter)
        {
            _editorCenter = editorCenter;
        }

        public void Bind(VisualElement root)
        {
            _overlayManager = new DisplacementOverlayManager(root);
            _overlayManager.EnableChanged = enabled => _toolEnabled = enabled;
            _overlayManager.RetryBind = RetryBind;
            BindEditorCenter();
        }

        public void Dispose()
        {
        }

        private void RetryBind()
        {
            _editorCenter?.TryAutoBind();
            BindEditorCenter();
        }

        private void BindEditorCenter()
        {
            if (_editorCenter == null || _editorCenter.player == null)
            {
                _overlayManager?.SetBindedViewVisible(false);
                return;
            }
            _overlayManager?.SetBindedViewVisible(true);
            _overlayManager?.SetEnabledState(_toolEnabled);
        }

        /// <summary>
        /// 刷新调试数据（由外部 Host 每帧驱动）。
        /// </summary>
        public void RefreshDebugData()
        {
            if (_overlayManager == null) return;

            if (_editorCenter == null || _editorCenter.player == null)
            {
                _overlayManager.SetBindedViewVisible(false);
                return;
            }

            if (!_toolEnabled)
            {
                _overlayManager.SetEnabledState(false);
                _overlayManager.ClearData("已禁用");
                return;
            }

            _overlayManager.SetEnabledState(true);
            _overlayManager.UpdateDebugData(_editorCenter.player);
        }
    }
}
