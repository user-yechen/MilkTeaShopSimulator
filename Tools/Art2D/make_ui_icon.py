# -*- coding: utf-8 -*-
"""按 spec 生成扁平 UI 图标（PIL，4x 超采样）。

用法：
  python Tools/Art2D/make_ui_icon.py --spec ArtSource/2D/specs/icon_coin.json
spec 字段：id / name / type(coin|gear|star|button|panel) / size / colors / output / text
"""
import argparse
import json
import os
import math
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

SS = 4  # 超采样倍数
FONT_BOLD = "C:/Windows/Fonts/msyhbd.ttc"
FONT_REG = "C:/Windows/Fonts/msyh.ttc"


def hex2rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def vertical_gradient(w, h, top, bottom):
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / max(h - 1, 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
        d.line([(0, y), (w, y)], fill=c)
    return img


def masked_gradient(size, mask, top, bottom):
    grad = vertical_gradient(size, size, top, bottom).convert("RGBA")
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(grad, (0, 0), mask)
    return out


def add_shadow(base, shape_mask, offset=(0, 10), blur=18, alpha=70):
    size = base.size[0]
    sh = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    sh.paste((0, 0, 0, alpha), (0, 0), shape_mask)
    sh = sh.filter(ImageFilter.GaussianBlur(blur))
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(sh, (offset[0], offset[1]))
    out.alpha_composite(base)
    return out


def font(size, bold=True):
    try:
        return ImageFont.truetype(FONT_BOLD if bold else FONT_REG, size)
    except Exception:
        return ImageFont.load_default()


def draw_coin(size, colors):
    c1, c2, c3 = colors
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    r = int(size * 0.46)
    box = [size // 2 - r, size // 2 - r, size // 2 + r, size // 2 + r]
    d.ellipse(box, fill=255)
    body = masked_gradient(size, mask, hex2rgb(c1), hex2rgb(c2))
    img = add_shadow(body, mask)

    # 内环
    d2 = ImageDraw.Draw(img)
    rr = int(size * 0.37)
    d2.ellipse([size // 2 - rr, size // 2 - rr, size // 2 + rr, size // 2 + rr],
               outline=hex2rgb(c3) + (140,), width=max(2, size // 90))

    # 高光
    hi = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    dh = ImageDraw.Draw(hi)
    dh.ellipse([size * 0.24, size * 0.16, size * 0.62, size * 0.40], fill=(255, 255, 255, 60))
    img.alpha_composite(hi.filter(ImageFilter.GaussianBlur(size * 0.02)))

    # ¥ 符号
    f = font(int(size * 0.42))
    d2.text((size / 2, size / 2), "¥", font=f, fill=hex2rgb(c3) + (255,), anchor="mm", stroke_width=0)
    return body, img


def draw_gear(size, colors):
    c1, c2, c3 = colors
    mask = Image.new("L", (size, size), 0)
    teeth = 8
    for i in range(teeth):
        ang = 2 * math.pi * i / teeth
        cx = size / 2 + math.cos(ang) * size * 0.36
        cy = size / 2 + math.sin(ang) * size * 0.36
        tw, th = size * 0.16, size * 0.12
        layer = Image.new("L", (size, size), 0)
        ImageDraw.Draw(layer).rounded_rectangle(
            [cx - tw / 2, cy - th / 2, cx + tw / 2, cy + th / 2],
            radius=size * 0.02, fill=255)
        layer = layer.rotate(math.degrees(ang) + 90, center=(cx, cy))
        mask = ImageChops.lighter(mask, layer)

    body_r = int(size * 0.34)
    ImageDraw.Draw(mask).ellipse(
        [size // 2 - body_r, size // 2 - body_r, size // 2 + body_r, size // 2 + body_r], fill=255)

    body = masked_gradient(size, mask, hex2rgb(c1), hex2rgb(c2))
    img = add_shadow(body, mask)

    # 中心孔（透明）
    hr = int(size * 0.13)
    px = img.load()
    for y in range(size):
        for x in range(size):
            if (x - size / 2) ** 2 + (y - size / 2) ** 2 < hr * hr:
                px[x, y] = (0, 0, 0, 0)

    d3 = ImageDraw.Draw(img)
    d3.ellipse([size // 2 - hr - 2, size // 2 - hr - 2, size // 2 + hr + 2, size // 2 + hr + 2],
               outline=hex2rgb(c3) + (120,), width=max(2, size // 120))
    return body, img


def draw_star(size, colors):
    c1, c2, c3 = colors
    pts = []
    cx = cy = size / 2
    R = size * 0.46
    r = R * 0.42
    for i in range(10):
        ang = -math.pi / 2 + i * math.pi / 5
        rad = R if i % 2 == 0 else r
        pts.append((cx + math.cos(ang) * rad, cy + math.sin(ang) * rad))
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)
    body = masked_gradient(size, mask, hex2rgb(c1), hex2rgb(c2))
    img = add_shadow(body, mask)
    hi = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(hi).ellipse(
        [size * 0.30, size * 0.18, size * 0.52, size * 0.34], fill=(255, 255, 255, 70))
    img.alpha_composite(hi.filter(ImageFilter.GaussianBlur(size * 0.02)))
    return body, img


def draw_panel(size, colors):
    c1, c2, c3 = colors
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle([size * 0.04, size * 0.04, size * 0.96, size * 0.96],
                        radius=size * 0.12, fill=255)
    body = masked_gradient(size, mask, hex2rgb(c1), hex2rgb(c2))
    img = add_shadow(body, mask)
    d2 = ImageDraw.Draw(img)
    d2.rounded_rectangle([size * 0.04, size * 0.04, size * 0.96, size * 0.96],
                         radius=size * 0.12, outline=hex2rgb(c3) + (90,), width=max(2, size // 160))
    return body, img


def apply_state(img, state):
    if state == "normal":
        return img
    if state == "pressed":
        a = img.getchannel("A")
        rgb = img.convert("RGB").point(lambda v: int(v * 0.88))
        out = rgb.convert("RGBA")
        out.putalpha(a)
        return out
    if state == "disabled":
        rgb = img.convert("RGB")
        gray = rgb.convert("L").convert("RGB")
        blended = Image.blend(rgb, gray, 0.6)
        out = blended.convert("RGBA")
        a = img.getchannel("A").point(lambda v: int(v * 0.5))
        out.putalpha(a)
        return out
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", required=True)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    with open(args.spec, "r", encoding="utf-8") as f:
        spec = json.load(f)

    w, h = spec.get("size", [512, 512])
    size = max(w, h) * SS
    colors = spec.get("palette") or spec.get("colors") or ["#F0C000", "#C08000", "#605000"]
    kind = spec.get("type") or "coin"

    if kind == "coin":
        body, art = draw_coin(size, colors)
    elif kind == "gear":
        body, art = draw_gear(size, colors)
    elif kind == "star":
        body, art = draw_star(size, colors)
    elif kind in ("panel", "button"):
        body, art = draw_panel(size, colors)
    else:
        raise SystemExit("未知类型: " + kind)

    out_dir = os.path.dirname(spec.get("output", "out.png"))
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    states = spec.get("states") or ["normal"]
    written = []
    for st in states:
        img = apply_state(art, st).resize((w, h), Image.LANCZOS)
        name = spec.get("output", "out.png")
        if len(states) > 1:
            root, ext = os.path.splitext(name)
            name = "%s_%s%s" % (root, st, ext)
        img.save(name)
        written.append(name)

    # 预览联络图（深底）
    prev = Image.new("RGB", (w * len(states) + 20 * (len(states) + 1), h + 40), (40, 42, 48))
    for i, st in enumerate(states):
        im = Image.open(written[i]).convert("RGBA")
        bg = Image.new("RGBA", (w, h), (90, 92, 98, 255))
        bg.alpha_composite(im)
        prev.paste(bg.convert("RGB"), (20 + i * (w + 20), 20))
    prev_path = os.path.splitext(spec.get("output", "out.png"))[0] + "_preview.png"
    prev.save(prev_path)
    print("WROTE", ", ".join(written), "| preview:", prev_path)


if __name__ == "__main__":
    main()
