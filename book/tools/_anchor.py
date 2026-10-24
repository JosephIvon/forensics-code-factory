# -*- coding: utf-8 -*-
"""坐标锚点：**全书只此一处**定义。

为什么单独一个文件：书里每一处坐标都写着 `trae_factory@<commit>`，而工具读源文件时
也要用一个 commit。这两个东西**必须**是同一个——写成两处，早晚有一天它们会分叉，
于是"书里说的锚"与"工具实际读的锚"对不上，审计器却全绿：
**那正是"0 失败"变成假话的典型形状。**

仓名与目录名
------------
本卷从书仓搬进代码仓之后，**锚点就是本仓自己的某一次提交**（`954c210`，本卷的停机点）。
读者 clone 本仓即可用 `git show 954c210:<path>` 把每条坐标核到——这正是并仓的目的。

坐标前半截写的 `trae_factory` 是**项目标识**，与目录名/公开仓名
`forensics-code-factory` 是两层东西（2026-10-24 目录改名为与仓名一致，项目标识保留，
见仓根 `README.md` 的「仓名对照」）。`ANCHOR_LABEL` 必须与正文里的字符串逐字一致；
`REPO_NAME` 只用于**显示**，不参与找目录。

切锚的规矩：**先改这里并提交（那一版含最终代码与真值基座），
再改书里正文里的锚点字符串**。顺序反了，书就会去核一个还不存在的 commit。
"""
from __future__ import annotations

from pathlib import Path

#: 代码坐标按这个 commit 读（`git show <rev>:<path>`）
ANCHOR_COMMIT = "954c210"

#: 书里正文用的写法（与正文里的字符串必须逐字一致，由结构门禁核对）
ANCHOR_LABEL = f"trae_factory@{ANCHOR_COMMIT}"

#: 仓名（书里 `trae_factory@<commit>` 的前半截）——项目标识，不是目录名
REPO_NAME = "trae_factory"

#: 本仓根（`book/tools/_anchor.py` → `book` → 仓根）
REPO_ROOT = Path(__file__).resolve().parents[2]

#: **跨仓锚**：本卷有少数坐标指向兄弟仓（第 2、3 章的真值链与手册那两处）。
#: 它们不是本仓坐标，所以各有各的锚；列在这里是为了让结构门禁能区分
#: 「锚写错了」与「这是别的仓的锚」。值必须与那两个仓里实际存在的提交一致。
FOREIGN_ANCHORS = {
    "axiscope": "3576c9c",
    "mcopilot": "413aaba",
}
