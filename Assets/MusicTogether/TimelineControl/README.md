# Timeline Control Track 使用说明

## 概述

在 Timeline 的 clip 中控制另一个 Timeline（PlayableDirector）的播放状态：跳转、快进、快退。

---

## 核心组件

| 文件 | 作用 |
|------|------|
| `TimelineControlTrack` | 自定义轨道 |
| `TimelineControlAsset` | Clip 资产，配置目标 Timeline 和控制参数 |
| `TimelineControlBehaviour` | 运行时逻辑，clip 激活时执行一次控制操作 |
| `MusicTime` | 时间结构体，支持秒数或 BPM+音符 |
| `NoteType` | 音符类型枚举 |

---

## 使用步骤

### 1. 添加 Track
在 Timeline 窗口右键 → **MusicTogether → Timeline Control Track**

### 2. 添加 Clip
右键轨道 → Add Clip

### 3. 配置 Inspector
- **Target Director**: 拖入要控制的 PlayableDirector
- **Control Mode**: 选择 JumpTo / FastForward / FastRewind
- **Start Time**: 跳转目标时间（JumpTo 模式）
- **Delta Time**: 快进/快退增量（FastForward/FastRewind 模式）

---

## 时间配置

每个 MusicTime 字段支持两种模式：

### 秒数模式（useMusicalTime = false）
直接填秒数，如 `3.5` 秒。

### 音乐时间模式（useMusicalTime = true）
- **BPM**: 如 120
- **音符类型**: Quarter / Eighth / Half 等
- 计算公式：`(4 / denominator) × (60 / bpm)`
- 三连音额外乘 2/3

示例：BPM=120, Quarter → 0.5秒; Eighth → 0.25秒

---

## 控制模式说明

### JumpTo
clip 开始播放时，直接跳转到 startTime 指定的时间。

### FastForward
clip 开始播放时，从 clip 当前时间 **快进** deltaTime。
`targetTime = clipTime + deltaTime`

### FastRewind
clip 开始播放时，从 clip 当前时间 **快退** deltaTime。
`targetTime = clipTime - deltaTime`

---

## 示例

```
Timeline A (主时间线):
  ├─ Audio Track
  │   └─ Music Clip (0s - 120s)
  │
  └─ Timeline Control Track
      ├─ Clip1: JumpTo → 目标Timeline 跳到 30s (0s 位置)
      ├─ Clip2: FastForward +2拍 (8s 位置, BPM=120)
      └─ Clip3: FastRewind -1小节 (16s 位置, BPM=120, Whole)
```

---

## 注意事项

1. Clip 只在 **OnBehaviourPlay** 时执行一次操作，不会每帧执行
2. 目标时间会自动 clamp 到 `[0, duration]` 范围
3. Clip 循环播放时会重置执行状态（OnBehaviourPause 重置标志）
4. Target Director 为 null 时静默跳过
