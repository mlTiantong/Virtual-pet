# 资源与状态映射表（v6 GreenSynced）

本表用于说明 WPF 原型中的行为状态会播放哪些动画资源，以及这些资源在当前 v6 绿色底基线中的可用程度。

| 状态/事件 | 动画ID | 用途 | 当前状态 |
|---|---|---|---|
| `startup` | `idle` | 启动后默认站姿 | 正式可用 |
| `idle_loop` | `idle / idle_blink / idle_cheer / idle_yawn` | 普通待机、眨眼、开心、低体力 | 正式可用 |
| `hover_default` | `hover_curious` | 普通悬停好奇反馈 | 正式可用 |
| `hover_head_face` | `hover_shy` | 头/脸悬停害羞反馈 | 正式可用 |
| `hover_low_energy` | `idle_yawn` | 低体力悬停反馈 | 正式可用 |
| `tap_head` | `part_head_pat` | 摸头反馈 | 可用；后续可补专用摸头图 |
| `tap_face` | `part_face_pout / part_face_blush` | 脸部点击、害羞/鼓脸 | 正式可用 |
| `tap_hand_low_intimacy` | `part_hand_invite` | 手部邀请 | 正式可用 |
| `tap_hand_high_intimacy` | `part_hand_highfive` | 击掌/挥手 | 正式可用 |
| `tap_body_outfit` | `part_outfit_show` | 衣装展示 | 正式可用 |
| `tap_accessory` | `part_accessory_proud` | 配饰展示 | 正式可用 |
| `tap_feet` | `part_feet_step` | 脚/鞋边界反馈 | 可用；后续建议补专用小跳躲开 |
| `repeat_tap` | `tap_annoyed` | 连续点击不满/鼓脸 | 正式可用 |
| `drag_start_move` | `drag_hold` | 拖拽中 | 正式可用 |
| `drag_drop` | `drop` | 放下/回到普通站姿 | 可用；当前为站稳占位 |
| `feed_snack` | `feed_snack` | 点心反馈 | 正式可用 |
| `feed_meal` | `feed_meal` | 正餐反馈 | 正式可用 |
| `feed_tea/rest` | `rest_tea` | 热茶/安抚 | 可用；当前复用甜点/喂食源图，建议补专用捧茶 |
| `study_running` | `study_guard` | 学习守护/读书 | 正式可用 |
| `study_complete` | `study_complete` | 学习完成庆祝 | 正式可用 |
| `chat_reply` | `talking` | 气泡聊天说话姿态 | 可用；当前复用邀请姿态 |
| `sleep` | `sleep_lie` | 睡眠/低体力休息 | 正式可用 |
## animations 到绿色源图映射

| animation 文件 | source key |
|---|---|
| `animations/drag_hold/000.png` | `drag_hold` |
| `animations/drop/000.png` | `idle_soft` |
| `animations/feed_meal/000.png` | `feed_meal` |
| `animations/feed_snack/000.png` | `feed_snack` |
| `animations/hover_backstep/000.png` | `hover_shy` |
| `animations/hover_curious/000.png` | `idle_soft` |
| `animations/hover_shy/000.png` | `hover_shy` |
| `animations/idle/000.png` | `idle_neutral` |
| `animations/idle/001.png` | `idle_soft` |
| `animations/idle/002.png` | `hover_shy` |
| `animations/idle_blink/000.png` | `idle_closed_smile` |
| `animations/idle_cheer/000.png` | `idle_cheer` |
| `animations/idle_yawn/000.png` | `idle_yawn` |
| `animations/outfit_daily/000.png` | `idle_neutral` |
| `animations/part_accessory_proud/000.png` | `accessory_proud` |
| `animations/part_face_blush/000.png` | `idle_soft` |
| `animations/part_face_pout/000.png` | `annoyed_pout` |
| `animations/part_feet_step/000.png` | `hover_shy` |
| `animations/part_hand_highfive/000.png` | `hand_wave` |
| `animations/part_hand_invite/000.png` | `hand_invite` |
| `animations/part_hand_wave/000.png` | `hand_wave` |
| `animations/part_head_pat/000.png` | `idle_closed_smile` |
| `animations/part_outfit_show/000.png` | `outfit_show` |
| `animations/rest_tea/000.png` | `feed_snack` |
| `animations/sleep_lie/000.png` | `sleep_lie` |
| `animations/study_complete/000.png` | `idle_cheer` |
| `animations/study_guard/000.png` | `study_reading` |
| `animations/talking/000.png` | `hand_invite` |
| `animations/tap_annoyed/000.png` | `annoyed_pout` |
