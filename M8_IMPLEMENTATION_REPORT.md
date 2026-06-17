# M8 代码侧实现报告

> 角色：代码侧开发者  
> 日期：2026-06-12  
> 对应计划：`M8_CHARACTER_CONSISTENCY_FIX_PLAN.md`

---

## 一、已完成的代码侧工作

### 1. 动画叠化过渡（Crossfade）

**问题**：原先 `PlayAnimation()` 是纯硬切——立即跳到新动画第 0 帧，无任何过渡。

**实现**（`PetWindow.xaml` + `PetWindow.xaml.cs`）：
- XAML 新增 `<Image x:Name="PetSpriteOverlay">` 作为叠化层
- 动画切换流程：
  1. 停止帧计时器，取消正在进行的叠化
  2. 加载新动画首帧到 Overlay（Opacity=0）
  3. 150ms 内：PetSprite 1→0 淡出，Overlay 0→1 淡入
  4. 完成后：PetSprite.Source 接管新帧，Overlay 隐藏，帧计时器重启
- 连续快速触发时自动取消前一叠化，取最新动作

**文件**：`PetWindow.xaml:98-104`，`PetWindow.xaml.cs:149-200`

### 2. 防重触发

**问题**：当前已在播 idle 时，再调 `PlayAnimation("idle")` 会导致闪回第 0 帧。

**实现**（`PetWindow.xaml.cs:153`）：
- 循环动画 + 相同 ID → 直接 `return`，不做任何操作
- 不影响 one-shot 动画的正常重触发

### 3. MotionSequenceService（动作序列引擎）

**文件**：`Services/MotionSequenceService.cs`

- 读取 `assets/motion-sequence.m8.json` 中的序列定义
- 按步骤顺序播放：动画帧 → 道具运动 → 等待时长 → 下一动画帧 → ...
- 支持中途取消（`Cancel()`）
- 序列结束后自动返回 idle

**接口**：
```csharp
Task PlaySequenceAsync(
    string sequenceId,          // "feed_snack", "feed_meal" 等
    Action<string, bool> playAnimation,  // 回调节点动画
    Func<string, double, double, Task>? showAndMoveProp,  // 道具显隐+移动
    Func<string, double, int>? tweenProp  // 道具动画
);
```

### 4. MotionTweenService（道具动画引擎）

**文件**：`Services/MotionTweenService.cs`

| 方法 | 用途 | 曲线 |
|------|------|------|
| `FlyToAsync(element, from, to, ms)` | 道具飞入（位置） | EaseOut |
| `ScaleToAsync(element, from, to, ms)` | 道具缩放 | Linear |
| `FadeToAsync(element, from, to, ms)` | 道具淡入/淡出 | Linear |
| `BounceAsync(element, amp, ms)` | 弹跳效果 | AutoReverse ×2 |

### 5. PropLayerService（道具层管理）

**文件**：`Services/PropLayerService.cs`

- XAML 新增 `<Canvas x:Name="PropLayer">` 独立道具画布
- `ShowProp(propId, x, y)` — 从 `prop-manifest.m8.json` 加载道具图，显示在指定坐标
- `HideProp(propId)` / `HideAllProps()` — 隐藏/清理道具
- 自动处理缺失图片（生成占位 BitmapSource）

### 6. 数据模型

**文件**：`Models/MotionSequence.cs`

| 类 | 用途 |
|---|------|
| `MotionSequenceManifest` | 序列配置根 |
| `MotionSequenceDef` | 单个序列（含 steps 列表） |
| `MotionStep` | 单个步骤（animation / prop / motion / durationMs） |
| `PropManifest` | 道具定义根 |
| `PropDef` | 单个道具（id / sheet / width / height） |

### 7. 配置文件

| 文件 | 用途 |
|------|------|
| `assets/motion-sequence.m8.json` | 动作序列。当前每个序列仅含单步动画映射，等 M8-2 多阶段资源到位后扩展 |
| `assets/prop-manifest.m8.json` | 道具清单。已定义 9 个道具 ID 占位（snack / meal / drink / fx），实际 PNG 待生成 |

### 8. PetWindow 整合

- 构造函数初始化 `PropLayerService` + `MotionSequenceService`
- `Feed()` 方法优先走 sequence 通道，fallback 到单次 `PlayAnimation`
- `ReloadAssetsButton_Click` 同步重载 manifest
- `PetWindow_Closing` 清理叠化、序列、道具

---

## 二、构建状态

- 构建：**通过**（仅一个预存 DPI 警告）
- 签名：PS 脚本 + 产出 .exe 均已 Authenticode 签名
- 资产验证：`validate-assets.ps1` 通过（含 M8 配置：6 序列 / 9 道具）

---

## 三、主管审核修复（2026-06-12）

代码审查发现 7 项问题，已全部修复：

| # | 严重度 | 问题 | 修复 | 文件 |
|---|--------|------|------|------|
| 1 | 中 | `Cancel()` 后 `PetSprite.Opacity` 残留中间值导致闪烁 | 取消叠化后立即 `Opacity=1` + 隐藏 Overlay | `PetWindow.xaml.cs:173-174` |
| 2 | 中 | 资源重载时旧 `_motionSeq` 未取消，产生僵尸 Task | `new` 前调用 `_motionSeq?.Cancel()` | `PetWindow.xaml.cs:589` |
| 3 | 低 | `ShowProp` 重复调用时旧 Image 未从 Canvas 移除，累积孤儿元素 | 添加前先清理同 ID 旧控件 | `PropLayerService.cs:43-44` |
| 4 | 低 | 占位图每次新建，未存入缓存导致重复分配 | `Freeze()` 后写入 `_propCache` | `PropLayerService.cs:93` |
| 5 | 低 | `ScaleToAsync`/`BounceAsync` 直接替换 `RenderTransform`，丢失已有变换 | 新增 `GetOrAddScaleTransform()` 保留 `TransformGroup` | `MotionTweenService.cs:13-32` |
| 6 | 低 | `FadeToAsync` 对 `Collapsed` 元素不设 `Visible`，动画不可见 | 统一设为 `Visibility.Visible` | `MotionTweenService.cs:80` |
| 7 | 建议 | `validate-assets.ps1` 未覆盖 M8 配置文件 | 追加 `motion-sequence.m8.json` 和 `prop-manifest.m8.json` 验证 | `validate-assets.ps1:55-92` |

---

## 四、缺失项（等待美术侧交付）

以下项目在代码侧已完成对接，但**运行时依赖对应资源文件到位**：

### 缺失 1：M8-2 多阶段角色图

当前状态：27 个精灵表（`spritesheets/*.png`），每表 16 帧，单动画无分阶段。

M8-2 需要产出（以 `feed_snack` 为例）：

```
assets/animations/feed_snack/
├─ 000_notice.png      ← 注意到食物
├─ 001_reach.png       ← 伸手
├─ 002_take.png        ← 接住
├─ 003_eat.png         ← 食用
└─ 004_happy.png       ← 满足
```

以及 `hover`、`pat_head`、`face_reaction`、`hand_invite`、`study_guard` 等其他 7 组的分阶段图。

**代码侧已就绪**：`motion-sequence.m8.json` 的 steps 数组直接支持多动画串联。资源到位后只需更新 JSON：
```json
"feed_snack": {
  "steps": [
    { "animation": "feed_snack_notice", "durationMs": 500 },
    { "animation": "feed_snack_reach",  "durationMs": 600 },
    { "animation": "feed_snack_take",   "durationMs": 600 },
    { "animation": "feed_snack_eat",    "durationMs": 800 },
    { "animation": "feed_snack_happy",  "durationMs": 900 }
  ]
}
```

### 缺失 2：M8-1 角色主设基线图

`CHARACTER_STYLE_LOCK.md` 中定义的 7 张主设基准图：

| 文件 | 大小 | 用途 |
|------|------|------|
| `character_master_idle.png` | 900×900 | 标准正面立绘 |
| `character_master_blink.png` | 900×900 | 闭眼/眨眼 |
| `character_master_smile.png` | 900×900 | 开心表情 |
| `character_master_shy.png` | 900×900 | 害羞表情 |
| `character_master_pout.png` | 900×900 | 生气/鼓脸 |
| `character_master_reach.png` | 900×900 | 伸手动作 |
| `character_master_read.png` | 900×900 | 学习姿态 |

所有图需遵守 `CHARACTER_STYLE_LOCK.md` 中的统一规范（画布、脚底线、头身比、发色、裙色、鞋袜、发饰）。

### 缺失 3：M8-4 道具 PNG

`prop-manifest.m8.json` 已定义 9 个道具 ID，需对应 PNG 文件：

```
assets/props/
├─ snack/
│  ├─ prop_cookie.png    (64×64)
│  ├─ prop_cake.png      (64×64)
│  └─ prop_candy.png     (64×64)
├─ meal/
│  ├─ prop_omurice.png   (80×80)
│  └─ prop_bento.png     (80×80)
├─ drink/
│  └─ prop_tea.png       (48×64)
└─ fx/
   ├─ heart.png          (48×48)
   ├─ sparkle.png        (48×48)
   └─ small_star.png     (32×32)
```

**代码侧已就绪**：`PropLayerService.ShowProp()` → `MotionTweenService.FlyToAsync()` → `HideProp()` 流程已通。

### 缺失 4：M8-5 新版 animation-manifest.json

当前 `animation-manifest.json`（schema 2）是单层精灵表映射。M8 多阶段动画需要升级 manifest：

- 新增 `animation-manifest.m8.json`（schema 3），支持 `type: "sequence"` 或每个阶段独立 `AnimationSpec`
- 或者保持现有结构，每个分阶段动画注册为独立 ID（如 `feed_snack_notice`、`feed_snack_reach`...）

需与美术侧协调确定最终结构。

### 缺失 5：M8-4 动作序列中道具飞行动画对接

`MotionSequenceService.PlaySequenceAsync` 的 `showAndMoveProp` 回调参数已定义，但在 `Feed()` 中尚未传入具体实现（当前传 `null`）。需要等道具图到位后，完善飞行路径：

```csharp
// 伪代码示意（需根据实际道具图和角色坐标调整）
showAndMoveProp: async (propId, _, _) =>
{
    var prop = _propLayer!.ShowProp(propId, hudX, hudY);
    if (prop != null)
    {
        await _tween!.FlyToAsync(prop, hudPoint, handPoint, 700);
        await _tween!.ScaleToAsync(prop, 1, 0.3, 300);
        _propLayer!.HideProp(propId);
    }
}
```

---

## 五、美术侧需求汇总

| 优先级 | 交付物 | 数量 | 格式 |
|--------|--------|------|------|
| P0 | 角色主设基准图 | 7 张 | 900×900 PNG，透明底 |
| P0 | 高频动作分阶段图（8 组） | 约 40 张 | 各 PNG / 精灵表 |
| P1 | 道具 PNG（零食/正餐/茶/特效） | 9 张 | PNG，透明底 |
| P1 | 精灵表生成脚本输出 | 对应 animation 数 | 4×4 精灵表 1024×1024 |
| P2 | `animation-manifest.m8.json` | 1 个 | JSON |
| P2 | `CHARACTER_STYLE_LOCK.md` | 1 个 | Markdown |

**统一规范**（必须在所有图中保持一致）：
- 画布尺寸：900×900
- 脚底对齐线统一
- 发色同色板、裙子主色同色板
- 头身比、鞋袜、发饰不变
- 禁止随机换装、换色、改比例

---

## 六、下一步

1. 美术侧产出 M8-1 主设基准图 → 验证角色一致性
2. 美术侧产出 M8-2 分阶段动作图 → 更新 `motion-sequence.m8.json` 中的 steps
3. 美术侧产出 M8-4 道具图 → 补全 `Feed()` 中的飞行/缩放回调
4. 联调 M8-5 + M8-6：更新 manifest，跑通完整投喂流程
5. 签产出 .exe，打包 `DesktopPet_M1_WPF_Prototype_v8_ConsistentMotion.zip`
