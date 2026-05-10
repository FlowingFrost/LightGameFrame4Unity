using LightGameFrame.DataManager;
using UnityEngine;

namespace MusicTogether.DancingBall.EditorTool
{
    /// <summary>
    /// 编辑器快捷键配置 - 单例ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "EditorShortcutConfig", menuName = "MusicTogether/DancingBall/Editor Shortcut Config")]
    [SingletonConfig("Data/DancingBall/EditorShortcutConfig", "EditorShortcutConfig")]
    public class EditorShortcutConfig : SingletonScriptableObject<EditorShortcutConfig>
    {
        [Header("Navigation Shortcuts")]
        public KeyCode previousBlock = KeyCode.LeftArrow;
        public KeyCode nextBlock = KeyCode.RightArrow;
        public KeyCode sprint = KeyCode.LeftControl;

        [Header("Road Operations")]
        public KeyCode truncateRoad = KeyCode.T;
        public KeyCode truncateAndCreateRoad = KeyCode.Y;
        public KeyCode continueCreateRoad = KeyCode.N;

        [Header("TurnType Shortcuts")]
        public KeyCode setTurnTypeNone = KeyCode.S;
        public KeyCode setTurnTypeForward = KeyCode.W;
        public KeyCode setTurnTypeRight = KeyCode.D;
        public KeyCode setTurnTypeLeft = KeyCode.A;
        public KeyCode setTurnTypeJump = KeyCode.Space;

        [Header("DisplacementType Shortcuts")]
        public KeyCode setDisplacementTypeNone = KeyCode.Backspace;
        public KeyCode setDisplacementTypeUp = KeyCode.U;
        public KeyCode setDisplacementTypeDown = KeyCode.LeftShift;
        public KeyCode setDisplacementTypeForwardUp = KeyCode.E;
        public KeyCode setDisplacementTypeForwardDown = KeyCode.Q;

        /// <summary>
        /// 静态访问当前配置实例
        /// </summary>
        public static EditorShortcutConfig Config => Instance;
    }
}
