# BlockDisplacementData 扩展指南

## 概述

Block Displacement 系统用于定义方块在道路上的偏移规则（转弯、上下坡等），是整个 DancingBall 编辑器的核心扩展点之一。

系统分为三层：

| 层 | 职责 | 位置 |
|---|---|---|
| **Data** | 数据定义、序列化、规则计算 | `Data/` |
| **UI** | 每个数据类型的专属编辑面板 | `EditorTool/UIManager/` |
| **Controller** | 连接 Data 与 UI，托管生命周期 | `EditorTool/Controller/` |

当前只有一个内置实现：`ClassicBlockDisplacementData`。新增数据类型时，Data 和 UI 都需要各自实现，然后通过 Factory 注册。

---

## 1. Data 层 — 数据契约

所有位移数据必须实现 `IBlockDisplacementData`：

```csharp
public interface IBlockDisplacementData
{
    int BlockIndex_Local { get; }
    void ApplyDisplacementRule(List<IBlock> blocks);
    int GetBlockIndexDelta();
}
```

### 新增数据类型的步骤

1. 创建实现类，例如 `AdvancedBlockDisplacementData`：

```csharp
[Serializable]
public class AdvancedBlockDisplacementData : IBlockDisplacementData
{
    // 必须提供 (int blockLocalIndex) 签名的构造函数
    public AdvancedBlockDisplacementData(int blockLocalIndex)
    {
        BlockIndex_Local = blockLocalIndex;
    }

    public int BlockIndex_Local { get; private set; }

    public void ApplyDisplacementRule(List<IBlock> blocks)
    {
        // 实现位移逻辑
    }

    public int GetBlockIndexDelta()
    {
        // 返回相对于当前 Block 的偏移量
    }
}
```

**重要约束**：构造函数必须接受 `int blockLocalIndex`，因为 `RoadData.CreateBlockDisplacementData()` 通过 `Activator.CreateInstance(type, blockLocalIndex)` 反射创建。

### 序列化

如果数据类型需要 JSON 序列化，需要在 `SceneDataJsonArchive.cs` 中创建对应的 Archive DTO：

```csharp
[Serializable]
public class AdvancedBlockDisplacementArchive
{
    public string someProperty;
    public string anotherProperty;

    public static List<IBlockDisplacementData> FromBlockList(List<IBlockDisplacementData> blockList) { ... }
    public static List<IBlockDisplacementData> ToBlockList(List<IBlockDisplacementData> archiveList) { ... }
}
```

并在 `BlockDisplacementArchive` 中添加对应的分支。

---

## 2. UI 层 — 编辑面板

每个数据类型可以有专属的编辑 UI。UI 组件必须实现 `IBlockDisplacementUIManager`：

```csharp
public interface IBlockDisplacementUIManager : IDisposable
{
    VisualElement rootVisualElement { get; }
    void SetData(IBlockDisplacementData data);
    event Action<IBlockDisplacementData> OnDataChanged;
}
```

### 新增 UI 的步骤

1. 在 `UI/BlockDisplacementData/` 下创建 UXML 模板，例如 `Advanced.uxml`。
   根元素命名为 `advanced-root` 以避免冲突，其余元素采用有意义的 name 供 Manager 绑定。

2. 创建 Manager 类，例如 `AdvancedBlockDisplacementUIManager`：

```csharp
public class AdvancedBlockDisplacementUIManager : UIManagerBase, IBlockDisplacementUIManager
{
    public const string UxmlPath = "Assets/MusicTogether/DancingBall/UI/BlockDisplacementData/Advanced.uxml";

    public VisualElement rootVisualElement => Root;
    public event Action<IBlockDisplacementData> OnDataChanged;

    public AdvancedBlockDisplacementUIManager(VisualElement root) : base(root)
    {
        BindElements();
    }

    public void Dispose()
    {
        Root?.parent?.Remove(Root);
    }

    public void SetData(IBlockDisplacementData data)
    {
        // 强转为具体类型，更新 UI
    }
}
```

3. 在 `BlockDisplacementUIFactory.EnsureInitialized()` 中注册：

```csharp
Register<AdvancedBlockDisplacementData>(container =>
{
#if UNITY_EDITOR
    var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AdvancedBlockDisplacementUIManager.UxmlPath);
    var root = tree.CloneTree();
    container.Add(root);
    return new AdvancedBlockDisplacementUIManager(root);
#else
    return null;
#endif
});
```

### UIManagerBase

`UIManagerBase` 会自动将当前主题的样式表（Common + Dark）应用到传入的 `root` 上。新的 Manager 不需要手动处理主题。

### 关于容器

两个宿主在 UXML 中各预留了一个空容器：

- `SelectionWindow.uxml` → `#block-displacement-container`
- `BlockEditorPanel.uxml` → `#block-displacement-container`

Factory 创建的 Manager 会将 UXML 克隆树装入这个容器，切换数据类型时自动清除旧树。

---

## 3. Controller 层 — 自动适配

`SelectionViewController` 和 `InspectorViewController` 已经改为通过 Factory 创建 UI，不直接引用任何具体 Manager 类型。

```csharp
// 两个 Controller 中的模式完全一致：
_currentDisplacementUI?.Dispose();
_displacementContainer?.Clear();

_currentDisplacementUI = BlockDisplacementUIFactory.Create(_displacementContainer, dataToShow);
if (_currentDisplacementUI != null)
{
    _currentDisplacementUI.SetData(dataToShow);
    _currentDisplacementUI.OnDataChanged += OnDisplacementDataChanged;
}
```

新增数据类型后，Controller 层不需要任何修改。

---

## 4. Factory 注册表

`BlockDisplacementUIFactory` 使用显式注册（非反射），方案透明可控。

### 注册方式

```csharp
// 泛型参数 TData 是数据类型，lambda 接收容器并返回 Manager
Register<TData>(container => { ... });
```

### 查询方式

```csharp
BlockDisplacementUIFactory.HasCreator(data)      // 是否存在对应的 UI 工厂
BlockDisplacementUIFactory.Create(container, data) // 创建 UI Manager
```

### 设计决策

- 为什么不用反射 + Attribute：Unity 编辑器场景下反射扫描的开销与复杂度不划算，显式注册更透明、更容易调试。
- 为什么不用 switch + enum：switch 是编译时固定的，不符合开闭原则。用 `Dictionary<Type, Factory>` 可以在不修改既有代码的前提下新增类型。

---

## 5. 完整新增数据类型 Checklist

- [ ] 创建 Data 类，实现 `IBlockDisplacementData`
- [ ] 提供 `(int blockLocalIndex)` 构造函数
- [ ] 如果需 JSON 序列化，添加 Archive DTO 并在 `BlockDisplacementArchive` 中注册
- [ ] 创建 UXML 模板（放在 `UI/BlockDisplacementData/`）
- [ ] 创建 UI Manager 类，继承 `UIManagerBase`，实现 `IBlockDisplacementUIManager`
- [ ] 暴露 `public const string UxmlPath`
- [ ] 在 `BlockDisplacementUIFactory.EnsureInitialized()` 中加一行 `Register<>()`
- [ ] 可选：在 `BlockDisplacementDataType` 枚举中添加新值（用于 `EnumField` 下拉选择）

不需要修改的文件：
- `SelectionWindow.uxml` / `BlockEditorPanel.uxml`
- `SelectionViewController.cs` / `InspectorViewController.cs`
- `InspectorWindowManager.cs` / `SelectionWindowManager.cs`
- `EditorCenter.cs`

---

## 6. 关键文件索引

| 职责 | 文件 |
|---|---|
| 数据接口 | `Data/Interfaces.cs` |
| Data 基类示例 | `Data/ClassicBlockDisplacementData.cs` |
| JSON 序列化 Archive | `Data/SceneDataJsonArchive.cs` (搜索 `BlockDisplacementArchive`) |
| 数据类型枚举 | `EditorTool/BlockDisplacementDataType.cs` |
| UI 接口 | `EditorTool/UIManager/IBlockDisplacementUIManager.cs` |
| UI 工厂 | `EditorTool/UIManager/BlockDisplacementUIFactory.cs` |
| UI Manager 示例 | `EditorTool/UIManager/ClassicBlockDisplacementUIManager.cs` |
| UXML 模板示例 | `UI/BlockDisplacementData/Classic.uxml` |
| USS 样式（共用） | `UI/BlockDisplacementData/DisplacementData_Common.uss` |
| Selection 宿主 Controller | `EditorTool/Controller/SelectionViewController.cs` |
| Inspector 宿主 Controller | `EditorTool/Controller/InspectorViewController.cs` |
