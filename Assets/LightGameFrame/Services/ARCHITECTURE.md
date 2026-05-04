# 服务系统架构设计

## 问题

1. 所有服务必须继承 MonoBehaviour，无法在 Edit Mode 下独立运行
2. 调试工具必须进入 Play Mode 才能工作
3. 服务不能脱离 Unity 生命周期进行单元测试

## 目标

- 通用型服务去 Mono 化，可脱离场景独立运行
- Editor 下纯 C# 调试工具直接工作（不进入 Play Mode）
- 双 Manager：Runtime（Mono） + Editor（纯 C#），服务实例各自独立
- Mono 服务保留，运行时行为不变

---

## 快速开始

### 创建纯 C# 服务（推荐）

继承 `GameServiceBase<T>`，不需要挂载到场景：

```csharp
using LightGameFrame.Services;

[AutoService]
public class MyDataService : GameServiceBase<MyDataService>
{
    public override Type[] ServiceDependencies => new[] { typeof(OtherService) };

    protected override void OnInitialize() { }
    protected override void OnUpdate() { }

    public string GetData() => "hello";
}
```

### 创建 Mono 服务（需要 Gizmos / 协程等）

继承 `MonoServiceBase<T>`，保留 MonoBehaviour：

```csharp
[AutoService]
public class MyDebugDrawService : MonoServiceBase<MyDebugDrawService>
{
    protected override void OnInitialize() { }
    private void OnDrawGizmos() { }
}
```

### 使用服务

```csharp
// 运行时（Play Mode）
var data = RuntimeLocator.GetService<MyDataService>();

// 编辑器（Edit Mode）
var editorData = EditorLocator.GetService<MyEditorService>();

// 懒加载（仅 RuntimeLocator）
var svc = RuntimeLocator.GetOrCreateService<MyDataService>();

// 通过接口
public class Consumer
{
    private IServiceLocator _locator;
    public Consumer(IServiceLocator locator) => _locator = locator;
    public void Run() => _locator.GetService<MyDataService>();
}
```

---

## 架构总览

```
┌────────────────────────────────────────────────────────┐
│                    接口层（不动）                        │
│  IGameService / IUpdateService / IServiceLocator        │
└────────────────────────────────────────────────────────┘
          ▲                           ▲
          │                           │
┌─────────────────┐       ┌──────────────────────┐
│  GameServiceBase │       │  MonoServiceBase      │
│  （纯 C#）       │       │  （MonoBehaviour）     │
│  new T() → 可用  │       │  AddComponent → 可用   │
│  Editor + Play   │       │  Play Mode only        │
└─────────────────┘       └──────────────────────┘
          ▲                           ▲
          │                           │
          └──────────┬───────────────┘
                     │
          ┌──────────────────────────┐
          │    IServiceLocator        │
          │  (GetService, Register)   │
          └──────────────────────────┘
               ▲               ▲
               │               │
    ┌──────────────────┐ ┌─────────────────────┐
    │ RuntimeLocator   │ │ EditorLocator        │
    │ MonoBehaviour    │ │ [InitializeOnLoad]   │
    │ Update() 驱动    │ │ EditorApplication    │
    │                  │ │   .update 驱动       │
    │ 管理 纯C# + Mono │ │ 只管理 纯C#          │
    │ Play Mode 创建   │ │ Edit Mode 创建       │
    └──────────────────┘ └─────────────────────┘
```

---

## 接口层（不变）

```csharp
public interface IGameService
{
    int ServicePriority { get; }
    bool IsInitialized { get; }
    void Initialize();
    void Cleanup();
}

public interface IUpdateService : IGameService
{
    bool UpdateEnabled { get; }
    void OnUpdate(float deltaTime);
}

public interface IServiceLocator
{
    T GetService<T>() where T : class, IGameService;
    void RegisterService(IGameService service);
    void UnregisterService(IGameService service);
}
```

---

## 两层基类

### GameServiceBase<T> — 纯 C#，去 Mono

```csharp
public abstract class GameServiceBase<T> : IUpdateService 
    where T : GameServiceBase<T>, new()
{
    public bool IsInitialized { get; private set; }
    public bool UpdateEnabled { get; set; } = true;
    public virtual int ServicePriority => 100;
    public virtual Type[] ServiceDependencies => Array.Empty<Type>();

    public void Initialize() { if (IsInitialized) return; OnInitialize(); IsInitialized = true; }
    public void OnUpdate(float deltaTime) { if (!IsInitialized || !UpdateEnabled) return; OnUpdate(); }
    public void Cleanup() { if (!IsInitialized) return; OnCleanup(); IsInitialized = false; }

    protected virtual void OnInitialize() { }
    protected virtual void OnUpdate() { }
    protected virtual void OnCleanup() { }
}
```

特点：
- 无任何 Unity 依赖，new T() 即用
- 可在 Edit Mode 和 Play Mode 各自独立实例化
- 可单元测试（mock 或直接 new）

### MonoServiceBase<T> — 保留 MonoBehaviour

```csharp
public abstract class MonoServiceBase<T> : MonoBehaviour, IUpdateService 
    where T : MonoServiceBase<T>
{
    public bool IsInitialized { get; private set; }
    public bool UpdateEnabled { get; set; } = true;
    public virtual int ServicePriority => 100;
    public virtual Type[] ServiceDependencies => Array.Empty<Type>();

    public void Initialize() { if (IsInitialized) return; OnInitialize(); IsInitialized = true; }
    public void OnUpdate(float dt) { if (!IsInitialized || !UpdateEnabled) return; OnUpdate(); }
    public void Cleanup() { if (!IsInitialized) return; OnCleanup(); IsInitialized = false; }

    protected virtual void OnInitialize() { }
    protected virtual void OnUpdate() { }
    protected virtual void OnCleanup() { }

    // 非 [AutoService] 方式挂载到场景的 Mono 服务通过 Awake 自行注册
    protected virtual void Awake() { RuntimeLocator.Register(this); }
    protected virtual void OnDestroy() { if (IsInitialized) RuntimeLocator.Unregister(this); }
}
```

仅在 Play Mode 由 RuntimeLocator 管理。EditorLocator 不处理它。

---

## AutoService 标签

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class AutoServiceAttribute : Attribute
{
    public AutoServiceMode Mode { get; set; } = AutoServiceMode.PlayMode;
    public bool ForceCreate { get; set; } = false;
    public bool? RequireMono { get; set; } = null; // null → 自动推断
}

public enum AutoServiceMode
{
    PlayMode,    // 仅 Play Mode 注册
    EditorOnly,  // 仅 Edit Mode 注册
    Dual,        // 两边都注册
}
```

注册模式与 Locator 的对应：

| Mode | RuntimeLocator (Play) | EditorLocator (Edit) |
|------|----------------------|----------------------|
| PlayMode | 注册 | 跳过 |
| EditorOnly | 跳过 | 注册 |
| Dual | 注册 | 注册 |

类型判断（扫描时）：

```csharp
// RuntimeLocator: 继承 MonoBehaviour → Mono 分支 (AddComponent)，否则 纯C# 分支 (new T())
// EditorLocator: 继承 MonoBehaviour → 直接跳过（Mono在Editor不可实例化），否则 纯C# 分支 (new T())
```

---

## 双 Locator 生命周期

### RuntimeLocator (MonoBehaviour)

```
Awake()
  ├── 扫描程序集 [AutoService] (Mode = PlayMode | Dual)
  │   ├── 纯C# 类型 → new T() → 加入实例列表
  │   └── Mono 类型 → AddComponent → 加入实例列表
  │       (Locator 持有引用，不依赖 Awake 自行注册)
  │
  ├── 拓扑排序: 解析 ServiceDependencies → 检测循环依赖 → 报错终止
  ├── 按序调用 Initialize()（所有 [AutoService] 服务一次性初始化）
  │   └── 排序规则: 依赖拓扑为主，同层无依赖按优先级
  │
  ├── 全部注册到内部 registry
  └── DontDestroyOnLoad

Update()
  ├── 处理 pending 队列（非 [AutoService] 的 Mono 服务通过 Awake 注册）
  │   └── 按优先级排序 → 逐一 Initialize()
  └── 所有已注册服务 → 逐一 OnUpdate(dt)

OnDestroy()         → CleanupAll()
OnApplicationQuit() → CleanupAll()
```

### EditorLocator (纯 C#, [InitializeOnLoad])

```
[InitializeOnLoad] static ctor
  ├── new EditorLocator() + Initialize() 直接执行
  │   ├── 扫描程序集 [AutoService]
  │   │   ├── 过滤 Mode = EditorOnly | Dual 的类型
  │   │   ├── Mono 类型 → 跳过
  │   │   └── 纯C# 类型 → new T()
  │   ├── 拓扑排序: 解析 ServiceDependencies → 检测循环依赖 → 报错终止
  │   ├── 按序 Initialize()（排序规则同 RuntimeLocator）
  │   └── 挂 EditorApplication.update + playModeStateChange
  └── 无需 [DidReloadScripts] 清理——域重载已清除旧订阅

EditorApplication.update 驱动
  ├── 定期调用 IUpdateService.OnUpdate(dt)
  └── dt = EditorApplication.timeSinceStartup 差值

playModeStateChange 事件
  ├── ExitingEditMode → 暂停 Update（_isPaused = true）
  └── EnteredEditMode → 恢复 Update（_isPaused = false）
```

### 生命周期对照

| 事件 | RuntimeLocator | EditorLocator |
|------|---------------|---------------|
| 初始化 | Awake() 触发 | [InitializeOnLoad] 静构触发 |
| 扫描时机 | Awake → 一次性扫描 | Initialize → 一次性扫描 |
| 初始化策略 | 纯C#: 一次性拓扑排序 init；Mono: pending 队列 init | 一次性拓扑排序 init |
| Update 驱动 | MonoBehaviour.Update() | EditorApplication.update |
| deltaTime | Time.deltaTime | timeSinceStartup 差值 |
| 暂停 | 无暂停概念 | playModeStateChange 控制 |
| 销毁 | OnDestroy / OnAppQuit | 随 AppDomain 回收 |
| 重编译 | 域重载 → 全量重建 | [InitializeOnLoad] 静构重建 |
| Domain Reload ON | 静态全清 → 重建 | [InitializeOnLoad] → 重建 |
| Domain Reload OFF | 实例存留 | 实例存留但暂停 |

### 服务实例隔离

```
Edit Mode:      EditorLocator 持有服务实例 A
                  │（不可见、不接续）
Play Mode:      RuntimeLocator 持有服务实例 B（全新）

没有 Editor 实例流入 Play Mode，没有 Play Mode 状态干扰。

---

## 依赖顺序

### 声明方式

服务通过虚属性声明对哪些服务有直接依赖：

```csharp
[AutoService(Mode = AutoServiceMode.Dual)]
public class ConfigService : GameServiceBase<ConfigService> { ... }

[AutoService(Mode = AutoServiceMode.Dual)]
public class AudioService : GameServiceBase<AudioService>
{
    public override Type[] ServiceDependencies => new[] { typeof(ConfigService) };
}
```

### 解析流程

```
扫描 [AutoService]
  → 创建所有实例（纯C# new T(), Mono AddComponent）
  → 构建有向图（从 ServiceDependencies 收集边）
  → 检测循环依赖
      ├── 有环 → Debug.LogError + 列出环节点 → 终止
      └── 无环 → Kahn 拓扑排序
           → 同层无依赖节点按优先级排序
           → 按序 Initialize()
```

### 优先级 vs 依赖

两者共存，依赖优先：

| 维度 | 依赖 | 优先级 |
|------|------|--------|
| 语义 | "必须等 B 初始化完" | "希望比 C 早一点" |
| 违反后果 | 运行时出错 | 无后果 |
| 排序权重 | 主条件 | 次级（同层无序节点间） |

优先级只在无依赖关系的服务之间生效：`A(依赖C, 优先级10) + B(依赖C, 优先级20)` → 排序结果 `C → A → B`。

### 约束

- 纯 C# 服务不应声明对 Mono 服务的依赖（Editor 下 Mono 不存在）
- 非 [AutoService] 的场景预置 Mono 服务不在拓扑排序范围内

---

## 文件结构

```
Services/
├── ARCHITECTURE.md
├── Core/
│   ├── IGameService.cs
│   ├── IServiceLocator.cs
│   ├── AutoServiceAttribute.cs
│   ├── GameServiceBase.cs       # 纯 C# 基类
│   └── MonoServiceBase.cs       # Mono 基类
├── Locator/
│   └── RuntimeLocator.cs        # 运行时 Locator（MonoBehaviour）
├── Editor/
│   └── EditorLocator.cs         # 编辑器 Locator（纯 C#, [InitializeOnLoad]）
├── DebugDrawService.cs
└── DebugDrawServiceExample.cs
```

---

## 迁移状态

已完成的基础设施（框架层已就绪）：
- [x] `GameServiceBase.cs` / `MonoServiceBase.cs` — 双基类
- [x] `IServiceLocator.cs` — Locator 接口
- [x] `RuntimeLocator.cs` — 运行时 Locator
- [x] `EditorLocator.cs` — 编辑器 Locator（`[InitializeOnLoad]` 静构初始化）
- [x] `AutoServiceAttribute` — Mode + RequireMono + ForceCreate

待完成的服务迁移（业务层逐步推进）：
- [ ] `DebugDrawService` → `MonoServiceBase`（建议后续迁移）
- [ ] 逐服务从旧基类迁移到新基类
- [ ] 清理旧文件：`ServiceManager.cs`、`ScriptServiceBase.cs`（确认无引用后删除）
