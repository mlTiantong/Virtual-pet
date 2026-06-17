# DesktopPet M1 WPF Prototype

这是“蓝发桌宠”M1 可运行主循环原型。

当前美术资源处于 AI 骨骼实验模式：

- `assets/reference/参考图.png` 是唯一保留的人物基础参考图。
- `art_sources/actions/drag_hold_source.png` 是当前拖拽保持动作的 AI 姿态源图；生成脚本会自行缩放并按基础参考图校色。
- `scripts/prototype-rig-actions.py` 会从参考图抠绿底，并用上身/下身/侧发轻量分层、pivot 和局部弹性形变生成实验动作帧。
- `assets/runtime/sheets/` 保存当前生成的待机、悬停、拖拽起手、拖拽保持、落地、摸头、点击反应、手部互动、学习、聊天、投喂和换装开心等透明 PNG spritesheet。
- `assets/animation-manifest.json` 由生成脚本同步写入，同时保留 `reference_pose` 指向原始参考图。
- `assets/motion-sequence.m8.json` 和 `assets/prop-manifest.m8.json` 保留为空壳，避免旧投喂道具引用。
- 不统一的动作帧、SpriteSheet、预览图、道具图、服装/装饰图、旧绿幕源图和 contact sheet 均已移除。

## 工程栈

- Windows 11
- WPF / C#
- .NET 8
- 本地 JSON 存档
- PNG 人物参考图与实验序列帧
- 透明无边框桌宠窗口
- `WM_NCHITTEST` 透明区域穿透

## 已实现能力

- WPF 透明无边框桌宠窗口。
- PNG manifest 加载与实验 spritesheet 播放。
- 当前动作由 AI 骨骼实验脚本生成，覆盖 `idle_m8`、`hover_m8`、`drag_start`、`drag_hold`、`drop`、`pat_head_m8`、`face_reaction_m8`、`tap_annoyed`、`hand_invite_m8`、`study_guard_m8`、`talking`、`feed_snack`、`feed_meal`、`rest_tea`、`idle_cheer_m8`。
- 透明像素 `WM_NCHITTEST` 穿透。
- 整窗点击穿透开关，托盘菜单可恢复。
- 鼠标悬停、点击、连续点击升级、拖拽、放下反馈。
- 气泡消息，含普通气泡和学习 pinned 气泡。
- HUD 五类入口：照料、学习、状态、个性化、设置。
- 投喂点心/正餐/热茶，更新状态和本地存档。
- 学习计时：25/45/60 分钟、暂停、继续、取消、完成写入学习记录。
- 本地 JSON：`pet-state.json`、`user-settings.json`、`study-record.json`。

## 运行

在 Windows 11 + .NET 8 SDK 环境：

```powershell
cd DesktopPet_M1_WPF_Prototype_v6_GreenSynced
.\scripts\run.ps1
```

或：

```powershell
dotnet run --project .\src\DesktopPet.App\DesktopPet.App.csproj
```

## 构建

```powershell
.\scripts\build.ps1
```

输出目录：

```text
artifacts\publish\DesktopPet.App
```

## 资源目录

```text
src/DesktopPet.App/assets/
├─ animation-manifest.json
├─ motion-sequence.m8.json
├─ prop-manifest.m8.json
├─ reference/
│  └─ 参考图.png
├─ runtime/
│  └─ sheets/
│     ├─ idle_m8.png
│     ├─ hover_m8.png
│     ├─ drag_start.png
│     ├─ drag_hold.png
│     ├─ drop.png
│     └─ ... generated action sheets
└─ dialogue/

art_sources/
└─ actions/
   └─ drag_hold_source.png
```

## AI 图片处理流程

`src/DesktopPet.App/ai 绘画/` 是临时投喂目录，已在 `.gitignore` 中忽略，不直接作为运行资源提交。新图先放这里，确认可用后再复制到 `art_sources/actions/` 作为正式动作源。

接入一张新动作图时按这个顺序处理：

1. 先只处理一个动作，避免姿态、比例和触发逻辑混在一起。
2. 检查图片是否为透明 PNG、四角 alpha 是否为 0、人物是否全身完整、头发和鞋子是否没有裁切。
3. 和 `src/DesktopPet.App/assets/reference/参考图.png` 对比角色一致性：发型、发色、眼睛、服装、裙摆大小、腿长、头身比例必须接近；如果比例明显不一致，先不要接入。
4. 适合接入后，把图片复制到 `art_sources/actions/<action>_source.png`；不要放进 `src/DesktopPet.App/assets/reference/`，该目录只保留唯一基础参考图。
5. 在 `scripts/prototype-rig-actions.py` 中为该动作添加或复用动作源加载逻辑，让脚本负责缩放、按参考图校色、alpha 加固和 spritesheet 生成。
6. 运行 `python .\scripts\prototype-rig-actions.py`，检查 `artifacts\rig-prototype\*_matched.png` 和 `rig_prototype_contact_sheet.png`。
7. 检查 `rig_quality_report.json`，重点看 `cornerAlphaMax` 是否为 0、`normalizedAlphaLossPctMax` 是否低于阈值。
8. 运行 `.\scripts\validate-assets.ps1` 和 `dotnet build .\DesktopPet.M1.sln -c Release`。
9. 运行项目实测动作切换、透明穿透、背景透色和比例一致性。

经验规则：

- 拖拽、坐下、趴下等大姿态可以用外部动作源图；待机、轻微呼吸、普通 hover 优先继续从基础参考图变形。
- 动作源图本身已经有大幅头发或身体流向时，脚本里的 `hair_wave`、`upper_wave`、`lower_wave` 要收小，否则容易出现拉扯变形。
- 透明桌宠会显示背后的窗口内容，所以生成帧会把主体 alpha 加固为不透明，只保留边缘抗锯齿透明。
- 表情好但腿长、裙摆大小或身体比例不一致的图不能直接接入；宁可重画，也不要让动作之间出现“换人感”。

## 生成实验动作

运行：

```powershell
python .\scripts\prototype-rig-actions.py
```

脚本会重建 `assets/runtime/sheets/`、同步写入 `assets/animation-manifest.json`，并输出预览图和质量报告到：

```text
artifacts\rig-prototype\drag_hold_matched.png
artifacts\rig-prototype\rig_prototype_contact_sheet.png
artifacts\rig-prototype\rig_diagnostics.png
artifacts\rig-prototype\rig_quality_report.json
```

质量报告会检查透明边界和归一化 alpha 覆盖损失；如果拆层形变造成明显透明空洞，生成脚本会失败。

## 资源检查

运行：

```powershell
.\scripts\validate-assets.ps1
```

脚本会检查参考图是否存在、manifest 引用和 spritesheet 尺寸是否完整，并验证代码里的动画 ID 与对话分类都能在资源清单中找到。

## 当前限制

- 当前实验动作是脚本生成的分层/骨骼近似效果，已包含自动侧发层，但不是最终逐帧精修美术。
- 投喂道具、预览图、SpriteSheet 和服装/装饰图片均已移除，后续需要重新统一制作。
- WPF 运行需要 Windows + .NET 8 SDK。

## 推荐下一步

1. 运行 `scripts/validate-assets.ps1`。
2. 运行 `scripts/run.ps1` 验证窗口、透明穿透、HUD 和交互。
3. 调整 `scripts/prototype-rig-actions.py` 的分层、锚点和动作参数。
4. 生成更细的真实拆层后，再替换当前近似骨骼帧。
