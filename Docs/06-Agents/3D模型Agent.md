# 3D 模型 Agent 手册

> 2026-11-27　｜　项目里**唯一负责 3D 模型生产**的 Agent
> 2D → 3D 由 **Tripo** 完成，减面/修整由 **Blender** 完成，贴图与材质交给美术 Agent

---

## 1. 定位与边界

| 做什么 | 不做什么 |
|---|---|
| 按 spec 产出 3D 模型（设备 / 道具 / 家具） | 不做贴图、不做材质风格（美术 Agent） |
| 用 Tripo 做 **2D 图 → 3D 模型** | 不做 3D → 2D 图标（[3转2图标Agent](3转2图标Agent.md)） |
| 用 Blender 减面、修轴心、导 GLB | 不改代码、不接 Prefab |
| 出预览图、面数/尺寸报告 | 不自己发明 spec |

**一句话**：模型从「一张图」到「能进 Unity 的 GLB」这一段，全归这个 Agent。

---

## 2. 输入

1. **spec**：`ArtSource/3D/specs/<id>.json`（面数预算、尺寸、轴心、材质数、输出路径）
2. **参考图**：`ArtSource/2D/concepts/<id>.png`
   - 单物体、纯白/浅灰底、物体占画面 70–85%
   - 3/4 视角或正视图；**不要**多物体、文字、水印、复杂背景
   - 1024×1024 足够；Tripo 支持多视图时补侧面/背面

> 参考图优先用「项目内已有模型渲的 3/4 图」——风格与现有资产一致，比网图可靠。

---

## 3. 工具链

| 环节 | 工具 | 说明 |
|---|---|---|
| 2D → 3D | **Tripo CLI**（主） | `npx -y tripo-cli`，浏览器授权，不需要 API Key |
| 2D → 3D（备选） | **TripoSR**（本地） | 只适合圆润单件；权重已在 `D:\Tools\triposr-env` |
| 减面 | `Tools/3D/decimate_to_budget.py` | Blender 无头，减到 spec 预算 |
| 多视角渲染 | `Tools/3D/render_views.py` | 输入图 / 结果预览 / 对比 |
| 轴心与尺寸 | `Tools/3D/`（脚本）+ Blender | pivot 底部中心、+Z 朝前、Scale=1 |

---

## 4. 标准流程

```
1) 写 spec          面数 / 尺寸 / 轴心 / 材质数 / 输出路径
2) 出参考图         3/4 视角、白底、1024
3) Tripo 生成       P1 + face_limit=spec 预算 + GLTF
4) 减面/修整        Blender：减到预算、删残留节点、轴心到底面
5) 渲染验收         0/120/240 三视角 preview
6) 交付             ArtSource/3D/final/<id>.glb + 报告
```

---

## 5. 命令

### 5.1 登录（只需一次）

```powershell
npx -y tripo-cli login      # 浏览器授权，key 自动落盘
npx -y tripo-cli whoami
npx -y tripo-cli balance
```

### 5.2 生成（2D → 3D）

```powershell
npx -y tripo-cli make ArtSource\2D\concepts\珍珠.png `
  --for game-mobile `
  --model tripo-p1 `
  -p face_limit=1500 `
  -p texture_size=1024 `
  --then "convert:GLTF" `
  -o ArtSource\3D\raw
```

- `--for game-mobile`：预设面数上限 15,000
- `-p face_limit=<spec 预算>`：直接压到目标面数（实测生效）
- 格式只认 `GLTF / USDZ / FBX / OBJ / STL / 3MF`（没有 "GLB"）
- 先 `--dry-run` 看计划，不扣积分

### 5.3 减面与导出

```powershell
& D:\Blender\blender.exe -b -P Tools\3D\decimate_to_budget.py -- `
  --in ArtSource\3D\raw\0\mesh.obj --tris 1500 --out ArtSource\3D\final\珍珠.glb
```

### 5.4 预览

```powershell
& D:\Blender\blender.exe -b -P Tools\3D\render_views.py -- `
  --model ArtSource\3D\final\珍珠.glb --out-dir Temp\review `
  --views 0,120,240 --size 512 --pitch 15 --perspective --bg transparent --prefix 珍珠
```

---

## 6. 验收清单

- [ ] 三角面 ≤ spec 的 `triBudget`
- [ ] 材质数 = 1，贴图 ≤ 1024（若 Tripo 带贴图）
- [ ] 轴心在**底部中心**，朝向 +Z，Scale = 1
- [ ] 尺寸与 spec 一致（长宽高误差 ≤ 5%）
- [ ] 无相机 / 灯光 / 空节点残留
- [ ] 命名 `类型_名称_变体.glb`
- [ ] 三视角预览无明显破面、悬空、反向法线

---

## 7. 失败处理

| 情况 | 处理 |
|---|---|
| Tripo 任务卡住 > 15 分钟 | 取消重试；降参数（P1、512 贴图、关 PBR） |
| 面数压不下来 | 先 Tripo 减面接口，再 Blender Decimate |
| 生成成团块 | 换参考图（更清晰的 3/4）、或改用参数化建模 |
| 需要贴图 | 交美术 Agent；模型 Agent 只保证几何与 UV 可用 |
| 没网 / 没额度 | 用本地 TripoSR（只限圆润单件） |

**已知结论**（`模型测试/`）：TripoSR 对设备/家具基本不可用；这类资产优先 Tripo 云 P1，仍不行就 Blender 手建。

---

## 8. 与其它 Agent 的交接

```
美术总监 / 你 ── spec + 参考图 ──▶ 3D 模型 Agent
                                      │
                                      ├─ GLB（几何） ──▶ 美术 Agent（材质/贴图）
                                      └─ GLB（成品） ──▶ 技术美术 / Unity 接入
```

- 与 **3转2 图标 Agent** 相反：这边是 2D→3D，那边是 3D→2D
- 与 **美术 Agent** 的分界：模型 Agent 交「能用的几何」，美术 Agent 管「好不好看」

---

## 9. 调用模板

> 用 3D 模型 Agent 按 spec 生成「珍珠」，Tripo P1 + face_limit 200，出 GLB 和三视角预览。

```
你是 3D 模型 Agent。先读 Docs/ArtSource/3D/specs/<id>.json、Docs/02-规范/美术风格.md 与本文。
任务：按 spec 生成 <资产名>。
要求：
1) 参考图用 3/4 白底 1024；
2) Tripo CLI：--for game-mobile --model tripo-p1 -p face_limit=<预算> --then "convert:GLTF"；
3) 用 decimate_to_budget.py 减到预算，render_views.py 出 0/120/240 预览；
4) 按第 6 节自检并报告面数/尺寸/材质数。
不要做贴图、不要接 Prefab。
```
