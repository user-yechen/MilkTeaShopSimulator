# 设备概念图

> AI 生成的设备类 2D 资产（加热机、制冰机、封口机、货架等）。

## 要求

- 单个设备、白底或浅灰底、3/4 视角、主体占画面 70–85%
- 不要文字、水印、logo、背景道具
- 1024×1024 起，供 Tripo / TripoSR 或 UI 使用

## 命名

`<设备>_<风格>_<序号>.png`，如 `加热机_低模_01.png`。

## 配套

同名 `.txt` 写：

```
checkpoint: xxx.safetensors
prompt: ...
negative: ...
size: 1024x1024  steps: 28  cfg: 5.5  seed: 123456
```
