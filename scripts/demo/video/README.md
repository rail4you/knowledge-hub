# 演示视频生成流水线（录屏 + 中文旁白 TTS）

用**生产环境真实操作录屏** + **中文语音合成**生成带旁白与字幕的演示视频。
成片：`out/final/特殊教育课程教学资源平台-功能演示.mp4`（8 分 42 秒 / 1920×1080 / 23 MB）。

配套文档：`docs/演示视频-语音脚本与分镜.md`（逐场景旁白原文与分镜表）

---

## 1. 流水线

```
voiceover.json（文案 + 标题卡文字）
   │
   ├─ tts.sh ────────────► out/tts/<scene>.mp3 + <scene>.srt   （MiniMax TTS，含精确时间轴）
   │
   ├─ scenes/<scene>.mjs ─► out/raw/<scene>.webm               （Playwright 录屏，生产环境真实点击）
   │
   └─ build.py ──────────► out/final/<scene>.mp4 → 成片        （标题卡 + 录屏 + 烧录字幕 + 旁白）
```

## 2. 依赖

| 依赖 | 用途 | 安装 |
|---|---|---|
| Node 20+ | 录屏脚本 | — |
| `playwright-core` + 本机 Chrome | 驱动真实浏览器录屏 | `npm i`（`channel: 'chrome'`，不需要下载 Chromium） |
| Playwright 自带 ffmpeg | 录屏编码（webm） | `node node_modules/playwright-core/cli.js install ffmpeg` |
| 系统 `ffmpeg` | 合成、转码、字幕叠加 | `brew install ffmpeg` |
| `mmx`（MiniMax CLI） | 中文语音合成 | `npm i -g mmx-cli` + `mmx auth login` |

> ⚠️ 本机 `ffmpeg` 未编译 `drawtext` / `libass`，所以**标题卡和字幕都是先用无头 Chrome 渲染成 PNG**
> （`render-text.mjs` / `render-subs.mjs`），再用 `overlay` 贴到画面上，而不是 ffmpeg 画字。

## 3. 命令

```bash
cd scripts/demo/video
npm i                                        # 首次
./tts.sh                                     # 全部旁白（或 ./tts.sh s01-overview）
./record-all.sh s01-overview s02-resource-create ...   # 逐场景录屏（生产环境）
python3 build.py                             # 合成（或 python3 build.py s05-studio-review 只做事后合成）
```

单个场景的录制脚本也可以直接跑：`node scenes/s05-studio-review.mjs`。

## 4. 场景与录屏脚本

| 场景 | 脚本 | 说明 |
|---|---|---|
| 片头 / 收尾 | （无录屏） | 只有标题卡 + 旁白 |
| 平台总览 | `s01-overview.mjs` | 门户首页 → 教师工作台 → 资源中心 |
| 课程资源管理（后台） | `s02-resource-create.mjs` | 建资源（模块下拉 10 栏目）→ 上传（CDP 限速让进度条可见）→ 保存 → 提交审核 |
| 课程资源管理（前台） | `s03-resource-browse.mjs` | 按模块筛选 → 详情抽屉 → 预览弹窗 → 视频真实播放 |
| 名师工作室（成员） | `s04-studio-upload.mjs` | 成员从个人课程资源选文件上传 → 待审核 |
| 名师工作室（领衔人） | `s05-studio-review.mjs` | **双窗口**：领衔人通过并发布 ↔ 成员刷新可见 |
| 互动课堂课表 | `s06-timetable.mjs` | 周 → 悬浮 tooltip → 日 → 月 → 周 → 课程筛选 |
| 教师教研白板（创建） | `s07-whiteboard-create.mjs` | **双窗口**：新建白板 + 邀请 ↔ 被邀请方列表出现并进入 |
| 教师教研白板（共创） | `s08-whiteboard-collab.mjs` | **双窗口**：加/改/拖文本、便签、远程光标、素材插入 |
| 数据准备工具 | `tools/upload-image-resource.mjs` | 为演示账号补一份图片/文档资源 |

双窗口场景共用 `lib/browser.mjs` 的 `newRecordingContext`：两个 context 各录一份 webm，
`build.py` 里 `hstack` 成 1920×540 再居中到 1920×1080（左右分屏）。

## 5. 时长对齐策略（`build.py`）

| 情况 | 处理 |
|---|---|
| 录屏时长 ≈ 旁白 | 直接用 |
| 录屏短于旁白 | 按比例放慢（下限 0.6 倍速），仍不足则用末帧定格补足 |
| 录屏长于旁白 | 至多 1.25 倍速并裁掉尾部 |

> 注：两个 context 并行录制时浏览器渲染帧率下降，录屏时长可能短于真实操作时长，
> 上面的放慢策略同时也修正了这一点。

## 6. 已知缺口（生产环境实测，建议后续修）

| # | 现象 | 影响 | 建议 |
|---|---|---|---|
| 1 | 资源表单的「资源类型」只放出**文档 / 视频**（枚举里还有音频/图片/PPT） | 白板「从课程资源选图片」永远选不到素材；图片类资源只能靠接口造 | 表单补齐 5 种类型，或按扩展名自动判定类型 |
| 2 | 白板元素早期**不落库**（SignalR 长连接持有环境 UoW，连接结束整体回滚） | 「刷新即丢」 | 已在 `WhiteboardHub` 用独立 UoW 修复（本次提交） |
| 3 | 拖动文本后宽度被压到 60px（取 Group 宽度 = 0） | 文本变竖排 | 已改为取内部 Text 节点宽度（本次提交） |
| 4 | 工作室「删过的资料再传同一资源」直接 500（唯一索引不排除软删除行） | 用户看到 500 | 已在 `StudioAppService` 先物理清除软删除行（本次提交）；建议唯一索引加 `WHERE IsDeleted = false` 过滤 |
| 5 | 资源中心「按模块」筛选曾失效（生成 proxy 未包含 `module` 参数） | 前台筛选不动 | 已在 `resource.ts` 手写 REST 调用带上 module（本次提交）；建议重新生成 proxy |
| 6 | 课表悬浮信息用原生 `title` | 录屏/远程演示看不到 tooltip | 已改为 `nz-tooltip`（本次提交） |

## 7. 清理

```bash
rm -rf out/raw out/rawtmp out/work out/frames out/logs   # 中间产物
```

演示数据清理（生产）：

```bash
# 资源：DELETE /api/app/resource/{id}（宿主管理员）
# 白板：DELETE /api/app/whiteboard/{id}
# 工作室资料：DELETE /api/app/studio/{studioId}/material/{materialId}
```
