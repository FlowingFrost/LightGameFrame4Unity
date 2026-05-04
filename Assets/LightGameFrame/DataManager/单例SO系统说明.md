# 单例ScriptableObject系统

## 核心理念

将单例模式直接内置到ScriptableObject中，实现跨场景配置管理，无需任何GameObject依赖。

## 关键特性

### 零场景依赖

```csharp
// 任何地方直接访问，自动初始化
var uiConfig = UIConfig.Config;
var gameConfig = GameConfig.Config;
```

### 完全静态化

```csharp
GameConfig.ApplyGameSettings();
GameConfig.SaveToJson();
GameConfig.ResetToDefault();
```

### 懒加载初始化

第一次访问时自动从 Resources 加载 .asset 模板，创建运行时副本，然后叠加 JSON override。

### 完整生命周期管理

```csharp
if (GameConfig.IsInitialized) { }
GameConfig.EnsureInitialized();
GameConfig.ResetToDefault();
GameConfig.DeletePersistedData();
```

## 系统架构

```
SingletonScriptableObject<T>
├── 命名空间推导 Resources 路径（零声明）
│   └── LightGameFrame.RoadEditor.FooConfig → Data/RoadEditor/FooConfig
├── 从 Resources 加载 .asset 模板 → Instantiate 运行时副本
├── 版本感知的 JSON override（可选）
│   ├── StreamingAssets/*.json   （随构建发布）
│   ├── persistentDataPath/*.json（运行时保存）
│   └── 仅当 JSON.configVersion ≥ 模板.configVersion 时应用
└── 完整的生命周期控制
```

## 数据存放规则

路径由命名空间自动推导，取命名空间最后一个 segment 作为子目录：

| 完整类型 | Resources 路径 | StreamingAssets 路径 |
|---|---|---|
| `LightGameFrame.DataManager.UIConfig` | `Data/DataManager/UIConfig` | `DataManager/UIConfig.json` |
| `LightGameFrame.RoadEditor.Config` | `Data/RoadEditor/Config` | `RoadEditor/Config.json` |
| 无命名空间的 `MyConfig` | `Data/MyConfig` | `MyConfig.json` |

所有 .asset 文件集中在 `Assets/Resources/Data/` 下，按子目录自动分类。

如需显式覆盖路径，使用 `[SingletonConfig]` Attribute（常态下不需要）。

## 嵌套 Section 模式

推荐用 `[System.Serializable]` 嵌套类收拢属于同一模块的字段，避免 Inspector 平铺：

```csharp
public class UIConfig : SingletonScriptableObject<UIConfig>
{
    public static UIConfig Config => Instance;

    public PanelSettings panelSettings;
    public string defaultUITransitionResourcePath = "Transitions/DefaultUI";

    public WindowElements elements = new();
    public AeroSnap aeroSnap = new();

    [System.Serializable]
    public class WindowElements
    {
        public string titleBar = "TitleBar";
        public string closeButton = "CloseButton";
    }

    [System.Serializable]
    public class AeroSnap
    {
        public bool enabled = true;
        public float snapThreshold = 40f;
    }
}
```

访问方式：

```csharp
UIConfig.Config.elements.titleBar;
UIConfig.Config.aeroSnap.enabled;
```

## 版本感知的 JSON Override

防止旧版本的 JSON 覆盖新版的配置默认值。

### 版本字段

每个 Config 在 Inspector 中暴露 `configVersion` 字段（默认 0），由开发者手动维护。

```csharp
// SingletonScriptableObject 基类内置
public int configVersion = 0;          // 手动递增
public string configUpdatedAt = "";    // 保存时自动更新
```

### 加载规则

```
模板 .asset (configVersion=2)
  ↓
JSON 候选：
  ├─ StreamingAssets/xxx.json  → 版本检查 → v2 ≥ 模板 v2 ✓
  └─ persistentDataPath/xxx.json → 版本检查 → v1 < 模板 v2 ✗ 跳过
  ↓
选出最佳：版本最高 → 日期最新
  ↓
叠加到模板副本上
```

### 工作流

1. 修改配置结构或默认值 → 在 .asset Inspector 中把 `configVersion` 递增
2. 构建发布 → StreamingAssets 中的 JSON 包含新的版本号
3. 用户之前保存的旧版本 JSON → 自动跳过，不会降级
4. 用户运行时再次保存 → 新 JSON 带有新版本号，正常生效

### 保存

`SaveToJson()` 自动刷新 `configUpdatedAt`，但 `configVersion` 不自增：

```csharp
GameConfig.SaveToJson();        // 写入 persistentDataPath
GameConfig.ResetToDefault();    // 重新从 Resources 模板初始化
GameConfig.DeletePersistedData(); // 删除持久化 JSON
```

## 创建自定义配置

```csharp
namespace LightGameFrame.MyModule
{
    public class MyConfig : SingletonScriptableObject<MyConfig>
    {
        public static MyConfig Config => Instance;

        public SectionA sectionA = new();
        public SectionB sectionB = new();

        [System.Serializable]
        public class SectionA
        {
            public string myValue = "default";
        }

        [System.Serializable]
        public class SectionB
        {
            public int count = 10;
        }
    }
}
```

.asset 文件放置在 `Assets/Resources/Data/MyModule/MyConfig.asset`，由命名空间自动推导。

## 性能优势

1. **懒加载** - 只有访问时才初始化
2. **零开销** - 运行时没有 MonoBehaviour 组件
3. **内存高效** - 单例模式确保只有一个实例
4. **加载快速** - 启动时一次性加载，后续直接访问

## 注意事项

1. **Resources 文件必须存在** - 系统依赖 Resources/Data/ 目录下的 ScriptableObject 文件
2. **JSON 文件可选** - 如果所有 JSON 源版本均低于模板版本，或不存在 JSON，则使用模板默认值
3. **configVersion 默认 0** - 不需要版本保护时无需修改，行为与旧版一致
4. **编辑器友好** - 在 Inspector 中修改 .asset 会同步到运行时副本