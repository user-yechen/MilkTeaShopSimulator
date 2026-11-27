# ComfyUI 集成

> 本地 ComfyUI 装在 `D:\ComfyUI\ComfyUI-Installs\ComfyUI\ComfyUI`，模型在 `D:\ComfyUI\ShareModles`。
> 项目通过 `generate.py` 调它的 HTTP API 出图，结果放 `训练成果/`。

## 启动

```powershell
powershell -ExecutionPolicy Bypass -File Tools\ComfyUI\start_comfyui.ps1
```

等价于：

```powershell
cd D:\ComfyUI\ComfyUI-Installs\ComfyUI\ComfyUI
.venv\Scripts\python.exe main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch `
  --extra-model-paths-config D:\UnityProject\milktea\Tools\ComfyUI\extra_model_paths.yaml
```

> 用 `.venv` 里的 Python，`standalone-env` 没有装 torch。

## 出图

```powershell
python Tools\ComfyUI\generate.py --prompt "..." --out 训练成果\图像\设备\加热机_概念_01.png
```

常用参数：

| 参数 | 说明 |
|---|---|
| `--checkpoint` | 模型文件名（`--list` 查看） |
| `--init-image` | 图生图底图（会先上传到 ComfyUI） |
| `--denoise` | 图生图重绘幅度，0.45–0.7 比较稳 |
| `--steps` / `--cfg` / `--seed` | 采样参数；seed 会写进同名 `.txt` |

## 模型现状

| 文件 | 大小 | 类型 | 能否直接用 |
|---|---:|---|---|
| theIWondermix_v7 | 7.1 GB | SDXL 完整 checkpoint（含 text encoder） | ✅ |
| ribeyeMixAnima_v10 | 4.2 GB | 仅 UNet（Flux 架构，无 CLIP） | ❌ 需要配套 text encoder / VAE |
| mygooV005Unturbo_v10 | 4.2 GB | 仅 UNet（Flux 架构） | ❌ 同上 |

## 已知问题

- API 报 `clip input is invalid: None` → 选到了 UNet-only 模型，换 `theIWondermix_v7`
- 生成失败时 `generate.py` 会打印 ComfyUI 的 `exception_message`，不会被 `KeyError` 吞掉
