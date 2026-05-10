# Dancing Ball 动画系统设计文档

> 提取自 DancingBall_O1 项目（老版本），供新版 Dancing Ball 系统对接参考。

---

## 一、核心架构

动画系统以 **Brick（瓦片/Tile）** 为核心单元，动画参数绑定在 Tile 上，由玩家行进事件驱动。

```
关卡加载
  → SceneLookManager 生成 Brick[] 网格（所有砖块构成的路径图）
  → PlayerMovingTowards 控制玩家在砖块间跳跃

玩家踩砖
  → Brick.OnPlayerLand()
    → AnimHighlight()      ← 砖块被踩时的反馈动画
    → AnimBricksAhead(N)   ← 向前预播放 N 个砖的出场动画
    → PerfectTimeManager   ← 判定时机，驱动 Placer 音符标记动画
```

---

## 二、绑定在 Tile 上的动画参数

每个 Brick 通过 `BrickAnimType` 枚举携带自己的动画行为定义：

```csharp
// 8 种出场动画类型
public enum BrickAnimType {
    defaultRotation = 0,    // 旋转出现
    moveUp = 1,             // 从下方升起
    moveDownFromSky = 2,    // 从天而降 + 缩放淡入
    disableAndFbxAnim = 3,  // 禁用默认网格，播放 FBX 动画
    nothing = 4,            // 立即显示（无动画）
    rotateAndScale = 5,     // 旋转 + 缩放
    moveUpAndScale = 6,     // 升起 + 缩放
    scale = 7               // 纯缩放出现
}
```

每个 Brick 实例挂载的动画字段：

| 字段 | 说明 |
|------|------|
| `animShowType` | 出场动画类型 |
| `animHideType` | 离场动画类型 |
| `animHighlightType` | 被踩时的反馈动画（scale / moveDown / none） |
| `scallingCurve` | XZ 面缩放曲线（踩砖节拍反馈） |
| `scallingYCurve` | Y 轴缩放曲线 |
| `scallingCurveOnBeat` | 精确命中节拍时的缩放曲线 |
| `OnShowBrickAnimCompleted` | 出场动画完成回调（Action） |
| `startFactor` | **静态** 瀑布递进系数（见下节） |

Brick 之间通过 `neighbors` 列表形成路径图，`ShowSpawnAnimRecursive(depth)` 沿 `neighbors` 递归传播。

---

## 三、核心设计：动画是否会提前播放？

### 答案：会。

系统通过 **瀑布递进（Cascading Pre-play）** 机制让前方的砖提前播放出场动画。

### 机制详解

```
Player 踩到 Brick #N
  → PlayerMovingTowards.OnPlayerLand()
    → currentBrick.ShowSpawnAnimRecursive(showBricksAheadCounter, isFirstBrick=true)
                                         ↑ 关卡配置值，通常 4~8
```

`ShowSpawnAnimRecursive(depth)` 的实现逻辑：

```
ShowSpawnAnimRecursive(depth=4):
    for each neighbor in neighbors:    // 沿路径图向前探测
        neighbor.ShowSpawnAnimRecursive(3)
            neighbor.ShowSpawnAnimRecursive(2)
                neighbor.ShowSpawnAnimRecursive(1)
                    neighbor.ShowSpawnAnimRecursive(0) → 终止

每一层递归调用 StartSpawnAnimCor(startFactor)：
    #0: startFactor = 0.9  (最近的砖，从 90% 进度开始播)
    #1: startFactor = 0.8  (第 2 个砖，从 80% 开始)
    #2: startFactor = 0.7  (第 3 个砖)
    #3: startFactor = 0.6  (第 4 个砖)
    ...
    直到 startFactor 降为 0，剩余砖保持原生 factor=0
```

### 视觉效果

- **远处砖**（startFactor ≈ 0.0）：从动画初始状态开始，完全不可见或初现
- **中距离砖**（startFactor ≈ 0.3~0.6）：部分完成动画，正在浮现中
- **近处砖**（startFactor ≈ 0.7~0.9）：接近完成动画，几乎完全显示
- **当前砖**（isFirstBrick=true）：startFactor 重置为 0.9，单独处理

这创造了**砖块从远处逐渐浮现向玩家靠拢的瀑布效果**。

### startFactor 的作用

`startFactor` 是一个视觉进度偏移量（非时间偏移）。在 `StartSpawnAnimCor` 的每帧 Update 中：

```csharp
factor += Time.deltaTime * speed;  // factor 从 startFactor 开始增长

switch (animShowType) {
    case rotateAndScale:
        rotation = Quaternion.Lerp(startRot, endRot, factor);
        scale    = Vector3.Lerp(Vector3.zero, Vector3.one, factor * 0.5);
        break;
    // ...其他类型类似，都用 factor 驱动 Lerp
}

// factor 到达 2.0 后结束，触发回调
OnShowBrickAnimCompleted?.Invoke();
```

**关键点：** factor 的增长范围是 `[startFactor, 2.0]`，当 startFactor 已经接近 2.0 时（如 0.8），动画立即跳到接近完成状态。远处砖的 factor 从 0 开始需要跑满全程。

---

## 四、时间队列管理

### 不存在中心化时间线

每个砖的 `StartSpawnAnimCor` 是一个独立的 `IEnumerator` 协程，在 `Update` 中各自推进。没有全局 Sequencer 或 Timeline。

### 队列由三项机制共同维持

| 机制 | 作用 |
|------|------|
| **递归传播** (`ShowSpawnAnimRecursive`) | 以当前砖为根，沿 `neighbors` 图 BFS 递归 N 层，每层启动对应砖的协程 |
| **startFactor 递进** (`0.9 → 0.8 → 0.7 → ...`) | 每深一层递归，factor 减小 0.1，远处砖从更早期的动画状态起步 |
| **完成回调链** (`OnShowBrickAnimCompleted`) | 某些依赖动画完成才能触发的逻辑（如 DiamondOnPath 的出现），通过订阅这个回调等待 |

### 三种不同粒度的触发

```
1. 整关启动时
   PlayerMovingTowards 在起始砖调用 ShowSpawnAnimRecursive(count, isFirstBrick=true)
   → 一次性预热前方 N 个砖

2. 每次踩砖时
   Brick.OnPlayerLand() → AnimBricksAhead(showBricksAheadCounter)
   → 向前推进一步，最新露出来的那个砖从 startFactor 链尾开始播

3. 依赖链
   DiamondOnPath 检查 brick.animShowType:
     如果 type == nothing → 立即显示钻石
     否则 → 订阅 brick.OnShowBrickAnimCompleted → 等动画播完再显示
```

---

## 五、踩砖时的动画管线

玩家踩到砖的完整动画顺序：

```
1. PlayerMovingTowards.OnPlayerLand()
   - 记录 currentPlayerBrick
   - 调用 JumpFx.Show()（跳跃粒子动画）
   - 调用 ShowSpawnAnimRecursive(count) 预热前方砖

2. Brick.OnPlayerLand()
   - 停止并重启 highlight 协程（反馈动画）
   - 调用 AnimBricksAhead(count) 再次预热
   - 设置 PerfectTimeManager 当前砖引用

3. PerfectTimeManager.Update()
   - 每帧检测 player 离当前砖 center 的距离
   - 驱动 PlacerAnim（音符标记的颜色/位置动画）
   - 判定 tap 精度 → 驱动 TapText 动画

4. Brick.OnShowBrickAnimCompleted
   - 钻石/道具检查砖是否完成出场动画
   - 若完成 → 显示钻石/道具
   - 若未完成 → 等待回调
```

---

## 六、给新版系统的建议

### 你的核心问题：动画是否会提前播放？如何管理时序？

旧版答案是：**会提前，用 startFactor 瀑布递进来管理**。但这种方式有局限：

1. **时序不精确** — startFactor 基于递归深度（整数步进），无法精确控制每个砖的动画启动时刻，只能"大致靠前"
2. **与节拍脱节** — 实际关卡是音乐驱动的，但动画只用 `Time.deltaTime` 推进，与 BPM 无关
3. **邻居图耦合** — 依赖 `neighbors` 正确连接，如果路径分叉或循环，递归行为不确定

### 新版可考虑的改进方向

| 方案 | 适用场景 | 复杂度 |
|------|---------|--------|
| **A. 保留 startFactor 递进** | 与旧版行为一致，最小改动 | 低 |
| **B. 时间偏移取代因子偏移** | 每个砖的协程延迟启动（`WaitForSeconds(offset)`）而不是跳帧 | 低 |
| **C. 基于节拍/BPM 的窗口** | 音乐游戏核心需求：前方 N 拍的砖提前播，拍点时刻正好到完全显示 | 中 |
| **D. 中心化 AnimationQueue** | 一个队列管理所有活跃动画，精确控制优先级和时序 | 高 |
| **E. 双状态切换** | 砖有两个状态："预热态"（不完全显示但已可见）和"激活态"（完全显示），切换由节拍事件驱动 | 中 |
| **F. Unity Timeline** | 适合固定序列（GameOver），不适合 runtime 触发 | 仅用于固定场景 |

如果你在新版中已经实现了基于 BPM 的节拍系统，那么 **C 或 E** 可能是最自然的升级方向：让动画的时间窗口直接与节拍位置挂钩，而不是靠 `startFactor` 近似。
