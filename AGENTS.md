# AGENTS.md — 奶茶店模拟器

> 本文件由 DSH 在会话首次请求时自动注入（`dsh-agent-instructions`，65 KB 预算）。
> 目的：让任何新会话先熟悉项目，再动工。

---

## 0. 启动协议（每个新会话必须先执行）

1. 读 `Assets/上下文.txt` —— 当前状态、待办、经验教训
2. 读 `Docs/00-文档导航.md` —— 文档地图，知道什么在哪
3. 按任务读对应文档（至少读一份，不要凭记忆改）：
   - 数值 / 坐标 / 存档 / 构建 → `Docs/03-数据/数据手册.md`
   - 接口 / 系统 / 调用关系 → `Docs/03-数据/脚本接口.md`
   - 美术 / 图标 / 风格 → `Docs/02-规范/美术风格.md`、`Docs/06-Agents/*.md`
   - 3D 模型生产 → `Docs/06-Agents/3D模型Agent.md`（Tripo 2D→3D）
   - 文案 / 文本 → `Docs/06-Agents/文案Agent.md`、`Assets/Resources/文案/文案表.csv`
   - 摆放 / 穿模 → `Docs/02-规范/摆放与穿模规范.md`
   - 3D 生成原理 / 本地 TripoSR → `Docs/05-3D/3D资产管线-Tripo.md`
4. **用 3–5 条 bullet 向用户复述**：
   - 现在能玩什么、卡在哪
   - 这次要改哪个模块、它依赖谁、谁依赖它
   - 怎么验收（场景里看什么 / 跑什么命令）
5. 复述确认后再动工。信息不足就问，不要猜。

> 跳过第 4 步直接改代码 = 违规。

---

## 1. 项目速览

| 项 | 值 |
|---|---|
| 引擎 | Unity 2022.3.56f1c1，Built-in 渲染管线 |
| 平台 | Android（minSdk 22 / target 35），横屏 |
| 包名 / 版本 | `com.yechen.milktea` / 0.1.0 |
| 主场景 | `Assets/Scenes/奶茶店.unity` |
| 一键出包 | `Unity.exe -batchmode -quit -projectPath <路径> -buildTarget Android -executeMethod BuildScript.BuildAndroid` |
| 仓库 | https://github.com/user-yechen/MilkTeaShopSimulator |

目录：

```
Assets/脚本/     运行时（33 个）
Assets/Editor/   编辑器工具（菜单「奶茶店/…」）
Assets/Scenes/   主场景
Docs/            规范与文档（唯一权威）
ArtSource/       不参与 Unity 导入的源文件
Tools/           Art2D / 3D 脚本
Build/           出包产物（gitignore）
```

---

## 2. 文档规则（单一口径）

- **同一事实只写一处**：数值→数据手册，接口→脚本接口，风格→美术风格，状态→上下文
- 上下文只记「状态 / 待办 / 经验 / 变更」，不复制规范
- 改完代码同步改文档；文档与代码冲突时以代码为准，并修正文档
- 新增文档要在 `Docs/00-文档导航.md` 登记

---

## 3. 硬规则

| 规则 | 原因 |
|---|---|
| 不改场景物体名字 | 大量逻辑按名查找（营收额/台面/杯位/金额），改名静默失效 |
| 不改色板与美术风格 | 除非用户明确要求；先看 `Docs/02-规范/美术风格.md` |
| 新资产先写 spec | 面数/尺寸/轴心/材质按 spec 验收 |
| 不提交 `Library/ Temp/ Build/` | 已在 .gitignore；提交前 `git status` |
| 改 public 接口必须更新脚本接口 | 影响面在文档里有速查表 |
| 大改前先开分支 / 先提交 | 可回滚 |
| 不猜 API / 不臆造字段 | 先读脚本或文档 |

---

## 4. 完成定义

- [ ] 代码能编译（不能验证要明确说明）
- [ ] 相关文档已更新（数据/接口/风格/上下文按需）
- [ ] 给出「怎么验证」一句话（场景里看什么、跑什么命令）
- [ ] 提交信息说明改了什么、为什么

---

## 5. 常用命令

```powershell
# 出包
Unity.exe -batchmode -quit -projectPath D:\UnityProject\milktea -buildTarget Android `
  -executeMethod BuildScript.BuildAndroid -logFile Build/build.log

# 减面（Blender）
& D:\Blender\blender.exe -b -P Tools\3D\decimate_to_budget.py -- --in a.glb --tris 1200 --out b.glb

# 3D→2D 图标
& D:\Blender\blender.exe -b -P Tools\Art2D\render_icon.py -- --model m.glb --out raw.png --yaw 180 --solid 0.92
python Tools\Art2D\icon_post.py --in raw.png --out icon.png --outline --shadow
```

> `python` = `C:\Users\叶尘\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe`

---

## 6. 会话开始时的自检清单

写代码前，确认自己已经知道：

- [ ] 当前游戏能跑通什么流程
- [ ] 要改的类在哪个文件、公开方法是什么
- [ ] 这个改动会影响到谁（查《脚本接口》影响面）
- [ ] 数据/配置在哪（价格表、存档、场景坐标）
- [ ] 改动后怎么验证、怎么回滚
