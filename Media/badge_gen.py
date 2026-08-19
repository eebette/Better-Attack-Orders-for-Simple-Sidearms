#!/usr/bin/env python3
"""BAO badge + Workshop preview.

Same geometry system as the CE+SS suite badges (300x100 circle + full-width bar
+ ring knockout; 512 preview) so the family reads as one set. Identity: VIOLET
accent — deliberately far from teal (Loadout Quality), amber (Loadouts), olive
(Patch) and red (Tactics); steel blue was tried first and read as a washed-out
teal. Art is self-drawn pistols, never CE-derived: this is a Simple Sidearms
mod with no CE dependency.

Run from Media/: python3 badge_gen.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONT = "/usr/share/fonts/dejavu-sans-fonts/DejaVuSansCondensed-Bold.ttf"
BLACK = (0, 0, 0, 255)
WHITE = (255, 255, 255, 255)
VIOLET = (154, 104, 224, 255)


def pistol(d, ox, oy, s, flip=False):
    """Semi-auto pistol silhouette pointing right; ox,oy = top-left of slide."""
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


def render_badge():
    S = 4
    W, H = 300 * S, 100 * S
    bar = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    db = ImageDraw.Draw(bar)
    db.rectangle([0, 25 * S, 300 * S, 74 * S], fill=BLACK)
    hole = Image.new("L", (W, H), 0)
    dh = ImageDraw.Draw(hole)
    cx, cy, r, gap = 50 * S, 50 * S, 50 * S, 5 * S
    dh.ellipse([cx - (r + gap), cy - (r + gap), cx + (r + gap), cy + (r + gap)], fill=255)
    dh.rectangle([0, 0, 5 * S, H], fill=255)
    bar.putalpha(Image.composite(Image.new("L", (W, H), 0), bar.getchannel("A"), hole))

    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img.alpha_composite(bar)
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BLACK)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=VIOLET, width=3 * S)
    pistol(d, 21 * S, 22 * S, 0.95 * S)
    pistol(d, 33 * S, 62 * S, 0.55 * S, flip=True)

    f1 = ImageFont.truetype(FONT, 14 * S)
    f2 = ImageFont.truetype(FONT, 10 * S)
    CX = 202 * S
    t1 = "BETTER ATTACK ORDERS"
    w1 = d.textlength(t1, font=f1)
    d.text((CX - w1 / 2, 33 * S), t1, font=f1, fill=WHITE)
    t2 = "for SIMPLE SIDEARMS"
    K = 1.6 * S
    w2 = sum(d.textlength(c, font=f2) + K for c in t2) - K
    x = CX - w2 / 2
    for ch in t2:
        d.text((x, 55 * S), ch, font=f2, fill=VIOLET)
        x += d.textlength(ch, font=f2) + K
    img.resize((300, 100), Image.LANCZOS).save(os.path.join(HERE, "Badge_BAO.png"))
    print("wrote Badge_BAO.png")


def render_preview():
    P = 4
    W = H = 512 * P
    img = Image.new("RGBA", (W, H), (12, 12, 12, 255))
    d = ImageDraw.Draw(img)
    cx, cy, r = 256 * P, 190 * P, 140 * P
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BLACK, outline=VIOLET, width=8 * P)

    # the deadlock pair: one in hand, one waiting
    pistol(d, (256 - 95) * P, 120 * P, 3.1 * P)
    pistol(d, (256 - 60) * P, 225 * P, 1.9 * P, flip=True)

    f1 = ImageFont.truetype(FONT, 36 * P)
    f2 = ImageFont.truetype(FONT, 30 * P)
    f3 = ImageFont.truetype(FONT, 20 * P)
    for text, font, y, color in [
        ("BETTER ATTACK ORDERS", f1, 362 * P, WHITE),
        ("for SIMPLE SIDEARMS", f2, 406 * P, WHITE),
        ("EVERY CARRIED WEAPON COUNTS", f3, 456 * P, VIOLET),
    ]:
        w = d.textlength(text, font=font)
        d.text(((W - w) / 2, y), text, font=font, fill=color)
    img.resize((512, 512), Image.LANCZOS).save(os.path.join(HERE, "..", "About", "Preview.png"))
    print("wrote About/Preview.png")


if __name__ == "__main__":
    render_badge()
    render_preview()
