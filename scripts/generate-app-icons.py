#!/usr/bin/env python3
"""Generate KakiMoni / kakipen WinUI app icons (Host / Client / Layout)."""

from __future__ import annotations

import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO = Path(__file__).resolve().parents[1]

# kakimoni-all launcher palette (#0f172a body, #7dd3fc accent)
C_BG = (15, 23, 42)
C_BG_DEEP = (11, 19, 36)
C_CANVAS = (30, 41, 59)
C_BORDER = (51, 65, 85)
C_INK = (56, 189, 248)
C_INK_SOFT = (125, 211, 252)
C_PEN = (248, 250, 252)
C_MUTED = (100, 116, 139)
C_WHITE = (255, 255, 255)


def lerp(a: int, b: int, t: float) -> int:
    return int(a + (b - a) * t)


def lerp_rgb(
    a: tuple[int, int, int],
    b: tuple[int, int, int],
    t: float,
) -> tuple[int, int, int]:
    return (lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t))


def stroke_points(w: int, h: int) -> list[tuple[float, float]]:
    """Handwriting-like curve: lower-left → upper-right."""
    points: list[tuple[float, float]] = []
    for i in range(0, 101, 1):
        t = i / 100.0
        x = w * (0.24 + 0.50 * t + 0.03 * math.sin(t * math.pi * 2.2))
        y = h * (0.74 - 0.42 * t + 0.06 * math.sin(t * math.pi * 1.6))
        points.append((x, y))
    return points


def draw_tapered_stroke(
    draw: ImageDraw.ImageDraw,
    points: list[tuple[float, float]],
    max_width: float,
    color: tuple[int, int, int],
) -> None:
    n = len(points)
    for i, (x, y) in enumerate(points):
        taper = math.sin(math.pi * i / max(n - 1, 1))
        radius = max(1.0, max_width * (0.28 + 0.72 * taper) * 0.5)
        draw.ellipse(
            (x - radius, y - radius, x + radius, y + radius),
            fill=color,
        )


def draw_pen_nib(
    draw: ImageDraw.ImageDraw,
    w: int,
    h: int,
    tip_x: float,
    tip_y: float,
    angle_deg: float,
) -> None:
    angle = math.radians(angle_deg)
    body_len = w * 0.26
    half_w = w * 0.038

    bx = tip_x - body_len * math.cos(angle)
    by = tip_y - body_len * math.sin(angle)
    dx = math.sin(angle) * half_w
    dy = -math.cos(angle) * half_w

    body = [
        (tip_x, tip_y),
        (tip_x - w * 0.09 * math.cos(angle), tip_y - w * 0.09 * math.sin(angle)),
        (bx + dx, by + dy),
        (bx - dx, by - dy),
    ]
    draw.polygon(body, fill=C_PEN)
    draw.ellipse(
        (tip_x - w * 0.028, tip_y - w * 0.028, tip_x + w * 0.028, tip_y + w * 0.028),
        fill=C_INK,
    )


def draw_kakipen_mark(draw: ImageDraw.ImageDraw, w: int, h: int) -> None:
    points = stroke_points(w, h)
    stroke_w = max(4, w // 22)
    draw_tapered_stroke(draw, points, stroke_w * 1.18, C_INK_SOFT)
    draw_tapered_stroke(draw, points, stroke_w, C_INK)

    tip_x, tip_y = points[0]
    nxt_x, nxt_y = points[min(8, len(points) - 1)]
    angle = math.degrees(math.atan2(nxt_y - tip_y, nxt_x - tip_x)) + 180
    draw_pen_nib(draw, w, h, tip_x, tip_y, angle)


def draw_canvas_frame(draw: ImageDraw.ImageDraw, w: int, h: int) -> tuple[float, float, float, float]:
    inset = w * 0.11
    box = (inset, inset, w - inset, h - inset)
    radius = int(w * 0.08)
    draw.rounded_rectangle(box, radius=radius, fill=C_CANVAS, outline=C_BORDER, width=max(1, w // 96))
    return box


def draw_host_overlay(draw: ImageDraw.ImageDraw, box: tuple[float, float, float, float]) -> None:
    x0, y0, x1, y1 = box
    r = (x1 - x0) * 0.045
    line_w = max(1, int((x1 - x0) // 80))
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    seats = (
        (x0 + (x1 - x0) * 0.18, y0 + (y1 - y0) * 0.20),
        (x1 - (x1 - x0) * 0.18, y0 + (y1 - y0) * 0.20),
        (x0 + (x1 - x0) * 0.18, y1 - (y1 - y0) * 0.20),
        (x1 - (x1 - x0) * 0.18, y1 - (y1 - y0) * 0.20),
    )
    for sx, sy in seats:
        draw.line([(cx, cy), (sx, sy)], fill=C_MUTED, width=line_w)
    for sx, sy in seats:
        draw.ellipse((sx - r, sy - r, sx + r, sy + r), fill=C_INK_SOFT)
    draw.ellipse((cx - r * 1.1, cy - r * 1.1, cx + r * 1.1, cy + r * 1.1), fill=C_INK)


def draw_layout_overlay(draw: ImageDraw.ImageDraw, box: tuple[float, float, float, float]) -> None:
    x0, y0, x1, y1 = box
    cols, rows = 3, 2
    gap = (x1 - x0) * 0.04
    cell_w = ((x1 - x0) - gap * (cols + 1)) / cols
    cell_h = ((y1 - y0) - gap * (rows + 1)) / rows
    radius = max(1, int(cell_w * 0.12))
    for row in range(rows):
        for col in range(cols):
            cx = x0 + gap + col * (cell_w + gap)
            cy = y0 + gap + row * (cell_h + gap)
            fill = C_BORDER if (row, col) != (0, 1) else C_MUTED
            draw.rounded_rectangle(
                (cx, cy, cx + cell_w, cy + cell_h),
                radius=radius,
                outline=fill,
                width=max(1, int((x1 - x0) // 96)),
            )


def draw_kigo_watermark(draw: ImageDraw.ImageDraw, w: int, h: int, font: ImageFont.ImageFont) -> None:
    if w < 180:
        return
    text = "書"
    bbox = draw.textbbox((0, 0), text, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    draw.text(
        (w * 0.58 - tw / 2, h * 0.18 - th / 2),
        text,
        fill=(45, 55, 72),
        font=font,
    )


def render_background(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    cx = cy = size / 2
    max_r = size * 0.72
    for y in range(size):
        for x in range(size):
            t = y / max(size - 1, 1)
            base = lerp_rgb(C_BG_DEEP, C_BG, t)
            dist = math.hypot(x - cx, y - cy) / max_r
            vignette = min(1.0, dist * 0.35)
            color = lerp_rgb(base, C_BG_DEEP, vignette * 0.45)
            px[x, y] = (*color, 255)

    radius = int(size * 0.22)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(img, mask=mask)
    return out


def render_symbol(draw: ImageDraw.ImageDraw, w: int, h: int, variant: str) -> None:
    box = draw_canvas_frame(draw, w, h)

    if variant == "layout":
        draw_layout_overlay(draw, box)

    if variant == "host":
        draw_host_overlay(draw, box)

    if w >= 180:
        font = load_font(int(w * 0.34))
        draw_kigo_watermark(draw, w, h, font)

    draw_kakipen_mark(draw, w, h)


VARIANTS: dict[str, dict] = {
    "host": {"dir": REPO / "src/KakiMoni.Host/Assets", "variant": "host"},
    "client": {"dir": REPO / "src/KakiMoni.Client/Assets", "variant": "client"},
    "layout": {"dir": REPO / "src/KakiMoni.Layout/Assets", "variant": "layout"},
}


def render_square(meta: dict, size: int) -> Image.Image:
    render_size = max(size * 4, 512)
    base = render_background(render_size)
    draw = ImageDraw.Draw(base)
    render_symbol(draw, render_size, render_size, meta["variant"])
    if render_size != size:
        return base.resize((size, size), Image.Resampling.LANCZOS)
    return base


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for candidate in (
        Path(r"C:\Windows\Fonts\YuGothM.ttc"),
        Path(r"C:\Windows\Fonts\meiryo.ttc"),
        Path(r"C:\Windows\Fonts\segoeui.ttf"),
    ):
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def render_wide(meta: dict, width: int, height: int, name: str) -> Image.Image:
    img = Image.new("RGBA", (width, height), (*C_BG, 255))
    draw = ImageDraw.Draw(img)

    icon_size = int(min(width, height) * 0.58)
    icon = render_square(meta, icon_size)
    x = int(width * 0.07)
    y = (height - icon_size) // 2
    img.paste(icon, (x, y), icon)

    font = load_font(max(26, height // 9))
    sub_font = load_font(max(16, height // 14))
    labels = {
        "host": ("kakipen", "KakiMoni 親機"),
        "client": ("kakipen", "KakiMoni 子機"),
        "layout": ("kakipen", "KakiMoni Layout"),
    }
    brand, title = labels.get(name, ("kakipen", "KakiMoni"))
    label_x = x + icon_size + int(width * 0.045)
    draw.text((label_x, height * 0.34), brand, fill=C_INK_SOFT, font=font)
    draw.text((label_x, height * 0.52), title, fill=C_WHITE, font=sub_font)
    return img


def save_ico(path: Path, master: Image.Image) -> None:
    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = [master.resize((s, s), Image.Resampling.LANCZOS).convert("RGBA") for s in sizes]
    frames[0].save(
        path,
        format="ICO",
        sizes=[(s, s) for s in sizes],
        append_images=frames[1:],
    )


def write_assets(name: str) -> None:
    meta = VARIANTS[name]
    out_dir: Path = meta["dir"]
    out_dir.mkdir(parents=True, exist_ok=True)

    master = render_square(meta, 256)
    save_ico(out_dir / "AppIcon.ico", master)
    render_square(meta, 24).save(out_dir / "Square44x44Logo.targetsize-24_altform-unplated.png")
    render_square(meta, 48).save(out_dir / "Square44x44Logo.targetsize-48_altform-lightunplated.png")
    render_square(meta, 50).save(out_dir / "StoreLogo.png")
    render_square(meta, 48).save(out_dir / "LockScreenLogo.scale-200.png")
    render_square(meta, 88).save(out_dir / "Square44x44Logo.scale-200.png")
    render_square(meta, 300).save(out_dir / "Square150x150Logo.scale-200.png")
    render_wide(meta, 620, 300, name).save(out_dir / "Wide310x150Logo.scale-200.png")
    render_wide(meta, 1240, 600, name).save(out_dir / "SplashScreen.scale-200.png")
    print(f"[{name}] -> {out_dir}")


def main(argv: list[str]) -> int:
    targets = argv or list(VARIANTS.keys())
    for name in targets:
        if name not in VARIANTS:
            print(f"Unknown variant: {name}", file=sys.stderr)
            return 1
        write_assets(name)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
