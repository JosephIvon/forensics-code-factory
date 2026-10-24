#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_book_structure.py —— 本卷结构体检。

逐章检查必备件：
  标题 / **本章承接**（须在章首 12 行内）/ **学完你会** / **前置** / 素材坐标 / 已验算 /
  证据与坐标区 / 可验证主张 / 讲师切分 / 习题 / 尾注坐标
检查导读页：标题 / 写给读者的建议 / 章速查表 / 证据与坐标区指引。
检查书级前后言：标题 / 主张块 / 尾注坐标。
并扫描违禁项：会话坐标、裸行号坐标。

**外加一条跨文件核对**：正文里写的锚点字符串（`axiscope@<commit>`）
必须与 `tools/_anchor.py` 里的 `ANCHOR_LABEL` **逐字一致**。
  为什么：书里说的锚与工具实际读的锚若是两个，坐标审计照样全绿——
  那就是"0 失败"变成假话的形状。锚点只该有一处定义，另一处必须被核对。
"""
from __future__ import annotations

import io
import os
import re
import sys
from pathlib import Path

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover - 环境相关
    pass

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _anchor import ANCHOR_COMMIT, ANCHOR_LABEL, FOREIGN_ANCHORS, REPO_NAME  # noqa: E402

BOOK = Path(__file__).resolve().parents[1]
SRC = BOOK / "chapters"
WANT_CHAPTERS = 4
BAD_PATTERNS = [
    # 「不设会话坐标」是正文里的否定用法，不是违禁项 → 一并排除
    (re.compile(r"(?<![禁不无])(?<!不设)会话坐标|本轮对话|上文提到|前面说过"), "会话坐标"),
    (re.compile(r"（`:\d+"), "裸行号坐标"),
]

bad = 0

guide = SRC / "00-导读.md"
files = sorted(x for x in os.listdir(SRC) if x.endswith(".md") and x != "00-导读.md")
print(f"[章节] 章数 {len(files)} / 预期 {WANT_CHAPTERS}", "OK" if len(files) == WANT_CHAPTERS else "❌")
if len(files) != WANT_CHAPTERS:
    bad += 1

if guide.is_file():
    g = io.open(guide, encoding="utf-8").read()
    gneed = {
        "标题": g.startswith("# "),
        "读者建议": "写给读者的建议" in g,
        "章速查": "速查" in g,
        "证据区指引": "证据与坐标" in g,
    }
    gmiss = [k for k, v in gneed.items() if not v]
    ghits = [lbl for pat, lbl in BAD_PATTERNS if pat.search(g)]
    if gmiss or ghits:
        bad += 1
        print(f"  ❌ 00-导读.md：缺 {gmiss or '无'}{' ｜ ' + ', '.join(ghits) if ghits else ''}")
    else:
        print(f"  ✅ 00-导读.md（{len(g.splitlines())} 行）")
else:
    bad += 1
    print("  ❌ 00-导读.md：不存在")

for fn in files:
    text = io.open(SRC / fn, encoding="utf-8").read()
    lines = text.splitlines()
    head = "\n".join(lines[:12])
    need = {
        "标题": text.startswith("# "),
        "承接段": "**本章承接**" in head,
        "学完你会": "**学完你会**" in head,
        "前置": "**前置**" in head,
        "素材坐标": "**素材坐标**" in text,
        "已验算": "**已验算**" in text,
        "证据与坐标区": "## 证据与坐标" in text,
        "可验证主张": ("可验证主张" in text),
        "讲师切分": ("**讲师 90 分钟**" in text) or ("讲师切分" in text),
        "尾注坐标": "事实性句子坐标" in text,
        "习题": "习题" in text,
    }
    miss = [k for k, v in need.items() if not v]
    hits = []
    for pat, label in BAD_PATTERNS:
        for m in pat.finditer(text):
            hits.append(f"{label}@L{text[:m.start()].count(chr(10)) + 1}")
    if miss:
        bad += 1
        print(f"  ❌ {fn}：缺 {miss}")
    elif hits:
        bad += 1
        print(f"  ❌ {fn}：{' ｜ '.join(hits)}")
    else:
        print(f"  ✅ {fn}（{len(lines)} 行）")

# ---- 书级前后言体检 ----
for fn in ("前言.md", "尾声.md"):
    path = BOOK / fn
    if not path.is_file():
        print(f"  ❌ {fn}：文件不存在")
        bad += 1
        continue
    text = io.open(path, encoding="utf-8").read()
    need = {
        "标题": text.startswith("# "),
        "主张块": "> **" in text,
        "尾注坐标": "事实性句子坐标" in text,
    }
    miss = [k for k, v in need.items() if not v]
    hits = []
    for pat, label in BAD_PATTERNS:
        for m in pat.finditer(text):
            hits.append(f"{label}@L{text[:m.start()].count(chr(10)) + 1}")
    if miss or hits:
        bad += 1
        print(f"  ❌ {fn}：缺 {miss or '无'}{' ｜ ' + ', '.join(hits) if hits else ''}")
    else:
        print(f"  ✅ {fn}（{len(text.splitlines())} 行）")

# ---- 锚点跨文件核对：正文里写的锚点字符串必须与 _anchor.py 一致 ----
all_book = []
for base in (SRC,):
    for fn in sorted(os.listdir(base)):
        if fn.endswith(".md"):
            all_book.append((fn, io.open(base / fn, encoding="utf-8").read()))
for fn in ("前言.md", "尾声.md", "ASSETS.md", "OUTLINE.md"):
    p = BOOK / fn
    if p.is_file():
        all_book.append((fn, io.open(p, encoding="utf-8").read()))

# 锚点分两类：
#   · 本仓锚 `trae_factory@<sha>` —— 必须等于 _anchor.py 的 ANCHOR_COMMIT；
#   · 跨仓锚 `axiscope@<sha>` / `mcopilot@<sha>` —— 必须等于 FOREIGN_ANCHORS。
#     本卷第 2、3、4 章有跨仓坐标（真值链的官方件、收尾案卷的对侧仓），
#     它们不是"写错了"，但**必须与登记值一致**——否则就是死指针
#     （第 4 章曾写 `axiscope@b95ffb2`，那个提交已随 axiscope 历史改写消失）。
declared: list[tuple[str, str, str]] = []
for fn, text in all_book:
    for m in re.finditer(r"([A-Za-z_][A-Za-z0-9_]*)@([0-9a-f]{6,40})", text):
        declared.append((fn, m.group(1), m.group(2)))

mine = [(fn, h) for fn, repo, h in declared if repo == REPO_NAME]
wrong_mine = [(fn, h) for fn, h in mine if h != ANCHOR_COMMIT]
foreign = [(fn, repo, h) for fn, repo, h in declared if repo in FOREIGN_ANCHORS]
wrong_foreign = [(fn, repo, h) for fn, repo, h in foreign if h != FOREIGN_ANCHORS[repo]]
unknown = sorted({repo for fn, repo, h in declared
                  if repo != REPO_NAME and repo not in FOREIGN_ANCHORS})

if wrong_mine or wrong_foreign or unknown:
    bad += 1
    if wrong_mine:
        print(f"  ❌ 本仓锚与 tools/_anchor.py 不一致（_anchor.py = {ANCHOR_COMMIT}）：")
        for fn, h in sorted(set(wrong_mine)):
            print(f"      {fn}: {REPO_NAME}@{h}")
    if wrong_foreign:
        print(f"  ❌ 跨仓锚与 FOREIGN_ANCHORS 不一致（死指针）：")
        for fn, repo, h in sorted(set(wrong_foreign)):
            print(f"      {fn}: {repo}@{h}（登记值 {FOREIGN_ANCHORS[repo]}）")
    if unknown:
        print(f"  ❌ 未登记的仓名出现在锚点里：{unknown}（要么是本仓名 {REPO_NAME}，要么登记进 FOREIGN_ANCHORS）")
else:
    print(f"  ✅ 锚点字符串一致（本仓 {len(set(mine))} 处 @{ANCHOR_COMMIT}"
          f" ｜ 跨仓 {len(set(foreign))} 处，均与 FOREIGN_ANCHORS 一致）")

print("问题章数：", bad)
sys.exit(1 if bad else 0)
