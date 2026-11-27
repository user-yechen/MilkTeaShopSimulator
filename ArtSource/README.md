# ArtSource 说明

这里存放**不参与 Unity 导入**的美术源文件。Unity 只读 `Assets/`，所以 PSD / SVG / BLEND / 原始 GLB 放这里，避免被导入成冗余资源。

```
ArtSource/
  2D/
    concepts/   概念图、参考图（可来自 SD / 网图，注意许可）
    icons/      图标源文件（SVG / PSD / AI）
    ui/         面板、按钮源文件（PSD）
    renders/    产品图标渲染输出（512²，透明底）
  3D/
    specs/      每件资产的 spec JSON（模型师/QA 都按它验收）
    raw/        Tripo / Blender 原始输出（面数高、未修）
    final/      减面、重 UV、校色后的 GLB（可直接导入 Unity）
```

规则：

1. 每件资产**先写 spec**（参考 `3D/specs/_template.json`），再开工。
2. `raw/` 可以乱，`final/` 必须干净（命名、缩放、轴心、材质数都合规）。
3. `final/` 的 GLB 复制进 `Assets/模型/<类别>/` 后再导入 Unity。
4. 源文件与导出图同名，例如 `topping_pearl.psd` / `topping_pearl.png`。
