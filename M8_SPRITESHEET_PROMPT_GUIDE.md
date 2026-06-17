# M8 桌宠 Spritesheet 提示词手册

> 用途：生成 5×5 (25帧) 动画 spritesheet，每帧 900×900，总图 4500×4500
> 目标 AI 平台：Midjourney v6+ / DALL-E 3 / Stable Diffusion (ComfyUI)
> 参考图：使用 `character_master_idle.png` 作为 image prompt / reference image

---

## 一、角色锁定描述块（所有提示词必须包含）

以下文本作为每个提示词的"角色锚点"，不可省略、不可修改。

```
CHARACTER LOCK (copy this EXACTLY into every prompt):
A petite chibi anime girl, super-deformed with oversized head (head-to-body ratio 1:2).
Hair: very long, straight-to-wavy, icy silver-blue color (#B8D4E3), flowing past her waist to lower thighs, with two small black X-shaped hairpins above her forehead and a light-blue satin ribbon bow tied on the left side of her hair.
Eyes: large, round, saturated sky-blue irises (#6EB5E0) with prominent white highlight dots.
Outfit: pale ice-blue (#C8DFF0) frilly one-piece dress with white lace trim along collar, cuffs, and hemline; a small blue ribbon bow at the center of the chest neckline; short puffed sleeves.
Legs: white opaque thigh-high socks with subtle polka-dot pattern; light-blue (#A8CCE0) Mary-Jane shoes with white soles and small bow detail.
Style: clean anime lineart, soft cel-shading, pastel color palette, game sprite aesthetic.
```

---

## 二、5×5 网格规格描述块（所有提示词必须包含）

```
LAYOUT: A single image containing a strict 5×5 grid of equal-sized square cells.
Each cell shows the SAME character (identical design, same outfit, same hair color, same proportions).
The grid has 5 columns (left to right = frame 1 through 5 of an animation cycle) and 5 rows (each row = a different animation).
Background of every cell: solid flat green (#00FF00 chroma-key green) OR pure white (#FFFFFF).
No text, no labels, no borders, no gridlines between cells.
Each cell must show the character's FULL BODY from head to shoes, centered in the cell, feet touching the bottom edge.
Total image aspect ratio: 1:1 (square). Resolution: as high as possible.
```

---

## 三、各动作行描述

### Row 1: idle（待机循环）— 最重要的修复项

当前问题：5帧身体姿态完全一致，无呼吸感。
目标：有真实呼吸起伏 + 发丝微动 + 偶尔眨眼。

```
ROW 1 — IDLE BREATHING CYCLE (5 frames, left to right):
Frame 1: Neutral standing pose, arms relaxed at sides, normal breathing, eyes open, gentle smile. Hair hangs naturally.
Frame 2: Slight inhale — shoulders rise 2%, chest expands subtly, hair sways gently to the right as if touched by a soft breeze. Eyes open.
Frame 3: Peak inhale — shoulders at highest point, hair sway at maximum rightward drift, eyes beginning to close (half-lidded).
Frame 4: Exhale — shoulders drop back down, hair returns toward center and sways slightly left, eyes fully closed (blink).
Frame 5: Return to neutral — shoulders at rest position, hair settles back to natural hang, eyes open again. Identical to Frame 1 to enable seamless loop.

KEY MOTION: The body subtly RISES and FALLS with breathing (~2% vertical shift). Hair sways ~5 degrees left-right. The BLINK happens in frame 4. Everything else stays identical.
```

### Row 2: hover（鼠标悬停反应）

```
ROW 2 — HOVER REACTION (5 frames, left to right):
Frame 1: Neutral standing, arms at sides, looking forward calmly.
Frame 2: Slight surprise — eyes widen, eyebrows raise, head tilts 5° to the right, one hand lifts slightly from body.
Frame 3: Recognition — soft blush appears on cheeks (pink tint), small shy smile forms, hands clasped together in front of chest.
Frame 4: Shy reaction — deeper blush, eyes look slightly downward and to the side, shoulders hunch inward slightly, a small pink heart icon floats near her head.
Frame 5: Settling — blush fades slightly, returns to gentle smile, hands lower back toward sides, posture relaxes.

KEY MOTION: Emotional progression from calm → surprised → shy/blushing → peak shy → settling. Body language shifts from open → closed/protective → relaxing.
```

### Row 3: pat_head（摸头反应）

```
ROW 3 — HEAD PAT REACTION (5 frames, left to right):
Frame 1: Neutral standing, looking forward.
Frame 2: An invisible hand touches her head — eyes widen in surprise, shoulders tense up, head ducks down slightly.
Frame 3: Enjoying the pat — eyes close into happy crescents (^_^), big smile, slight head tilt into the invisible hand, small pink heart appears.
Frame 4: Content — eyes still closed happily, gentle smile, both hands raised to chest level in a "fist pump" gesture of joy, two small hearts floating.
Frame 5: After the pat — eyes open with a soft happy expression, slight blush remaining, hands lower back, one hand touches her own head.

KEY MOTION: Surprise → enjoyment → peak joy → afterglow. The character's HEAD DUCKS DOWN in frame 2 (being patted) and TILTS in frame 3 (leaning into it). Expression is the primary storytelling tool.
```

### Row 4: face_reaction（戳脸反应）

```
ROW 4 — FACE POKE REACTION (5 frames, left to right):
Frame 1: Neutral standing, calm expression.
Frame 2: Surprised poke — eyes go wide (O_O), mouth forms small "o" shape, one hand flies up to touch the poked cheek, body flinches slightly backward.
Frame 3: Blushing — intense blush covers both cheeks (bright pink), eyes become teary/sparkly, mouth slightly open, steam/embarrassment lines near ears.
Frame 4: Pout — cheeks puffed out, eyebrows furrowed in mock-anger, arms crossed over chest, looking away to the side.
Frame 5: Recovery — pout softens, one eye squints in a playful glare, arms uncross, small smirk returns.

KEY MOTION: Shock → embarrassment → mock anger → playful recovery. Body goes from FLINCHING BACK → PUFFING UP → CROSSING ARMS → RELAXING.
```

### Row 5: hand_invite（招手/邀请）

```
ROW 5 — HAND INVITE / WAVE (5 frames, left to right):
Frame 1: Neutral standing, arms at sides.
Frame 2: Right arm begins to raise — elbow lifts to shoulder height, hand still near body, eyes brighten with anticipation.
Frame 3: Hand fully extended — right arm extended forward and slightly up, palm facing outward (inviting gesture), big welcoming smile, body leans slightly forward.
Frame 4: Wave motion — same arm position but hand tilts to the right (wave right), slight head tilt, sparkle effect near the hand.
Frame 5: Wave return — hand tilts back to center (wave left), arm begins to lower slightly, gentle smile.

KEY MOTION: Arm RAISE → EXTEND → WAVE RIGHT → WAVE LEFT. The arm describes a smooth arc from resting position to fully extended. The wave in frames 4-5 is a subtle LEFT-RIGHT hand tilt, not a full arm swing.
```

---

## 四、完整提示词（可直接使用）

### 完整 5×5 Spritesheet 提示词

```
Spritesheet of a chibi anime desktop pet character in a strict 5×5 grid layout (25 frames total). Each cell is an equal-sized square showing the same character's full body on a solid green (#00FF00) background.

CHARACTER (MUST be identical in every cell):
Petite chibi anime girl, head-to-body ratio 1:2. Very long icy silver-blue hair (#B8D4E3) flowing past waist with black X hairpins above forehead and blue ribbon bow on left side. Large round sky-blue eyes (#6EB5E0) with white highlights. Pale ice-blue (#C8DFF0) frilly dress with white lace trim, blue neck bow, puffed sleeves. White thigh-high socks with polka dots, light-blue Mary-Jane shoes. Clean anime lineart, soft cel-shading, pastel palette.

ROW 1 (IDLE): Neutral stand → shoulders rise (inhale) → peak inhale with hair sway right → exhale with eyes closing (blink) → return to neutral. Subtle 2% vertical breathing motion + hair sway.
ROW 2 (HOVER): Neutral → eyes widen surprised → blush + shy smile + hands clasped → deep blush + pink heart + looking down → settling back to calm.
ROW 3 (PAT HEAD): Neutral → head ducks (surprised by touch) → eyes close happily (^_^) + heart → peak joy + fists up + hearts → afterglow + touching own head.
ROW 4 (FACE POKE): Neutral → O_O surprise + hand to cheek → intense blush + teary eyes → puffed cheeks + arms crossed + looking away → playful recovery smirk.
ROW 5 (WAVE): Arms at sides → right arm raises → arm extended palm out + smile → hand tilts right (wave) → hand tilts left (wave return).

CRITICAL RULES:
- The character must look EXACTLY THE SAME in every cell (same hair color, same dress color, same proportions, same art style).
- Only POSE and EXPRESSION change between cells. Never change outfit, hair color, or accessories.
- Each cell shows FULL BODY (head to shoes), character centered, feet near bottom.
- No text, no labels, no numbers, no borders between cells.
- Grid must be perfectly aligned: 5 equal columns, 5 equal rows.

Style: game sprite sheet, anime, chibi, clean lines, soft shading, transparent-ready.
--ar 1:1 --s 200 --v 6.1
```

### 负面提示词（Negative Prompt）

```
DO NOT include: different hair colors, different dress colors, purple or pink dress, missing limbs, extra limbs, cropped body parts, partial body, different outfits between frames, text, watermarks, labels, numbers, gridlines, borders, backgrounds other than solid green, inconsistent art style, realistic proportions, multiple characters, accessories not described, shoe/sock redesign.
```

---

## 五、按行单独生成的提示词（推荐方案）

由于 AI 在单次生成 25 个一致帧时极易出现角色漂移，**推荐方案是逐行单独生成 1×5 横条**，后期再拼接为 5×5。

### Row 1 — idle 单独提示词

```
Horizontal strip of 5 equal square frames showing a chibi anime girl's idle breathing animation cycle.

CHARACTER (identical in all 5 frames): Petite chibi anime girl (1:2 head-body ratio). Very long icy silver-blue hair with black X hairpins and blue side bow. Large sky-blue eyes. Pale ice-blue frilly dress with white lace trim, blue neck bow. White thigh-high socks, light-blue shoes. Clean anime lineart, soft cel-shading.

ANIMATION CYCLE (left to right):
Frame 1: Neutral standing, arms at sides, eyes open, calm gentle smile, hair hangs naturally.
Frame 2: Subtle inhale — shoulders rise ~2%, hair sways gently right, eyes open, same smile.
Frame 3: Peak inhale — shoulders highest, hair sway max right, eyes half-closed (drowsy).
Frame 4: Exhale — shoulders drop, hair sways back left, eyes fully CLOSED (blink), peaceful expression.
Frame 5: Return to Frame 1 — shoulders at rest, hair centered, eyes open, calm smile. Seamless loop point.

Each frame: full body visible head to shoes, character centered, solid green (#00FF00) background, no text, no borders.
--ar 5:1 --s 200 --v 6.1
```

### Row 2 — hover 单独提示词

```
Horizontal strip of 5 equal square frames showing a chibi anime girl's hover/shy reaction animation.

CHARACTER (identical in all 5 frames): [同上方角色锁定块]

ANIMATION CYCLE (left to right):
Frame 1: Neutral standing, arms at sides, calm forward gaze.
Frame 2: Surprised — eyes widen, eyebrows up, head tilts 5° right, one hand lifts slightly.
Frame 3: Blush + shy smile — pink cheeks, hands clasped at chest, slight head tilt.
Frame 4: Peak shy — deeper blush, eyes downcast, shoulders hunch, small pink heart near head.
Frame 5: Settling — blush fading, gentle smile returns, hands lowering, posture relaxing.

Each frame: full body, centered, solid green background, no text.
--ar 5:1 --s 200 --v 6.1
```

### Row 3 — pat_head 单独提示词

```
Horizontal strip of 5 equal square frames showing a chibi anime girl's head-pat reaction.

CHARACTER (identical in all 5 frames): [同上方角色锁定块]

ANIMATION CYCLE (left to right):
Frame 1: Neutral standing, looking forward.
Frame 2: Head ducks down slightly, eyes wide with surprise (being patted), shoulders tense.
Frame 3: Eyes close into happy crescents (^_^), big smile, head tilts into the invisible pat, small pink heart.
Frame 4: Peak joy — eyes still happily closed, both hands raised to chest in fist-pump, two hearts floating.
Frame 5: Afterglow — eyes open softly, slight blush, one hand touching own head, gentle smile.

Each frame: full body, centered, solid green background, no text.
--ar 5:1 --s 200 --v 6.1
```

### Row 4 — face_reaction 单独提示词

```
Horizontal strip of 5 equal square frames showing a chibi anime girl's face-poke reaction.

CHARACTER (identical in all 5 frames): [同上方角色锁定块]

ANIMATION CYCLE (left to right):
Frame 1: Neutral standing, calm expression.
Frame 2: O_O surprise — eyes wide, mouth small "o", one hand to cheek, body flinches back slightly.
Frame 3: Intense blush — bright pink cheeks, teary sparkle eyes, mouth open, steam lines near ears.
Frame 4: Pout — cheeks puffed, eyebrows furrowed, arms crossed, looking away to the side.
Frame 5: Recovery — smirk, one eye squinting playfully, arms uncrossing, blush fading.

Each frame: full body, centered, solid green background, no text.
--ar 5:1 --s 200 --v 6.1
```

### Row 5 — hand_invite 单独提示词

```
Horizontal strip of 5 equal square frames showing a chibi anime girl's waving/inviting gesture.

CHARACTER (identical in all 5 frames): [同上方角色锁定块]

ANIMATION CYCLE (left to right):
Frame 1: Neutral standing, arms at sides.
Frame 2: Right arm raising — elbow lifts to shoulder height, hand near body, eyes brighten.
Frame 3: Arm extended — right hand forward, palm outward, big welcoming smile, body leans forward slightly.
Frame 4: Wave right — same arm position, hand tilts right, sparkle near hand, head tilt.
Frame 5: Wave left — hand tilts back left, arm starts lowering, gentle smile.

Each frame: full body, centered, solid green background, no text.
--ar 5:1 --s 200 --v 6.1
```

---

## 六、使用建议

### 推荐工作流

1. **优先用逐行方案（第五节）**：1×5 横条比 5×5 全图的角色一致性好 10 倍
2. **始终附带参考图**：将 `character_master_idle.png` 作为 image prompt 上传
3. **色值校准**：生成后用吸管工具检查裙子色值是否接近 #C8DFF0，偏离超过 10% 需重新生成
4. **后期拼接**：用 Python/PIL 或 Photoshop 将 5 张 1×5 横条拼接为 5×5 spritesheet
5. **切帧**：每张横条等分 5 帧，输出为 `animations_m8/<组名>/000.png` ~ `004.png`

### 常见问题与对策

| 问题 | 原因 | 对策 |
|------|------|------|
| 角色跨帧变脸 | AI 无法保持 25 帧一致 | 改用逐行 1×5 方案 |
| 衣服颜色漂移 | 提示词颜色描述不够精确 | 加入 HEX 色值锁定 |
| 网格不对齐 | AI 不理解"5×5 grid" | 改用逐行方案 + 后期拼接 |
| 帧之间无差异 | AI 倾向生成相同图 | 在提示词中用大写强调 KEY MOTION |
| 身体被裁剪 | "full body"不够明确 | 加入"head to shoes visible" |
| 出现多余肢体 | AI 幻觉 | 在负面提示词中加入"extra limbs" |

### 平台特定参数

| 平台 | 推荐设置 |
|------|---------|
| Midjourney | `--ar 5:1 --s 200 --v 6.1 --cref <character_master_url>` |
| DALL-E 3 | Size: 1536×1024 (横条), Style: vivid |
| Stable Diffusion | Steps: 30+, CFG: 7-9, Sampler: DPM++ 2M Karras, ControlNet: OpenPose |
