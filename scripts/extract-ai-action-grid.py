from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_INPUT = ROOT / "src" / "DesktopPet.App" / "ai 绘画" / "多动作图.png"
DEFAULT_OUTPUT = ROOT / "art_sources" / "actions"
DEFAULT_PREVIEW = ROOT / "artifacts" / "ai-action-grid" / "extracted_actions_contact_sheet.png"

GRID_COLUMNS = 3
GRID_ROWS = 2
SOURCE_CANVAS_SIZE = 900
SOURCE_PADDING = 48

ACTION_SLOTS = [
    ("clasp_idle_source.png", "clasp_idle", 0, 0),
    ("wave_source.png", "wave", 1, 0),
    ("drink_source.png", "drink", 2, 0),
    ("sleepy_source.png", "sleepy", 0, 1),
    ("study_read_source.png", "study_read", 1, 1),
    ("plush_hug_source.png", "plush_hug", 2, 1),
]


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

    subject = ~green_bg
    alpha_img = Image.fromarray((subject.astype(np.uint8) * 255), "L")
    alpha_img = alpha_img.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.45))
    alpha = np.array(alpha_img).astype(np.uint8)

    foreground = alpha > 6
    neutral_green = (r * 0.45) + (b * 0.55) + 10
    green_cap = np.maximum(r, b) + 8
    edge_weight = np.clip((180 - alpha) / 180, 0, 1)
    capped_green = np.minimum(g, green_cap)
    despilled_green = capped_green * (1 - edge_weight) + np.minimum(capped_green, neutral_green) * edge_weight
    arr[..., 1] = np.where(foreground, despilled_green, g)
    arr[..., 3] = alpha
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def alpha_bbox(image: Image.Image, threshold: int = 8) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A").point(lambda p: 255 if p > threshold else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        raise RuntimeError("No foreground pixels found after chroma key.")
    return bbox


def fit_source_canvas(image: Image.Image) -> Image.Image:
    bbox = alpha_bbox(image)
    cropped = image.crop(bbox)
    available = SOURCE_CANVAS_SIZE - SOURCE_PADDING * 2
    scale = min(available / cropped.width, available / cropped.height)
    size = (round(cropped.width * scale), round(cropped.height * scale))
    resized = cropped.resize(size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (SOURCE_CANVAS_SIZE, SOURCE_CANVAS_SIZE), (0, 0, 0, 0))
    x = round((SOURCE_CANVAS_SIZE - resized.width) / 2)
    y = round((SOURCE_CANVAS_SIZE - resized.height) / 2)
    canvas.alpha_composite(resized, (x, y))
    return canvas


def make_contact_sheet(items: list[tuple[str, Image.Image]], path: Path) -> None:
    thumb = 180
    label_h = 26
    gap = 12
    columns = 3
    rows = (len(items) + columns - 1) // columns
    sheet = Image.new(
        "RGBA",
        (gap + columns * (thumb + gap), gap + rows * (thumb + label_h + gap)),
        (20, 24, 32, 255),
    )
    draw = ImageDraw.Draw(sheet)
    for idx, (label, image) in enumerate(items):
        col = idx % columns
        row = idx // columns
        x = gap + col * (thumb + gap)
        y = gap + row * (thumb + label_h + gap)
        preview = Image.new("RGBA", (thumb, thumb), (42, 46, 58, 255))
        preview.alpha_composite(image.resize((thumb, thumb), Image.Resampling.LANCZOS))
        sheet.alpha_composite(preview, (x, y))
        draw.text((x + 6, y + thumb + 6), label, fill=(228, 236, 255, 255))

    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path)


def extract_grid(input_path: Path, output_root: Path, preview_path: Path) -> list[Path]:
    source = Image.open(input_path).convert("RGBA")
    cell_w = source.width // GRID_COLUMNS
    cell_h = source.height // GRID_ROWS
    output_root.mkdir(parents=True, exist_ok=True)

    saved: list[Path] = []
    previews: list[tuple[str, Image.Image]] = []
    for filename, label, col, row in ACTION_SLOTS:
        x0 = col * cell_w
        y0 = row * cell_h
        x1 = source.width if col == GRID_COLUMNS - 1 else (col + 1) * cell_w
        y1 = source.height if row == GRID_ROWS - 1 else (row + 1) * cell_h
        cell = source.crop((x0, y0, x1, y1))
        keyed = chroma_key_green(cell)
        action = fit_source_canvas(keyed)
        out = output_root / filename
        action.save(out, optimize=True, compress_level=9)
        saved.append(out)
        previews.append((label, action))

    make_contact_sheet(previews, preview_path)
    return saved


def main() -> None:
    parser = argparse.ArgumentParser(description="Extract a 3x2 green-screen AI action grid into action source PNGs.")
    parser.add_argument("--input", type=Path, default=DEFAULT_INPUT)
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--preview", type=Path, default=DEFAULT_PREVIEW)
    args = parser.parse_args()

    saved = extract_grid(args.input, args.out_dir, args.preview)
    for path in saved:
        print(path)
    print(f"Preview: {args.preview}")


if __name__ == "__main__":
    main()
