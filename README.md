# DesktopPet M1 WPF Prototype - v6 GreenSynced

这是“蓝发桌宠”M1 可运行主循环原型的 **v6 工程同步版**。

本版以 `v5 GreenReset` 资源为基线，完成了工程、资源、manifest、状态映射与文档同步：

- `assets/animations/` 已由绿色背景源图抠图结果回填。
- `assets/source_green_full/` 保留全部绿色背景原图。
- `assets/source_green_keyed/` 保留绿色抠图后的透明源图。
- `assets/source_green_manifest.json` 记录动画帧与绿色源图的对应关系。
- `animation-manifest.json` 与当前动画目录完成一致性检查。
- 代码里的状态到动作映射已与当前资源基线对齐。

## 工程栈

- Windows 11
- WPF / C#
- .NET 8
- 本地 JSON 存档
- PNG 动作帧资源
- 透明无边框桌宠窗口
- `WM_NCHITTEST` 透明区域穿透

## 已实现能力

- WPF 透明无边框桌宠窗口。
- PNG 动作 manifest 加载与轻量帧播放。
- v5 绿色底资源抠图结果接入。
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
├─ source_green_manifest.json
├─ source_green_full/      # 绿色背景原图
├─ source_green_keyed/     # 抠图后的透明源图
├─ animations/             # WPF 原型实际加载的动作帧
└─ dialogue/
```

## 检查文件

包根目录包含：

- `ENGINEERING_SYNC_REPORT_v6.md`
- `RESOURCE_STATE_MAPPING_v6.md`
- `ANIMATIONS_FROM_GREEN_CONTACT_SHEET_v6.png`
- `GREEN_SOURCE_KEYED_CONTACT_SHEET_v6.png`

## 当前限制

- 当前多数动作仍为单帧原型资源，不是真正的 10-60 FPS 序列帧动画。
- `rest_tea` 目前复用甜点/喂食相关绿色源图作为占位；后续建议补一张专用“捧茶”绿色源图。
- 服装切换目前保存状态并触发展示动作，但尚未实现分层 overlay 服装系统。
- 当前环境不是 Windows，无法在这里实际运行 WPF；请在 Windows + .NET 8 SDK 上做最终编译验证。

## 推荐下一步

1. 在 Windows 上运行 `scripts/validate-assets.ps1`。
2. 运行 `scripts/run.ps1` 验证窗口、透明穿透、HUD 和交互。
3. 对 `rest_tea`、`drop`、`part_feet_step` 这类近似资源补专用绿色底动作图。
4. 将单帧动作替换为真正的 PNG 序列帧。
