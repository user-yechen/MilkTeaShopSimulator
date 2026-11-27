# 2D 美术 Agent 手册

> 配套：[美术风格.md](../美术风格.md)（色板与规范）、[美术产线方案.md](../美术产线方案.md)（整体流程）

---

## 0. 先说清能力边界

当前 DSH 会话里**没有接入图像生成模型**（SD / ComfyUI / 文生图 API 都没有）。
所以「2D 美术 Agent」不是「让 AI 画图」，而是下面四种确定性产出：

| 能力 | 做什么 | 适用 |
|---|---|---|
| 程序化绘制 | 用 Python(Pillow) / SVG 代码画图标、按钮、面板、色块 | UI 图标、按钮三态、9-slice、金币/星星等几何图标 |
| Blender 渲染 | 用现成 3D 杯子渲染 2D 产品图标（统一相机/灯光） | 订单图标、商店商品图 |
| 素材加工 | 裁切、缩放、去底、描边、图集、命名、生成 .meta | 所有进入 Unity 的 2D 资源 |
| 提示词包 | 产出可直接粘贴到 SD/ComfyUI 的 prompt + 参考图 | 需要手绘感的概念图、贴图 |

如果你本机装了 ComfyUI，可以再让 Agent 走它的 HTTP API（`/prompt`）——**需要你确认后再接**，本文按「无生成模型」写。

---

## 1. 三个角色

### A. UI 图标 Agent（2D-UI）

- **职责**：按钮、面板、功能图标（设置/地图/商店/金币）、按钮三态、9-slice 面板
- **输入**：`spec`（见第 2 节）+ 色板
- **输出**：
  - 源文件 `ArtSource/2D/icons/*.svg`
  - 导出 `Assets/贴图/UI/<名>.png`（1024×1024 或 512×512，透明底）
  - 预览 `Temp/art_review/<名>_preview.png`
- **验收**：颜色全部来自色板；圆角 12/24；无描边；透明边距 ≥10%；三态齐全

### B. 产品图 Agent（2D-Product）

- **职责**：订单/商店用的「杯 + 液体 + 小料」图标里，**没有 3D 模型可渲的那部分**（手绘/合成、小料贴图、液体色块）
- **分工**：有 3D 模型的图标交给 [3转2图标Agent.md](3转2图标Agent.md)（Blender 渲染 + 后期），本角色不再重复实现渲染
- **输出**：`Assets/Resources/贴图/订单/<名>.png`，512×512，透明底
- **验收**：与现有 `中奶茶杯/大奶茶杯/牛奶/茶` 同角度、同光照、同构图；液体颜色对应该口味

### C. 资产 QA Agent（2D-QA）

- **职责**：批量检查色板偏差、alpha 边缘、尺寸、命名、是否重复；生成联络表
- **输出**：`Temp/art_review/report.md` + `contact_sheet.png`
- **验收**：所有资产通过才允许合入

---

## 2. 2D 资产 spec 模板

```json
{
  "id": "icon_coin",
  "name": "金币图标",
  "category": "ui-icon",
  "size": [512, 512],
  "palette": ["#F0C000", "#A07000", "#605000"],
  "shapes": ["circle", "inner-ring", "star"],
  "states": ["normal", "pressed", "disabled"],
  "transparentMargin": 0.1,
  "output": "Assets/贴图/UI/UI_金币.png",
  "notes": "参考现有 金币.png，不要改角度"
}
```

产品图 spec：

```json
{
  "id": "drink_pearl_milk_tea",
  "name": "珍珠奶茶",
  "category": "product-icon",
  "cup": "大",
  "liquidColor": "#A67D54",
  "toppings": ["#2B1A12"],
  "camera": "3/4, 20deg",
  "size": [512, 512],
  "output": "Assets/Resources/贴图/订单/珍珠奶茶.png"
}
```

---

## 3. 工作流（每件资产）

```
spec → 生成 → 预览图 → 审核（人/主控）→ 修正 → 导出到 Assets → QA 批量校验 → 提交
```

**审核只看三件事**：颜色对不对、剪影认不认得出、放大会不会糊。
不合格必须指出具体问题（如「金币外圈太暗，改用 #A07000」），不接受「再好看点」这种反馈。

---

## 4. 常用命令

**已实现工具**

| 工具 | 作用 |
|---|---|
| `Tools/Art2D/make_ui_icon.py` | 按 spec 画扁平图标（coin/gear/star/panel），支持三态、4x 超采样 |
| `Tools/3D/decimate_to_budget.py` | Blender 无头减面到目标三角面（3D 用） |

```powershell
# 生成金币图标（normal/pressed/disabled 三态 + 预览图）
& $py Tools\Art2D\make_ui_icon.py --spec ArtSource\2D\specs\icon_coin.json

# 生成设置齿轮
& $py Tools\Art2D\make_ui_icon.py --spec ArtSource\2D\specs\icon_gear.json
```

> `$py` = `C:\Users\叶尘\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe`

**规划中（做首个商品图标时补）**

- `Tools/Art2D/render_product_icon.py`：Blender 渲染杯+液体为 512² 透明 PNG
- `Tools/Art2D/qa_2d.py`：批量色板/尺寸/alpha 校验 + 联络表

---

## 5. 验收清单（QA 用）

- [ ] 尺寸符合 spec，透明底，边距 ≥10%
- [ ] 主色与色板偏差 ΔE ≤ 8（肉眼无明显色差）
- [ ] 无黑描边、无多余阴影、无锯齿
- [ ] 三态齐全（按钮类），按下态整体压暗约 12%
- [ ] 命名符合 `UI_/图标_/订单_` 前缀
- [ ] 已放入正确目录，且 `Resources` 下的路径与代码一致
- [ ] 生成预览图并附在交付说明里

---

## 6. 如何召唤这些 Agent

在本会话里直接说，例如：

> 让 2D-UI agent 做「金币」和「设置」两个图标，按 512 出，带三态

我会用下面的提示词起子 Agent（你也可以自己复制去别的会话）：

```
你是 2D-UI Agent。先读 D:\UnityProject\milktea\Docs\美术风格.md 和
Docs\Agents\2D美术Agent.md，严格遵守色板与验收清单。

任务：<资产名 + spec>
产出：SVG 源 + PNG 导出 + 预览图，路径按手册。
要求：不要自创颜色；不要改现有资产；完成后按第 5 节自检并汇报。
```

**并行建议**：一次最多 2 个 2D Agent（一个 UI、一个产品图），QA Agent 最后统一跑。

---

## 7. 禁止事项

- 禁止自创色板外的颜色、渐变方向、描边风格
- 禁止改动 `Assets/` 里已有的贴图（除非 spec 明确要求替换）
- 禁止把 2200×2200 源图直接塞进移动端（必须按目标尺寸导出）
- 禁止无 spec 开工；禁止交付无预览图的资产
