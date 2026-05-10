using System;
using System.Collections.Generic;
using MusicTogether.DancingBall.Data;
using UnityEngine;

namespace MusicTogether.DancingBall.EditorTool.Controller
{
    /// <summary>
    /// 集中管理所有快捷键绑定。从 EditorShortcutConfig 加载键位，编辑时调用 EditorCenter 对应方法。
    /// </summary>
    public class EditorShortcutDispatcher
    {
        private readonly EditorCenter _center;
        private readonly Dictionary<KeyCode, Action> _shortcuts = new();

        public string HintText { get; private set; } = "← → 前后切换";

        public EditorShortcutDispatcher(EditorCenter center)
        {
            _center = center;
            LoadDefaults();
        }

        private void LoadDefaults()
        {
            _shortcuts[KeyCode.LeftArrow] = () => _center.PreviousBlock();
            _shortcuts[KeyCode.RightArrow] = () => _center.NextBlock();
        }

        public void LoadFromConfig()
        {
            var config = EditorShortcutConfig.Config;
            if (config == null) return;

            // Navigation
            _shortcuts[config.previousBlock] = () => _center.PreviousBlock();
            _shortcuts[config.nextBlock] = () => _center.NextBlock();

            // Road operations
            _shortcuts[config.truncateRoad] = () => _center.TruncateRoadAtSelectedBlock();
            _shortcuts[config.truncateAndCreateRoad] = () => _center.TruncateAndCreateRoad();
            _shortcuts[config.continueCreateRoad] = () => _center.ContinueCreateRoad();

            // TurnType
            _shortcuts[config.setTurnTypeNone] = () => _center.SetSelectedBlockTurnType(ClassicBlockDisplacementData.TurnType.None);
            _shortcuts[config.setTurnTypeForward] = () => _center.SetSelectedBlockTurnType(ClassicBlockDisplacementData.TurnType.None);
            _shortcuts[config.setTurnTypeRight] = () => _center.SetSelectedBlockTurnType(ClassicBlockDisplacementData.TurnType.Right);
            _shortcuts[config.setTurnTypeLeft] = () => _center.SetSelectedBlockTurnType(ClassicBlockDisplacementData.TurnType.Left);
            _shortcuts[config.setTurnTypeJump] = () => _center.SetSelectedBlockTurnType(ClassicBlockDisplacementData.TurnType.Jump);

            // DisplacementType
            _shortcuts[config.setDisplacementTypeNone] = () => _center.SetSelectedBlockDisplacementType(ClassicBlockDisplacementData.DisplacementType.None);
            _shortcuts[config.setDisplacementTypeUp] = () => _center.SetSelectedBlockDisplacementType(ClassicBlockDisplacementData.DisplacementType.Up);
            _shortcuts[config.setDisplacementTypeDown] = () => _center.SetSelectedBlockDisplacementType(ClassicBlockDisplacementData.DisplacementType.Down);
            _shortcuts[config.setDisplacementTypeForwardUp] = () => _center.SetSelectedBlockDisplacementType(ClassicBlockDisplacementData.DisplacementType.ForwardUp);
            _shortcuts[config.setDisplacementTypeForwardDown] = () => _center.SetSelectedBlockDisplacementType(ClassicBlockDisplacementData.DisplacementType.ForwardDown);

            HintText = BuildHintText(config);
        }

        private static string BuildHintText(EditorShortcutConfig c)
        {
            string prev = KeyCodeDisplay(c.previousBlock);
            string next = KeyCodeDisplay(c.nextBlock);
            string tr = KeyCodeDisplay(c.truncateRoad);
            string ty = KeyCodeDisplay(c.truncateAndCreateRoad);
            string tn = KeyCodeDisplay(c.continueCreateRoad);

            string fwd = KeyCodeDisplay(c.setTurnTypeForward);
            string left = KeyCodeDisplay(c.setTurnTypeLeft);
            string right = KeyCodeDisplay(c.setTurnTypeRight);
            string noneT = KeyCodeDisplay(c.setTurnTypeNone);
            string jump = KeyCodeDisplay(c.setTurnTypeJump);

            string noneD = KeyCodeDisplay(c.setDisplacementTypeNone);
            string up = KeyCodeDisplay(c.setDisplacementTypeUp);
            string down = KeyCodeDisplay(c.setDisplacementTypeDown);
            string fwdUp = KeyCodeDisplay(c.setDisplacementTypeForwardUp);
            string fwdDown = KeyCodeDisplay(c.setDisplacementTypeForwardDown);

            return $"{prev} {next} 前后切换 \n {tr} 截断 {ty} 拆分 {tn} 末尾续 \n" + 
                    $"{fwd}/{left}/{noneT}/{right} 转向 | {jump} 跳跃\n" +
                    $"{fwdDown}/{fwdUp}/{up}/{down}/{noneD} 位移";
        }

        private static string KeyCodeDisplay(KeyCode key) => key switch
        {
            KeyCode.LeftArrow => "←",
            KeyCode.RightArrow => "→",
            KeyCode.LeftShift => "Shift",
            KeyCode.RightShift => "Shift",
            KeyCode.Backspace => "Back",
            KeyCode.Space => "Space",
            KeyCode.Return => "Enter",
            KeyCode.Escape => "Esc",
            KeyCode.LeftControl => "Ctrl",
            KeyCode.RightControl => "Ctrl",
            KeyCode.LeftAlt => "Alt",
            KeyCode.RightAlt => "Alt",
            _ => key.ToString()
        };

        public bool ProcessKey(KeyCode key)
        {
            if (_shortcuts.TryGetValue(key, out var action))
            {
                action?.Invoke();
                return true;
            }
            return false;
        }

        public bool HasBinding(KeyCode key) => _shortcuts.ContainsKey(key);
    }
}
