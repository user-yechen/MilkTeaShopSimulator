# -*- coding: utf-8 -*-
"""渲染图的图标化后处理：裁切、居中、描边、接触阴影，输出 512 透明 PNG。

用法：
  python Tools/Art2D/icon_post.py --in raw.png --out icon.png --size 512 --outline --shadow
"""
import argparse
import os
from PIL import Image, ImageFilter, ImageDraw, ImageChops

COLOR_OUTLINE = (150, 155, 160, 150)
COLOR_SHADOW = (40, 30, 20, 55)


def trim(img):
    bbox = img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    return img.crop(bbox) if bbox else img


def add_outline(img, px=2, color=COLOR_OUTLINE):
    mask = img.getchannel("A").point(lambda v: 255 if v > 10 else 0)
    grown = mask.filter(ImageFilter.MaxFilter(px * 2 + 1))
    edge = ImageChops.subtract(grown, mask)
    layer = Image.new("RGBA", img.size, color)
    layer.putalpha(edge.point(lambda v: int(color[3] * v / 255)))
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.alpha_composite(layer)
    out.alpha_composite(img)
    return out


def add_shadow(img, width_ratio=0.62, alpha=55):
    w, h = img.size
    bbox = img.getchannel("A").getbbox()
    if not bbox:
        return img
    x0, y0, x1, y1 = bbox
    obj_w = x1 - x0
    sh_w = int(obj_w * width_ratio)
    sh_h = max(8, int(sh_w * 0.12))
    cx = (x0 + x1) // 2
    cy = y1 - sh_h // 3

    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(shadow)
    d.ellipse([cx - sh_w // 2, cy - sh_h // 2, cx + sh_w // 2, cy + sh_h // 2],
              fill=(40, 30, 20, alpha))
    shadow = shadow.filter(ImageFilter.GaussianBlur(max(6, sh_h // 2)))

    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.alpha_composite(shadow)
    out.alpha_composite(img)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", required=True)
    ap.add_argument("--out", dest="dst", required=True)
    ap.add_argument("--size", type=int, default=512)
    ap.add_argument("--fill", type=float, default=0.86, help="物体占画面比例")
    ap.add_argument("--outline", action="store_true")
    ap.add_argument("--shadow", action="store_true")
    a = ap.parse_args()

    img = Image.open(a.src).convert("RGBA")
    img = trim(img)

    target = int(a.size * a.fill)
    scale = min(target / img.width, target / img.height)
    img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))),
                     Image.LANCZOS)

    canvas = Image.new("RGBA", (a.size, a.size), (0, 0, 0, 0))
    canvas.alpha_composite(img, ((a.size - img.width) // 2, (a.size - img.height) // 2))

    if a.shadow:
        canvas = add_shadow(canvas)
    if a.outline:
        canvas = add_outline(canvas)

    os.makedirs(os.path.dirname(os.path.abspath(a.dst)), exist_ok=True)
    canvas.save(a.dst)
    print("WROTE", a.dst, canvas.size)


if __name__ == "__main__":
    main()
