# M8 Round 2 美术资源审核报告

> 审核者：全流程审核  
> 日期：2026-06-12  
> 对应资源包：`DesktopPet_M8_ArtPack_Round2`

---

## 一、审核结论

**P0 项目全部通过。** 6 组动画帧（20 张）+ 3 个道具 + 21 张绿幕源图已拷贝至工程目录并注册到 manifest，构建 0 错误，视觉抽检角色一致性优秀。

---

## 二、Round 2 交付物清单

### 已交付并通过审核

| 类别 | 内容 | 帧/张数 | 状态 |
|------|------|---------|------|
| hover 动画 | look → react → settle | 3 帧 | 通过 |
| pat_head 动画 | touch → enjoy → after | 3 帧 | 通过 |
| face_reaction 动画 | surprise → blush → recover | 3 帧 | 通过 |
| hand_invite 动画 | raise → extend → wave | 3 帧 | 通过 |
| study_guard 动画 | read → think → nod | 3 帧 | 通过 |
| idle 动画 | 含新增 breathe_in / breathe_out | 5 帧 | 通过 |
| 道具 | prop_cake / prop_candy / prop_omurice | 3 个 | 通过 |
| 绿幕源图 | source_green_round2/ | 21 张 | 通过 |
| 接触表 | 动画 + 道具 | 2 张 | 通过 |
| 文档 | 交付报告 / 动画映射 / 检查清单 / 审核笔记 | 4 份 | 通过 |

### 视觉审核结果

抽检 hover/001、pat_head/001、face_reaction/000、idle/003 四帧：

- 发型发色：冰蓝/银蓝长发，黑色 X 发夹，左侧蓝色蝴蝶结 — 一致
- 服装：冰蓝色荷叶边连衣裙，白色饰边 — 一致
- 鞋袜：白色过膝袜，浅蓝色鞋 — 一致
- 画布：900×900 透明底 — 一致
- 各帧间表情/姿态差异合理，过渡自然

---

## 三、命名修复说明

审核过程中发现并修复了以下命名/格式问题（已在工程侧处理完毕，美术组无需返工）：

1. Round 2 的 `animation-manifest.round2.json` 使用自定义分组格式，引擎需要 schema 2 的 flat key-value 格式。已由审核侧转换为引擎兼容格式并合入 `animation-manifest.json`。
2. Round 2 的 `prop-manifest.round2.json` 使用 `path` 字段和 `props_m8/` 路径前缀，引擎期望 `sheet` 字段和 `props/` 路径。已由审核侧转换并合入 `prop-manifest.m8.json`。
3. `hand_invite/`、`study_guard/`、`idle/` 三个目录与 Round 1 同名，以追加帧方式合并（Round 1 单帧 → Round 2 多帧），无冲突。

---

## 四、下一步交付清单（Round 3 / P1）

以下资源是 M8 里程碑完全闭合所需的最后一批美术交付：

### P1-1：feed_snack 分阶段重绘（5 帧）

当前 Round 1 的 `feed_snack_01_notice` 到 `feed_snack_05_happy` 五帧均为同一张图复用，缺少差异化姿态。需要重绘为：

| 文件名 | 姿态描述 |
|--------|----------|
| `feed_snack_01_notice/000.png` | 注意到食物，微微抬头/侧目 |
| `feed_snack_02_reach/000.png` | 伸手去拿/够食物 |
| `feed_snack_03_take/000.png` | 接过食物，双手捧住 |
| `feed_snack_04_eat/000.png` | 吃东西，嘴部动作 |
| `feed_snack_05_happy/000.png` | 吃完满足，眯眼/微笑 |

### P1-2：feed_meal 分阶段重绘（5 帧）

同上逻辑，用于正餐投喂场景。

| 文件名 | 姿态描述 |
|--------|----------|
| `feed_meal_01_notice/000.png` | 注意到正餐 |
| `feed_meal_02_reach/000.png` | 伸手 |
| `feed_meal_03_take/000.png` | 接过 |
| `feed_meal_04_eat/000.png` | 进食 |
| `feed_meal_05_happy/000.png` | 满足 |

### P1-3：rest_tea 多阶段动画（3 帧）

喝茶休息动作序列。

| 文件名 | 姿态描述 |
|--------|----------|
| `rest_tea_01_hold/000.png` | 双手捧茶杯 |
| `rest_tea_02_sip/000.png` | 喝茶 |
| `rest_tea_03_relax/000.png` | 放下杯子，放松 |

### P1-4：sleep_lie 多阶段动画（3 帧）

趴着/躺着睡觉。

| 文件名 | 姿态描述 |
|--------|----------|
| `sleep_lie_01_down/000.png` | 趴下/躺下 |
| `sleep_lie_02_sleep/000.png` | 闭眼睡觉 |
| `sleep_lie_03_dream/000.png` | 做梦（可加小气泡/zzZ） |

### P1-5：drag_hold 多阶段动画（3 帧）

被拖拽时的表情/姿态变化。

| 文件名 | 姿态描述 |
|--------|----------|
| `drag_hold_01_surprise/000.png` | 被抓起，惊讶 |
| `drag_hold_02_dangle/000.png` | 悬空晃荡 |
| `drag_hold_03_resign/000.png` | 放弃挣扎 |

### 约束提醒

- 画布 900×900，透明底
- 角色基准参照 `CHARACTER_STYLE_LOCK.md`（冰蓝长发、黑色 X 发夹、左侧蓝色蝴蝶结、冰蓝荷叶边连衣裙、白色过膝袜、浅蓝色鞋）
- 文件命名：`animations_m8/<组名>/000.png`（每帧一个文件，序号递增）
- 同时提供绿幕源图至 `source_green_round3/`
- 道具如涉及新物品，提供 `props_m8/prop_xxx.png` + 对应绿幕版

---

## 五、交付格式参考

```
DesktopPet_M8_ArtPack_Round3/
├── animations_m8/
│   ├── feed_snack_01_notice/000.png
│   ├── feed_snack_02_reach/000.png
│   ├── ... (其余 feed 帧)
│   ├── rest_tea_01_hold/000.png
│   ├── ... (其余 rest_tea 帧)
│   ├── sleep_lie_01_down/000.png
│   ├── ... (其余 sleep_lie 帧)
│   ├── drag_hold_01_surprise/000.png
│   └── ... (其余 drag_hold 帧)
├── source_green_round3/
│   ├── feed_snack_notice.png
│   ├── ... (其余绿幕源图)
├── manifests/
│   ├── animation-manifest.round3.json
│   └── asset-index.round3.json
└── docs/
    └── ROUND3_DELIVERY_REPORT.md
```

预计总帧数：约 21 帧（5 + 5 + 3 + 3 + 3 + 2 备用）。
