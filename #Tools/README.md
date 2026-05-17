# Tools

## switch-project-recovery.sh

ProjectRecovery 插件在源码模式和 DLL 模式之间切换。

**DLL 模式**：不受项目编译错误影响，`[InitializeOnLoad]` 始终生效。
**源码模式**：可以修改代码、调试。

### 用法

```bash
bash '#Tools/switch-project-recovery.sh'          # 查看当前模式
bash '#Tools/switch-project-recovery.sh' dll      # 切换到 DLL 模式
bash '#Tools/switch-project-recovery.sh' source   # 切换到源码模式
```

> 路径带 `#`，zsh 需要用引号包裹，或用 `bash` 执行。

### 切换到 DLL 的前提

项目必须先在 Unity 中成功编译过，`Library/ScriptAssemblies/ProjectRecovery.dll` 存在。
