# DesktopPet 资源包修复报告（v5：全量绿色底重置）

## 本次修复内容

- 已将当前可用的 **全部绿色背景源图** 收集进工程包。
- 已对全部绿色背景源图执行 **chroma key 抠图**，生成透明 PNG。
- 已将 `assets/animations/` 下的动画资源 **全部替换为绿色源图抠图结果或其别名复用结果**。
- 已同步新增 `source_green_manifest.json`，记录 **动画文件 -> 绿色源图** 的对应关系。
- 已保留原始绿色背景图，目录：`src/DesktopPet.App/assets/source_green_full/`。
- 已保留抠图后的透明源图，目录：`src/DesktopPet.App/assets/source_green_keyed/`。

## 检查结论

- 绿色背景源图数量：**16**
- 动画帧映射数量：**29**
- Manifest 缺失资源数：**0**
- 当前结论：**`animations` 目录已由绿色底源图回填重置完成。**

## 动画资源与绿色源图对应表

| 动画文件 | 绿色源图键名 |
|---|---|
| `drag_hold/000.png` | `drag_hold` |
| `drop/000.png` | `idle_soft` |
| `feed_meal/000.png` | `feed_meal` |
| `feed_snack/000.png` | `feed_snack` |
| `hover_backstep/000.png` | `hover_shy` |
| `hover_curious/000.png` | `idle_soft` |
| `hover_shy/000.png` | `hover_shy` |
| `idle/000.png` | `idle_neutral` |
| `idle/001.png` | `idle_soft` |
| `idle/002.png` | `hover_shy` |
| `idle_blink/000.png` | `idle_closed_smile` |
| `idle_cheer/000.png` | `idle_cheer` |
| `idle_yawn/000.png` | `idle_yawn` |
| `outfit_daily/000.png` | `idle_neutral` |
| `part_accessory_proud/000.png` | `accessory_proud` |
| `part_face_blush/000.png` | `idle_soft` |
| `part_face_pout/000.png` | `annoyed_pout` |
| `part_feet_step/000.png` | `hover_shy` |
| `part_hand_highfive/000.png` | `hand_wave` |
| `part_hand_invite/000.png` | `hand_invite` |
| `part_hand_wave/000.png` | `hand_wave` |
| `part_head_pat/000.png` | `idle_closed_smile` |
| `part_outfit_show/000.png` | `outfit_show` |
| `rest_tea/000.png` | `feed_snack` |
| `sleep_lie/000.png` | `sleep_lie` |
| `study_complete/000.png` | `idle_cheer` |
| `study_guard/000.png` | `study_reading` |
| `talking/000.png` | `hand_invite` |
| `tap_annoyed/000.png` | `annoyed_pout` |

## 附带文件

- `GREEN_SOURCE_KEYED_CONTACT_SHEET_v5.png`：全部绿色源图抠图后的总览。
- `ANIMATIONS_FROM_GREEN_CONTACT_SHEET_v5.png`：回填到 animations 后的总览。
- `src/DesktopPet.App/assets/source_green_manifest.json`：源图与目标动画映射 JSON。
