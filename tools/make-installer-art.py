"""Génère les images de l'installateur (installer/art) à partir de l'icône de l'appli.

python tools/make-installer-art.py
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "installer" / "art"
ICON = ROOT / "src" / "BetterFences" / "Assets" / "BetterFences.ico"
BG_TOP, BG_BOTTOM = (31, 38, 66), (21, 23, 29)
GLOWS = [((0.15, 0.10), (79, 124, 255)), ((0.95, 0.55), (139, 92, 246)), ((0.30, 0.95), (20, 184, 166))]


def logo():
    ico = Image.open(ICON)
    best = max(ico.info.get("sizes", [ico.size]))
    ico.size = best
    return ico.convert("RGBA")


def gradient(w, h, glow_strength=0.55):
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / max(1, h - 1)
        d.line([(0, y), (w, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(BG_TOP, BG_BOTTOM)))
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    r = max(w, h) * 0.38
    for (fx, fy), col in GLOWS:
        cx, cy = fx * w, fy * h
        g.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col + (round(255 * glow_strength * 0.45),))
    glow = glow.filter(ImageFilter.GaussianBlur(r * 0.55))
    img.paste(glow, (0, 0), glow)
    return img


def tile(size, color, alpha):
    """Petite tuile arrondie façon dossier Android (décor)."""
    t = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(t).rounded_rectangle([0, 0, size - 1, size - 1], radius=size * 0.3, fill=color + (alpha,))
    return t


def side_image(w, h):
    img = gradient(w, h).convert("RGBA")
    s = w / 164
    # Tuiles décoratives semi-transparentes
    for (x, y, sz, col, a) in [(18, 40, 30, (79, 124, 255), 70), (112, 70, 22, (236, 72, 153), 60),
                               (24, 230, 22, (34, 197, 94), 55), (116, 250, 34, (245, 158, 11), 55),
                               (70, 290, 16, (139, 92, 246), 70)]:
        tl = tile(round(sz * s), col, a)
        img.alpha_composite(tl, (round(x * s), round(y * s)))
    # Logo avec halo
    size = round(96 * s)
    lg = logo().resize((size, size), Image.LANCZOS)
    halo = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    cx, cy = w // 2, round(h * 0.42)
    ImageDraw.Draw(halo).ellipse([cx - size * 0.75, cy - size * 0.75, cx + size * 0.75, cy + size * 0.75],
                                 fill=(120, 150, 255, 90))
    img.alpha_composite(halo.filter(ImageFilter.GaussianBlur(size * 0.35)))
    img.alpha_composite(lg, (cx - size // 2, cy - size // 2))
    return img.convert("RGB")


def small_image(n):
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    lg = logo().resize((n, n), Image.LANCZOS)
    img.alpha_composite(lg)
    return img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for pct in (100, 125, 150, 200, 250):
        k = pct / 100
        side_image(round(164 * k), round(314 * k)).save(OUT / f"wizard-{pct}.png")
        small_image(round(55 * k)).save(OUT / f"small-{pct}.png")
    gradient(1280, 1000, glow_strength=0.35).save(OUT / "background.png")
    print("images écrites dans", OUT)


if __name__ == "__main__":
    main()
