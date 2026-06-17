# M8：角色一致性修复计划

这一步的目标不是继续小修，而是把资源体系从"零散图片"升级成"统一角色资产 + 分阶段动作 + 道具层"。

---

## M8 目标

解决当前两个核心问题：

- **角色不一致**
  - 衣服颜色不同
  - 人物大小不同
  - 发型、头身比、鞋袜、裙摆长度不统一
- **动作僵硬**
  - 投喂像切换图片
  - 缺少动作过程
  - 食物和人物绑死在一张图里，不方便做过渡动画

最终交付一版：**角色风格统一、动作更自然、投喂有过程、道具可独立移动**的资源与工程版本。

---

## M8 分阶段计划

### M8-1：建立角色主设基线

先固定"她到底长什么样"。

需要产出：

| 文件 | 用途 |
|------|------|
| `character_master_idle.png` | 标准正面立绘 |
| `character_master_blink.png` | 闭眼/眨眼基准 |
| `character_master_smile.png` | 开心基准 |
| `character_master_shy.png` | 害羞基准 |
| `character_master_pout.png` | 生气/鼓脸基准 |
| `character_master_reach.png` | 伸手动作基准 |
| `character_master_read.png` | 学习姿态基准 |

同时写入一份：

**`CHARACTER_STYLE_LOCK.md`**

里面固定：

- 画布尺寸：900 × 900
- 脚底对齐线
- 角色高度范围
- 头身比
- 发色
- 裙子主色
- 袜子高度
- 鞋子颜色
- 发饰位置
- **禁止随机换装、换色、换鞋、改头身比**

**这一阶段的验收标准：**

放在同一张总览图里，看起来必须是**同一个角色**，而不是"同主题不同角色"。

---

### M8-2：重做高频动作组

只重做最常看到的动作，不一次性铺太大。

优先重做这 8 组：

| 动作组 | 目标 |
|--------|------|
| `idle` | 稳定待机，统一角色比例 |
| `hover` | 鼠标靠近时自然反应 |
| `pat_head` | 摸头反馈 |
| `face_reaction` | 点脸/害羞/鼓脸 |
| `hand_invite` | 伸手互动 |
| `feed_snack` | 点心投喂 |
| `feed_meal` | 正餐投喂 |
| `study_guard` | 学习陪伴 |

每组不再只有 1 张图，而是改成 **2~5 个阶段图**。

例如：

```
feed_snack/
├─ 000_notice.png
├─ 001_reach.png
├─ 002_take.png
├─ 003_eat.png
└─ 004_happy.png
```

这样程序动画层才能做出真正的"过程"。

---

### M8-3：投喂动作重构

投喂是现在最僵硬的地方，所以单独重做。

**点心投喂流程**

```
User clicks snack
→ prop_snack 从 HUD 飞向角色
→ 角色 notice，看向食物
→ 角色 reach，伸手接近
→ 角色 take，准备吃
→ prop_snack 缩小/消失
→ 角色 eat
→ 角色 happy
→ 回到 idle
```

**正餐投喂流程**

```
User clicks meal
→ prop_meal 出现
→ 角色 notice
→ 角色双手接过
→ 角色低头吃
→ 满足表情
→ 状态更新
→ 回到 idle
```

---

### M8-4：道具层拆分

食物不要再画死在角色图里。

新增目录：

```
assets/props/
├─ snack/
│  ├─ prop_cookie.png
│  ├─ prop_cake.png
│  └─ prop_candy.png
├─ meal/
│  ├─ prop_omurice.png
│  └─ prop_bento.png
├─ drink/
│  └─ prop_tea.png
└─ fx/
   ├─ heart.png
   ├─ sparkle.png
   └─ small_star.png
```

好处：

- 食物可以飞入
- 可以停在角色手边
- 可以缩小消失
- 同一个角色动作可以复用不同食物
- 后续加新食物不用重画人物

---

### M8-5：更新资源 manifest

原来的 `animation-manifest.json` 需要升级。

新增：

- `asset-manifest.m8.json`
- `animation-manifest.m8.json`
- `prop-manifest.m8.json`
- `motion-sequence.m8.json`

其中 `motion-sequence.m8.json` 用来描述动作流程，例如：

```json
{
  "feed_snack": {
    "steps": [
      { "animation": "feed_snack_notice", "durationMs": 500 },
      { "prop": "snack_cookie", "motion": "flyToHand", "durationMs": 700 },
      { "animation": "feed_snack_take", "durationMs": 600 },
      { "animation": "feed_snack_eat", "durationMs": 800 },
      { "animation": "feed_snack_happy", "durationMs": 900 }
    ]
  }
}
```

---

### M8-6：工程层同步

代码里要从"播放某个动画"升级成"播放动作序列"。

新增或修改：

| 文件/类 | 作用 |
|---------|------|
| `MotionSequenceService.cs` | 播放多阶段动作 |
| `PropLayerService.cs` | 管理食物/星星/爱心等道具 |
| `MotionTweenService.cs` | 飞入、缩放、淡出、弹跳 |
| `PetWindow.xaml` | 增加 PropLayer |
| `PetWindow.xaml.cs` | 投喂、学习、互动改为播放 sequence |
| `animation-manifest.json` | 多帧动作 |
| `prop-manifest.json` | 道具资源 |
| `motion-sequence.json` | 动作流程 |

---

## M8 验收标准

### 角色一致性

必须满足：

- 同一套发色
- 同一套裙子颜色
- 同一套鞋袜
- 同一头身比
- 同一画布尺寸
- 同一脚底对齐线
- 同一视觉大小

### 动作自然度

至少满足：

- idle 有呼吸感
- hover 不突兀
- 点击反馈不是硬切
- 投喂至少 4 个阶段
- 食物有飞入/消失过程
- 投喂结束有满足反馈
- 学习状态不再像静态贴图

### 工程完整性

必须满足：

- animations 不缺资源
- props 不缺资源
- manifest 引用全部存在
- WPF 能正常运行
- 原来的 HUD 按钮仍可触发动作
- `validate-assets.ps1` 能检查动画和道具

---

## M8 交付物

最终交付：

| 交付物 | 文件名 |
|--------|--------|
| 新的工程包 | `DesktopPet_M1_WPF_Prototype_v8_ConsistentMotion.zip` |
| 角色风格锁定文档 | `CHARACTER_STYLE_LOCK.md` |
| 动作资源清单 | `M8_ACTION_ASSET_PLAN.md` |
| 道具资源清单 | `PROP_MANIFEST_M8.json` |
| 动作序列配置 | `motion-sequence.m8.json` |
| 资源总览图 | `M8_ASSET_CONTACT_SHEET.png` |
| 实现报告 | `M8_IMPLEMENTATION_REPORT.md` |
