#!/usr/bin/env python3
"""生成一个结构合法的最小 PDF（用于 E2E 测试资源上传 / 预览 / 下载）。

不依赖任何第三方库：手写对象 + 正确 xref 偏移表，PDF 阅读器都能打开。

用法: mkpdf.py <输出文件> <标题文本>
"""
import sys


def build(title: str) -> bytes:
    # PDF 文本串里的 '(' ')' '\' 需要转义
    safe = title.replace("\\", r"\\").replace("(", r"\(").replace(")", r"\)")

    objs = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
        b"/Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
    ]

    lines = [
        b"BT",
        b"/F1 16 Tf",
        b"72 760 Td",
        b"(" + safe.encode("latin-1", "replace") + b") Tj",
        b"/F1 11 Tf",
        b"0 -30 Td",
        b"(KnowledgeHub E2E test material.) Tj",
        b"0 -18 Td",
        b"(Course: Python Data Analysis) Tj",
        b"ET",
    ]
    stream = b"\n".join(lines)
    objs.append(b"<< /Length " + str(len(stream)).encode() + b" >>\nstream\n" + stream + b"\nendstream")

    out = bytearray(b"%PDF-1.4\n%\xe2\xe3\xcf\xd3\n")
    offsets = []
    for i, body in enumerate(objs, start=1):
        offsets.append(len(out))
        out += str(i).encode() + b" 0 obj\n" + body + b"\nendobj\n"

    xref_pos = len(out)
    n = len(objs) + 1
    out += b"xref\n0 " + str(n).encode() + b"\n"
    out += b"0000000000 65535 f \n"
    for off in offsets:
        out += ("%010d 00000 n \n" % off).encode()
    out += (b"trailer\n<< /Size " + str(n).encode() + b" /Root 1 0 R >>\nstartxref\n"
            + str(xref_pos).encode() + b"\n%%EOF\n")
    return bytes(out)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    out_path = sys.argv[1]
    title = sys.argv[2] if len(sys.argv) > 2 else "KnowledgeHub E2E"
    with open(out_path, "wb") as fh:
        fh.write(build(title))
    return 0


if __name__ == "__main__":
    sys.exit(main())
