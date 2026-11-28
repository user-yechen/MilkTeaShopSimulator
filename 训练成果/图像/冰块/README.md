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

1. 把 `Assets/模型/冰块/冰块_中_lv2.glb` 拖进场景（或做成 Prefab）
2. 同时选中 **容器（前制作台lv2/Cube.001）** 和 **冰块模型**（容器在前）
3. 菜单：**奶茶店/摆放/冰块堆对齐容器**

工具（`Assets/Editor/IcePileAlignTool.cs`）会：

- 父级设为容器
- 等比缩放到容器内宽 98%（保持方块比例）
- 底面贴容器内底 +2 mm，XZ 居中

用的是 Renderer 包围盒，**不依赖 pivot 在哪**；容器没有 Renderer 时退回 Collider。

手工对齐公式：

```
localPosition = (0, 容器内底 y − 冰块底 y, 0)   // 冰块原点在底面中心
localScale    = 1（模型已按凹槽尺寸生成）
```

## 生成

```powershell
& D:\Blender\blender.exe -b -P Tools\3D\make_ice_bin.py -- `
  --level mid --out 训练成果\图像\冰块\冰块_中_lv2.png `
  --glb ArtSource\3D\final\冰块_中_lv2.glb --size 1024
```

`BIN_X / BIN_Y / BIN_Z` 在脚本顶部；换容器时改这三个数重跑。