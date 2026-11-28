# -*- coding: utf-8 -*-
"""调用本地 ComfyUI 的 /prompt API 生成图片，保存到项目并写同名 .txt 记录参数。

用法：
  python Tools/ComfyUI/generate.py --prompt "..." --out 训练成果/图像/设备/x.png
  python Tools/ComfyUI/generate.py --list
"""
import argparse
import json
import os
import random
import time
import urllib.parse
import urllib.request
import uuid

SERVER = "http://127.0.0.1:8188"


def api(path, data=None):
    url = SERVER + path
    if data is None:
        req = urllib.request.Request(url)
    else:
        req = urllib.request.Request(url, data=json.dumps(data).encode("utf-8"),
                                     headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8"))


def list_checkpoints():
    info = api("/object_info/CheckpointLoaderSimple")
    return info["CheckpointLoaderSimple"]["input"]["required"]["ckpt_name"][0]


def upload_image(path):
    boundary = "----comfy" + uuid.uuid4().hex
    with open(path, "rb") as f:
        data = f.read()
    name = os.path.basename(path)
    body = b""
    body += ("--%s\r\nContent-Disposition: form-data; name=\"image\"; filename=\"%s\"\r\n"
             "Content-Type: image/png\r\n\r\n" % (boundary, name)).encode("utf-8")
    body += data + b"\r\n"
    body += ("--%s\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n" % boundary).encode("utf-8")
    body += ("--%s--\r\n" % boundary).encode("utf-8")
    req = urllib.request.Request(SERVER + "/upload/image", data=body,
                                 headers={"Content-Type": "multipart/form-data; boundary=" + boundary})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))["name"]


def build_workflow(args):
    wf = {
        "1": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": args.checkpoint}},
        "2": {"class_type": "CLIPTextEncode",
              "inputs": {"text": args.prompt, "clip": ["1", 1]}},
        "3": {"class_type": "CLIPTextEncode",
              "inputs": {"text": args.negative, "clip": ["1", 1]}},
        "4": {"class_type": "EmptyLatentImage",
              "inputs": {"width": args.width, "height": args.height, "batch_size": 1}},
        "5": {"class_type": "KSampler",
              "inputs": {"seed": args.seed, "steps": args.steps, "cfg": args.cfg,
                         "sampler_name": args.sampler, "scheduler": args.scheduler,
                         "denoise": args.denoise, "model": ["1", 0], "positive": ["2", 0],
                         "negative": ["3", 0], "latent_image": ["4", 0]}},
        "6": {"class_type": "VAEDecode",
              "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage",
              "inputs": {"filename_prefix": args.prefix, "images": ["6", 0]}},
    }
    if getattr(args, "mask", ""):
        wf["10"] = {"class_type": "LoadImage", "inputs": {"image": args.init_image_name}}
        wf["11"] = {"class_type": "LoadImageMask",
                    "inputs": {"image": args.mask_name, "channel": "red"}}
        wf["12"] = {"class_type": "VAEEncodeForInpaint",
                    "inputs": {"pixels": ["10", 0], "vae": ["1", 2],
                               "mask": ["11", 0], "grow_mask_by": 8}}
        wf["5"]["inputs"]["latent_image"] = ["12", 0]
    elif args.init_image:
        wf["10"] = {"class_type": "LoadImage", "inputs": {"image": args.init_image_name}}
        wf["11"] = {"class_type": "VAEEncode",
                    "inputs": {"pixels": ["10", 0], "vae": ["1", 2]}}
        wf["5"]["inputs"]["latent_image"] = ["11", 0]
    return wf


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--prompt", default="")
    ap.add_argument("--negative", default="text, logo, watermark, blurry, low quality, extra objects, people, cluttered background")
    ap.add_argument("--out", default="")
    ap.add_argument("--checkpoint", default="")
    ap.add_argument("--width", type=int, default=1024)
    ap.add_argument("--height", type=int, default=1024)
    ap.add_argument("--steps", type=int, default=28)
    ap.add_argument("--cfg", type=float, default=6.0)
    ap.add_argument("--seed", type=int, default=-1)
    ap.add_argument("--sampler", default="euler")
    ap.add_argument("--scheduler", default="normal")
    ap.add_argument("--prefix", default="milktea")
    ap.add_argument("--timeout", type=int, default=600)
    ap.add_argument("--init-image", default="")
    ap.add_argument("--mask", default="")
    ap.add_argument("--denoise", type=float, default=1.0)
    ap.add_argument("--list", action="store_true")
    args = ap.parse_args()

    if args.list:
        for c in list_checkpoints():
            print(c)
        return

    if not args.prompt or not args.out:
        raise SystemExit("需要 --prompt 和 --out")

    if not args.checkpoint:
        args.checkpoint = list_checkpoints()[0]
    if args.seed < 0:
        args.seed = random.randint(0, 2 ** 31 - 1)

    args.init_image_name = ""
    args.mask_name = ""
    if args.init_image:
        args.init_image_name = upload_image(args.init_image)
    if args.mask:
        args.mask_name = upload_image(args.mask)
        print("uploaded", args.init_image_name)

    client_id = str(uuid.uuid4())
    wf = build_workflow(args)
    resp = api("/prompt", {"prompt": wf, "client_id": client_id})
    pid = resp["prompt_id"]
    print("prompt_id", pid)

    start = time.time()
    hist = None
    while time.time() - start < args.timeout:
        h = api("/history/" + pid)
        if pid in h:
            hist = h[pid]
            break
        time.sleep(2)
    if hist is None:
        raise SystemExit("超时：任务仍在队列，可用 /history 查询")

    if not hist.get("outputs") or "7" not in hist["outputs"]:
        msgs = hist.get("status", {}).get("messages", [])
        err = [m for m in msgs if m[0] == "execution_error"]
        raise SystemExit("生成失败：" + json.dumps(err[-1][1].get("exception_message", "")[:300], ensure_ascii=False))

    imgs = hist["outputs"]["7"]["images"]
    img = imgs[0]
    q = urllib.parse.urlencode({"filename": img["filename"],
                                "subfolder": img.get("subfolder", ""),
                                "type": img.get("type", "output")})
    with urllib.request.urlopen(SERVER + "/view?" + q, timeout=60) as r:
        data = r.read()

    out = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "wb") as f:
        f.write(data)

    meta = ("checkpoint: %s\nprompt: %s\nnegative: %s\nsize: %dx%d\nsteps: %d\ncfg: %s\nseed: %d\n"
            "sampler: %s / %s\ncomfyui: %s\n" %
            (args.checkpoint, args.prompt, args.negative, args.width, args.height,
             args.steps, args.cfg, args.seed, args.sampler, args.scheduler, SERVER))
    with open(os.path.splitext(out)[0] + ".txt", "w", encoding="utf-8") as f:
        f.write(meta)

    print(json.dumps({"out": out, "seed": args.seed, "checkpoint": args.checkpoint,
                      "seconds": round(time.time() - start, 1)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
