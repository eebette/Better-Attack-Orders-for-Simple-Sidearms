#!/usr/bin/env python3
"""BAO Workshop preview (512x512). Self-contained pistol art — deliberately no
CE-derived assets: this is an SS-only mod."""
from PIL import Image, ImageDraw, ImageFont

S = 4
W = H = 512 * S
BLACK = (0, 0, 0, 255)
WHITE = (255, 255, 255, 255)
ACCENT = (74, 124, 168, 255)  # steel blue — outside the suite's palette on purpose

img = Image.new("RGBA", (W, H), (12, 12, 12, 255))
d = ImageDraw.Draw(img)
cx, cy, r = 256 * S, 190 * S, 140 * S
d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BLACK, outline=ACCENT, width=8 * S)


def pistol(d, ox, oy, s, flip=False):
    def P(pts):
        out = []
        for x, y in pts:
            if flip:
                x = 60 - x
            out.append((ox + x * s, oy + y * s))
        return out
    body = [(0, 4), (3, 4), (3, 2), (6, 2), (6, 4), (50, 4), (50, 2), (53, 2),
            (53, 4), (58, 4), (58, 13), (44, 13), (44, 17), (33, 17), (33, 27),
            (24, 27), (24, 19), (20, 19), (13, 40), (1, 40), (5, 17), (0, 15)]
    d.polygon(P(body), fill=WHITE)
    d.polygon(P([(27, 19), (31, 19), (31, 24), (27, 24)]), fill=BLACK)
    d.polygon(P([(25.5, 19), (27.5, 19), (27.5, 23.5), (25.5, 22.5)]), fill=WHITE)


# two pistols: the deadlock pair — one in hand, one waiting
pistol(d, (256 - 95) * S, 120 * S, 3.1 * S)
pistol(d, (256 - 60) * S, 225 * S, 1.9 * S, flip=True)

F = "/usr/share/fonts/dejavu-sans-fonts/DejaVuSansCondensed-Bold.ttf"
f1 = ImageFont.truetype(F, 36 * S)
f2 = ImageFont.truetype(F, 30 * S)
f3 = ImageFont.truetype(F, 20 * S)
for text, font, y, color in [
    ("BETTER ATTACK ORDERS", f1, 362 * S, WHITE),
    ("for SIMPLE SIDEARMS", f2, 406 * S, WHITE),
    ("EVERY CARRIED WEAPON COUNTS", f3, 456 * S, ACCENT),
]:
    w = d.textlength(text, font=font)
    d.text(((W - w) / 2, y), text, font=font, fill=color)

img.resize((512, 512), Image.LANCZOS).save(__file__.rsplit("/", 1)[0] + "/../About/Preview.png")
print("written")
