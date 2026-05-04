# DancingBall EditorTool 架构设计

## 1. 定位

DancingBall 的编辑器工具系统。在 Editor 和 Play Mode 下提供一致的地图编辑体验（选择、导航、CRUD）。

**依赖关系**：依赖 `UIDrawManagerService`（Play Mode 下的窗口宿主），但不与 UI 框架耦合。Editor 下直接使用 EditorWindow/Overlay。

## 2. 三层架构

```
Host（创建窗口 + 提供 VisualElement 容器）
  │
  │ Play Mode:  PlayModeEditorHost [AutoService(PlayMode)]
  │   - 主动创建：服务初始化时通过 UIDrawManagerService 打开三个编辑窗口
  │   - 加载 UXML、new Controller、Bind
  │
  │ Editor Mode: PanelWindow / Overlay
  │   - 被动呼出：用户点 MenuItem 或 Overlay 默认显示
  │   - 加载 UXML、new Controller、Bind
  │   - 窗口关闭时 Dispose Controller
  │
  ├── Controller（纯 C# 桥接层）
  │    - 接收 EditorCenter 引用（由 Host 传入构造函数）
  │    - 创建 UIManager、订阅 EditorCenter 事件、挂接 UIManager Action
  │    - 不含任何 Unity Editor API，两个模式共用同一份代码
  │
  └── UIManager（已存在的 View 层，不变）
       - 描述 UXML 结构、控件查询赋值
       - 不知道 EditorCenter 存在
       - 暴露 Action（如 RoadCreateRequested），由 Controller 挂接
```

### 各层职责

| 层 | 职责 | 约束 |
|---|---|---|
| **Host** | 创建窗口，提供 VisualElement 容器，管理窗口生命周期 | Editor Mode 和 Play Mode 各一套实现 |
| **Controller** | 桥接 EditorCenter 与 UIManager | 纯 C#，无 Editor API 引用，两个模式共用 |
| **UIManager** | UXML 绑定、元素赋值、暴露 Action | 已存在，不变 |
| **EditorCenter** | 选择状态、CRUD、事件总线 | Dual AutoService |

## 3. 组件详述

### 3.1 EditorCenter

```csharp
[AutoService(Mode = AutoServiceMode.Dual)]
public class EditorCenter : GameServiceBase<EditorCenter>
```

| 环境 | 管理方 | 发现地图/玩家 |
|---|---|---|
| Editor Mode | EditorLocator | `TryAutoBind()` 在场景中查找 |
| Play Mode | RuntimeLocator | `TryAutoBind()` 在场景中查找 |

事件总线：
- `OnSelectionChanged(int roadIndex, int blockIndex)`
- `OnRoadSelectionChanged(IRoad)`
- `OnBlockSelectionChanged(IBlock, IBlockDisplacementData)`
- `LookAtObject(GameObject)`
- `OnRoadListChanged(List<RoadData>)`
- `OnBlockDisplacementListChanged(List<IBlockDisplacementData>)`
- `SendMessage(string)`

### 3.2 Controller

```csharp
public interface IEditorViewController : IDisposable
{
    void Bind(VisualElement root);
}

public interface IShortcutReceiver
{
    void SetShortcut(KeyCode key, Action action);
    void OnKeyDown(KeyCode key);
}
```

Controller 构造时接收 `EditorCenter` 引用（由 Host 注入），不允许自解析。

| Controller | 关联 UIManager | 快捷键 |
|---|---|---|
| `InspectorViewController` | `InspectorWindowManager` + `ClassicBlockDisplacementUIManager` | 无 |
| `SelectionViewController` | `SelectionWindowManager` + `ClassicBlockDisplacementUIManager` | ←/→ 导航 |
| `DisplacementViewController` | `DisplacementOverlayManager` | 无 |

Controller 在 `Bind()` 中完成：
1. 创建 UIManager
2. 订阅 EditorCenter 事件
3. 挂接 UIManager 的 Action → EditorCenter 方法

在 `Dispose()` 中：
1. 取消订阅 EditorCenter 事件
2. 清理 UIManager

#### Host 特有逻辑处理

Controller 不引用 Editor 程序集类型，Editor 特有的操作（如打开 `RoadCreateWindow` 弹窗）通过事件暴露给 Host：

```csharp
// Controller
public Action RoadCreateDialogRequested { get; set; }

// Editor Host 订阅
ctrl.RoadCreateDialogRequested = () =>
    RoadCreateWindow.ShowWindow(editorCenter.selectedRoad, callback);

// Play Mode 下不处理（没有 EditorWindow）
```

#### 快捷键：IShortcutReceiver

需要键盘输入的 Controller 实现此接口，Host 只转发原始 KeyCode：

```csharp
// Host（Editor Overlay）:
void OnSceneGUI(SceneView sceneView) {
    if (e.type == EventType.KeyDown) _ctrl.OnKeyDown(e.keyCode);
}

// Host（Play Mode）:
// 由 PlayModeEditorHost.OnUpdate() 每帧驱动，快捷键通过 SelectionViewController 内部字典处理
```

### 3.3 Host

#### Play Mode Host

```csharp
[AutoService(Mode = AutoServiceMode.PlayMode)]
public class PlayModeEditorHost : GameServiceBase<PlayModeEditorHost>
```

| 视图 | 创建方式 |
|---|---|
| Inspector 面板 | UIDrawManagerService.Open(uxml) → InspectorViewController.Bind(handle.RootVE) |
| Selection 浮层 | UIDrawManagerService.Open(uxml) → SelectionViewController.Bind(handle.RootVE) |
| Displacement 浮层 | UIDrawManagerService.Open(uxml) → DisplacementViewController.Bind(handle.RootVE) |

创建时序：
1. RuntimeLocator 拓扑排序：UIDrawManagerService > EditorCenter > PlayModeEditorHost
2. `UIDrawManagerService.OnInitialize()` → 创建 UIDocument + UIRootContainer
3. `EditorCenter.OnInitialize()` → `TryAutoBind()` 找场景 Map/Player
4. `PlayModeEditorHost.OnInitialize()` → 通过 `UIDrawManagerService.Open()` 创建三个窗口，每个绑定 Controller
5. 之后每帧 `OnUpdate()` → 驱动 Controller 刷新（如 Displacement 调试数据）

#### Editor Mode Host

| 视图 | 宿主 | 说明 |
|---|---|---|
| Inspector 面板 | `PanelWindow` | 泛用窗口，传入 lambda 创建 Controller |
| Selection 浮层 | `SelectionOverlay : Overlay` | 默认显示在 SceneView |
| Displacement 浮层 | `DisplacementOverlay : Overlay` | 默认显示在 SceneView |
| Settings 面板 | `SettingsWindow : EditorWindow` | 直接挂 UIManager，无需 Controller |

**PanelWindow**——泛用 EditorWindow：

```csharp
PanelWindow.Show("DancingBall Inspector", new Vector2(520, 360),
    root => new InspectorViewController(editorCenter).Also(ctrl => ctrl.Bind(root)));
// PanelWindow 接管生命周期，窗口关闭时 Dispose Controller
```

#### 创建时序对比

```
Play Mode:                           Editor Mode:
  RuntimeLocator 自动扫描             用户点 MenuItem / Overlay 默认显示
  → UIDrawManagerService 初始化        → Host 从 EditorLocator 拿 EditorCenter
  → EditorCenter 初始化               → Host 加载 UXML
  → PlayModeEditorHost 创建窗口        → new Controller(editorCenter).Bind(root)
  → 每个窗口绑定 Controller             → 窗口关闭时 Dispose
```

### 3.4 UIManager（已有，不变）

现有类不动，仅在其上层添加 Controller 层。

| 文件 | 说明 |
|---|---|
| `UIManagerBase` | 抽象基类，ApplyTheme |
| `InspectorWindowManager` | 三层编辑（Map/Road/Block） |
| `SelectionWindowManager` | 选择信息、跳转 |
| `DisplacementOverlayManager` | 位移调试图 |
| `SettingsWindowManager` | 快捷键配置 |
| `RoadCreateWindowManager` | 道路创建表单 |
| `ClassicBlockDisplacementUIManager` | Block 位移网格编辑 |

## 4. 程序集约束

Unity 编译规则：
- `Controller/` 目录**不在** Editor 文件夹中 → 编译到 `Assembly-CSharp`（主程序集）
- `Editor/` 目录 → 编译到 `Assembly-CSharp-Editor`

**Controller 不能引用 Editor 程序集中的任何类型**。约束处理方式：
- EditorCenter 引用由 Host 反向注入（构造函数参数）
- Editor 特有操作（如 RoadCreateWindow 弹窗）→ Controller 暴露事件，Host 订阅

```csharp
// Controller 不写这段代码（编译错误）：
// EditorCenter ec = EditorLocator.GetService<EditorCenter>(); // ❌

// Host 拿到引用后传入：
new InspectorViewController(editorCenter); // ✓
```

## 5. 文件结构

```
DancingBall/EditorTool/
├── ARCHITECTURE.zh.md
├── EditorCenter.cs                    # [AutoService(Dual)] 服务
├── EditorConfig.cs
├── EditorShortcutConfig.cs
├── BlockDisplacementDataType.cs
├── Interfaces.cs
├── PlayModeEditorHost.cs              # [AutoService(PlayMode)] 窗口管理服务
├── Controller/                        # 纯 C#，两个模式共用
│   ├── IEditorViewController.cs       # Bind(VisualElement) + Dispose()
│   ├── IShortcutReceiver.cs           # SetShortcut() + OnKeyDown()
│   ├── InspectorViewController.cs     # EditorCenter ↔ InspectorWindowManager
│   ├── SelectionViewController.cs     # EditorCenter ↔ SelectionWindowManager, ←/→ 快捷键
│   └── DisplacementViewController.cs  # EditorCenter ↔ DisplacementOverlayManager
├── Editor/
│   ├── PanelWindow.cs                 # 泛用 EditorWindow
│   ├── SelectionOverlay.cs            # 壳 → SelectionViewController
│   ├── DisplacementOverlay.cs         # 壳 → DisplacementViewController
│   ├── InspectorWindow.cs             # → PanelWindow + InspectorViewController
│   ├── SettingsWindow.cs              # 直接挂 UIManager
│   ├── RoadCreateWindow.cs            # 直接挂 UIManager
│   ├── SceneDataJsonArchiveUtility.cs
│   └── SceneDataMigrationWindow.cs
└── UIManager/                         # 不变
```

## 6. 实施状态

- ✅ EditorCenter 已改为 Dual
- ✅ Controller 基础结构（IEditorViewController、IShortcutReceiver）
- ✅ InspectorViewController（提取自 InspectorWindow）
- ✅ SelectionViewController（提取自 SelectionOverlay）
- ✅ DisplacementViewController（提取自 DisplacementOverlay）
- ✅ PanelWindow 泛用窗口
- ✅ PlayModeEditorHost（Play Mode 窗口服务）
- ✅ Editor 窗口重构为壳
