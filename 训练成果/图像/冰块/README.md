# 冰块堆（冰柜储备）

> 冰柜里三种储备量的冰块堆：低 / 中 / 高。已用 Blender 参数化生成，**可直接交 Tripo**。

## 三张图

| 文件 | 颗数 | 表现 | 用途 |
|---|---:|---|---|
| `冰块_低.png` | 12 | 薄薄一层、散在底部 | 冰格快用完 |
| `冰块_中.png` | 55 | 半满、堆成小丘 | 正常营业 |
| `冰块_高.png` | 130 | 满溢、堆到边缘 | 刚补货 |

- 1024×1024、白底、3/4 俯视、低模方块（倒角 6mm）
- 冰色 `#73AEE0` 左右的淡蓝，哑光，便于 Tripo 识别切面
- 不含容器：容器在 Unity 里单独做，模型只做「冰堆」

## 生成方式

`Tools/3D/make_ice_pile.py`（Blender 无头）：

```powershell
& D:\Blender\blender.exe -b -P Tools\3D\make_ice_pile.py -- `
  --level mid --out 训练成果\图像\冰块\冰块_中.png --size 1024
```

`--level` 取 `low / mid / high`，颗数与堆高在脚本顶部 `CONF` 里调。

> 先用 ComfyUI 试过 9 张，SDXL 会把冰块画成透明盒或巨型晶体，遂改参数化。

## 下一步

挑一张（或三张分别）交 Tripo：`--model tripo-p1 -p face_limit=300`，低模方块不需要高面数。
