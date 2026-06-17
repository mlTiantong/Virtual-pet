# DesktopPet M1 WPF Prototype

这是“蓝发桌宠”M1 可运行主循环原型。

当前美术资源已重置为参考图模式：

- `assets/reference/character_reference.png` 是唯一保留的人物基础参考图。
- `assets/animation-manifest.json` 只把默认动画映射到这张参考图。
- `assets/motion-sequence.m8.json` 和 `assets/prop-manifest.m8.json` 保留为空壳，避免旧投喂道具引用。
- 不统一的动作帧、SpriteSheet、预览图、道具图、服装/装饰图、旧绿幕源图和 contact sheet 均已移除。

## 工程栈

- Windows 11
- WPF / C#
- .NET 8
- 本地 JSON 存档
- 单张 PNG 人物参考图
- 透明无边框桌宠窗口
- `WM_NCHITTEST` 透明区域穿透

## 已实现能力

- WPF 透明无边框桌宠窗口。
- PNG manifest 加载与基础参考图显示。
- 当前仅保留基础人物参考图，旧动作资源等待重做。
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
│  └─ character_reference.png
└─ dialogue/
```

## 资源检查

运行：

```powershell
.\scripts\validate-assets.ps1
```

脚本会检查当前只保留一张人物参考图；任何额外图片、未引用图片或缺失引用都会视为失败。

## 当前限制

- 当前美术资源只保留基础人物参考图，交互动作会回退到默认静态图。
- 投喂道具、预览图、SpriteSheet 和服装/装饰图片均已移除，后续需要重新统一制作。
- WPF 运行需要 Windows + .NET 8 SDK。

## 推荐下一步

1. 运行 `scripts/validate-assets.ps1`。
2. 运行 `scripts/run.ps1` 验证窗口、透明穿透、HUD 和交互。
3. 基于 `assets/reference/character_reference.png` 重新设计统一规格的动作资源。
4. 动作资源统一后，再恢复 manifest、预览图和道具序列。
