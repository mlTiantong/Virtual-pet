from __future__ import annotations

import json
import math
import shutil
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = ROOT / "src" / "DesktopPet.App" / "assets"
REFERENCE = ASSET_ROOT / "reference" / "参考图.png"
OUT_ROOT = ASSET_ROOT / "runtime" / "animations"
ARTIFACT_ROOT = ROOT / "artifacts" / "rig-prototype"

CANVAS_SIZE = 768
CHARACTER_HEIGHT = 690
FOOT_Y = 742

HIT_REGIONS = [
    {"id": "face", "x": 0.36, "y": 0.18, "w": 0.28, "h": 0.18},
    {"id": "head", "x": 0.22, "y": 0.02, "w": 0.56, "h": 0.35},
    {"id": "hand", "x": 0.25, "y": 0.34, "w": 0.5, "h": 0.2},
    {"id": "feet", "x": 0.36, "y": 0.74, "w": 0.28, "h": 0.23},
]


@dataclass(frozen=True)
class RigFrame:
    lower_dx: float = 0
    lower_dy: float = 0
    lower_sx: float = 1
    lower_sy: float = 1
    lower_angle: float = 0
    lower_wave: float = 0
    lower_wave_phase: float = 0
    lower_pull_x: float = 0
    lower_pull_y: float = 0
    lower_pull_focus_y: float = 0.72
    lower_pull_radius: float = 0.72
    upper_dx: float = 0
    upper_dy: float = 0
    upper_sx: float = 1
    upper_sy: float = 1
    upper_angle: float = 0
    upper_wave: float = 0
    upper_wave_phase: float = 0
    upper_pull_x: float = 0
    upper_pull_y: float = 0
    upper_pull_focus_y: float = 0.22
    upper_pull_radius: float = 0.55


@dataclass(frozen=True)
class SequenceDef:
    id: str
    frames: list[RigFrame]
    fps: int
    loop: bool
    duration_ms: int
    return_to_idle: bool = True


def chroma_key_green(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    arr = np.array(rgba).astype(np.float32)
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]

    green_bg = (
        (g > 110)
        & (g > r * 1.18)
        & (g > b * 1.18)
        & ((g - r) > 35)
        & ((g - b) > 35)
    )

    alpha = np.where(green_bg, 0, 255).astype(np.uint8)
    alpha_img = Image.fromarray(alpha, "L").filter(ImageFilter.GaussianBlur(0.55))
    alpha = np.array(alpha_img).astype(np.uint8)

    foreground = alpha > 8
    green_cap = np.maximum(r, b) + 22
    arr[..., 1] = np.where(foreground, np.minimum(g, green_cap), g)
    arr[..., 3] = alpha
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def fit_to_canvas(character: Image.Image) -> tuple[Image.Image, tuple[int, int, int, int]]:
    alpha = character.getchannel("A").point(lambda p: 255 if p > 16 else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        raise RuntimeError("Reference image did not produce a usable character alpha mask.")

    crop = character.crop(bbox)
    scale = CHARACTER_HEIGHT / crop.height
    size = (round(crop.width * scale), round(crop.height * scale))
    resized = crop.resize(size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    x = round((CANVAS_SIZE - resized.width) / 2)
    y = round(FOOT_Y - resized.height)
    canvas.alpha_composite(resized, (x, y))
    fitted_bbox = (x, y, x + resized.width, y + resized.height)
    return canvas, fitted_bbox


def make_layers(character: Image.Image, bbox: tuple[int, int, int, int]) -> tuple[Image.Image, Image.Image]:
    x0, y0, x1, y1 = bbox
    height = y1 - y0
    cut_start = y0 + height * 0.50
    cut_end = y0 + height * 0.68

    y = np.arange(CANVAS_SIZE, dtype=np.float32)[:, None]
    upper_weight = np.clip((cut_end - y) / (cut_end - cut_start), 0, 1)

    arr = np.array(character).astype(np.float32)
    alpha = arr[..., 3] / 255.0
    upper_alpha = alpha * upper_weight
    lower_alpha = alpha * (1.0 - upper_weight)

    upper = arr.copy()
    lower = arr.copy()
    upper[..., 3] = upper_alpha * 255
    lower[..., 3] = lower_alpha * 255
    return (
        Image.fromarray(np.clip(lower, 0, 255).astype(np.uint8), "RGBA"),
        Image.fromarray(np.clip(upper, 0, 255).astype(np.uint8), "RGBA"),
    )


def scale_about(layer: Image.Image, sx: float, sy: float, center: tuple[float, float]) -> Image.Image:
    if abs(sx - 1) < 0.0001 and abs(sy - 1) < 0.0001:
        return layer

    width, height = layer.size
    scaled = layer.resize((max(1, round(width * sx)), max(1, round(height * sy))), Image.Resampling.BICUBIC)
    canvas = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    paste_x = round(center[0] - center[0] * sx)
    paste_y = round(center[1] - center[1] * sy)
    canvas.alpha_composite(scaled, (paste_x, paste_y))
    return canvas


def transform_layer(
    layer: Image.Image,
    *,
    sx: float,
    sy: float,
    angle: float,
    center: tuple[float, float],
    dx: float,
    dy: float,
) -> Image.Image:
    scaled = scale_about(layer, sx, sy, center)
    return scaled.rotate(
        angle,
        resample=Image.Resampling.BICUBIC,
        center=center,
        translate=(round(dx), round(dy)),
        fillcolor=(0, 0, 0, 0),
    )


def bilinear_sample(arr: np.ndarray, src_x: np.ndarray, src_y: np.ndarray) -> np.ndarray:
    height, width = arr.shape[:2]
    valid = (src_x >= 0) & (src_x < width - 1) & (src_y >= 0) & (src_y < height - 1)

    x0 = np.floor(np.clip(src_x, 0, width - 1)).astype(np.int32)
    y0 = np.floor(np.clip(src_y, 0, height - 1)).astype(np.int32)
    x1 = np.clip(x0 + 1, 0, width - 1)
    y1 = np.clip(y0 + 1, 0, height - 1)

    wx = (src_x - x0)[..., None]
    wy = (src_y - y0)[..., None]

    top = arr[y0, x0] * (1 - wx) + arr[y0, x1] * wx
    bottom = arr[y1, x0] * (1 - wx) + arr[y1, x1] * wx
    out = top * (1 - wy) + bottom * wy
    out[~valid] = 0
    return out


def elastic_pull_layer(
    layer: Image.Image,
    pull_x: float,
    pull_y: float,
    focus_y: float,
    radius: float,
) -> Image.Image:
    if abs(pull_x) < 0.01 and abs(pull_y) < 0.01:
        return layer

    bbox = layer.getchannel("A").getbbox()
    if bbox is None:
        return layer

    x0, y0, x1, y1 = bbox
    width = max(1, x1 - x0)
    height = max(1, y1 - y0)
    center_x = x0 + width * 0.5
    center_y = y0 + height * focus_y
    radius_x = max(1.0, width * 0.70)
    radius_y = max(1.0, height * radius)

    yy, xx = np.mgrid[0:CANVAS_SIZE, 0:CANVAS_SIZE].astype(np.float32)
    distance = np.sqrt(((xx - center_x) / radius_x) ** 2 + ((yy - center_y) / radius_y) ** 2)
    weight = np.clip(1.0 - distance, 0.0, 1.0) ** 2

    src_x = xx - pull_x * weight
    src_y = yy - pull_y * weight
    sampled = bilinear_sample(np.array(layer).astype(np.float32), src_x, src_y)
    return Image.fromarray(np.clip(sampled, 0, 255).astype(np.uint8), "RGBA")


def wave_layer_horizontal(layer: Image.Image, amplitude: float, phase: float, power: float = 1.35) -> Image.Image:
    if abs(amplitude) < 0.01:
        return layer

    bbox = layer.getchannel("A").getbbox()
    if bbox is None:
        return layer

    arr = np.array(layer)
    out = np.zeros_like(arr)
    y0, y1 = bbox[1], bbox[3]
    height = max(1, y1 - y0)

    for y in range(CANVAS_SIZE):
        if y < y0 or y >= y1:
            out[y] = arr[y]
            continue

        yn = (y - y0) / height
        weight = max(0.0, min(1.0, yn)) ** power
        shift = int(round(amplitude * weight * math.sin(phase + yn * math.tau * 1.12)))
        if shift > 0:
            out[y, shift:] = arr[y, : CANVAS_SIZE - shift]
        elif shift < 0:
            out[y, : CANVAS_SIZE + shift] = arr[y, -shift:]
        else:
            out[y] = arr[y]

    return Image.fromarray(out, "RGBA")


def render_frame(
    lower: Image.Image,
    upper: Image.Image,
    frame: RigFrame,
    root_pivot: tuple[float, float],
    neck_pivot: tuple[float, float],
) -> Image.Image:
    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    lower_layer = transform_layer(
        lower,
        sx=frame.lower_sx,
        sy=frame.lower_sy,
        angle=frame.lower_angle,
        center=root_pivot,
        dx=frame.lower_dx,
        dy=frame.lower_dy,
    )
    lower_layer = elastic_pull_layer(
        lower_layer,
        frame.lower_pull_x,
        frame.lower_pull_y,
        frame.lower_pull_focus_y,
        frame.lower_pull_radius,
    )
    lower_layer = wave_layer_horizontal(lower_layer, frame.lower_wave, frame.lower_wave_phase, power=1.12)
    upper_layer = transform_layer(
        upper,
        sx=frame.upper_sx,
        sy=frame.upper_sy,
        angle=frame.upper_angle,
        center=neck_pivot,
        dx=frame.upper_dx,
        dy=frame.upper_dy,
    )
    upper_layer = elastic_pull_layer(
        upper_layer,
        frame.upper_pull_x,
        frame.upper_pull_y,
        frame.upper_pull_focus_y,
        frame.upper_pull_radius,
    )
    upper_layer = wave_layer_horizontal(upper_layer, frame.upper_wave, frame.upper_wave_phase, power=1.45)
    canvas.alpha_composite(lower_layer)
    canvas.alpha_composite(upper_layer)
    return canvas


def idle_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        phase = math.tau * i / count
        breath = math.sin(phase)
        sway = math.sin(phase + 0.7)
        frames.append(
            RigFrame(
                lower_dy=-5.0 * breath,
                lower_sx=1.0 - 0.004 * breath,
                lower_sy=1.0 + 0.008 * breath,
                lower_wave=1.2,
                lower_wave_phase=phase + 0.5,
                upper_dy=-7.0 * breath,
                upper_angle=1.0 * sway,
                upper_wave=2.8,
                upper_wave_phase=phase + 1.1,
            )
        )
    return frames


def hover_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        phase = math.tau * i / count
        bounce = math.sin(phase)
        frames.append(
            RigFrame(
                lower_dy=-8.0 - 3.0 * bounce,
                lower_sx=0.998,
                lower_sy=1.006,
                lower_angle=0.9 * math.sin(phase + 0.4),
                lower_wave=2.2,
                lower_wave_phase=phase + 1.2,
                upper_dy=-11.0 - 5.0 * bounce,
                upper_angle=2.1 * math.sin(phase + 0.9),
                upper_wave=4.6,
                upper_wave_phase=phase + 1.8,
            )
        )
    return frames


def drag_hold_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        phase = math.tau * i / count
        swing = math.sin(phase)
        lag = math.sin(phase - 0.6)
        frames.append(
            RigFrame(
                lower_dy=-30.0 + 4.0 * math.sin(phase + 0.2),
                lower_sx=0.992,
                lower_sy=1.018,
                lower_angle=3.0 * swing,
                lower_wave=4.8,
                lower_wave_phase=phase + 2.3,
                upper_dy=-35.0 + 3.0 * math.sin(phase),
                upper_angle=2.2 * lag,
                upper_wave=8.0,
                upper_wave_phase=phase + 3.1,
            )
        )
    return frames


def drop_frames() -> list[RigFrame]:
    keys = [
        (-26, 0.985, 1.025),
        (-18, 0.990, 1.018),
        (-6, 0.998, 1.008),
        (14, 1.020, 0.972),
        (26, 1.040, 0.935),
        (14, 1.018, 0.982),
        (-5, 0.992, 1.016),
        (-10, 0.988, 1.020),
        (-3, 0.998, 1.006),
        (2, 1.004, 0.996),
        (0, 1.000, 1.000),
        (0, 1.000, 1.000),
    ]
    frames: list[RigFrame] = []
    for idx, (dy, sx, sy) in enumerate(keys):
        settle = idx / max(1, len(keys) - 1)
        impact = math.sin(settle * math.pi)
        frames.append(
            RigFrame(
                lower_dy=dy,
                lower_sx=sx,
                lower_sy=sy,
                lower_wave=3.0 * impact,
                lower_wave_phase=settle * math.tau + 1.0,
                upper_dy=dy - 2.0 * math.sin(settle * math.pi),
                upper_sx=sx,
                upper_sy=sy,
                upper_angle=1.4 * math.sin(settle * math.tau),
                upper_wave=5.6 * impact,
                upper_wave_phase=settle * math.tau + 2.2,
            )
        )
    return frames


def pat_head_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        t = i / max(1, count - 1)
        press = math.sin(math.pi * t)
        rebound = math.sin(math.tau * t) * (1.0 - t)
        frames.append(
            RigFrame(
                lower_dy=1.2 * press,
                lower_sy=1.0 - 0.004 * press,
                upper_dy=7.0 * press - 2.0 * rebound,
                upper_sx=1.0 + 0.018 * press,
                upper_sy=1.0 - 0.036 * press + 0.008 * max(0.0, -rebound),
                upper_angle=0.8 * math.sin(math.tau * t),
                upper_pull_y=8.0 * press,
                upper_pull_focus_y=0.10,
                upper_pull_radius=0.42,
                upper_wave=5.0 * press,
                upper_wave_phase=t * math.tau + 0.9,
            )
        )
    return frames


def face_reaction_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        t = i / max(1, count - 1)
        envelope = math.sin(math.pi * t)
        side = math.sin(math.tau * t * 1.25)
        frames.append(
            RigFrame(
                lower_dx=1.5 * side * envelope,
                upper_dx=7.0 * side * envelope,
                upper_angle=-2.6 * side * envelope,
                upper_pull_x=7.0 * side * envelope,
                upper_pull_focus_y=0.28,
                upper_pull_radius=0.50,
                upper_wave=3.6 * envelope,
                upper_wave_phase=t * math.tau + 1.4,
            )
        )
    return frames


def tap_annoyed_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        t = i / max(1, count - 1)
        fade = 1.0 - t
        shake = math.sin(math.tau * t * 4.0) * fade
        frames.append(
            RigFrame(
                lower_dx=3.0 * shake,
                lower_angle=0.9 * shake,
                lower_wave=2.0 * fade,
                lower_wave_phase=t * math.tau + 0.4,
                upper_dx=8.0 * shake,
                upper_angle=-3.5 * shake,
                upper_pull_x=6.0 * shake,
                upper_pull_focus_y=0.25,
                upper_wave=5.0 * fade,
                upper_wave_phase=t * math.tau + 2.0,
            )
        )
    return frames


def hand_invite_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        phase = math.tau * i / count
        wave = math.sin(phase * 2.0)
        breath = math.sin(phase)
        frames.append(
            RigFrame(
                lower_dx=1.5 * wave,
                lower_dy=-2.0 * breath,
                lower_angle=0.7 * wave,
                lower_wave=2.0,
                lower_wave_phase=phase + 1.2,
                upper_dx=4.5 * wave,
                upper_dy=-4.0 - 2.0 * breath,
                upper_angle=2.8 * wave,
                upper_pull_x=5.0 * wave,
                upper_pull_focus_y=0.44,
                upper_pull_radius=0.42,
                upper_wave=4.8,
                upper_wave_phase=phase + 2.0,
            )
        )
    return frames


def study_guard_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        phase = math.tau * i / count
        breath = math.sin(phase)
        frames.append(
            RigFrame(
                lower_dy=-2.8 * breath,
                lower_sx=1.0 - 0.002 * breath,
                lower_sy=1.0 + 0.004 * breath,
                lower_wave=0.7,
                lower_wave_phase=phase + 0.2,
                upper_dy=-4.0 * breath,
                upper_angle=0.35 * math.sin(phase + 0.8),
                upper_wave=1.2,
                upper_wave_phase=phase + 0.9,
            )
        )
    return frames


def talking_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        t = i / max(1, count - 1)
        syllable = math.sin(math.tau * t * 3.0)
        envelope = math.sin(math.pi * t)
        frames.append(
            RigFrame(
                lower_dy=-1.0 * envelope,
                upper_dy=-4.0 * envelope - 1.6 * syllable,
                upper_sx=1.0 + 0.005 * max(0.0, syllable),
                upper_sy=1.0 - 0.006 * max(0.0, syllable),
                upper_angle=0.7 * math.sin(math.tau * t),
                upper_wave=2.0 * envelope,
                upper_wave_phase=t * math.tau + 1.0,
            )
        )
    return frames


def feed_snack_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        t = i / max(1, count - 1)
        hop = max(0.0, math.sin(math.pi * t * 2.0))
        settle = math.sin(math.pi * t)
        frames.append(
            RigFrame(
                lower_dy=-8.0 * hop + 3.0 * settle,
                lower_sx=1.0 + 0.010 * settle,
                lower_sy=1.0 - 0.016 * settle,
                lower_wave=2.0 * settle,
                lower_wave_phase=t * math.tau + 1.5,
                upper_dy=-11.0 * hop + 2.0 * settle,
                upper_angle=1.6 * math.sin(math.tau * t),
                upper_wave=4.0 * settle,
                upper_wave_phase=t * math.tau + 2.0,
            )
        )
    return frames


def feed_meal_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        t = i / max(1, count - 1)
        nod = math.sin(math.pi * t)
        settle = math.sin(math.tau * t * 1.5) * (1.0 - t)
        frames.append(
            RigFrame(
                lower_dy=3.0 * nod - 2.0 * settle,
                lower_sx=1.0 + 0.012 * nod,
                lower_sy=1.0 - 0.018 * nod,
                upper_dy=5.0 * nod - 3.0 * settle,
                upper_sx=1.0 + 0.010 * nod,
                upper_sy=1.0 - 0.014 * nod,
                upper_angle=1.2 * settle,
                upper_wave=2.8 * nod,
                upper_wave_phase=t * math.tau + 1.7,
            )
        )
    return frames


def rest_tea_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        phase = math.tau * i / count
        calm = math.sin(phase)
        frames.append(
            RigFrame(
                lower_dy=2.0 + 2.0 * calm,
                lower_sx=1.0 + 0.004 * calm,
                lower_sy=1.0 - 0.006 * calm,
                lower_wave=0.8,
                lower_wave_phase=phase + 0.6,
                upper_dy=3.0 + 3.0 * calm,
                upper_angle=0.9 * math.sin(phase + 0.9),
                upper_wave=1.6,
                upper_wave_phase=phase + 1.2,
            )
        )
    return frames


def idle_cheer_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        t = i / max(1, count - 1)
        hop = max(0.0, math.sin(math.pi * t * 2.0))
        side = math.sin(math.tau * t)
        frames.append(
            RigFrame(
                lower_dy=-12.0 * hop,
                lower_sx=1.0 - 0.006 * hop,
                lower_sy=1.0 + 0.012 * hop,
                lower_angle=1.2 * side,
                lower_wave=2.8 * hop,
                lower_wave_phase=t * math.tau + 0.8,
                upper_dy=-16.0 * hop,
                upper_angle=3.2 * side,
                upper_pull_x=5.0 * side * hop,
                upper_pull_focus_y=0.36,
                upper_wave=6.0 * hop,
                upper_wave_phase=t * math.tau + 1.4,
            )
        )
    return frames


def sequence_defs() -> list[SequenceDef]:
    return [
        SequenceDef("idle_m8", idle_frames(16), fps=8, loop=True, duration_ms=0, return_to_idle=False),
        SequenceDef("hover_m8", hover_frames(12), fps=10, loop=False, duration_ms=1200),
        SequenceDef("drag_hold", drag_hold_frames(12), fps=10, loop=True, duration_ms=0, return_to_idle=False),
        SequenceDef("drop", drop_frames(), fps=12, loop=False, duration_ms=1000),
        SequenceDef("pat_head_m8", pat_head_frames(8), fps=12, loop=False, duration_ms=700),
        SequenceDef("face_reaction_m8", face_reaction_frames(8), fps=12, loop=False, duration_ms=700),
        SequenceDef("tap_annoyed", tap_annoyed_frames(8), fps=12, loop=False, duration_ms=700),
        SequenceDef("hand_invite_m8", hand_invite_frames(10), fps=10, loop=False, duration_ms=1000),
        SequenceDef("study_guard_m8", study_guard_frames(16), fps=8, loop=True, duration_ms=0, return_to_idle=False),
        SequenceDef("talking", talking_frames(8), fps=12, loop=False, duration_ms=700),
        SequenceDef("feed_snack", feed_snack_frames(8), fps=12, loop=False, duration_ms=700),
        SequenceDef("feed_meal", feed_meal_frames(8), fps=12, loop=False, duration_ms=700),
        SequenceDef("rest_tea", rest_tea_frames(10), fps=10, loop=False, duration_ms=1000),
        SequenceDef("idle_cheer_m8", idle_cheer_frames(10), fps=12, loop=False, duration_ms=850),
    ]


def save_sequence(
    name: str,
    specs: list[RigFrame],
    lower: Image.Image,
    upper: Image.Image,
    root_pivot: tuple[float, float],
    neck_pivot: tuple[float, float],
) -> list[Image.Image]:
    out_dir = OUT_ROOT / name
    out_dir.mkdir(parents=True, exist_ok=True)
    frames: list[Image.Image] = []
    for idx, spec in enumerate(specs):
        frame = render_frame(lower, upper, spec, root_pivot, neck_pivot)
        frame.save(out_dir / f"{idx:03d}.png", optimize=True, compress_level=9)
        frames.append(frame)
    return frames


def frame_paths(name: str, count: int) -> list[str]:
    return [f"runtime/animations/{name}/{idx:03d}.png" for idx in range(count)]


def write_manifest(defs: list[SequenceDef]) -> None:
    animations: dict[str, object] = {
        "reference_pose": {
            "id": "reference_pose",
            "type": "frames",
            "fps": 1,
            "loop": False,
            "durationMs": 1000,
            "returnToIdle": False,
            "frames": ["reference/参考图.png"],
        }
    }

    for spec in defs:
        animations[spec.id] = {
            "id": spec.id,
            "type": "frames",
            "fps": spec.fps,
            "loop": spec.loop,
            "durationMs": spec.duration_ms,
            "returnToIdle": spec.return_to_idle,
            "frames": frame_paths(spec.id, len(spec.frames)),
        }

    manifest = {
        "schema": 2,
        "characterId": "blue_girl_m1",
        "defaultAnimation": "idle_m8",
        "assetBaseline": "rig_prototype_v2",
        "hitRegions": HIT_REGIONS,
        "animations": animations,
    }
    (ASSET_ROOT / "animation-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def preview_tile(image: Image.Image, label: str, size: int = 180) -> Image.Image:
    label_h = 26
    tile = Image.new("RGBA", (size, size + label_h), (28, 32, 42, 255))
    preview = Image.new("RGBA", (size, size), (42, 46, 58, 255))
    thumb = image.resize((size, size), Image.Resampling.LANCZOS)
    preview.alpha_composite(thumb)
    tile.alpha_composite(preview, (0, 0))
    ImageDraw.Draw(tile).text((8, size + 6), label, fill=(228, 236, 255, 255))
    return tile


def save_rig_diagnostics(
    reference: Image.Image,
    character: Image.Image,
    lower: Image.Image,
    upper: Image.Image,
    bbox: tuple[int, int, int, int],
    root_pivot: tuple[float, float],
    neck_pivot: tuple[float, float],
) -> None:
    ARTIFACT_ROOT.mkdir(parents=True, exist_ok=True)
    overlay = character.copy()
    draw = ImageDraw.Draw(overlay)
    x0, y0, x1, y1 = bbox
    draw.rectangle((x0, y0, x1, y1), outline=(120, 190, 255, 220), width=3)
    draw.line((root_pivot[0], root_pivot[1], neck_pivot[0], neck_pivot[1]), fill=(255, 205, 95, 240), width=5)
    for x, y, color in (
        (root_pivot[0], root_pivot[1], (255, 120, 96, 255)),
        (neck_pivot[0], neck_pivot[1], (96, 220, 255, 255)),
    ):
        draw.ellipse((x - 10, y - 10, x + 10, y + 10), fill=color)

    tiles = [
        preview_tile(reference.convert("RGBA"), "reference"),
        preview_tile(character, "keyed fit"),
        preview_tile(upper, "upper layer"),
        preview_tile(lower, "lower layer"),
        preview_tile(overlay, "rig pivots"),
    ]
    gap = 14
    sheet = Image.new(
        "RGBA",
        (gap + len(tiles) * (tiles[0].width + gap), tiles[0].height + gap * 2),
        (16, 20, 28, 255),
    )
    x = gap
    for tile in tiles:
        sheet.alpha_composite(tile, (x, gap))
        x += tile.width + gap
    sheet.convert("RGB").save(ARTIFACT_ROOT / "rig_diagnostics.png")


def save_contact_sheet(sequences: dict[str, list[Image.Image]]) -> None:
    ARTIFACT_ROOT.mkdir(parents=True, exist_ok=True)
    thumb_size = 128
    gap = 12
    label_h = 22
    label_w = 110
    max_cols = max(len(frames) for frames in sequences.values())
    sheet_w = label_w + gap + max_cols * (thumb_size + gap)
    sheet_h = gap + len(sequences) * (thumb_size + label_h + gap)
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (20, 24, 32, 255))
    draw = ImageDraw.Draw(sheet)

    y = gap
    for name, frames in sequences.items():
        draw.text((gap, y + thumb_size // 2 - 8), name, fill=(225, 235, 255, 255))
        x = label_w + gap
        for frame in frames:
            thumb = frame.resize((thumb_size, thumb_size), Image.Resampling.LANCZOS)
            preview = Image.new("RGBA", (thumb_size, thumb_size), (42, 46, 58, 255))
            preview.alpha_composite(thumb)
            sheet.alpha_composite(preview, (x, y))
            x += thumb_size + gap
        y += thumb_size + label_h + gap

    sheet.convert("RGB").save(ARTIFACT_ROOT / "rig_prototype_contact_sheet.png")


def main() -> None:
    if not REFERENCE.exists():
        raise FileNotFoundError(REFERENCE)

    if OUT_ROOT.exists():
        shutil.rmtree(OUT_ROOT)
    OUT_ROOT.mkdir(parents=True, exist_ok=True)

    reference = Image.open(REFERENCE)
    keyed = chroma_key_green(reference)
    character, bbox = fit_to_canvas(keyed)
    lower, upper = make_layers(character, bbox)

    x0, y0, x1, y1 = bbox
    w, h = x1 - x0, y1 - y0
    root_pivot = (x0 + w * 0.5, y0 + h * 0.82)
    neck_pivot = (x0 + w * 0.50, y0 + h * 0.43)

    defs = sequence_defs()
    sequences = {
        spec.id: save_sequence(spec.id, spec.frames, lower, upper, root_pivot, neck_pivot)
        for spec in defs
    }
    write_manifest(defs)
    save_rig_diagnostics(reference, character, lower, upper, bbox, root_pivot, neck_pivot)
    save_contact_sheet(sequences)

    print(f"Generated {sum(len(frames) for frames in sequences.values())} frames under {OUT_ROOT}")
    print(f"Manifest: {ASSET_ROOT / 'animation-manifest.json'}")
    print(f"Diagnostics: {ARTIFACT_ROOT / 'rig_diagnostics.png'}")
    print(f"Preview: {ARTIFACT_ROOT / 'rig_prototype_contact_sheet.png'}")


if __name__ == "__main__":
    main()
