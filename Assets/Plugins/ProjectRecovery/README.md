# ProjectRecovery - 项目编译恢复助手

当项目编译失败时（缺失插件、脚本被误删等），自动弹出恢复助手窗口，向用户提供错误恢复指导。

## 功能

### 自动检测编译错误
通过 `CompilationPipeline.assemblyCompilationFinished` 事件监听，当项目中任何程序集编译出错时自动触发。使用 `EditorApplication.update` 轮询等待编译结束后弹出窗口。

### 菜单入口
`Tools > Project Recovery` 可随时手动打开窗口。

### 分析功能
点击「分析错误信息」按钮后执行三类检测：

1. **缺失插件检测** — 检查关键插件目录是否存在，缺失则给出安装指引
   - DOTween（gitignore 排除，需手动安装）
   - Odin Inspector（Sirenix）

2. **代码完整性检测** — 检查核心代码目录是否存在
   - `Assets/LightGameFrame/` — LightGameFrame 框架
   - `Assets/MusicTogether/` — 主游戏代码

3. **命名空间检查** — 扫描项目所有 .cs 文件的 `using` 指令，收集外部命名空间，检查对应 DLL/目录是否存在
   - `DG.Tweening` → DOTween 目录
   - `Sirenix.*` → Sirenix 目录
   - `TMPro` / `Unity.VisualScripting` → Unity 包管理器管理

### 停止显示
点击「停止显示该窗口」按钮后，本次编辑器会话内不再自动弹窗。

## 文件结构

```
Assets/Plugins/ProjectRecovery/
├── ProjectRecovery.asmdef        # Editor-only 程序集定义
├── README.md
└── Editor/
    ├── RecoveryHelper.cs          # 主逻辑（编译检测 + 分析 + 窗口）
    ├── RecoveryWindow.uxml        # UI 布局
    └── RecoveryWindow.uss         # 样式（脉冲动画、发光动画、淡入过渡）
```

## 已知限制

- 窗口无法真正全屏（Unity `EditorWindow.maximized` 对浮动窗口无效，当前停靠在 SceneView 旁边）
- 作为 .cs 源码时，如果项目存在编译错误，`[InitializeOnLoad]` 不会执行，自动弹窗不生效。需编译为预编译 DLL 才能解决此问题

## 源码备份

`#Backup/ProjectRecovery/` 目录下保存了 .cs 源码、.uxml 和 .uss 的备份，便于后续编译为 DLL 时使用。
