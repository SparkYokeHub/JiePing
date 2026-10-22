# -*- coding: utf-8 -*-
"""Generate the multi-resolution app icon for JiePing."""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))

SIZE = 256
SS = 4
BG_TOP = (24, 27, 34)
BG_BOTTOM = (13, 15, 19)
INK = (247, 244, 236)
ACCENT = (255, 104, 72)
MUTED = (160, 166, 178)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def render(size):
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Rounded dark tile with a soft vertical gradient.
    radius = int(s * 0.225)
    tile = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    td = ImageDraw.Draw(tile)
    for y in range(s):
        td.line([(0, y), (s, y)], fill=lerp(BG_TOP, BG_BOTTOM, y / max(1, s - 1)) + (255,))
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s - 1, s - 1], radius=radius, fill=255)
    img.alpha_composite(Image.composite(tile, Image.new("RGBA", (s, s), (0, 0, 0, 0)), mask))
    d = ImageDraw.Draw(img)

    # Hairline inner rim.
    d.rounded_rectangle(
        [int(s * 0.012), int(s * 0.012), s - int(s * 0.012), s - int(s * 0.012)],
        radius=int(radius * 0.96), outline=(255, 255, 255, 26), width=max(1, int(s * 0.005)),
    )

    # Minimal viewfinder: four corner brackets.
    inset = int(s * 0.235)
    arm = int(s * 0.135)
    stroke = max(2, int(s * 0.052))
    left, top, right, bottom = inset, inset, s - inset, s - inset
    for x, y, dx, dy in (
        (left, top, 1, 1),
        (right, top, -1, 1),
        (left, bottom, 1, -1),
        (right, bottom, -1, -1),
    ):
        d.line([(x, y), (x + dx * arm, y)], fill=INK + (255,), width=stroke)
        d.line([(x, y), (x, y + dy * arm)], fill=INK + (255,), width=stroke)

    # Coral shutter dot at the optical centre.
    cy = s // 2
    r = int(s * 0.058)
    d.ellipse([s // 2 - r, cy - r, s // 2 + r, cy + r], fill=ACCENT + (255,))

    return img.resize((size, size), Image.LANCZOS)


sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
images = [render(size) for size in sizes]
images[-1].save(
    os.path.join(HERE, "app.ico"),
    format="ICO",
    sizes=[(size, size) for size in sizes],
    append_images=images[:-1],
)
# Also keep a high-res PNG for documentation / store art.
render(512).save(os.path.join(HERE, "app_512.png"))
print("wrote app.ico", [im.size for im in images])
