#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成「网络安全技术」课程的纸质试卷样张（用于 OCR 解析测试）。

产出：
  1. network-security-exam.html —— 试卷排版源文件（手写体用逐字随机旋转/抖动模拟）
  2. network-security-exam.pdf —— A4 试卷 PDF（Chrome headless 打印）
  3. network-security-exam.png —— 试卷图片（PDF 栅格化，供「纸质试卷分析」上传）

题目与「网络安全技术」课程题库一一对应（按 章节 sortOrder + 建题时间 排序）：
  1. HTTPS 传输安全            判断题  5 分
  2. 对称加密算法识别          单选题  5 分
  3. 数字签名的作用            填空题  5 分
  4. 对称与非对称加密对比      简答题 10 分
  5. 防火墙默认策略            单选题  5 分
  6. 入侵检测系统缩写          单选题  5 分

用法：
  python3 scripts/paper-exam-sample/gen_paper_exam.py
  bash   scripts/paper-exam-sample/build.sh
"""

import html
import os
import random

random.seed(20261020)

HERE = os.path.dirname(os.path.abspath(__file__))

COURSE = "网络安全技术"
TITLE = "网络安全技术 期末考试试卷"
CLASS_NAME = "计算机应用技术 2301 班"
EXAM_DATE = "2026-10-20"

# 默认学生（qidi 租户示例账号 demo_student / 张晨）
STUDENT_NAME = "张晨"
STUDENT_NO = "demo_student"

INK = "#1c3d70"        # 学生蓝黑笔
RED = "#c0392b"        # 老师红笔
PRINT = "#1a1a1a"      # 印刷体

# ── 试卷内容 ────────────────────────────────────────────────
# kind: choice / judge / blank / short
QUESTIONS = [
    {
        "no": 1,
        "kind": "一、判断题",
        "text": "HTTPS 使用 TLS 协议保证传输过程中的机密性与完整性。",
        "full": 5,
        "score": 5,
        "options": None,
        "answer": "正确",
    },
    {
        "no": 2,
        "kind": "二、单选题",
        "text": "以下属于对称加密算法的是？",
        "full": 5,
        "score": 5,
        "options": ["RSA", "AES", "ECDSA", "SHA-256"],
        "answer": "B",
    },
    {
        "no": 3,
        "kind": "三、填空题",
        "text": "数字签名主要保证数据的完整性和 ______ 性。",
        "full": 5,
        "score": 5,
        "options": None,
        "answer": "不可否认",
    },
    {
        "no": 4,
        "kind": "四、简答题",
        "text": "简述对称加密与非对称加密的区别及适用场景。",
        "full": 10,
        "score": 7,
        "options": None,
        "answer": "对称加密加解密用同一把密钥，速度快，适合大批量数据加密；"
                  "非对称加密用公钥私钥对，速度慢，适合密钥交换和数字签名。",
    },
    {
        "no": 5,
        "kind": "五、单选题",
        "text": "防火墙的默认策略应遵循哪一原则？",
        "full": 5,
        "score": 0,
        "options": ["全部允许", "最小权限", "随机放行", "按 IP 长度放行"],
        "answer": "A",
    },
    {
        "no": 6,
        "kind": "六、单选题",
        "text": "用于检测网络入侵行为的系统缩写是？",
        "full": 5,
        "score": 5,
        "options": ["IDS", "DNS", "DHCP", "NAT"],
        "answer": "A",
    },
]

TOTAL_FULL = sum(q["full"] for q in QUESTIONS)
TOTAL_SCORE = sum(q["score"] for q in QUESTIONS)


def ink(text, color=INK, size=None, cls="hand"):
    """把一段文字渲染成「手写体」：逐字随机旋转 + 垂直抖动 + 笔迹颜色。"""
    out = []
    for ch in text:
        rot = random.uniform(-2.6, 2.6)
        dy = random.uniform(-1.1, 1.1)
        scale = random.uniform(0.985, 1.015)
        style = (
            f"color:{color};display:inline-block;"
            f"transform:rotate({rot:.2f}deg) translateY({dy:.2f}px) scale({scale:.3f});"
        )
        if size:
            style += f"font-size:{size}px;"
        out.append(f'<span style="{style}">{html.escape(ch)}</span>')
    return f'<span class="{cls}">{"".join(out)}</span>'


def build_html():
    parts = []
    parts.append(f"""<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8" />
<title>{html.escape(TITLE)}</title>
<style>
  @page {{ size: A4; margin: 16mm 15mm 14mm; }}
  * {{ box-sizing: border-box; }}
  html, body {{ margin: 0; padding: 0; }}
  body {{
    font-family: "Hiragino Sans GB", "Songti SC", serif;
    color: {PRINT};
    background: #fff;
    /* 轻微扫描倾斜 + 纸张灰边，模拟拍照/扫描件 */
    transform: rotate(-0.25deg);
    transform-origin: center;
  }}
  .sheet {{
    position: relative;
    padding: 6px 4px 10px;
    background:
      radial-gradient(120% 90% at 50% 40%, #ffffff 60%, #f3f1ec 100%);
  }}
  .sheet::after {{
    content: "";
    position: absolute; inset: 0;
    pointer-events: none;
    background: linear-gradient(90deg, rgba(0,0,0,.05), rgba(0,0,0,0) 6%, rgba(0,0,0,0) 94%, rgba(0,0,0,.05));
  }}
  h1 {{
    text-align: center;
    font-size: 23px;
    letter-spacing: 3px;
    margin: 4px 0 10px;
    font-weight: 700;
  }}
  .meta {{
    display: flex;
    flex-wrap: wrap;
    justify-content: space-between;
    gap: 2px 20px;
    font-size: 13.5px;
    padding-bottom: 6px;
    border-bottom: 1px solid #444;
    margin-bottom: 12px;
  }}
  .field {{ display: inline-flex; gap: 6px; align-items: baseline; white-space: nowrap; }}
  .field .label {{ color: #444; }}
  .section-title {{
    font-size: 15px;
    font-weight: 700;
    margin: 16px 0 8px;
  }}
  .q {{
    margin: 0 0 18px;
    page-break-inside: avoid;
  }}
  .q-head {{
    font-size: 14.5px;
    line-height: 1.95;
  }}
  .q-score {{ margin-left: 30px; white-space: nowrap; }}
  .opts {{
    font-size: 13.5px;
    margin: 2px 0 0 30px;
    display: flex;
    gap: 30px;
    color: #333;
  }}
  .answer {{
    margin: 4px 0 0 30px;
    font-size: 15px;
    line-height: 1.9;
  }}
  .answer .label {{ color: #666; font-size: 13px; margin-right: 6px; }}
  .hand {{ font-family: "Hiragino Sans GB", "Songti SC", serif; }}
  .foot {{
    margin-top: 18px;
    padding-top: 10px;
    border-top: 1px dashed #999;
    display: flex;
    justify-content: space-between;
    font-size: 13.5px;
    color: #555;
  }}
</style>
</head>
<body>
<div class="sheet">
""")

    parts.append(f"  <h1>{html.escape(TITLE)}</h1>\n")
    parts.append(
        '  <div class="meta">\n'
        f'    <span class="field"><span class="label">学号：</span><span class="hand">{html.escape(STUDENT_NO)}</span></span>\n'
        f'    <span class="field"><span class="label">姓名：</span>{ink(STUDENT_NAME)}</span>\n'
        f'    <span class="field"><span class="label">班级：</span><span class="hand">{html.escape(CLASS_NAME)}</span></span>\n'
        f'    <span class="field"><span class="label">考试日期：</span><span class="hand">{html.escape(EXAM_DATE)}</span></span>\n'
        f'    <span class="field"><span class="label">课程：</span><span class="hand">{html.escape(COURSE)}</span></span>\n'
        '  </div>\n'
    )

    last_kind = None
    for q in QUESTIONS:
        if q["kind"] != last_kind:
            parts.append(f'  <div class="section-title">{html.escape(q["kind"])}</div>\n')
            last_kind = q["kind"]

        # 题干 + 印刷满分 + 老师手写得分（同一行，便于按行解析得分/满分）
        parts.append(
            f'  <div class="q">\n'
            f'    <div class="q-head">\n'
            f'      <span class="q-text">{q["no"]}. {html.escape(q["text"])}'
            f'（{q["full"]}分）</span>\n'
            f'      <span class="q-score">{ink("得分：%d分" % q["score"], RED, 15)}</span>\n'
            f'    </div>\n'
        )
        if q["options"]:
            letters = "ABCD"
            opts = "".join(
                f'<span>{letters[i]}. {html.escape(o)}</span>'
                for i, o in enumerate(q["options"])
            )
            parts.append(f'    <div class="opts">{opts}</div>\n')
        parts.append(
            f'    <div class="answer"><span class="label">作答：</span>{ink(q["answer"], INK, 15)}</div>\n'
            f'  </div>\n'
        )

    parts.append(
        '  <div class="foot">\n'
        f'    <span>阅卷人签字：{ink("王老师", RED, 13)}</span>\n'
        f'    <span>总分：{ink("%d分" % TOTAL_SCORE, RED, 13)} / {TOTAL_FULL} 分</span>\n'
        '  </div>\n'
    )
    parts.append("</div>\n</body>\n</html>\n")
    return "".join(parts)


def main():
    global STUDENT_NAME, STUDENT_NO, CLASS_NAME, EXAM_DATE

    import argparse

    parser = argparse.ArgumentParser(description="生成网络安全技术纸质试卷样张")
    parser.add_argument("--name", default=STUDENT_NAME, help="考生姓名")
    parser.add_argument("--no", dest="student_no", default=STUDENT_NO, help="考生学号")
    parser.add_argument("--class", dest="class_name", default=CLASS_NAME, help="班级")
    parser.add_argument("--date", default=EXAM_DATE, help="考试日期")
    parser.add_argument("--out", default="network-security-exam.html", help="输出 HTML 文件名")
    parser.add_argument("--seed", type=int, default=20261020, help="手写体抖动随机种子")
    args = parser.parse_args()

    STUDENT_NAME = args.name
    STUDENT_NO = args.student_no
    CLASS_NAME = args.class_name
    EXAM_DATE = args.date
    random.seed(args.seed)

    out = os.path.join(HERE, args.out)
    with open(out, "w", encoding="utf-8") as f:
        f.write(build_html())
    print(f"[gen] {out}")
    print(f"[gen] 考生 {STUDENT_NAME}（{STUDENT_NO}）· 题目 {len(QUESTIONS)} 道 · 满分 {TOTAL_FULL} 分 · 作答合计 {TOTAL_SCORE} 分")


if __name__ == "__main__":
    main()
