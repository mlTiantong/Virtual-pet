# DesktopPet M1 v6 工程同步报告

## 同步内容

- 以 v5 GreenReset 资源为基线复制工程。
- 修复 `README.md` 和 PowerShell 脚本中的控制字符问题。
- 修复 `PetWindow.xaml.cs` 中状态文本换行的 C# 字符串问题。
- 将 `animation-manifest.json` 标记为 `v6_green_synced`，并声明 `source_green_manifest.json`。
- 修正 `source_green_manifest.json`：`animation_to_source` 使用 `animations/...` 完整相对路径。
- 新增 `scripts/validate-assets.ps1`，同时验证 animation manifest 与 source_green manifest。
- 新增 `RESOURCE_STATE_MAPPING_v6.md`，说明行为状态、动画 ID 与绿色源图的关系。
- 重新生成绿色源图抠图总览和 animations 回填总览。

## 资源检查结果

- Manifest 动画条目：**27**
- Manifest 引用帧：**29**
- 缺失资源：**0**
- 绿色源图键数量：**16**
- 回填映射数量：**29**

## 当前结论

当前工程中的 `assets/animations/` 已与 `source_green_manifest.json` 同步；manifest 引用资源无缺失。

## 仍建议后续补图的资源

- `rest_tea`：建议生成真正的捧茶/热茶绿色底动作。
- `drop`：建议生成专用的“放下站稳”动作。
- `part_feet_step`：建议生成专用的小跳/脚鞋躲开动作。
- `talking`：建议生成专用说话姿态或口型帧。

## Windows 验证建议

```powershell
.\scripts\validate-assets.ps1
.\scripts\run.ps1
```