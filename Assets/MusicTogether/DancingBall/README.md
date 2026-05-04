# DancingBall Editor 使用与开发手册

## 目录

1. [项目概览](#1-项目概览)
2. [运行手册](#2-运行手册)
3. [项目结构](#3-项目结构)
4. [架构设计](#4-架构设计)
5. [扩展指南](#5-扩展指南)
6. [主题与配置](#6-主题与配置)
7. [文件索引](#7-文件索引)

---

## 1. 项目概览

DancingBall 是一个音乐节奏游戏的关卡编辑器，用于在 Unity 编辑器和 Play Mode 下对地图（Map）、道路（Road）、方块（Block）进行可视化的编辑和调试。

**核心能力：**
- 在 SceneView 中选择和导航 Road / Block
- 查看和编辑 Block 的位移规则（DisplacementData）
- 对 Road 的 CRUD 操作
- 实时位移数据的可视化调试（Overlay）
- 亮色/暗色主题切换

---

## 2. 运行手册

### 2.1 场景准备

使用编辑器前，场景中必须存在以下对象：

| 组件 | 说明 | 查找方式 |
|------|------|---------|
| `IMap`（推荐 `ClassicMap`） | 地图根对象，挂载在场景中的某个 GameObject 上 | `EditorCenter.TryAutoBind()` 通过 `Resources.FindObjectsOfTypeAll` 自动查找 |
| `BallPlayer`（可选） | 运行时玩家，用于 Displacement Debug 可视化 | `GameObject.FindObjectOfType<BallPlayer>()` 自动查找 |

如果场景中有多个 `IMap`，EditorCenter 会绑定到第一个找到的。

建议将 `ClassicMap` 挂载在场景的根级 GameObject 上。

### 2.2 Editor 模式启动

**方式一：Inspector 面板**
- 菜单栏 → `MusicTogether/DancingBall/Inspector`
- 打开独立的 EditorWindow，包含 Map / Road / Block 三级编辑面板

**方式二：SceneView Overlay（自动显示）**
- `Block Editor` Overlay — 显示在 SceneView 左上角，提供 Block 选择、导航和位移编辑
- `Displacement Debug` Overlay — 显示在 SceneView 左上角，提供实时位移数据可视化

如果 Overlay 没有出现，在 SceneView 右上角 `Overlay` 菜单中手动启用。

**方式三：设置窗口**
- 菜单栏 → `MusicTogether/DancingBall/Settings`
- 配置快捷键

### 2.3 Play Mode 启动

进入 Play Mode 后，`[AutoService]` 系统会自动按以下顺序初始化：

```
UIDrawManagerService (创建 UI 面板系统)
    → EditorCenter (自动绑定场景中的 IMap + BallPlayer)
        → PlayModeEditorHost (创建 Inspector / Selection / Displacement 三个窗口)
```

三个窗口以可拖拽面板的形式出现在 Game View 上。如果未出现，检查场景是否包含 `IMap` 组件。

### 2.4 基本操作

| 操作 | 方法 |
|------|------|
| 选择 Block | 在 SceneView 中点击 Block 物体 |
| 前后导航 Block | `←` / `→` 键 |
| 跳转到指定 Block | Selection 面板 → 输入 Road Index / Block Index → 点击 Go |
| 编辑 Block 位移 | 选择 Block → 在位移网格中点击 TurnType / DisplacementType |
| 创建位移数据 | Inspector 面板 → Block 标签 → 选择类型 → 点击创建 |
| 删除位移数据 | Inspector 面板 → Block 标签 → 点击删除 |
| 批量应用位移 | Inspector 面板 → 在位移列表中选择多个 Block → 点击批量应用 |
| 创建 Road | Inspector 面板 → Map 标签 → 点击新建 |
| 删除 Road | Inspector 面板 → Map 标签 → 选择 Road → 点击删除 |
| 复制 Road | Inspector 面板 → Map 标签 → 选择 Road → 点击复制 |

### 2.5 主题切换

主题由 `UISettings` 中的 `useDarkTheme` 控制。切换后重新打开窗口生效。

---

## 3. 项目结构

```
Assets/MusicTogether/DancingBall/
├── Data/                          # 数据模型层
│   ├── Interfaces.cs              # IBlockDisplacementData 接口
│   ├── ClassicBlockDisplacementData.cs  # 位移数据实现
│   ├── SceneData.cs               # 场景数据 ScriptableObject
│   └── SceneDataJsonArchive.cs    # JSON 序列化 Archive
│
├── EditorTool/                    # 编辑器工具系统
│   ├── EditorCenter.cs            # 中央枢纽（服务）
│   ├── PlayModeEditorHost.cs      # Play Mode 窗口宿主（服务）
│   ├── EditorConfig.cs            # 编辑器配置
│   ├── EditorShortcutConfig.cs    # 快捷键配置
│   ├── BlockDisplacementDataType.cs   # 位移数据类型枚举
│   ├── Interfaces.cs              # 编辑器的内部接口
│   ├── ARCHITECTURE.zh.md         # 三层架构设计文档
│   ├── DisplacementDataExtension.md   # 位移数据扩展指南
│   │
│   ├── Controller/                # 控制器层（纯 C#，不可引用 Editor API）
│   │   ├── IEditorViewController.cs   # 控制器接口
│   │   ├── IShortcutReceiver.cs       # 快捷键接收接口
│   │   ├── InspectorViewController.cs # Inspector 控制器
│   │   ├── SelectionViewController.cs # Selection 控制器
│   │   └── DisplacementViewController.cs # Displacement Overlay 控制器
│   │
│   ├── Editor/                    # Editor 模式宿主（可引用 Editor API）
│   │   ├── PanelWindow.cs         # 通用 EditorWindow 基类
│   │   ├── InspectorWindow.cs     # Inspector 窗口入口
│   │   ├── SelectionOverlay.cs    # SceneView Selection Overlay
│   │   ├── DisplacementOverlay.cs # SceneView Displacement Overlay
│   │   ├── SettingsWindow.cs      # 设置窗口
│   │   └── RoadCreateWindow.cs    # 创建道路对话框
│   │
│   └── UIManager/                 # UI 管理器层
│       ├── UIManagerBase.cs       # 基类（自动主题）
│       ├── IBlockDisplacementUIManager.cs    # 位移 UI 接口
│       ├── BlockDisplacementUIFactory.cs     # 位移 UI 工厂
│       ├── ClassicBlockDisplacementUIManager.cs  # Classic 位移面板
│       ├── InspectorWindowManager.cs
│       ├── SelectionWindowManager.cs
│       ├── DisplacementOverlayManager.cs
│       ├── SettingsWindowManager.cs
│       └── RoadCreateWindowManager.cs
│
├── Player/                        # 运行时玩家
│   ├── BallPlayer.cs              # 玩家移动引擎
│   └── MovementData.cs            # 移动数据结构
│
├── Scene/                         # 场景运行时实现
│   ├── Interfaces.cs              # IMap / IRoad / IBlock / ITileHolder / IBlockDebug
│   ├── ClassicMap.cs
│   ├── ClassicRoad.cs
│   ├── ClassicBlock.cs
│   └── ...
│
├── UI/                            # UI Toolkit 资源
│   ├── DancingBallEditor_Common.uss      # 亮色主题（含 CSS 变量）
│   ├── DancingBallEditor_Dark.uss        # 暗色主题覆盖
│   ├── NewStyle.uss                      # 新暗色主题（替代中）
│   ├── InspectorWindow.uxml
│   ├── SelectionWindow.uxml
│   ├── DisplacementOverlay.uxml
│   ├── SettingsWindow.uxml
│   ├── RoadCreateWindow.uxml
│   ├── InspectorWindow/
│   │   ├── MapEditorPanel.uxml
│   │   ├── RoadEditorPanel.uxml
│   │   └── BlockEditorPanel.uxml
│   └── BlockDisplacementData/
│       ├── Classic.uxml
│       └── DisplacementData_Common.uss
│
└── README.md                      # 本文档
```

---

## 4. 架构设计

### 4.1 三层架构

编辑器工具遵循三层架构，参见 [ARCHITECTURE.zh.md](EditorTool/ARCHITECTURE.zh.md)：

```
Host (负责创建窗口、提供 VisualElement)
 │
 ├── Play Mode:  PlayModeEditorHost   [AutoService]
 ├── Editor Mode: PanelWindow / Overlay [MenuItem / Overlay 属性]
 │
 ├── Controller (纯 C# 桥接层，不引用 Editor API)
 │     ├── 构造函数注入 EditorCenter
 │     ├── 创建 UIManager
 │     ├── 订阅 EditorCenter 事件
 │     └── 连接 UIManager 的 Action 到 EditorCenter 方法
 │
 └── UIManager (UI 绑定层)
       ├── 继承 UIManagerBase (自动应用主题)
       ├── 绑定 UXML 元素
       └── 暴露 Action / 事件供 Controller 连接
```

**关键约束：** Controller 代码位于 `EditorTool/Controller/`（不在 Editor 文件夹下），编译到 Assembly-CSharp，不得引用 `UnityEditor` 命名空间。

### 4.2 EditorCenter — 中央枢纽

`EditorCenter` 是一个 `[AutoService]` 的服务，作为编辑器工具的中央枢纽：

- 持有当前选中的 Map / Road / Block / DisplacementData
- 提供导航方法（`PreviousBlock` / `NextBlock` / `JumpTo`）
- 提供 CRUD 方法（`CreateRoad` / `DeleteSelectedRoad` / `CreateBlockDisplacementDataForSelected`）
- 通过 C# 事件发布状态变化（`OnBlockSelectionChanged` / `OnRoadSelectionChanged`）

Controller 通过构造函数注入获得 EditorCenter 引用，**不要自己通过 ServiceLocator 获取**。

### 4.3 双模式运行

| 方面 | Play Mode | Editor Mode |
|------|-----------|-------------|
| 服务定位器 | `RuntimeLocator` | `EditorLocator` |
| 窗口宿主 | `UIDrawManagerService` 面板 | `EditorWindow` / `Overlay` |
| Controller 代码 | 完全一致 | 完全一致 |
| Editor 特有功能 | 不可用 | 完整支持 |
| UXML 加载 | `AssetDatabase` (Editor 内) | `AssetDatabase` |

### 4.4 数据模型

```
SceneData (ScriptableObject)
 ├── SegmentList
 └── roadDataList (List<RoadData>)
       ├── roadName (唯一标识)
       ├── targetSegmentIndex
       ├── noteBeginIndex / noteEndIndex
       ├── loaclPosition / loaclRotation / localScale
       └── blockDisplacementDataList (List<IBlockDisplacementData>)
             └── ClassicBlockDisplacementData (具体实现)
                   ├── BlockIndex_Local
                   ├── TurnType (None / Left / Right / Jump)
                   └── DisplacementType (None / Up / Down / ForwardUp / ForwardDown)
```

---

## 5. 扩展指南

### 5.1 新增位移数据类型

这是最常见的扩展场景。详细步骤见 [DisplacementDataExtension.md](EditorTool/DisplacementDataExtension.md)。

**核心步骤：**
1. 实现 `IBlockDisplacementData`（必须提供 `(int blockLocalIndex)` 构造函数）
2. 创建 UXML 模板（放在 `UI/BlockDisplacementData/`）
3. 创建 UIManager，实现 `IBlockDisplacementUIManager`
4. 在 `BlockDisplacementUIFactory` 中注册

**不需要改动的文件：**
- 所有 Controller
- 所有 UXML 宿主文件
- EditorCenter

### 5.2 新增位移数据类型后序列化支持

在 `SceneDataJsonArchive.cs` 中找到 `BlockDisplacementArchive` 类：
- 添加对应的 Archive DTO 类
- 在 `FromBlockList` / `ToBlockList` 中添加新分支

### 5.3 新增编辑器窗口

以 InspectorWindow 为模板：

1. **创建 UXML** — 放在 `UI/` 下
2. **创建 UIManager** — 继承 `UIManagerBase`，绑定 UXML 元素
3. **创建 Controller** — 实现 `IEditorViewController`
4. **创建 Editor 宿主** — 使用 `PanelWindow` 或 `Overlay` 属性
5. **注册 Play Mode 宿主** — 在 `PlayModeEditorHost` 中添加窗口创建代码

### 5.4 新增 EditorCenter 操作

1. 在 `EditorCenter` 中添加操作方法（调用 SceneData / RoadData 的 API）
2. 方法末尾调用 `RefreshSelection()` 触发事件通知
3. 在 Controller 中连接 UIManager 的 Action 到新方法

### 5.5 如何选择宿主模式

| 场景 | 推荐宿主 |
|------|---------|
| 停靠面板 | `PanelWindow`（EditorWindow） |
| SceneView 内嵌 | `Overlay` |
| 弹出对话框 | `EditorWindow`（如 RoadCreateWindow） |
| Play Mode 面板 | 加到 `PlayModeEditorHost` 的窗口列表 |

---

## 6. 主题与配置

### 6.1 主题系统

主题通过 USS 变量实现。`UIManagerBase` 在构造时自动应用主题：

| 文件 | 说明 |
|------|------|
| `DancingBallEditor_Common.uss` | 所有主题共用的变量定义 + 亮色默认值 |
| `DancingBallEditor_Dark.uss` | 暗色主题覆盖（仅修改变量值和个别元素） |

主题切换由 `UISettings.useDarkTheme` 控制。

**所有自定义 UI 组件必须继承 `UIManagerBase`**，否则不会自动获得主题样式。

如果新增的 UXML 引用了 `.uss` 样式文件，推荐在 UXML 内使用 `<Style>` 标签引用，这样即使脱离 UIManagerBase 也能正确显示。

### 6.2 配置系统

| 配置 | 类型 | 说明 |
|------|------|------|
| `EditorConfig` | `SingletonScriptableObject` | 调试颜色配置 |
| `EditorShortcutConfig` | `SingletonScriptableObject` | 快捷键映射 |
| `UISettings` | `SingletonScriptableObject` | 主题样式表引用 + 暗色开关 |

配置资源在 `Resources/Data/DancingBall/` 路径下，通过 `SingletonScriptableObject<T>` 加载。

快捷键通过 `SettingsWindow`（菜单 → `MusicTogether/DancingBall/Settings`）可视化编辑。

---

## 7. 文件索引

### 入门必读

| 文档 | 说明 |
|------|------|
| [ARCHITECTURE.zh.md](EditorTool/ARCHITECTURE.zh.md) | 三层架构详细设计 |
| [DisplacementDataExtension.md](EditorTool/DisplacementDataExtension.md) | 位移数据扩展指南 |

### 关键代码文件

| 文件 | 说明 |
|------|------|
| `EditorTool/EditorCenter.cs` | 中央枢纽，所有编辑操作入口 |
| `EditorTool/PlayModeEditorHost.cs` | Play Mode 窗口宿主 |
| `EditorTool/Controller/IEditorViewController.cs` | 控制器接口 |
| `EditorTool/UIManager/UIManagerBase.cs` | UI 管理器基类 |
| `Data/Interfaces.cs` | `IBlockDisplacementData` 接口 |
| `Data/ClassicBlockDisplacementData.cs` | 位移数据实现参考 |
| `Scene/Interfaces.cs` | `IMap` / `IRoad` / `IBlock` 接口 |
| `Player/BallPlayer.cs` | 运行时玩家（位移数据消费者） |
