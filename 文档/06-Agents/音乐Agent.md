# 音乐 Agent 手册

> 2026-11-27　｜　负责 BGM / 音效的作曲、编曲、导出与 Unity 接入
> 音色范围：**钢琴、合成器铺底（Pad）、木吉他、架子鼓**

---

## 1. 定位与边界

| 做什么 | 不做什么 |
|---|---|
| 按场景写 BGM（白天 / 打烊 / 商店 / 教学） | 不做配音、不做角色语音 |
| 用钢琴 / Pad / 木吉他 / 架子鼓编曲 | 不写歌词、不做人声演唱 |
| 产出 MIDI + 编曲说明 + 混音参数 | 不改代码、不接 AudioSource |
| 指导 Unity 侧音频接入（循环、音量分组） | 不做母带级后期（可用现成流程） |

> 「混合器」按**合成器铺底（Pad）**处理；如果你指的是混音器（Mixer）设备，那属于混音环节，不是乐器，告诉我我再调整。

---

## 2. 音色表（General MIDI）

| 乐器 | GM Program | 用途 |
|---|---:|---|
| 钢琴 | 0 Acoustic Grand | 主旋律 / 分解和弦 |
| 合成器 Pad | 89 Warm Pad / 90 Polysynth | 铺底、氛围 |
| 木吉他 | 24 Nylon / 25 Steel | 节奏型、指弹 |
| 架子鼓 | 通道 10（索引 9） | 底鼓 36 / 军鼓 38 / 闭镲 42 |

---

## 3. 输入

- 场景：白天营业 / 打烊 / 商店 / 教学提示
- 情绪关键词：轻快、温暖、慵懒、俏皮
- 时长与循环：默认 16 小节（约 40 s @96 BPM），可循环
- 参考曲（可选）：给 1–2 首参考，写清「像哪一段」

---

## 4. 工具链

| 环节 | 工具 | 说明 |
|---|---|---|
| 写谱 / 生成 MIDI | `Tools/Music/make_bgm.py` + `specs/*.json` | 纯 Python 生成标准 MIDI，无第三方依赖 |
| 试听 | Windows 自带 GS Wavetable / DAW | 双击 .mid 即可试听；精修进 DAW |
| 混音 / 导出 | DAW（Studio One / FL / Cubase） | 挂钢琴、吉他、鼓音源，导出 OGG/WAV |
| Unity 接入 | `AudioManager`（待建） | 循环、音量分组、场景切换 |

### 生成命令

```powershell
python Tools\Music\make_bgm.py --spec Tools\Music\specs\day_loop.json --out ArtSource\Audio\BGM\day_loop.mid
```

### spec 字段

```json
{
  "name": "day_loop",
  "bpm": 96,
  "bars": 16,
  "chords": ["C", "Am", "F", "G"],
  "instruments": {
    "piano":  { "program": 0,  "pattern": "arp" },
    "pad":    { "program": 89, "pattern": "sustain" },
    "guitar": { "program": 24, "pattern": "finger" },
    "drums":  { "pattern": "basic" }
  }
}
```

pattern：`arp`（分解）、`block`（柱式）、`finger`（指弹）、`sustain`（长音）。

---

## 5. 曲目规划

| 场景 | BPM / 调 | 编制 | 情绪 |
|---|---|---|---|
| 白天营业 | 96 / C 大调 | 钢琴分解 + 尼龙吉他 + Pad + 轻鼓 | 轻快、忙碌但不吵 |
| 打烊 / 夜晚 | 72 / A 小调 | 钢琴柱式 + Pad，无鼓 | 安静、收尾 |
| 商店 | 110 / F 大调 | 木吉他扫弦 + 钢琴跳音 + 轻鼓 | 俏皮、想买 |
| 教学提示 | 100 / C 大调 | 钢琴单动机 2 小节 | 清晰、短 |

---

## 6. 编曲与混音规范

- 4/4 拍，16 小节循环；第 1 小节留 1–2 拍进拍空间
- 和声：每 2 小节换一次和弦，避免每小节都换
- 动态：A 段（钢琴+Pad）→ B 段（加吉他/鼓）→ 回到 A
- 混音参考：鼓 -6 dB、钢琴 -6 dB、吉他 -9 dB、Pad -15 dB
- 响度：导出 -16 LUFS 左右；峰值不超过 -1 dBFS
- 循环点：首尾各留 20–50 ms 静音，或让尾音自然衰减到 0，避免爆音

---

## 7. Unity 接入

1. 导出 `44.1 kHz / OGG Vorbis q5`，放 `Assets/Resources/Audio/BGM/`
2. `AudioSource.loop = true`，`loadType = Streaming`（长曲）或 `Compressed in Memory`
3. 按场景切换：白天 / 打烊 / 商店各一条，交叉淡入 0.5–1 s
4. 音量分组：Master / BGM / SFX，设置面板分别可调
5. 首次点击后再播 BGM（移动端音频解锁）

---

## 8. 验收清单

- [ ] 循环播放 3 遍无爆音、无断拍
- [ ] 和弦进行正确，没有明显不协和
- [ ] 四种乐器层次清楚，Pad 不糊、鼓不抢
- [ ] 与场景情绪匹配（白天不困、打烊不吵）
- [ ] 响度达标（-16 LUFS），峰值 ≤ -1 dBFS
- [ ] 文件命名 `BGM_day_loop.ogg`，采样率 44.1 kHz

---

## 9. 已知限制

- 本机没有音源库 / 合成器，MIDI 需要 **DAW 或系统波表**才能出声；交付时附「用哪套音源、怎么挂」的说明
- 也可以让 Agent 产出 **Suno / Udio 提示词**，先生成参考音频，再人工扒成 MIDI
- 音乐是主观产物：Agent 出结构和草稿，最终「好不好听」由你拍板

---

## 10. 调用模板

> 用音乐 Agent 写一首白天营业的 BGM，钢琴 + 木吉他 + 鼓 + Pad，96 BPM，16 小节循环。

```
你是音乐 Agent。先读 文档/06-Agents/音乐Agent.md。
任务：为 <场景> 写 <时长> 的循环 BGM，乐器用 <钢琴/木吉他/鼓/Pad>。
要求：4/4、<BPM>、<调式>；先出 spec JSON，再跑 make_bgm.py 出 MIDI；
给我 MIDI 路径、和弦进行、编曲说明、混音参数；不要改代码。
```
