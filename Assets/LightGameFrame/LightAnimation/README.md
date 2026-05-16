# LightAnimation

轻量级原子动画框架，面向海量动态对象的属性/层级/骨骼动画，由 Unity Timeline 驱动时间。

适用场景：滚动地图每行 5 格、每格独立动画、同时不超过 80 个对象播放、需要倒带和速度控制。

## 架构

```
Timeline (PlayableDirector)
  └── AnimationTrack (TrackAsset)
       └── AnimationMixerBehaviour
            └── AnimationManagerBase.Tick(time)
                 ├── _waitingClips (未播放，BeginTime ASC)
                 ├── _playingClips (正在播放)
                 └── _endedClips   (已结束，EndTime ASC)

正放: waiting → playing → ended    倒放: ended → playing → waiting
Reset 仅在 playing → waiting（倒带退出开始边界）时触发。
```

## 核心类型

### IAnimationClip

```csharp
public interface IAnimationClip
{
    double BeginTime { get; }
    double EndTime { get; }
    double Duration { get; }
    bool IsActive { get; set; }

    void Apply(double progress);    // progress ∈ [0,1]
    void Reset();                   // 恢复原始状态
    void CaptureOriginal();         // 记录当前状态为原始值
}
```

### Clip 类型

| 类型 | 目标 | 说明 |
|------|------|------|
| `PositionClip` | Transform | 位移，per-axis curve，local/world |
| `RotationClip` | Transform | 旋转，per-axis curve，local/world |
| `ScaleClip` | Transform | 缩放，per-axis curve |
| `ColorClip` | MeshRenderer | 颜色渐变，MaterialPropertyBlock |
| `CompositeClip` | 多个 Clip | 组合动画 |
| `UnityAnimationClip` | AnimationClip 资源 | 复杂层级/骨骼，SampleAnimation 逐帧采样 |

### 管理器

| 类型 | 职责 |
|------|------|
| `AnimationManagerBase` | 抽象管理器，维护三态列表 + Tick 状态机，4 个抽象方法供子类覆写边界判定 |
| `AnimationManager` | 纯时间驱动的默认实现 |

### Timeline 适配

| 类型 | 职责 |
|------|------|
| `AnimationTrack` | TrackAsset，绑定 AnimationManagerBase |
| `AnimationMixerBehaviour` | PlayableBehaviour，ProcessFrame 中调用 Manager.Tick(time) |
| `AnimationDriverAsset` | 最小 PlayableAsset，保持 Track 活跃 |

## 边界判定（可覆写）

```csharp
AnimationManagerBase:
  protected abstract bool ShouldWaitingToPlaying(clip, currentTime);
  protected abstract bool ShouldPlayingToEnded(clip, currentTime);
  protected abstract bool ShouldEndedToPlaying(clip, currentTime);
  protected abstract bool ShouldPlayingToWaiting(clip, currentTime);
```

子类示例（触发器动画）：

```csharp
public class DancingBallAnimationManager : AnimationManager
{
    private HashSet<IAnimationClip> _activated = new();
    public void ActivateClip(IAnimationClip clip) => _activated.Add(clip);

    protected override bool ShouldWaitingToPlaying(IAnimationClip clip, double currentTime)
        => base.ShouldWaitingToPlaying(clip, currentTime) && _activated.Contains(clip);
}
```

## 三态列表

| 列表 | 状态 | 排序 |
|------|------|------|
| `_waitingClips` | 未播放 | BeginTime ASC |
| `_playingClips` | 正在播放 | 不排序（Apply 必全遍历） |
| `_endedClips` | 已结束 | EndTime ASC |

Clip 不自动从列表移除——必须保留以支持倒带。只在 `Clear()` 时批量清理。

## 文件结构

```
LightGameFrame/LightAnimation/
  README.md                        ← 本文档
  DESIGN.md                        ← 旧设计文档（已合并）
  AnimationManagerBase.cs          ← 抽象基类（三态列表 + Tick 状态机）
  AnimationManager.cs              ← 纯时间实现
  AnimationClip/
    IAnimationClip.cs              ← 核心接口
    BaseAnimationClip.cs           ← 抽象基类（target + curve + Evaluate）
    PositionClip.cs                ← 位移
    RotationClip.cs                ← 旋转
    ScaleClip.cs                   ← 缩放
    ColorClip.cs                   ← 颜色
    CompositeClip.cs               ← 组合
    UnityAnimationClip.cs          ← Unity AnimationClip 包装
  Timeline/
    AnimationTrack.cs              ← TrackAsset
    AnimationMixerBehaviour.cs     ← PlayableBehaviour
    AnimationDriverAsset.cs        ← PlayableAsset
```

## 用法

```csharp
// 预注册
var manager = GetComponent<AnimationManager>();
manager.LoadClips(new List<IAnimationClip> { clip1, clip2, ... });

// 运行时临时增删
manager.Register(newClip);
manager.Unregister(oldClip);

// 每帧驱动（Timeline 自动调用）
manager.Tick(currentTime);

// 重置
manager.Clear();
```

### Editor 预览

1. 场景中创建 GameObject，挂载 `AnimationManager`
2. Timeline 窗口中创建 `AnimationTrack`，将 AnimationManager 拖入绑定槽
3. 在 AnimationManager Inspector 中配置 Clip（或通过代码 Register）
4. 播放/拖动 Timeline → 动画实时跟随
5. 倒带（向左拖动 playhead）→ 动画自动反向处理
