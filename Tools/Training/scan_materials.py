# -*- coding: utf-8 -*-
"""扫描 训练素材/ 生成清单（数量、格式、尺寸、重复），供 Agent 归纳。

用法：
  python Tools/Training/scan_materials.py
输出：
  训练素材/清单.json
  训练素材/清单.md
"""
import hashlib
import json
import os
import wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TRAIN = os.path.join(ROOT, "训练素材")
CATS = ["音乐", "文案", "图像"]
SKIP = {"README.md", "归纳.md"}

IMAGE_EXT = {".png", ".jpg", ".jpeg", ".webp", ".bmp"}
AUDIO_EXT = {".wav", ".mp3", ".flac", ".ogg", ".m4a"}
TEXT_EXT = {".txt", ".md", ".csv", ".json"}
DOC_EXT = {".docx", ".doc", ".pdf"}


def md5(path, chunk=1 << 20):
    h = hashlib.md5()
    with open(path, "rb") as f:
        while True:
            b = f.read(chunk)
            if not b:
                break
            h.update(b)
    return h.hexdigest()


def image_size(path):
    try:
        from PIL import Image
        with Image.open(path) as im:
            return im.size
    except Exception:
        return None


def wav_duration(path):
    try:
        with wave.open(path, "rb") as w:
            return round(w.getnframes() / float(w.getframerate()), 2)
    except Exception:
        return None


def main():
    result = {"root": "训练素材", "categories": {}, "duplicates": []}
    seen = {}

    for cat in CATS:
        folder = os.path.join(TRAIN, cat)
        items = []
        if os.path.isdir(folder):
            for name in sorted(os.listdir(folder)):
                path = os.path.join(folder, name)
                if not os.path.isfile(path) or name in SKIP:
                    continue
                ext = os.path.splitext(name)[1].lower()
                info = {
                    "name": name,
                    "ext": ext,
                    "size_kb": round(os.path.getsize(path) / 1024, 1),
                    "md5": md5(path),
                }
                if ext in IMAGE_EXT:
                    size = image_size(path)
                    if size:
                        info["width"], info["height"] = size
                elif ext == ".wav":
                    dur = wav_duration(path)
                    if dur:
                        info["duration_sec"] = dur
                items.append(info)

                if info["md5"] in seen:
                    result["duplicates"].append([seen[info["md5"]], cat + "/" + name])
                else:
                    seen[info["md5"]] = cat + "/" + name

        result["categories"][cat] = {
            "count": len(items),
            "total_mb": round(sum(i["size_kb"] for i in items) / 1024, 2),
            "items": items,
        }

    with open(os.path.join(TRAIN, "清单.json"), "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    lines = ["# 训练素材清单", "", "> 由 `Tools/Training/scan_materials.py` 自动生成，勿手改。", ""]
    for cat in CATS:
        c = result["categories"][cat]
        lines.append("## %s（%d 个，%.2f MB）" % (cat, c["count"], c["total_mb"]))
        lines.append("")
        if not c["items"]:
            lines.append("_空_")
            lines.append("")
            continue
        lines.append("| 文件 | 格式 | 大小 | 尺寸/时长 |")
        lines.append("|---|---|---:|---|")
        for i in c["items"]:
            extra = ""
            if "width" in i:
                extra = "%dx%d" % (i["width"], i["height"])
            elif "duration_sec" in i:
                extra = "%.2fs" % i["duration_sec"]
            lines.append("| %s | %s | %.1f KB | %s |" % (i["name"], i["ext"], i["size_kb"], extra))
        lines.append("")

    if result["duplicates"]:
        lines.append("## 重复文件")
        lines.append("")
        for a, b in result["duplicates"]:
            lines.append("- %s == %s" % (a, b))
        lines.append("")

    with open(os.path.join(TRAIN, "清单.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    total = sum(result["categories"][c]["count"] for c in CATS)
    print("扫描完成：%d 个文件" % total)
    for cat in CATS:
        c = result["categories"][cat]
        print("  %s: %d 个，%.2f MB" % (cat, c["count"], c["total_mb"]))
    print("输出：训练素材/清单.json、训练素材/清单.md")


if __name__ == "__main__":
    main()
