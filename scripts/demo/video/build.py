#!/usr/bin/env python3
"""
演示视频合成脚本
------------------------------------------------------------------------------
输入：
  voiceover.json          场景 + 旁白文案
  out/tts/<id>.mp3/.srt   语音与字幕（TTS 生成，含精确时间轴）
  out/raw/<scene>.webm    生产环境录屏
  out/state/*.json       场景间共享状态（如白板 Id）

输出：
  out/final/特殊教育课程教学资源平台-功能演示.mp4
  out/final/<scene>.mp4   单场景片段

每个场景 = 标题卡(2.5s) + 生产环境录屏 + 烧录字幕 + 旁白配音
"""
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "out"
RAW = OUT / "raw"
TTS = OUT / "tts"
TITLE_SEC = 2.5
W, H = 1920, 1080
FPS = 30


def run(cmd, **kw):
    r = subprocess.run(cmd, shell=isinstance(cmd, str), capture_output=True, text=True, **kw)
    if r.returncode != 0:
        print((r.stdout or "")[-2000:])
        print((r.stderr or "")[-4000:])
        raise SystemExit(f"命令失败: {cmd if isinstance(cmd, str) else ' '.join(map(str, cmd))}")
    return r


def dur(path):
    r = run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(path)])
    return float(r.stdout.strip())


def parse_srt(path):
    txt = Path(path).read_text(encoding="utf-8")
    out = []
    for block in re.split(r"\n\s*\n", txt.strip()):
        lines = [l for l in block.strip().splitlines() if l.strip()]
        if len(lines) < 2:
            continue
        m = re.match(r"(\d+):(\d+):(\d+)[,.](\d+)\s*-->\s*(\d+):(\d+):(\d+)[,.](\d+)", lines[1])
        if not m:
            continue
        g = [int(x) for x in m.groups()]
        start = g[0] * 3600 + g[1] * 60 + g[2] + g[3] / 1000
        end = g[4] * 3600 + g[5] * 60 + g[6] + g[7] / 1000
        text = " ".join(lines[2:]).strip()
        out.append((start, end, text))
    return out


def render_title(scene, png):
    run([
        "node", str(ROOT / "render-text.mjs"), "title", str(png),
        scene.get("group", ""), scene.get("title", ""), scene.get("subtitle", ""),
    ])


def render_subs(scene_id, entries, subdir):
    subdir.mkdir(parents=True, exist_ok=True)
    items = []
    for i, (_s, _e, text) in enumerate(entries, 1):
        items.append({"text": text, "out": f"sub-{i:03d}.png"})
    jf = subdir / "subs.json"
    jf.write_text(json.dumps(items, ensure_ascii=False), encoding="utf-8")
    run(["node", str(ROOT / "render-subs.mjs"), str(subdir), str(jf)])


def to_mp4(src, dst, vf=None, extra=None):
    cmd = [
        "ffmpeg", "-v", "error", "-y", "-i", str(src),
        "-r", str(FPS),
    ]
    if vf:
        cmd += ["-vf", vf]
    cmd += ["-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p"]
    if extra:
        cmd += extra
    cmd += [str(dst)]
    run(cmd)


def build_screen(scene, dst_mp4):
    """把录屏转成 1920x1080 / 30fps 的 mp4（双窗口场景做左右并排）"""
    sid = scene["id"]
    if scene.get("panes") == 2:
        prefix = sid.split("-")[0]
        a = RAW / f"{sid}-a.webm"
        if not a.exists():
            a = RAW / f"{prefix}-a.webm"
        b = RAW / f"{sid}-b.webm"
        if not b.exists():
            b = RAW / f"{prefix}-b.webm"
        if not (a.exists() and b.exists()):
            raise SystemExit(f"缺少双窗口录屏：{a} / {b}")
        tmp_a = dst_mp4.with_name(f"{sid}-a.mp4")
        tmp_b = dst_mp4.with_name(f"{sid}-b.mp4")
        to_mp4(a, tmp_a, vf=f"scale={W}:{H}:force_original_aspect_ratio=decrease,pad={W}:{H}:(ow-iw)/2:(oh-ih)/2,setsar=1")
        to_mp4(b, tmp_b, vf=f"scale={W}:{H}:force_original_aspect_ratio=decrease,pad={W}:{H}:(ow-iw)/2:(oh-ih)/2,setsar=1")
        # 左右并排 → 1920x540 → 放到 1920x1080 画布中间
        run([
            "ffmpeg", "-v", "error", "-y", "-i", str(tmp_a), "-i", str(tmp_b),
            "-filter_complex",
            f"[0:v][1:v]hstack=inputs=2,scale={W}:540:flags=lanczos[hs];"
            f"color=c=#0b2b2c:s={W}x{H}:d=60[bg];"
            f"[bg][hs]overlay=0:(H-540)/2:format=auto,format=yuv420p[v]",
            "-map", "[v]", "-r", str(FPS), "-c:v", "libx264", "-preset", "veryfast",
            "-crf", "20", "-pix_fmt", "yuv420p", str(dst_mp4),
        ])
    else:
        src = RAW / f"{sid}.webm"
        if not src.exists():
            raise SystemExit(f"缺少录屏：{src}")
        if scene.get("screen") is None:
            # 纯标题卡场景（片头）：不需要录屏
            return False
        to_mp4(src, dst_mp4, vf=f"scale={W}:{H}:force_original_aspect_ratio=decrease,pad={W}:{H}:(ow-iw)/2:(oh-ih)/2,setsar=1")


def main():
    meta = json.loads((ROOT / "voiceover.json").read_text(encoding="utf-8"))
    scenes = meta["scenes"]
    only = sys.argv[1:] or None

    final_dir = OUT / "final"
    final_dir.mkdir(parents=True, exist_ok=True)
    work = OUT / "work"
    work.mkdir(parents=True, exist_ok=True)

    scene_files = []
    for scene in scenes:
        sid = scene["id"]
        if only and sid not in only:
            continue
        print(f"── 合成 {sid} …")
        mp3 = TTS / f"{sid}.mp3"
        srt = TTS / f"{sid}.srt"
        if not mp3.exists():
            print(f"   跳过（缺少旁白 {mp3}）")
            continue

        narr_dur = dur(mp3)
        entries = parse_srt(srt)

        # 1) 标题卡
        title_png = work / f"{sid}-title.png"
        render_title(scene, title_png)
        title_mp4 = work / f"{sid}-title.mp4"
        run([
            "ffmpeg", "-v", "error", "-y", "-loop", "1", "-i", str(title_png),
            "-t", str(TITLE_SEC),
            "-vf", f"scale={W}:{H},fade=t=in:st=0:d=0.4,fade=t=out:st={TITLE_SEC - 0.4}:d=0.4,format=yuv420p",
            "-r", str(FPS), "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", str(title_mp4),
        ])

        # 2) 录屏（按旁白时长做轻微时长校正）；纯标题卡场景直接定格标题卡
        screen_mp4 = None
        if scene.get("screen") is None:
            held = work / f"{sid}-held.mp4"
            hold_sec = max(6.0, narr_dur + 1.5)
            run([
                "ffmpeg", "-v", "error", "-y", "-loop", "1", "-i", str(title_png),
                "-t", f"{hold_sec:.3f}",
                "-vf", f"scale={W}:{H},fade=t=in:st=0:d=0.6,format=yuv420p",
                "-r", str(FPS), "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", str(held),
            ])
            body = held
            total = hold_sec
            subdir = work / f"{sid}-subs"
            entries = []
        else:
            screen_mp4 = work / f"{sid}-screen.mp4"
            build_screen(scene, screen_mp4)
            s_dur = dur(screen_mp4)
            if s_dur > narr_dur * 1.04:
                factor = min(s_dur / narr_dur, 1.25)
                fixed = work / f"{sid}-screen-fix.mp4"
                run([
                    "ffmpeg", "-v", "error", "-y", "-i", str(screen_mp4),
                    "-vf", f"setpts={factor:.4f}*PTS,trim=duration={narr_dur:.3f},setpts=PTS-STARTPTS",
                    "-r", str(FPS), "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", str(fixed),
                ])
                screen_mp4 = fixed
            elif s_dur < narr_dur:
                # 录屏比旁白短：按比例放慢到旁白长度（下限 0.6 倍速，防止过慢），
                # 仍不足的部分再用末帧定格补足。
                ratio = s_dur / narr_dur
                factor = max(0.6, min(1.0, ratio * 1.02))
                new_dur = s_dur / factor
                pad = max(0.0, narr_dur - new_dur) + 0.4
                vf = f"setpts=1/{factor:.4f}*PTS,"
                if pad > 0.05:
                    vf += f"tpad=stop_mode=clone:stop_duration={pad:.3f},"
                vf += "format=yuv420p"
                fixed = work / f"{sid}-screen-fix.mp4"
                run([
                    "ffmpeg", "-v", "error", "-y", "-i", str(screen_mp4),
                    "-vf", vf,
                    "-r", str(FPS), "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", str(fixed),
                ])
                screen_mp4 = fixed

            # 3) 字幕 PNG
            subdir = work / f"{sid}-subs"
            render_subs(sid, entries, subdir)
            total = TITLE_SEC + dur(screen_mp4)

            # 4) 标题卡 + 录屏 合并
            body = work / f"{sid}-body.mp4"
            run([
                "ffmpeg", "-v", "error", "-y", "-i", str(title_mp4), "-i", str(screen_mp4),
                "-filter_complex", "[0:v][1:v]concat=n=2:v=1:a=0,format=yuv420p[v]",
                "-map", "[v]", "-r", str(FPS), "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", str(body),
            ])

        # 5) 叠加字幕 + 配音（字幕逐条 overlay，PNG 自带透明通道）
        delay_ms = 0 if scene.get("screen") is None else int(TITLE_SEC * 1000)
        inputs = ["-i", str(body)]
        for i in range(1, len(entries) + 1):
            inputs += ["-loop", "1", "-i", str(subdir / f"sub-{i:03d}.png")]
        inputs += ["-i", str(mp3)]

        chain = []
        prev = "0:v"
        for i, (s, e, _t) in enumerate(entries, 1):
            st = TITLE_SEC + s
            en = TITLE_SEC + e
            out = f"v{i}"
            chain.append(
                f"[{prev}][{i}:v]overlay=110:820:format=auto:"
                f"enable='between(t,{st:.3f},{en:.3f})'[{out}]"
            )
            prev = out
        delay_ms = 0 if scene.get("screen") is None else int(TITLE_SEC * 1000)
        chain.append(
            f"[{len(entries) + 1}:a]adelay={delay_ms}|{delay_ms},apad=pad_dur=1[a]"
        )
        scene_mp4 = final_dir / f"{sid}.mp4"
        run([
            "ffmpeg", "-v", "error", "-y", *inputs,
            "-filter_complex", ";".join(chain),
            "-map", "0:v" if not entries else f"[{prev}]", "-map", "[a]",
            "-t", f"{total + 0.5:.3f}",
            "-r", str(FPS), "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "160k", str(scene_mp4),
        ])
        print(f"   ✓ {scene_mp4.name}  {dur(scene_mp4):.1f}s")
        scene_files.append(scene_mp4)

    # 6) 汇总
    if scene_files:
        concat_list = work / "final-list.txt"
        with open(concat_list, "w", encoding="utf-8") as fh:
            for f in scene_files:
                fh.write(f"file '{f}'\n")
        out_video = final_dir / "特殊教育课程教学资源平台-功能演示.mp4"
        run([
            "ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", str(concat_list),
            "-c", "copy", str(out_video),
        ])
        print(f"\n完成 → {out_video}  时长 {dur(out_video):.1f}s")


if __name__ == "__main__":
    main()
