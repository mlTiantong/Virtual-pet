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
RUNTIME_ROOT = ASSET_ROOT / "runtime"
SHEET_ROOT = RUNTIME_ROOT / "sheets"
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
    hair_dx: float = 0
    hair_dy: float = 0
    hair_sx: float = 1
    hair_sy: float = 1
    hair_angle: float = 0
    hair_wave: float = 0
    hair_wave_phase: float = 0
    hair_pull_x: float = 0
    hair_pull_y: float = 0
    hair_pull_focus_y: float = 0.58
    hair_pull_radius: float = 0.82


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


def make_layers(character: Image.Image, bbox: tuple[int, int, int, int]) -> tuple[Image.Image, Image.Image, Image.Image]:
    x0, y0, x1, y1 = bbox
    width = x1 - x0
    height = y1 - y0
    cut_start = y0 + height * 0.50
    cut_end = y0 + height * 0.68

    y = np.arange(CANVAS_SIZE, dtype=np.float32)[:, None]
    x = np.arange(CANVAS_SIZE, dtype=np.float32)[None, :]
    upper_weight = np.clip((cut_end - y) / (cut_end - cut_start), 0, 1)

    x_center = x0 + width * 0.5
    x_norm_from_center = np.abs(x - x_center) / max(1.0, width * 0.5)
    y_norm = (y - y0) / max(1.0, height)
    side_gate = np.clip((x_norm_from_center - 0.48) / 0.20, 0, 1)
    vertical_gate = np.clip((y_norm - 0.18) / 0.18, 0, 1) * np.clip((0.90 - y_norm) / 0.16, 0, 1)
    hair_weight = np.clip((side_gate * vertical_gate - 0.20) / 0.45, 0, 1)

    arr = np.array(character).astype(np.float32)
    alpha = arr[..., 3] / 255.0
    hair_alpha = alpha * hair_weight
    body_alpha = alpha * (1.0 - hair_weight * 0.42)
    upper_alpha = body_alpha * upper_weight
    lower_alpha = body_alpha * (1.0 - upper_weight)

    upper = arr.copy()
    lower = arr.copy()
    hair = arr.copy()
    upper[..., 3] = upper_alpha * 255
    lower[..., 3] = lower_alpha * 255
    hair[..., 3] = hair_alpha * 255
    return (
        Image.fromarray(np.clip(lower, 0, 255).astype(np.uint8), "RGBA"),
        Image.fromarray(np.clip(upper, 0, 255).astype(np.uint8), "RGBA"),
        Image.fromarray(np.clip(hair, 0, 255).astype(np.uint8), "RGBA"),
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
    hair: Image.Image,
    frame: RigFrame,
    root_pivot: tuple[float, float],
    neck_pivot: tuple[float, float],
) -> Image.Image:
    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    hair_wave = frame.hair_wave + max(abs(frame.upper_wave), abs(frame.lower_wave)) * 0.85
    hair_wave_phase = frame.hair_wave_phase if abs(frame.hair_wave_phase) > 0.01 else frame.upper_wave_phase + 0.85
    hair_layer = transform_layer(
        hair,
        sx=frame.hair_sx * (1.0 + (frame.upper_sx - 1.0) * 0.35 + (frame.lower_sx - 1.0) * 0.15),
        sy=frame.hair_sy * (1.0 + (frame.upper_sy - 1.0) * 0.35 + (frame.lower_sy - 1.0) * 0.15),
        angle=frame.hair_angle + frame.upper_angle * 1.15 + frame.lower_angle * 0.45,
        center=neck_pivot,
        dx=frame.hair_dx + frame.upper_dx * 0.35 + frame.lower_dx * 0.25,
        dy=frame.hair_dy + frame.upper_dy * 0.45 + frame.lower_dy * 0.35,
    )
    hair_layer = elastic_pull_layer(
        hair_layer,
        frame.hair_pull_x + frame.upper_pull_x * 0.65 + frame.lower_pull_x * 0.25,
        frame.hair_pull_y + frame.upper_pull_y * 0.50 + frame.lower_pull_y * 0.20,
        frame.hair_pull_focus_y,
        frame.hair_pull_radius,
    )
    hair_layer = wave_layer_horizontal(hair_layer, hair_wave, hair_wave_phase, power=0.92)
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
    canvas.alpha_composite(hair_layer)
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
                hair_wave=1.4,
                hair_wave_phase=phase + 1.7,
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
                hair_angle=1.0 * math.sin(phase + 1.7),
                hair_wave=2.4,
                hair_wave_phase=phase + 2.6,
            )
        )
    return frames


def drag_start_frames(count: int) -> list[RigFrame]:
    frames: list[RigFrame] = []
    for i in range(count):
        t = i / max(1, count - 1)
        lift = 1.0 - (1.0 - t) ** 3
        snap = math.sin(math.pi * t)
        overshoot = math.sin(math.tau * t) * (1.0 - t)
        frames.append(
            RigFrame(
                lower_dy=-30.0 * lift - 4.0 * overshoot,
                lower_sx=1.0 - 0.010 * lift,
                lower_sy=1.0 + 0.022 * lift,
                lower_angle=1.8 * snap,
                lower_wave=4.0 * snap,
                lower_wave_phase=t * math.tau + 1.5,
                upper_dy=-35.0 * lift - 5.0 * overshoot,
                upper_sx=1.0 - 0.012 * lift,
                upper_sy=1.0 + 0.025 * lift,
                upper_angle=-1.6 * overshoot,
                upper_pull_y=-3.0 * snap,
                upper_pull_focus_y=0.34,
                upper_wave=6.2 * snap,
                upper_wave_phase=t * math.tau + 2.2,
                hair_dy=-6.0 * lift,
                hair_angle=-3.8 * snap,
                hair_wave=6.0 * snap,
                hair_wave_phase=t * math.tau + 3.0,
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
                hair_angle=3.8 * math.sin(phase - 1.0),
                hair_wave=5.4,
                hair_wave_phase=phase + 3.8,
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
                hair_angle=2.8 * impact * math.sin(settle * math.tau + 0.8),
                hair_wave=4.2 * impact,
                hair_wave_phase=settle * math.tau + 3.0,
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
                hair_dy=3.0 * press,
                hair_wave=3.0 * press,
                hair_wave_phase=t * math.tau + 1.8,
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
                hair_angle=2.0 * math.sin(phase * 2.0 - 0.8),
                hair_wave=3.0,
                hair_wave_phase=phase + 2.9,
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
                hair_wave=2.6 * settle,
                hair_wave_phase=t * math.tau + 2.8,
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
                hair_dy=2.0 * nod,
                hair_wave=2.2 * nod,
                hair_wave_phase=t * math.tau + 2.4,
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
                hair_angle=3.6 * side * hop,
                hair_wave=5.2 * hop,
                hair_wave_phase=t * math.tau + 2.3,
            )
        )
    return frames


def sequence_defs() -> list[SequenceDef]:
    return [
        SequenceDef("idle_m8", idle_frames(16), fps=8, loop=True, duration_ms=0, return_to_idle=False),
        SequenceDef("hover_m8", hover_frames(12), fps=10, loop=False, duration_ms=1200),
        SequenceDef("drag_start", drag_start_frames(8), fps=14, loop=False, duration_ms=560, return_to_idle=False),
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


def sheet_layout(count: int) -> tuple[int, int]:
    columns = min(4, max(1, count))
    rows = math.ceil(count / columns)
    return columns, rows


def save_sequence(
    name: str,
    specs: list[RigFrame],
    lower: Image.Image,
    upper: Image.Image,
    hair: Image.Image,
    root_pivot: tuple[float, float],
    neck_pivot: tuple[float, float],
) -> list[Image.Image]:
    SHEET_ROOT.mkdir(parents=True, exist_ok=True)
    frames: list[Image.Image] = []
    for spec in specs:
        frame = render_frame(lower, upper, hair, spec, root_pivot, neck_pivot)
        frames.append(frame)

    columns, rows = sheet_layout(len(frames))
    sheet = Image.new("RGBA", (columns * CANVAS_SIZE, rows * CANVAS_SIZE), (0, 0, 0, 0))
    for idx, frame in enumerate(frames):
        x = (idx % columns) * CANVAS_SIZE
        y = (idx // columns) * CANVAS_SIZE
        sheet.alpha_composite(frame, (x, y))
    sheet.save(SHEET_ROOT / f"{name}.png", optimize=True, compress_level=9)
    return frames


def sheet_path(name: str) -> str:
    return f"runtime/sheets/{name}.png"


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
        columns, rows = sheet_layout(len(spec.frames))
        animations[spec.id] = {
            "id": spec.id,
            "type": "spritesheet",
            "fps": spec.fps,
            "loop": spec.loop,
            "durationMs": spec.duration_ms,
            "returnToIdle": spec.return_to_idle,
            "sheet": sheet_path(spec.id),
            "columns": columns,
            "rows": rows,
            "frameCount": len(spec.frames),
            "frameWidth": CANVAS_SIZE,
            "frameHeight": CANVAS_SIZE,
            "frames": [],
        }

    manifest = {
        "schema": 2,
        "characterId": "blue_girl_m1",
        "defaultAnimation": "idle_m8",
        "assetBaseline": "rig_prototype_v5_drag_start",
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
    hair: Image.Image,
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
        preview_tile(hair, "side hair layer"),
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


def alpha_bbox(alpha: np.ndarray) -> list[int] | None:
    ys, xs = np.where(alpha > 8)
    if len(xs) == 0 or len(ys) == 0:
        return None
    return [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]


def save_quality_report(sequences: dict[str, list[Image.Image]]) -> None:
    ARTIFACT_ROOT.mkdir(parents=True, exist_ok=True)
    report: dict[str, object] = {
        "frameWidth": CANVAS_SIZE,
        "frameHeight": CANVAS_SIZE,
        "sequenceCount": len(sequences),
        "frameCountTotal": sum(len(frames) for frames in sequences.values()),
        "sequences": {},
    }
    sequence_reports: dict[str, object] = {}

    for name, frames in sequences.items():
        first = np.array(frames[0]).astype(np.int16)
        changed_pixels: list[int] = []
        alpha_pixels: list[int] = []
        corner_alpha_max = 0
        bboxes: list[list[int] | None] = []

        for frame in frames:
            arr = np.array(frame).astype(np.int16)
            diff = np.max(np.abs(arr - first), axis=2)
            changed_pixels.append(int(np.count_nonzero(diff > 8)))

            alpha = arr[..., 3]
            alpha_pixels.append(int(np.count_nonzero(alpha > 8)))
            corner_alpha_max = max(
                corner_alpha_max,
                int(alpha[0, 0]),
                int(alpha[0, -1]),
                int(alpha[-1, 0]),
                int(alpha[-1, -1]),
            )
            bboxes.append(alpha_bbox(alpha))

        columns, rows = sheet_layout(len(frames))
        sheet_file = SHEET_ROOT / f"{name}.png"
        sequence_reports[name] = {
            "frames": len(frames),
            "sheet": sheet_path(name),
            "sheetPixels": [columns * CANVAS_SIZE, rows * CANVAS_SIZE],
            "sheetBytes": sheet_file.stat().st_size if sheet_file.exists() else 0,
            "avgChangedPixelsVsFirst": round(sum(changed_pixels) / len(changed_pixels)),
            "maxChangedPixelsVsFirst": max(changed_pixels),
            "alphaPixelsMin": min(alpha_pixels),
            "alphaPixelsMax": max(alpha_pixels),
            "cornerAlphaMax": corner_alpha_max,
            "firstBBox": bboxes[0],
            "lastBBox": bboxes[-1],
        }

    report["sequences"] = sequence_reports
    (ARTIFACT_ROOT / "rig_quality_report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def main() -> None:
    if not REFERENCE.exists():
        raise FileNotFoundError(REFERENCE)

    if RUNTIME_ROOT.exists():
        shutil.rmtree(RUNTIME_ROOT)
    SHEET_ROOT.mkdir(parents=True, exist_ok=True)

    reference = Image.open(REFERENCE)
    keyed = chroma_key_green(reference)
    character, bbox = fit_to_canvas(keyed)
    lower, upper, hair = make_layers(character, bbox)

    x0, y0, x1, y1 = bbox
    w, h = x1 - x0, y1 - y0
    root_pivot = (x0 + w * 0.5, y0 + h * 0.82)
    neck_pivot = (x0 + w * 0.50, y0 + h * 0.43)

    defs = sequence_defs()
    sequences = {
        spec.id: save_sequence(spec.id, spec.frames, lower, upper, hair, root_pivot, neck_pivot)
        for spec in defs
    }
    write_manifest(defs)
    save_rig_diagnostics(reference, character, lower, upper, hair, bbox, root_pivot, neck_pivot)
    save_contact_sheet(sequences)
    save_quality_report(sequences)

    print(f"Generated {len(sequences)} sheets for {sum(len(frames) for frames in sequences.values())} frames under {SHEET_ROOT}")
    print(f"Manifest: {ASSET_ROOT / 'animation-manifest.json'}")
    print(f"Diagnostics: {ARTIFACT_ROOT / 'rig_diagnostics.png'}")
    print(f"Quality: {ARTIFACT_ROOT / 'rig_quality_report.json'}")
    print(f"Preview: {ARTIFACT_ROOT / 'rig_prototype_contact_sheet.png'}")


if __name__ == "__main__":
    main()
