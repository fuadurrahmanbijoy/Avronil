"""Generates assets/anjana.png and assets/anjana.ico (minimal gradient squircle with two speed arrows)."""
from PIL import Image, ImageDraw
import os

S = 1024
here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "..", "assets")

def gradient(size, c1, c2):
    g = Image.new("RGB", (size, size))
    px = g.load()
    for y in range(size):
        for x in range(size):
            t = (x + y) / (2 * (size - 1))
            px[x, y] = tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))
    return g

grad = gradient(S, (59, 130, 246), (34, 211, 176))
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=int(S * 0.23), fill=255)
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
img.paste(grad, (0, 0), mask)

d = ImageDraw.Draw(img)
w = int(S * 0.075)
white = (255, 255, 255, 255)

def line(p1, p2):
    d.line([p1, p2], fill=white, width=w)
    for p in (p1, p2):
        d.ellipse((p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2), fill=white)

def arrow(x, up):
    top, bot, h = 0.29 * S, 0.71 * S, 0.105 * S
    x = x * S
    line((x, top), (x, bot))
    tip, base = (top, top + h) if up else (bot, bot - h)
    line((x, tip), (x - h, base))
    line((x, tip), (x + h, base))

arrow(0.355, up=True)
arrow(0.645, up=False)

png = img.resize((256, 256), Image.LANCZOS)
png.save(os.path.join(out, "anjana.png"))
img.resize((256, 256), Image.LANCZOS).save(
    os.path.join(out, "anjana.ico"),
    sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("icon written")
