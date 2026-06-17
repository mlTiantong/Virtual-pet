# M8 代码侧集成待办清单

> 发起者：全流程审核  
> 日期：2026-06-12  
> 背景：Round 2 美术资源已全部拷贝至工程并注册到 manifest，但代码侧尚未对接任何新动画 ID 和道具

---

## 问题概述

Round 2 在 `animation-manifest.json` 中注册了 6 个新的多帧动画 ID（`hover_m8`、`pat_head_m8`、`face_reaction_m8`、`idle_m8`、`hand_invite_m8` 3帧版、`study_guard_m8` 3帧版），在 `prop-manifest.m8.json` 中注册了 3 个新道具（`prop_cake`、`prop_candy`、`prop_omurice`）。

**但目前没有任何 C# 代码引用这些新 ID。** 所有交互路径（点击、悬停、喂食、学习、空闲）仍然使用 Round 1 的旧 spritesheet ID。

---

## 修改清单

### 1. PetWindow.xaml.cs — Preload 列表（第 76 行）

当前只预加载旧 spritesheet ID。需要将新 `_m8` ID 加入预加载列表，否则首次播放会有加载延迟。

```csharp
// 当前
_catalog.Preload("idle", "idle_blink", "hover_curious", ...);

// 需要追加
_catalog.Preload(
    "idle", "idle_m8", "idle_blink",
    "hover_curious", "hover_shy", "hover_backstep", "hover_m8",
    "part_head_pat", "pat_head_m8",
    "part_face_pout", "part_face_blush", "face_reaction_m8",
    "part_hand_invite", "hand_invite_m8", "part_hand_highfive",
    "study_guard", "study_guard_m8",
    "tap_annoyed", "drag_hold", "drop",
    "feed_snack", "feed_meal", "rest_tea", "talking"
);
```

### 2. PetWindow.xaml.cs — idle 回退目标（多处）

所有 `PlayAnimation("idle", returnToIdle: false)` 需替换为 `PlayAnimation("idle_m8", returnToIdle: false)`。

涉及行号：

| 行号 | 当前 | 改为 |
|------|------|------|
| 46 | `_currentAnimationId = "idle"` | `_currentAnimationId = "idle_m8"` |
| 106 | `PlayAnimation("idle", returnToIdle: false)` | `PlayAnimation("idle_m8", returnToIdle: false)` |
| 239 | `PlayAnimation("idle", returnToIdle: false)` | `PlayAnimation("idle_m8", returnToIdle: false)` |
| 260 | `PlayAnimation("idle", returnToIdle: false)` | `PlayAnimation("idle_m8", returnToIdle: false)` |
| 465 | `PlayAnimation("idle", returnToIdle: false)` | `PlayAnimation("idle_m8", returnToIdle: false)` |
| 548 | `PlayAnimation("idle", returnToIdle: false)` | `PlayAnimation("idle_m8", returnToIdle: false)` |
| 596 | `PlayAnimation("idle", returnToIdle: false)` | `PlayAnimation("idle_m8", returnToIdle: false)` |

### 3. PetWindow.xaml.cs — study_guard 替换（2 处）

| 行号 | 当前 | 改为 |
|------|------|------|
| 467 | `PlayAnimation("study_guard", returnToIdle: false)` | `PlayAnimation("study_guard_m8", returnToIdle: false)` |
| 522 | `PlayAnimation("study_guard", returnToIdle: false)` | `PlayAnimation("study_guard_m8", returnToIdle: false)` |
| 538 | `PlayAnimation("study_guard", returnToIdle: false)` | `PlayAnimation("study_guard_m8", returnToIdle: false)` |

### 4. DefaultBehaviorScheduler.cs — HandleTap() 替换（第 18-22 行）

```csharp
// 当前
PetHitRegion.Head or PetHitRegion.Hair => new PetAction("part_head_pat", "tap.head"),
PetHitRegion.Face => new PetAction("part_face_pout", "tap.face"),
PetHitRegion.Hand => state.Intimacy >= 35
    ? new PetAction("part_hand_highfive", "tap.hand")
    : new PetAction("part_hand_invite", "tap.hand"),

// 改为
PetHitRegion.Head or PetHitRegion.Hair => new PetAction("pat_head_m8", "tap.head"),
PetHitRegion.Face => new PetAction("face_reaction_m8", "tap.face"),
PetHitRegion.Hand => state.Intimacy >= 35
    ? new PetAction("part_hand_highfive", "tap.hand")  // 此条暂无 M8 替代，保留
    : new PetAction("hand_invite_m8", "tap.hand"),
```

### 5. DefaultBehaviorScheduler.cs — HandleHover() 替换（第 33-34 行）

```csharp
// 当前
if (region is PetHitRegion.Head or PetHitRegion.Face) return new PetAction("hover_shy", "hover.head", BubbleSeconds: 3);
if (region is PetHitRegion.Hand) return new PetAction("part_hand_invite", "hover.hand", BubbleSeconds: 3);

// 改为
if (region is PetHitRegion.Head or PetHitRegion.Face) return new PetAction("hover_m8", "hover.head", BubbleSeconds: 3);
if (region is PetHitRegion.Hand) return new PetAction("hand_invite_m8", "hover.hand", BubbleSeconds: 3);
```

### 6. DefaultBehaviorScheduler.cs — HandleFeed() 扩展（第 38-43 行）

当前只有 `snack`/`meal`/`tea` 三种 foodKind。Round 2 新增了 `prop_cake`、`prop_candy`、`prop_omurice` 三个道具。

需要决定：
- 方案 A：将 cake/candy 作为 snack 的子类，omurice 作为 meal 的子类（不新增 foodKind，只在 motion-sequence 中随机选择道具）
- 方案 B：新增 `cake`/`candy`/`omurice` foodKind，对应新的 UI 按钮

建议采用方案 A（最小改动），在 motion-sequence.m8.json 中为 `feed_snack` 和 `feed_meal` 序列随机引用新道具。

### 7. motion-sequence.m8.json — 可选扩展

如果采用方案 A，可以在 `feed_snack` 序列的 prop 步骤中添加新道具的交替引用：

```json
// 当前 feed_snack 序列中
{ "prop": "prop_cookie", "motion": "flyToMouthOrHand", "durationMs": 520 }

// 可改为随机选择或固定使用新道具
{ "prop": "prop_cake", "motion": "flyToMouthOrHand", "durationMs": 520 }
```

如果采用方案 B，需要新增 `feed_cake`、`feed_candy`、`feed_omurice` 三个序列。

### 8. DefaultPetInteractionStateReducer.cs — ApplyFeed() 扩展（可选）

当前 `ApplyFeed()` 第 53-54 行的数值效果只区分 `meal`/`tea`/`snack`（默认）。如果新增 foodKind，需要在此处添加对应的饱腹/体力/心情数值。

```csharp
// 当前
state.Hunger = Clamp(state.Hunger - (foodKind == "meal" ? 28 : foodKind == "tea" ? 6 : 12));

// 如果新增 foodKind
state.Hunger = Clamp(state.Hunger - (foodKind == "meal" || foodKind == "omurice" ? 28 : foodKind == "tea" ? 6 : 12));
```

---

## 修改优先级

| 优先级 | 修改项 | 影响 |
|--------|--------|------|
| P0 | Preload 列表追加 `_m8` ID | 所有新动画首次播放流畅度 |
| P0 | idle 回退目标替换为 `idle_m8` | 全局动画过渡质量 |
| P0 | HandleTap/HandleHover 替换为 `_m8` ID | 用户交互体验 |
| P0 | study_guard 替换为 `study_guard_m8` | 学习模式体验 |
| P1 | HandleFeed 新道具扩展 | 投喂多样性 |
| P1 | motion-sequence 新道具引用 | 投喂动画丰富度 |
| P2 | StateReducer 新数值平衡 | 游戏性 |

---

## 验证要点

完成修改后，请验证：

1. 点击头部 → 应播放 `pat_head_m8`（3帧摸头动作），而非旧的 `part_head_pat`
2. 点击脸部 → 应播放 `face_reaction_m8`（3帧表情反应），而非旧的 `part_face_pout`
3. 悬停头部 → 应播放 `hover_m8`（3帧悬停反应），而非旧的 `hover_shy`
4. 空闲状态 → 应播放 `idle_m8`（5帧循环呼吸），而非旧的 `idle` spritesheet
5. 开始学习 → 应播放 `study_guard_m8`（3帧学习循环），而非旧的 `study_guard`
6. 喂食时若使用 motion-sequence → 新道具 `prop_cake`/`prop_candy`/`prop_omurice` 应能在屏幕上飞行
