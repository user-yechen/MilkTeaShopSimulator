# 3D 资产管线：Tripo 图生 3D

> 2026-11-27　｜　实测环境：Windows + RTX 4070 Laptop 8GB + Node 24
> 配套：[美术风格.md](美术风格.md)（面数/材质规范）、[美术产线方案.md](美术产线方案.md)

---

## 0. 结论：你不需要去找 API Key

Tripo 现在有三条接入方式，**前两条都不用 API Key**：

| 方式 | 是否需要 Key | 适合 |
|---|---|---|
| **DSH 官方插件** | 否（浏览器授权） | 最省事，对话里直接生成 |
| **Tripo CLI** | 否（`tripo login` 浏览器授权，Key 自动落盘） | 批量、脚本化、Agent 调用 |
| **API / MCP** | 是（控制台创建） | 自建服务、接入其他客户端 |

Tripo 官方页面已列出支持 **DeepSeek Harness**（也就是你现在用的这个），装插件、登录即可，不需要 API Key。

---

## 1. 三种接入方式

### A. DSH 插件（推荐先试）

1. 在 DSH 的插件入口搜 **Tripo**（或其官方扩展）
2. 安装后点「连接」，登录 Tripo 国内站并授权
3. 新开对话，直接说「用 Tripo 生成一个 XX，给我 GLB 下载链接」

> 我这边没有直接操作 DSH 插件市场的权限，这一步需要你在界面点。

### B. CLI（本机已验证可用）

```powershell
# 登录：浏览器授权，key 自动保存，无需手动复制
npx -y tripo-cli login

# 查看账号与余额
npx -y tripo-cli whoami
npx -y tripo-cli balance

# 试运行：只打印计划，不扣积分
npx -y tripo-cli make "珍珠，圆球" --dry-run --for game-mobile --model tripo-p1
```

**实测输出**（`--for game-mobile` 会自动把面数限制到 15000、输出 FBX）：
```json
{"steps":[{"payload":{"face_limit":15000,"texture":true,"pbr":false,"model":"P1-20260311"}},
          {"name":"convert","payload":{"format":"FBX","texture_size":2048}}]}
```

### C. MCP / API

```powershell
# 本地 MCP（给支持 MCP 的客户端）
npx -y tripo-cli mcp

# 远程 MCP
# https://developers.tripo3d.com/mcp
```

自建 API：控制台 <https://developers.tripo3d.com/zh/keys> → 创建密钥（只显示一次）→
`Authorization: Bearer {api_key}` → 建议存环境变量 `TRIPO_API_KEY`，**不要提交进仓库**。

---

## 2. 模型档位与面数

| 档位 | 面数上限 | 说明 |
|---|---:|---|
| P1 | 2 万 | 最轻，适合道具/小料 |
| P2（默认） | 5 万 | 质量与面数平衡 |
| H3.1 / v3.1 | 200 万 | 高精度，不适合直接进移动端 |
| `--for game-mobile` | 15,000（预设） | 自动带减面/贴图 |

**关键**：目标资产只有 300–1,500 面，**任何档位都要再减面**。好消息是：

- 生成请求本身支持 `face_limit`（实测 P1 可传 1500）
- 平台另有「减面 / 重拓扑 / 智能 UV / 贴图 / 格式转换」后处理接口

---

## 3. 从一张图到可用低模（推荐命令）

### 3.1 输入图要求

- 单个物体、居中、纯白或透明背景
- 3/4 视角或正视图，不要多物体、不要文字
- 1024×1024 即可；有背面就补一张多视图

### 3.2 生成（参数已用 dry-run 验证）

```powershell
npx -y tripo-cli make ArtSource\2D\concepts\珍珠.png `
  --for game-mobile `
  --model tripo-p1 `
  -p face_limit=1500 `
  -p texture_size=1024 `
  --then "convert:GLTF" `
  -o ArtSource\3D\raw
```

- `--then` 会**覆盖** game-mobile 默认链（默认是 FBX 2048）
- 格式只支持 `GLTF / USDZ / FBX / OBJ / STL / 3MF`（**没有 GLB 字样**；GLTF 可导入 Unity）
- 想保留默认链就去掉 `--then`，但要接受 FBX + 2048 贴图

### 3.3 后处理（必须做）

```
原始 GLB/FBX（1–2 万面）
  → 减面到 300–1500（Blender Decimate 或 Tripo 减面接口）
  → 重 UV / 图集（Tripo 智能 UV 或 Blender）
  → 贴图降到 512–1024，删掉 PBR 贴图组（只留 BaseColor）
  → 轴心移到底部中心、应用缩放
  → 导出 GLB → 放进 ArtSource/3D/final
```

Blender 批处理示例：

```powershell
& D:\Blender\blender.exe -b -P Tools\3D\decimate_to_budget.py -- `
    --in ArtSource\3D\raw\珍珠.glb --tris 1500 --out ArtSource\3D\final\珍珠.glb
```

### 3.4 进 Unity 前检查

- [ ] 面数 ≤ spec 的 triBudget
- [ ] 材质数 = 1，贴图 ≤ 1024
- [ ] 轴心在底部中心，朝向 +Z，Scale = 1
- [ ] 无相机/灯光/空节点残留
- [ ] 命名符合 `类型_名称_变体.glb`

---

## 4. 成本

- 按积分计费，P1 最便宜，H3.1 最贵
- `--dry-run` 不扣费，可以先看计划
- `tripo balance` 查余额，`tripo usage` 看最近消耗
- 生成失败一般不扣费，但**减面/重拓扑/加贴图**是单独步骤，也会计费

---

## 5. 本地部署替代（不走 Tripo）

Tripo 的商业模型不能本地部署；可本地跑的是它的开源版 **TripoSR**。你的 8GB 显存可以：

| 方案 | 显存 | 输出 | 评价 |
|---|---:|---|---|
| **TripoSR**（开源） | ~6–8 GB | 网格 + 顶点色，无 PBR 贴图 | 秒级，适合白模/小料，不适合带贴图的杯子 |
| **Stable Fast 3D** | ~7 GB | 带贴图的网格 | 质量比 TripoSR 好，可试 |
| Hunyuan3D-2 (mini) | 8–12 GB+ | 带贴图 | 8GB 勉强，需量化 |
| TRELLIS | 16 GB+ | 带贴图 | 4070 Laptop 跑不动 |

本地路线的真实成本是**环境**：PyTorch + CUDA + 依赖，第一次配通常 1–2 小时，且模型权重几个 GB。

**建议**：先用 CLI（几秒钟出结果），本地只作为断网/免费备选。TripoSR 项目：<https://github.com/VAST-AI-Research/TripoSR>

---

## 6. 风险与对策

| 风险 | 表现 | 对策 |
|---|---|---|
| 拓扑乱 | 三角面碎片、无法动画 | 只做静态道具；角色不用 AI 生成 |
| 面数超 | 移动端掉帧 | `face_limit` + 二次减面，QA 卡预算 |
| UV 差 | 贴图拉伸、接缝 | Tripo 智能 UV 或 Blender 重 UV |
| 风格漂移 | 和现有低模不搭 | 参考图用项目内模型渲染图；生成后按色板校色 |
| 贴图过大 | 包体膨胀 | 降到 512–1024，只留 BaseColor |
| 多物体 | 一张图出多个物件 | 输入图只放一个物体，或用网格分割接口 |
