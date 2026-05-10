# DancingBall 动画系统设计

## 一、核心架构

中心化动画队列（AnimationQueue），以绝对时间驱动所有动画。

```
currentTime → AnimationQueue.Tick(time)
                → 遍历注册的片段
                → 筛选 beginTime <= time <= endTime
                → progress = (time - beginTime) / duration
                → clip.Apply(progress)
```

队列只认时间，不关心触发源。可以从 MonoBehaviour.Update 调用，也可以挂 Timeline。

## 二、数据流水线

与 MovementData 走同一套模式：

```
编辑器/脚本数据                      烘焙                           运行时
══════════════════          ════════════════════          ══════════════════

IBlockAnimationScript  ──bake──→ AnimationClipData ──消费──→ AnimationQueue
(动画类型/曲线/参数)            (beginTime/endTime/target)    (Tick 驱动)

IBlockDisplacementData ──bake──→ MovementData     ──消费──→ BallPlayer
(TurnType/Displacement)        (时间+位置+朝向)              (插值移动)
```

两条线对称。脚本描述了"做什么动画"，烘焙产出"何时对谁做什么"，运行时只管插值。

## 三、核心类型

### AnimationQueue（中心）

```csharp
public class AnimationQueue
{
    List<IAnimationClip> clips;

    void Register(IAnimationClip clip);
    void Unregister(IAnimationClip clip);
    void Tick(double currentTime);          // 每帧调用，驱动所有活跃片段
    void Clear();                           // 关卡卸载时清空
}
```

单例或挂在场景根节点上。不依赖 Unity Timeline，但可被 Timeline Track 驱动（Track 每帧调 Tick）。

### IAnimationClip（单个片段）

一个不可变的数据结构，烘焙时产生，运行时只读：

```csharp
public interface IAnimationClip
{
    double BeginTime { get; }
    double EndTime { get; }
    double Duration { get; }
    bool IsActive { get; }                  // time 在区间内
    void Apply(double progress);            // progress ∈ [0, 1]
}
```

具体实现：

| Clip 类型 | 目标 | 作用 |
|-----------|------|------|
| PositionClip | Transform | 位移（支持 local/world） |
| RotationClip | Transform | 旋转 |
| ScaleClip | Transform | 缩放 |
| ColorClip | Material/MeshRenderer | 颜色渐变 |
| CompositeClip | 多个 IAnimationClip | 组合动画（如旋转+缩放同时） |

### IBlockAnimationScript（脚本数据）

挂在 Block 上的动画描述，存储在 RoadData 中，与 IBlockDisplacementData 平级：

```csharp
public interface IBlockAnimationScript
{
    int BlockIndex_Local { get; }

    /// 烘焙：根据 Block 的时间信息，产出可注册到 AnimationQueue 的 Clip 列表
    List<IAnimationClip> Bake(double blockBeginTime, double blockDuration,
                              List<Transform> tileTransforms);
}
```

## 四、存储位置

```
SceneData (ScriptableObject)
 ├── SegmentList
 └── roadDataList (List<RoadData>)
       ├── blockDisplacementDataList   ← 已有
       └── blockAnimationScriptList    ← 新增
```

Block 仍是纯场景对象，脚本数据在 ScriptableObject 的 RoadData 中。Block 只提供 TileHolder 的 Transform 引用，烘焙时传入。

## 五、一个 Block 在时间轴上的动画

```
time →
  ├── [出场动画] ──┤                    ├─ [踩踏动画] ┤
  ← beginTime -      beginTime →        ← tapTime     →
     offset
  └──────────────── MovementData ──────────────────────┘
```

- 出场动画的 endTime = block 的 beginTime（玩家到达时正好播完）
- 出场动画的 beginTime = endTime - offset（offset 由脚本配置）
- 踩踏动画的 beginTime = tap 时间点

## 六、烘焙流程

类似 ClassicRoad.GenerateBlockMovementData()，新增 GenerateBlockAnimationData()：

```
foreach block in Road:
    blockTime = Segment.GetNoteTime(noteBeginIndex + block.BlockLocalIndex)
    if RoadData.GetAnimationScript(block.BlockLocalIndex, out script):
        clips = script.Bake(blockTime, singleBlockDuration, tileTransforms)
        queue.RegisterAll(clips)
```

烘焙在编辑器或关卡加载时执行，运行时只消费 Clip 数据。

## 七、与 Vanilla 8 种 BrickAnimType 的映射

Vanilla 的出场动画类型本质上都是 Transform 组合，在中心队列中拆成多个 Clip：

| Vanilla 类型 | 中心队列 Clip 组合 |
|-------------|-------------------|
| rotateAndScale (5) | RotationClip + ScaleClip |
| moveUpAndScale (6) | PositionClip(↓→↑) + ScaleClip |
| scale (7) | ScaleClip |
| moveUp (1) | PositionClip(↓→↑) |
| moveDownFromSky (2) | PositionClip(↓从天降) + ScaleClip |
| defaultRotation (0) | RotationClip |
| nothing (4) | 无 Clip |
| disableAndFbxAnim (3) | 禁用模型 + FBX 动画（需额外支持） |

Vanilla 的 startFactor 瀑布递进在此方案中等价为：每个 Block 的出场动画 beginTime 比前一个 Block 提前 offset * depth，由烘焙时计算而非运行时递归。

## 八、Timeline 兼容

AnimationQueue 暴露 Tick(double time)，Timeline 侧只需一个 PlayableAsset 绑定队列引用：

```csharp
// Timeline 侧（轻量适配层）
public class AnimationQueuePlayable : PlayableAsset
{
    AnimationQueue queue;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        // 每帧把 Timeline 当前时间传给 queue.Tick(time)
    }
}
```

独立运行时：直接从 MonoBehaviour.Update 调用 `queue.Tick(levelTime)`。
Timeline 模式：Timeline 驱动 time，queue 被动消费。
队列本身不感知差异。

## 九、文件清单（计划）

```
SceneElements/
 ├── AnimationDesign.md              ← 本文档
 ├── Interfaces.cs                   ← 已有 IAnimationData，将扩展 IAnimationClip / IBlockAnimationScript
 ├── AnimationQueue.cs               ← 中心队列
 ├── AnimationClip/
 │   ├── PositionClip.cs
 │   ├── RotationClip.cs
 │   ├── ScaleClip.cs
 │   ├── ColorClip.cs
 │   └── CompositeClip.cs
 ├── Scripts/
 │   └── ClassicBlockAnimationScript.cs  ← 对应 Vanilla 8 种类型
 ├── SceneAnimationPlayer.cs         ← 从空壳升级：持有 AnimationQueue，负责 Tick
 └── SceneEventPlayer.cs             ← 后续实现
```
