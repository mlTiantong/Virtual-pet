# V8 SpriteSheet + Glass UI 更新说明

## 本版目标

这版按“新版 UI 参考图”的方向修项目代码，而不是只生成概念图。

主要完成：

1. 接入 4x4 SpriteSheet 动画资源包。
2. 将每个动作统一为 16 帧播放。
3. 所有动作资源在生成时强制第 16 帧等于第 1 帧，便于动作闭环。
4. 重写 WPF HUD 为玻璃拟态圆角面板，放在人物右侧，避免遮挡人物。
5. 添加动作预览区：站立 / 拖拽 / 思考 / 挥手。

## 主要改动文件

- `src/DesktopPet.App/PetWindow.xaml`
  - 重做为玻璃拟态 UI。
  - 窗口扩大为 760x540。
  - 人物放左侧，控制面板放右侧。
  - 设置页改成现代化开关行。
  - 底部加入动作预览卡片。

- `src/DesktopPet.App/PetWindow.xaml.cs`
  - 改为从 SpriteSheet 裁切帧播放。
  - 新增 HUD 关闭按钮。
  - 新增动作预览按钮。
  - 调整 HUD 定位，避免遮住人物。

- `src/DesktopPet.App/Services/AnimationCatalog.cs`
  - 新增 SpriteSheet 类型支持。
  - 支持字段：`type`, `sheet`, `columns`, `rows`, `frameCount`, `frameWidth`, `frameHeight`。
  - 使用 `CroppedBitmap` 从 4x4 图集中裁切每一帧。
  - 保留旧 frames 动画兼容逻辑。

- `src/DesktopPet.App/assets/animation-manifest.json`
  - schema 更新为 2。
  - 所有动作改为 `type: spritesheet`。
  - 每个动作统一 4 列 x 4 行，共 16 帧。

- `src/DesktopPet.App/assets/spritesheets/*.png`
  - 新增 27 个动作 SpriteSheet。

- `src/DesktopPet.App/assets/previews/*.png`
  - 新增动作预览缩略图。

- `src/DesktopPet.App/DesktopPet.App.csproj`
  - assets 改为 Content 并复制到输出目录，方便 XAML 预览图和运行时资源加载。

## 注意

生成式美术里部分动作仍可能存在姿态不完美的问题。为了项目可运行稳定，本版运行时 SpriteSheet 使用透明 PNG 资源重新规整，优先保证：

- 透明背景；
- 统一 4x4 / 16 帧；
- 首尾帧一致；
- WPF 可以稳定裁帧播放；
- 不再出现单帧硬切。

当前环境没有 `dotnet` 命令，因此无法在容器内完成实际 WPF 编译验证；已完成 XAML XML 校验、manifest JSON 校验和事件处理器静态检查。
