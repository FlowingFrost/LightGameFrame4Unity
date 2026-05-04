# UIDrawer 窗口管理框架

## 1. 定位

Play Mode 下的通用 UI 窗口管理服务。管理窗口的创建、层级、焦点、动画与生命周期。不依赖任何具体业务逻辑。

```
命名空间：LightGameFrame.UIDrawer
基类：    MonoServiceBase<UIDrawManagerService>
注册：    [AutoService(Mode = AutoServiceMode.PlayMode)]
```

## 2. 两种创建方式

调用者决定使用哪种方式，API 签名决定 Behaviour 的有无：

```csharp
var handle = uiManager.Open(visualTreeAsset, options);   // Behaviour == null（纯 UI）
var handle = uiManager.Open(windowPrefab, options);       // Behaviour != null（预制体窗口）
```

| 参数 | Behaviour | 用途 |
|------|-----------|------|
| `VisualTreeAsset` | null | 临时浮层、提示、不需要 chrome 的界面 |
| `GameObject` | 有 | 可拖拽、缩放、带标题栏的窗口 |

两种方式都返回 `WindowHandle`，调用方通过 `handle.HasBehaviour` 判断。

## 3. 核心概念

### WindowHandle

纯 C# 窗口身份标识，不继承 MonoBehaviour。

```csharp
public class WindowHandle
{
    public string Id { get; }
    public WindowState State { get; }        // Opening → Open → Closing → Closed
    public VisualElement RootVisualElement { get; }
    public UIInterfaceBehaviour Behaviour { get; }
    public string ParentId { get; }
    public bool IsTopLevel { get; }           // ParentId 为空
    public bool HasBehaviour { get; }         // Behaviour != null
    public bool IsMinimized { get; }
}
```

### UIInterfaceBehaviour

预制体窗口上的 MonoBehaviour，提供 UXML 实例化和 Transition 引用。**纯 UI 模式不需要。**

```csharp
public class UIInterfaceBehaviour : MonoBehaviour
{
    public VisualTreeAsset embeddedVisualTree;  // 窗口内容 UXML
    public UITransition transition;             // 可选过渡动画配置
    public VisualElement ContentRoot { get; }   // 内容挂载点
    public bool TryCreateEmbeddedRoot(out VisualElement root);
}
```

### WindowChrome

可选组件，挂载在预制体上提供窗口装饰功能。**纯 UI 模式不需要。**

- 拖拽标题栏移动窗口
- 边缘缩放（8 个方向缩放手柄）
- 全屏/半屏切换
- Aero Snap（拖拽到屏幕边缘吸附）
- 边界回弹动画

### UITransition + Modules

ScriptableObject 配置，定义窗口的 4 个动画阶段：

| 阶段 | 触发时机 |
|------|----------|
| Enter | 窗口打开时 |
| Exit | 窗口关闭时 |
| Cover | 被新窗口覆盖时 |
| Uncover | 覆盖的窗口关闭后露出时 |

内置 Module：
- **UIFadeModule** — 透明度渐变动画
- **UIScaleModule** — 缩放动画
- **UISlideModule** — 滑入滑出动画（支持相对/绝对距离）

## 4. 根容器

Play Mode 下，顶层 UI 必须挂在 UIDocument 上才能渲染。服务初始化时自动创建：

```
UIRoot (GameObject)
  ├── UIDocument
  │    └── rootVisualElement
  │         └── UIRootContainer (VisualElement, StretchToParentSize)
  │              ├── WindowHandle A (顶层窗口)
  │              ├── WindowHandle B (顶层窗口)
  │              └── ...
  └── [UIDrawManagerService] (MonoServiceBase)
```

PanelSettings 从 `UIConfig.panelSettings` 读取。

## 5. 生命周期

```
Open(uxml/prefab, options)
  → 创建 WindowHandle (State = Opening)
  → 实例化 UXML / Prefab
  → 挂到 RootContainer 或父窗口
  → 播放 Enter 动画（如有）
  → State = Open
  → 返回 handle

Close(windowId)
  → State = Closing
  → 播放 Exit 动画（如有）
  → 回收 UXML / Destroy(GameObject)
  → State = Closed
  → 触发 Root 排序 & 可交互更新

Focus(windowId)
  → 根家族置顶
  → ApplyRootSorting() → SiblingIndex 排序
  → UpdateRootInteractable() → 被覆盖窗口禁用输入
```

## 6. 层级与焦点

```
RootStack (List<string>) ← Focus 顺序排列

ApplyRootSorting():
  → 按 RootStack 顺序排列子元素的 SiblingIndex
  → Minimized 的窗口跳过

UpdateRootInteractable():
  disableCoveredInput=false → 全部可交互
  focusOnPointerDown=true   → 全部可交互（点击自动 Focus）
  其他                      → 仅顶层窗口可交互
```

## 7. 输入路由

`focusOnPointerDown` 开启时，所有顶层窗口注册 PointerDown → Focus 回调。点击任意窗口自动置顶。

快捷键等业务输入不在 UI 框架层处理，由上层 Controller/IShortcutReceiver 管理。

## 8. 配置：UIConfig

`LightGameFrame.DataManager.UIConfig`（SingletonScriptableObject）统一管理：

| 分组 | 内容 |
|------|------|
| Panel & Prefab | PanelSettings、windowFramePrefab、defaultUITransitionResourcePath |
| WindowElements | UXML 中的元素名（titleBar、toolBar、closeButton 等） |
| WindowButtonText | 按钮文字（close、minimize、restore、fullScreen） |
| ResizeHandles | 缩放手柄的元素名（top、right、bottomLeft 等） |
| FullscreenAnimation | 全屏/半屏切换动画参数 |
| WindowClamp | 拖拽回弹边界动画参数 |
| AeroSnap | 桌面式窗口吸附配置 |

`windowFramePrefab` 字段由你手动填入带 `UIInterfaceBehaviour` + `WindowChrome` 的预制体。

## 9. 与外部系统的关系

```
UIDrawManagerService
  ← 被上层 Controller 消费（提供窗口创建/关闭/聚焦）
  ← 被 WindowChrome 消费（关闭、聚焦、最小化）
  → 依赖 DataManager.UIConfig（PanelSettings、元素名、动画参数）
  → 依赖 UITransition（过渡动画配置）
```

## 10. 文件结构

```
LightGameFrame/
├── DataManager/
│   └── UIConfig.cs               # 全局配置（SingletonScriptableObject）
└── UI/
    ├── ARCHITECTURE.zh.md
    ├── UIDrawManagerService.cs   # [MonoServiceBase] 主服务
    ├── WindowHandle.cs           # 纯 C# 窗口身份 + WindowState 枚举
    ├── UIInterfaceBehaviour.cs   # MonoBehaviour（预制体窗口用）
    ├── OpenWindowOptions.cs      # Open/Close/Minimize/Restore 配置类
    ├── UITransition.cs           # ScriptableObject 过渡动画配置
    ├── WindowChrome.cs           # 窗口装饰（拖拽/缩放/全屏/AeroSnap）
    └── Transitions/
        ├── UIFadeModule.cs       # 透明度渐变
        ├── UIScaleModule.cs      # 缩放
        └── UISlideModule.cs      # 滑入滑出
```
