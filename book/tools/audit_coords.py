#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_coords.py —— 本卷坐标审计：把每处「文件:行号」引用解析回源文件，
打印被引行的**真实内容**，供人工核对"挂的那一行是不是真说了那句话"。

判据（三档，全绿才算过）：
  · 引用可解析（文件找到 + 行号存在）→ [OK]，附被引行原文
  · 行号超出文件行数 → [MISSING-LINE]（行锚腐烂 / 指向未来行）→ **判失败**
  · 文件根本找不到 → [MISSING-FILE] → **判失败**
    （例外：登记在 `book/evidence/REGISTRY.json` 里的**仓外原件**——
     它们按设计不在仓内，指针 + size + sha256 留在仓里。这类**必须有登记**，
     没登记的"仓外原件"一律判失败：否则拼错一个文件名也会被放过，
     门禁就成了只会说"通过"的摆设。）

**按锚点读源文件**：源文件内容从**锚点 commit** 取（`git show`），不在工作树取。
  锚点冻结 = 坐标含义冻结。工作树里改了行号，坐标不动；
  锚点往后挪，正文里的锚点字符串与本工具的 ANCHOR_COMMIT 一起改（见 `_anchor.py`）。

**书是活物，读工作树；只有代码读锚点。**
  `book/` 落在锚点根下面，若也按锚点读，书里对 `book/evidence/` 的引用
  会被拿上一次提交的文字去核——核的是旧行号、报的是假结果。

**行锚断言（可选但推荐）**：`book/checks.json` 里可以给关键坐标登记
"这一行必须含有哪几个词"。登记了就**当硬门**：那一行不含登记词 → 判失败。
  理由：「行还在」不等于「行在说那句话」。只验行号存在的那种审计，
  能挡住行锚腐烂，挡不住**指错行**——而指错行恰好是更常见也更危险的那一种。
"""
from __future__ import annotations

import io
import json
import os
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

# 控制台编码不是 UTF-8 时 print 会抛 UnicodeEncodeError —— 脚本崩在打印上，
# 而不是给出判定。GitHub Actions runner 的控制台是 cp1252，本机是 UTF-8：
# 于是本机绿、runner 红。所以 stdout 一律按 UTF-8 走。
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover - 环境相关
    pass

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _anchor import ANCHOR_COMMIT, ANCHOR_LABEL, FOREIGN_ANCHORS  # noqa: E402

BOOK_DIR = Path(__file__).resolve().parents[1]          # <repo>/book
REPO_ROOT = BOOK_DIR.parent                             # <repo>

# 短名 → 相对路径（书里允许写 `KB0:80` 这种简写）
# 本卷只用一个简写：`KB7` 是**兄弟仓 axiscope** 的知识库条目（第 4 章引用它做标题裁决）。
# 简写表里写清楚它属于哪个仓，免得被当成本仓文件去找。
ALIAS = {
    "KB7": ["@axiscope:docs/kb/KB7-dataset-da-rus-cat.md"],
}

CITE = re.compile(
    r"(?P<file>[A-Za-z0-9_\-一-鿿\./]+\.(?:md|py|ps1|json|jsonl|csv|txt|html|exe))"
    r"[:：](?P<line>\d+)(?:[-–](?P<line2>\d+))?"
)
ALIAS_CITE = re.compile(
    r"(?<![A-Za-z0-9_\-/\.])(?:KB(?P<kn>\d)|C(?P<cn>[1-7]))[:：](?P<line>\d+)"
    r"(?:[-–](?P<line2>\d+))?"
)

_cache: dict[str, list[str] | None] = {}


# ---------------------------------------------------------------- 锚点读

def _git_show(rel: str) -> list[str] | None:
    """从锚点 commit 读仓内文件内容；不在锚点里 → None。"""
    r = subprocess.run(["git", "-C", str(REPO_ROOT), "show", f"{ANCHOR_COMMIT}:{rel}"],
                       capture_output=True)
    if r.returncode != 0:
        return None
    return r.stdout.decode("utf-8", "replace").splitlines()


def load(abs_path: str) -> list[str] | None:
    """取文件内容：**代码读锚点，书读工作树**。"""
    if abs_path in _cache:
        return _cache[abs_path]
    ap = os.path.abspath(abs_path)
    book_abs = os.path.abspath(str(BOOK_DIR))
    if ap.startswith(book_abs + os.sep):
        try:
            with io.open(abs_path, "r", encoding="utf-8", errors="replace") as fh:
                _cache[abs_path] = fh.read().splitlines()
        except OSError:
            _cache[abs_path] = None
        return _cache[abs_path]
    try:
        rel = os.path.relpath(abs_path, str(REPO_ROOT)).replace(os.sep, "/")
    except ValueError:
        rel = ""
    anchored = _git_show(rel) if rel else None
    if anchored is not None:
        _cache[abs_path] = anchored
        return anchored
    try:
        with io.open(abs_path, "r", encoding="utf-8", errors="replace") as fh:
            _cache[abs_path] = fh.read().splitlines()
    except OSError:
        _cache[abs_path] = None
    return _cache[abs_path]


# ---------------------------------------------------------------- 解析

#: 兄弟仓根：本卷有几处坐标指向它们（真值链的官方 CSV、手册知识库条目）。
#: 只在**本机存在**时参与解析；不在位时那几条坐标会落进「仓外且不可判定」。
def _sibling_roots() -> dict[str, Path]:
    ws = REPO_ROOT.parent
    out: dict[str, Path] = {}
    for name in FOREIGN_ANCHORS:
        # 目录名可能与仓名不同（axiscope 同名；mcopilot 的目录叫 forensics-mcopilot）
        for cand in (name, "forensics-" + name):
            p = ws / cand
            if p.is_dir():
                out[name] = p
                break
    return out


SIBLINGS = _sibling_roots()


def origin_of(abs_path: str) -> str:
    """这个文件属于哪个仓：'' 本仓 / 'book' 书目录 / 兄弟仓名。"""
    ap = os.path.abspath(abs_path)
    for name, root in SIBLINGS.items():
        if ap.startswith(str(root) + os.sep):
            return name
    if ap.startswith(str(BOOK_DIR) + os.sep):
        return "book"
    return ""


def resolve(rel: str) -> tuple[str | None, list[str] | None]:
    """把书里写的路径解析成绝对路径 + 内容。找不到返回 (None, None)。

    依次试：本仓根 → 书的目录（书里省了 `book/` 前缀）→ 兄弟仓。
    兄弟仓命中说明这是**跨仓坐标**，打印时会标出来源，不冒充本仓坐标。
    """
    rel = rel.replace("/", os.sep)
    if rel.startswith("@"):
        # `@axiscope:docs/kb/…` 这种显式跨仓写法：按第一个 ":" 切成「仓名 : 仓内路径」
        name, _, inner = rel[1:].partition(":")
        root = SIBLINGS.get(name)
        if root and inner:
            cand = os.path.join(str(root), inner.replace("/", os.sep).replace(os.sep * 2, os.sep))
            if os.path.isfile(cand):
                return cand, load(cand)
        return None, None
    cands = [os.path.join(str(REPO_ROOT), rel), os.path.join(str(BOOK_DIR), rel)]
    if os.sep not in rel:
        # 只给文件名时按 basename 全仓找
        for dirpath, dirs, files in os.walk(str(REPO_ROOT)):
            dirs[:] = [d for d in dirs if d not in (".git", "__pycache__", ".pytest_cache")]
            if os.path.basename(rel) in files:
                cands.append(os.path.join(dirpath, os.path.basename(rel)))
    for c in cands:
        if os.path.isfile(c):
            lines = load(c)
            if lines is not None:
                return c, lines
    # 兄弟仓：同一相对路径
    for name, root in SIBLINGS.items():
        cand = os.path.join(str(root), rel)
        if os.path.isfile(cand):
            lines = load(cand)
            if lines is not None:
                return cand, lines
    # 只给 basename 时，再试兄弟仓里**书里点过名的落点**（官方真值件在 mcopilot 的
    # `knowledge/sdks/official_api/`，见本卷导读「关于坐标：这一卷有一处跨仓锚」）。
    if os.sep not in rel:
        for name, root in SIBLINGS.items():
            cand = os.path.join(str(root), "knowledge", "sdks", "official_api", rel)
            if os.path.isfile(cand):
                lines = load(cand)
                if lines is not None:
                    return cand, lines
    return None, None


def registered_external(rel: str) -> bool:
    """仓外原件登记表里有的路径 → 允许"不在仓内"。"""
    reg = BOOK_DIR / "evidence" / "REGISTRY.json"
    if not reg.is_file():
        return False
    try:
        data = json.loads(reg.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return False
    tail = rel.replace("\\", "/")
    for item in data.get("externals", []):
        p = str(item.get("path", "")).replace("\\", "/")
        if not p:
            continue
        if tail == p or tail.endswith("/" + p) or p.endswith("/" + tail):
            return True
    return False


# ---------------------------------------------------------------- 行锚断言

def load_checks() -> dict:
    p = BOOK_DIR / "checks.json"
    if not p.is_file():
        return {}
    try:
        return json.loads(p.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        print(f"  [CHECKS-UNREADABLE] {p}：{exc}")
        return {}


def check_keywords(coord: str, lines: list[str], ln: int, ln2: int, want) -> list[str]:
    """被引行必须含有登记词——否则"行还在"但"行没说那句话"，照样判失败。"""
    seg = " ".join(lines[i - 1] for i in range(ln, min(ln2, len(lines)) + 1)) if ln <= len(lines) else ""
    miss = [w for w in want if w not in seg]
    return miss


# ---------------------------------------------------------------- 主流程

def main() -> int:
    checks = load_checks()
    total = ok = bad = 0
    checked = 0
    per_file: dict[str, list] = defaultdict(list)
    files: list[str] = []
    chapters = BOOK_DIR / "chapters"
    if chapters.is_dir():
        files += [str(chapters / f) for f in sorted(os.listdir(chapters)) if f.endswith(".md")]
    for extra in ("前言.md", "尾声.md"):
        if (BOOK_DIR / extra).is_file():
            files.append(str(BOOK_DIR / extra))

    print("=" * 92)
    print(f"坐标审计：锚点 {ANCHOR_LABEL}（代码按锚点读，书读工作树）")
    print("=" * 92)

    for path in files:
        fn = os.path.basename(path)
        text = io.open(path, encoding="utf-8").read()
        cites: list[tuple[str, int, int, str]] = []
        for m in CITE.finditer(text):
            cites.append((m.group("file"), int(m.group("line")),
                          int(m.group("line2")) if m.group("line2") else int(m.group("line")),
                          "FILE"))
        for m in ALIAS_CITE.finditer(text):
            key = ("KB%s" % m.group("kn")) if m.group("kn") else ("C%s" % m.group("cn"))
            cites.append((key, int(m.group("line")),
                          int(m.group("line2")) if m.group("line2") else int(m.group("line")),
                          "ALIAS"))
        if not cites:
            continue
        print(f"\n—— {fn} ——")
        for raw, ln, ln2, kind in cites:
            total += 1
            if kind == "ALIAS":
                rel = None
                for cand in ALIAS.get(raw, []):
                    ap, lines = resolve(cand)
                    if lines is not None:
                        rel, lines = cand, lines
                        break
                if rel is None:
                    # 简写解析失败 ≠ 坐标错：它指向的文件可能登记在仓外原件表里
                    # （`KB7` → axiscope 的 docs/kb/KB7-…），干净克隆上不在位而已。
                    probe = raw
                    for cand in ALIAS.get(raw, []):
                        probe = cand.split(":", 1)[1] if cand.startswith("@") else cand
                        break
                    if registered_external(raw) or registered_external(probe):
                        print(f"  [EXTERNAL] {raw}:{ln}（仓外原件，已登记：指针+size+sha256 留在仓内）")
                        continue
                    bad += 1
                    print(f"  [MISSING-FILE] {raw}:{ln}（别名未解析，且未登记为仓外原件）")
                    continue
                abs_p = os.path.join(str(REPO_ROOT), rel.replace("/", os.sep))
                label = raw
            else:
                rel = raw
                abs_p, lines = resolve(raw)
                label = raw
                if lines is None:
                    # 简写解析失败时，用它的**目标路径**去查登记表：
                    # `KB7` → `docs/kb/KB7-dataset-da-rus-cat.md`，那个文件在 axiscope 仓，
                    # 干净克隆上不在位，但它是登记在册的仓外原件，不是断链。
                    probe = raw
                    for cand in ALIAS.get(raw, []):
                        probe = cand.split(":", 1)[1] if cand.startswith("@") else cand
                        break
                    if registered_external(raw) or registered_external(probe):
                        print(f"  [EXTERNAL] {raw}:{ln}（仓外原件，已登记：指针+size+sha256 留在仓内）")
                        continue
                    bad += 1
                    print(f"  [MISSING-FILE] {raw}:{ln}（仓内找不到；未登记为仓外原件）")
                    continue
            if ln < 1 or ln > len(lines):
                bad += 1
                print(f"  [MISSING-LINE] {label}:{ln}（该文件共 {len(lines)} 行）")
                continue
            if ln2 > len(lines) or ln2 < ln:
                bad += 1
                print(f"  [MISSING-LINE] {label}:{ln}-{ln2}（该文件共 {len(lines)} 行）")
                continue
            ok += 1
            per_file[abs_p].append((fn, ln, ln2))
            seg = " ".join(lines[i - 1].strip() for i in range(ln, min(ln2, ln + 1) + 1))
            if len(seg) > 160:
                seg = seg[:160] + "…"
            # 行锚断言
            want = checks.get(f"{rel}:{ln}") or checks.get(f"{rel}:{ln}-{ln2}") \
                or checks.get(f"{label}:{ln}")
            if want:
                checked += 1
                miss = check_keywords(f"{rel}:{ln}", lines, ln, ln2, want)
                if miss:
                    bad += 1
                    print(f"  [WRONG-LINE] {label}:{ln} 行在，但不含登记词 {miss} → {seg}")
                    continue
            org = origin_of(abs_p)
            tag = "" if org in ("", "book") else f"  ［跨仓:{org}］"
            print(f"  [OK] {label}:{ln}{'-' + str(ln2) if ln2 != ln else ''} → {seg}{tag}")

    print()
    print("=" * 92)
    print(f"坐标审计合计：引用 {total} ｜ 可解析 {ok} ｜ 失败 {bad}"
          + (f" ｜ 行锚断言核对 {checked} 处" if checked else ""))
    print("=" * 92)
    multi = 0
    for abs_p, cites in per_file.items():
        byfile = defaultdict(set)
        for fn, a, b in cites:
            byfile[fn].add((a, b))
        if len(byfile) > 1:
            multi += 1
            print(f"[多章引用同一文件] {os.path.relpath(abs_p, str(REPO_ROOT))}")
            for fn, ls in byfile.items():
                print(f"    {fn}: {sorted(ls)}")
    if multi:
        print("  （同一文件被多章引用不同行号——人工确认是否有意为之）")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
