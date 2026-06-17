# DesktopPet M1 WPF Prototype

这是“蓝发桌宠”M1 可运行主循环原型。

当前美术资源处于 AI 骨骼实验模式：

- `assets/reference/参考图.png` 是唯一保留的人物基础参考图。
- `scripts/prototype-rig-actions.py` 会从参考图抠绿底，并用轻量分层、pivot 和局部弹性形变生成实验动作帧。
- `assets/runtime/animations/` 保存当前生成的待机、悬停、拖拽、落地、摸头、点击反应、手部互动、学习、聊天、投喂和换装开心等透明 PNG 序列帧。
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
- PNG manifest 加载与实验序列帧播放。
- 当前动作由 AI 骨骼实验脚本生成，覆盖 `idle_m8`、`hover_m8`、`drag_hold`、`drop`、`pat_head_m8`、`face_reaction_m8`、`tap_annoyed`、`hand_invite_m8`、`study_guard_m8`、`talking`、`feed_snack`、`feed_meal`、`rest_tea`、`idle_cheer_m8`。
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
│  └─ animations/
│     ├─ idle_m8/
│     ├─ hover_m8/
│     ├─ drag_hold/
│     ├─ drop/
│     └─ ... generated action folders
└─ dialogue/
```

## 生成实验动作

运行：

```powershell
python .\scripts\prototype-rig-actions.py
```

脚本会重建 `assets/runtime/animations/`、同步写入 `assets/animation-manifest.json`，并输出预览图到：

```text
artifacts\rig-prototype\rig_prototype_contact_sheet.png
artifacts\rig-prototype\rig_diagnostics.png
```

## 资源检查

运行：

```powershell
.\scripts\validate-assets.ps1
```

脚本会检查参考图是否存在、manifest 引用是否完整，并把未引用图片视为失败。

## 当前限制

- 当前实验动作是脚本生成的分层/骨骼近似效果，不是最终逐帧精修美术。
- 投喂道具、预览图、SpriteSheet 和服装/装饰图片均已移除，后续需要重新统一制作。
- WPF 运行需要 Windows + .NET 8 SDK。

## 推荐下一步

1. 运行 `scripts/validate-assets.ps1`。
2. 运行 `scripts/run.ps1` 验证窗口、透明穿透、HUD 和交互。
3. 调整 `scripts/prototype-rig-actions.py` 的分层、锚点和动作参数。
4. 生成更细的真实拆层后，再替换当前近似骨骼帧。
