from __future__ import annotations

import math
import shutil
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = ROOT / "src" / "DesktopPet.App" / "assets"
REFERENCE = ASSET_ROOT / "reference" / "参考图.png"
OUT_ROOT = ASSET_ROOT / "runtime" / "animations"
ARTIFACT_ROOT = ROOT / "artifacts" / "rig-prototype"

CANVAS_SIZE = 768
CHARACTER_HEIGHT = 690
FOOT_Y = 742


@dataclass(frozen=True)
class RigFrame:
    lower_dx: float = 0
    lower_dy: float = 0
    lower_sx: float = 1
    lower_sy: float = 1
    lower_angle: float = 0
    upper_dx: float = 0
    upper_dy: float = 0
    upper_sx: float = 1
    upper_sy: float = 1
    upper_angle: float = 0


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
    upper_layer = transform_layer(
        upper,
        sx=frame.upper_sx,
        sy=frame.upper_sy,
        angle=frame.upper_angle,
        center=neck_pivot,
        dx=frame.upper_dx,
        dy=frame.upper_dy,
    )
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
                upper_dy=-7.0 * breath,
                upper_angle=0.8 * sway,
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
                lower_angle=0.5 * math.sin(phase + 0.4),
                upper_dy=-10.0 - 4.0 * bounce,
                upper_angle=1.5 * math.sin(phase + 0.9),
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
                lower_angle=2.2 * swing,
                upper_dy=-35.0 + 3.0 * math.sin(phase),
                upper_angle=1.4 * lag,
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
        frames.append(
            RigFrame(
                lower_dy=dy,
                lower_sx=sx,
                lower_sy=sy,
                upper_dy=dy - 2.0 * math.sin(settle * math.pi),
                upper_sx=sx,
                upper_sy=sy,
                upper_angle=0.8 * math.sin(settle * math.tau),
            )
        )
    return frames


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
        frame.save(out_dir / f"{idx:03d}.png")
        frames.append(frame)
    return frames


def save_contact_sheet(sequences: dict[str, list[Image.Image]]) -> None:
    ARTIFACT_ROOT.mkdir(parents=True, exist_ok=True)
    thumb_size = 128
    gap = 12
    label_h = 22
    max_cols = max(len(frames) for frames in sequences.values())
    sheet_w = gap + max_cols * (thumb_size + gap)
    sheet_h = gap + len(sequences) * (thumb_size + label_h + gap)
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (20, 24, 32, 255))

    y = gap
    for name, frames in sequences.items():
        x = gap
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

    keyed = chroma_key_green(Image.open(REFERENCE))
    character, bbox = fit_to_canvas(keyed)
    lower, upper = make_layers(character, bbox)

    x0, y0, x1, y1 = bbox
    w, h = x1 - x0, y1 - y0
    root_pivot = (x0 + w * 0.5, y0 + h * 0.82)
    neck_pivot = (x0 + w * 0.50, y0 + h * 0.43)

    sequences = {
        "idle_m8": save_sequence("idle_m8", idle_frames(16), lower, upper, root_pivot, neck_pivot),
        "hover_m8": save_sequence("hover_m8", hover_frames(12), lower, upper, root_pivot, neck_pivot),
        "drag_hold": save_sequence("drag_hold", drag_hold_frames(12), lower, upper, root_pivot, neck_pivot),
        "drop": save_sequence("drop", drop_frames(), lower, upper, root_pivot, neck_pivot),
    }
    save_contact_sheet(sequences)

    print(f"Generated {sum(len(frames) for frames in sequences.values())} frames under {OUT_ROOT}")
    print(f"Preview: {ARTIFACT_ROOT / 'rig_prototype_contact_sheet.png'}")


if __name__ == "__main__":
    main()
