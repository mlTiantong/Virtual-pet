# DesktopPet M1 WPF Prototype

这是“蓝发桌宠”M1 可运行主循环原型。

当前资源已整理为运行时精简集：

- `assets/animation-manifest.json` 是唯一动画入口，默认回退到 `idle_m8`。
- `assets/animations_m8/` 保留主角色 900x900 M8 动作帧。
- `assets/spritesheets/` 保留仍在交互中使用的 4x4 SpriteSheet。
- `assets/previews/` 只保留 HUD 实际引用的 4 张预览图。
- `assets/props/` 只保留投喂序列实际使用的 4 张道具/特效图。
- 旧绿幕源图、contact sheet、原始出图批次、服装/装饰图和未引用预览图已移除。

## 工程栈

- Windows 11
- WPF / C#
- .NET 8
- 本地 JSON 存档
- PNG 动作帧与 SpriteSheet 资源
- 透明无边框桌宠窗口
- `WM_NCHITTEST` 透明区域穿透

## 已实现能力

- WPF 透明无边框桌宠窗口。
- PNG 动作 manifest 加载与轻量帧播放。
- 精简后的 M8 动作资源接入。
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
├─ animations_m8/          # 主角色 M8 动作帧
├─ spritesheets/           # 仍被运行时使用的 4x4 SpriteSheet
├─ previews/               # HUD 预览图
├─ props/                  # 投喂序列道具和特效
└─ dialogue/
```

## 资源检查

运行：

```powershell
.\scripts\validate-assets.ps1
```

脚本会检查动画、预览图、道具、motion sequence 引用是否存在，并把未引用图片视为失败。

## 当前限制

- 当前多数动作仍为单帧原型资源，不是真正的 10-60 FPS 序列帧动画。
- `rest_tea`、拖拽和部分身体交互仍使用保留的 SpriteSheet 资源。
- 服装切换目前只保存状态并触发通用反馈，未保留服装/装饰图片资源。
- WPF 运行需要 Windows + .NET 8 SDK。

## 推荐下一步

1. 运行 `scripts/validate-assets.ps1`。
2. 运行 `scripts/run.ps1` 验证窗口、透明穿透、HUD 和交互。
3. 为 `rest_tea`、`drop`、`part_feet_step` 这类近似资源补正式 M8 动作帧。
4. 将单帧动作替换为真正的 PNG 序列帧。
