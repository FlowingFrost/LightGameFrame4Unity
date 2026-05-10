using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MusicTogether.DancingBall.EditorTool.UIManager
{
    public class SettingsWindowManager : UIManagerBase
    {
        private EnumField _settingsPrevBlockField;
        private EnumField _settingsNextBlockField;
        private EnumField _settingsSprintField;
        private EnumField _settingsTurnNoneField;
        private EnumField _settingsTurnForwardField;
        private EnumField _settingsTurnRightField;
        private EnumField _settingsTurnLeftField;
        private EnumField _settingsTurnJumpField;
        private EnumField _settingsDisplacementNoneField;
        private EnumField _settingsDisplacementUpField;
        private EnumField _settingsDisplacementDownField;
        private EnumField _settingsDisplacementForwardUpField;
        private EnumField _settingsDisplacementForwardDownField;
        private EnumField _settingsTruncateRoadField;
        private EnumField _settingsTruncateAndCreateField;
        private EnumField _settingsContinueCreateField;

        public Action ShortcutSettingsSaved { get; set; }

        public SettingsWindowManager(VisualElement root) : base(root)
        {
            BindElements();
        }

        public void LoadShortcutSettings(EditorShortcutConfig config = null)
        {
            if (config == null)
            {
                return;
            }

            _settingsPrevBlockField?.SetValueWithoutNotify(config.previousBlock);
            _settingsNextBlockField?.SetValueWithoutNotify(config.nextBlock);
            _settingsSprintField?.SetValueWithoutNotify(config.sprint);

            _settingsTurnNoneField?.SetValueWithoutNotify(config.setTurnTypeNone);
            _settingsTurnForwardField?.SetValueWithoutNotify(config.setTurnTypeForward);
            _settingsTurnRightField?.SetValueWithoutNotify(config.setTurnTypeRight);
            _settingsTurnLeftField?.SetValueWithoutNotify(config.setTurnTypeLeft);
            _settingsTurnJumpField?.SetValueWithoutNotify(config.setTurnTypeJump);

            _settingsDisplacementNoneField?.SetValueWithoutNotify(config.setDisplacementTypeNone);
            _settingsDisplacementUpField?.SetValueWithoutNotify(config.setDisplacementTypeUp);
            _settingsDisplacementDownField?.SetValueWithoutNotify(config.setDisplacementTypeDown);
            _settingsDisplacementForwardUpField?.SetValueWithoutNotify(config.setDisplacementTypeForwardUp);
            _settingsDisplacementForwardDownField?.SetValueWithoutNotify(config.setDisplacementTypeForwardDown);

            _settingsTruncateRoadField?.SetValueWithoutNotify(config.truncateRoad);
            _settingsTruncateAndCreateField?.SetValueWithoutNotify(config.truncateAndCreateRoad);
            _settingsContinueCreateField?.SetValueWithoutNotify(config.continueCreateRoad);
        }

        public void SaveShortcutSettings(EditorShortcutConfig config = null)
        {
            config ??= EditorShortcutConfig.Config;
            if (config == null)
            {
                return;
            }

            config.previousBlock = ResolveKeyCode(_settingsPrevBlockField?.value);
            config.nextBlock = ResolveKeyCode(_settingsNextBlockField?.value);
            config.sprint = ResolveKeyCode(_settingsSprintField?.value);

            config.setTurnTypeNone = ResolveKeyCode(_settingsTurnNoneField?.value);
            config.setTurnTypeForward = ResolveKeyCode(_settingsTurnForwardField?.value);
            config.setTurnTypeRight = ResolveKeyCode(_settingsTurnRightField?.value);
            config.setTurnTypeLeft = ResolveKeyCode(_settingsTurnLeftField?.value);
            config.setTurnTypeJump = ResolveKeyCode(_settingsTurnJumpField?.value);

            config.setDisplacementTypeNone = ResolveKeyCode(_settingsDisplacementNoneField?.value);
            config.setDisplacementTypeUp = ResolveKeyCode(_settingsDisplacementUpField?.value);
            config.setDisplacementTypeDown = ResolveKeyCode(_settingsDisplacementDownField?.value);
            config.setDisplacementTypeForwardUp = ResolveKeyCode(_settingsDisplacementForwardUpField?.value);
            config.setDisplacementTypeForwardDown = ResolveKeyCode(_settingsDisplacementForwardDownField?.value);

            config.truncateRoad = ResolveKeyCode(_settingsTruncateRoadField?.value);
            config.truncateAndCreateRoad = ResolveKeyCode(_settingsTruncateAndCreateField?.value);
            config.continueCreateRoad = ResolveKeyCode(_settingsContinueCreateField?.value);

            ShortcutSettingsSaved?.Invoke();
        }

        private void BindElements()
        {
            if (Root == null)
            {
                Debug.LogWarning("[SettingsWindowManager] Root is null.");
                return;
            }

            _settingsPrevBlockField = Root.Q<EnumField>("settings-prev-block");
            _settingsNextBlockField = Root.Q<EnumField>("settings-next-block");
            _settingsSprintField = Root.Q<EnumField>("settings-sprint");
            _settingsTurnNoneField = Root.Q<EnumField>("settings-turn-none");
            _settingsTurnForwardField = Root.Q<EnumField>("settings-turn-forward");
            _settingsTurnRightField = Root.Q<EnumField>("settings-turn-right");
            _settingsTurnLeftField = Root.Q<EnumField>("settings-turn-left");
            _settingsTurnJumpField = Root.Q<EnumField>("settings-turn-jump");
            _settingsDisplacementNoneField = Root.Q<EnumField>("settings-displacement-none");
            _settingsDisplacementUpField = Root.Q<EnumField>("settings-displacement-up");
            _settingsDisplacementDownField = Root.Q<EnumField>("settings-displacement-down");
            _settingsDisplacementForwardUpField = Root.Q<EnumField>("settings-displacement-forward-up");
            _settingsDisplacementForwardDownField = Root.Q<EnumField>("settings-displacement-forward-down");

            _settingsTruncateRoadField = Root.Q<EnumField>("settings-truncate-road");
            _settingsTruncateAndCreateField = Root.Q<EnumField>("settings-truncate-and-create");
            _settingsContinueCreateField = Root.Q<EnumField>("settings-continue-create");

            ConfigureKeyCodeField(_settingsPrevBlockField);
            ConfigureKeyCodeField(_settingsNextBlockField);
            ConfigureKeyCodeField(_settingsSprintField);
            ConfigureKeyCodeField(_settingsTurnNoneField);
            ConfigureKeyCodeField(_settingsTurnForwardField);
            ConfigureKeyCodeField(_settingsTurnRightField);
            ConfigureKeyCodeField(_settingsTurnLeftField);
            ConfigureKeyCodeField(_settingsTurnJumpField);
            ConfigureKeyCodeField(_settingsDisplacementNoneField);
            ConfigureKeyCodeField(_settingsDisplacementUpField);
            ConfigureKeyCodeField(_settingsDisplacementDownField);
            ConfigureKeyCodeField(_settingsDisplacementForwardUpField);
            ConfigureKeyCodeField(_settingsDisplacementForwardDownField);
            ConfigureKeyCodeField(_settingsTruncateRoadField);
            ConfigureKeyCodeField(_settingsTruncateAndCreateField);
            ConfigureKeyCodeField(_settingsContinueCreateField);

            RegisterShortcutSaveCallback(_settingsPrevBlockField);
            RegisterShortcutSaveCallback(_settingsNextBlockField);
            RegisterShortcutSaveCallback(_settingsSprintField);
            RegisterShortcutSaveCallback(_settingsTurnNoneField);
            RegisterShortcutSaveCallback(_settingsTurnForwardField);
            RegisterShortcutSaveCallback(_settingsTurnRightField);
            RegisterShortcutSaveCallback(_settingsTurnLeftField);
            RegisterShortcutSaveCallback(_settingsTurnJumpField);
            RegisterShortcutSaveCallback(_settingsDisplacementNoneField);
            RegisterShortcutSaveCallback(_settingsDisplacementUpField);
            RegisterShortcutSaveCallback(_settingsDisplacementDownField);
            RegisterShortcutSaveCallback(_settingsDisplacementForwardUpField);
            RegisterShortcutSaveCallback(_settingsDisplacementForwardDownField);
            RegisterShortcutSaveCallback(_settingsTruncateRoadField);
            RegisterShortcutSaveCallback(_settingsTruncateAndCreateField);
            RegisterShortcutSaveCallback(_settingsContinueCreateField);
        }

        private void RegisterShortcutSaveCallback(EnumField field)
        {
            if (field == null)
            {
                return;
            }

            field.RegisterValueChangedCallback(_ => SaveShortcutSettings());
        }

        private void ConfigureKeyCodeField(EnumField field)
        {
            if (field == null)
            {
                return;
            }

            if (field.value == null || field.value.GetType() != typeof(KeyCode))
            {
                field.Init(KeyCode.None);
                field.SetValueWithoutNotify(KeyCode.None);
            }
        }

        private static KeyCode ResolveKeyCode(Enum value)
        {
            return value is KeyCode keyCode ? keyCode : KeyCode.None;
        }
    }
}
