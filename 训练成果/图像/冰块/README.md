# 冰块堆（冰柜储备）

> 三档冰堆，尺寸对齐 `前制作台lv2` 的子对象 `Cube.001` 冰槽：
> **2.718 × 3.339 × 0.868**（Blender 坐标：x ±1.359，y ±1.670，z -0.292~0.576）。

## 文件

| 图 | Unity 内 GLB | 源 GLB | 块数 | 堆高 |
|---|---|---|---:|---:|
| `冰块_低_lv2.png` | `Assets/模型/冰块/冰块_低_lv2.glb` | `ArtSource/3D/final/冰块_低_lv2.glb` | 20 | ~0.19 |
| `冰块_中_lv2.png` | `Assets/模型/冰块/冰块_中_lv2.glb` | `ArtSource/3D/final/冰块_中_lv2.glb` | 70 | ~0.43 |
| `冰块_高_lv2.png` | `Assets/模型/冰块/冰块_高_lv2.glb` | `ArtSource/3D/final/冰块_高_lv2.glb` | 150 | ~0.74 |

- 底面 2.66 × 3.27（比凹槽内收 2%），方块 0.34–0.46
- **原点在底面中心**：x=0、z=0 底面，Y 向上；进 Unity 后 pivot 在冰块堆底部正中

## 放进 Unity / 避免穿模

1. 选中 **容器（前制作台lv2/Cube.001）**
2. 菜单：**奶茶店/摆放/创建冰块生成点** → 在容器内底中心生成一个空物体 `冰块生成点`
3. 把 `Assets/模型/冰块/冰块_中_lv2.glb` 拖成该空物体的子物体，Local Position 归零即可

冰块 GLB 原点在底面中心，所以挂到内底中心的空物体下就自然对齐，不需要再算偏移。

手工摆放公式：

```
生成点位置 = 容器内底中心
冰块 Local Position = (0,0,0)
冰块 Local Scale    = 1（模型已按凹槽尺寸生成）
```

## 生成

```powershell
& D:\Blender\blender.exe -b -P Tools\3D\make_ice_bin.py -- `
  --level mid --out 训练成果\图像\冰块\冰块_中_lv2.png `
  --glb ArtSource\3D\final\冰块_中_lv2.glb --size 1024
```

`BIN_X / BIN_Y / BIN_Z` 在脚本顶部；换容器时改这三个数重跑。