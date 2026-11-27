# 加热机 · TripoSR 试跑

> 2026-11-27　｜　输入：`ArtSource/2D/concepts/加热机_概念.png`（训练素材归纳后，用 Blender 参数化生成）
> 结论：**TripoSR 重建失败，出团块；同一份参数化几何直接导出 GLB 可用**

## 1. 输入

- 1024×1024、白底、3/4 前左视角
- 结构：双头蒸汽机，钢灰机身 + 黑色斜面面板 + 青色屏幕 + 红色手柄 + 双蒸汽杆 + 接水盘
- 生成脚本：`Tools/3D/make_heater_concept.py`（可复跑）

## 2. 跑过的三次

| 次数 | 输入处理 | 结果 |
|---|---|---|
| 1 | 透明底（alpha） | 团块，完全不可用 |
| 2 | 深色材质 + 透明底 | 仍是团块 |
| 3 | 白底 + 浅灰机身（当前输入） | 有台阶轮廓，但仍是团块 |

参数一致：`--device cuda:0 --mc-resolution 256 --no-remove-bg`，原始约 17.6 万面，减到 1,500 面后见 `预览.png`。

## 3. 结论

TripoSR 对「方盒 + 细杆」的工业设备依旧无法重建——和 `模型测试/全部模型` 的 15 个模型结论一致（§7.7）：**它只适合圆润单件，设备类不要用 TripoSR**。

## 4. 可用的替代

同一份参数化几何直接导出成 GLB，可进 Unity：

- `ArtSource/3D/final/加热机_参数化.glb`（约 99 KB，1,500 面级）
- 预览：`Temp/heater_param/加热机参数化_000.png`

如果一定要 AI 生成，走 **Tripo 云端 P1**（`face_limit=1500`），不要把 TripoSR 当备选。

## 5. 复跑

```powershell
# 出概念图 + GLB
& D:\Blender\blender.exe -b -P Tools\3D\make_heater_concept.py -- `
  --out ArtSource\2D\concepts\加热机_概念.png --glb ArtSource\3D\final\加热机_参数化.glb --size 1024

# TripoSR
cd D:\Tools\TripoSR
python run.py <概念图> --device cuda:0 --pretrained-model-name-or-path weights `
  --output-dir D:\Tools\triposr-heater --mc-resolution 256 --no-remove-bg
```
