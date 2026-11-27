$comfy = 'D:\ComfyUI\ComfyUI-Installs\ComfyUI\ComfyUI'
$cfg = Join-Path $PSScriptRoot 'extra_model_paths.yaml'
if (-not (Test-Path $comfy)) { throw "ComfyUI not found: $comfy" }
Set-Location $comfy
& "$comfy\.venv\Scripts\python.exe" main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch --extra-model-paths-config $cfg
