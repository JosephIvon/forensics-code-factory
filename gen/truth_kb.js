// truth_kb.js - 真值链接缝模块：生成时实时读取 mcopilot 仓库 knowledge/sdks/*.json，
// 为本工厂每条 P/Invoke 挂 [ 源 | 核对 | 提取 ] 真值链标注，未收录/无真值品牌显式降级。
//
// 设计原则（与 mcopilot 的 R1~R6 同一口径）：
//   源   = verified  ：这条结论从哪来（官方手册/官方绑定/公开资料/未标注）
//   核对 = check     ：验到哪一步（manual 人工核对 / hardware 真机 / auto 机器比对 / none 未核对）
//   提取 = extraction：签名对是否等于调用对（judgment = 有语义陷阱；mechanical = 机械可复核）
//   本模块只【标注与报告】，不改写任何签名 —— 两边口径不一致时显式列出，人工裁决，
//   绝不悄悄选边（谁都不是第三个事实源：唯一事实源是 mcopilot knowledge/sdks 的官方绑定行号）。
//
// 知识库发现顺序：环境变量 MCOPILOT_ROOT → 本机默认安装路径 → 找不到则整体降级（生成不中断）。
(function (root, factory) {
  if (typeof module === 'object' && module.exports) { module.exports = factory(); }
  else { root.MCTruth = factory(); }
})(typeof self !== 'undefined' ? self : this, function () {

  'use strict';

  // 工厂品牌 id → mcopilot 知识库文件名（knowledge/sdks/<名>.json）。没有对应文件的品牌走降级路径。
  var BRAND_SDK = { leadshine: 'dmc', gts: 'gts', zmotion: 'zmotion' };

  var DEFAULT_ROOTS = [
    (typeof process !== 'undefined' && process.env && process.env.MCOPILOT_ROOT) || null,
    'D:\\workspace\\mcopilot'
  ];

  var _mc = null;          // { root, sdks, available, note } —— 知识库加载缓存（进程内一次）
  var _runAudits = [];     // 本次 generate() 里各品牌的审计记录（docsFile 消费）

  // ---------- 知识库加载 ----------

  function loadMcopilot() {
    if (_mc) return _mc;
    _mc = { root: null, sdks: {}, available: false, note: '' };
    var fs = (typeof require === 'function') ? tryRequire('fs') : null;
    var path = (typeof require === 'function') ? tryRequire('path') : null;
    if (!fs || !path) {
      _mc.note = '非 Node 环境（浏览器单文件模式），mcopilot 知识库不可达';
      return _mc;
    }
    for (var i = 0; i < DEFAULT_ROOTS.length; i++) {
      var r = DEFAULT_ROOTS[i];
      if (!r) continue;
      var dir = path.join(r, 'knowledge', 'sdks');
      if (!fs.existsSync(dir)) continue;
      var sdks = {}, ok = true;
      var names = ['dmc', 'gts', 'zmotion'];
      for (var n = 0; n < names.length; n++) {
        try { sdks[names[n]] = JSON.parse(fs.readFileSync(path.join(dir, names[n] + '.json'), 'utf8')); }
        catch (e) { ok = false; break; }
      }
      if (ok) { _mc = { root: r, sdks: sdks, available: true, note: '' }; return _mc; }
    }
    _mc.note = '未找到 mcopilot 知识库（可设环境变量 MCOPILOT_ROOT 指向 mcopilot 仓库根）';
    return _mc;
  }

  function tryRequire(name) {
    try { return require(name); } catch (e) { return null; }
  }

  // fn名 → 条目；entry_point(导出名) → 条目
  function buildIndex(sdk) {
    var byFn = {}, byEp = {};
    var pinv = (sdk && sdk.pinvoke) || [];
    for (var i = 0; i < pinv.length; i++) {
      var e = pinv[i];
      if (e && e.fn) byFn[e.fn] = e;
      if (e && e.entry_point) byEp[e.entry_point] = e;
    }
    return { fn: byFn, ep: byEp };
  }

  // ---------- 标注格式（与 mcopilot 适配器生成器同一措辞） ----------

  function basename(p) { return String(p).split(/[\\\/]/).pop(); }

  function truthChainLine(entry) {
    var prov = entry.provenance || {};
    var seg = '源 ' + (entry.verified || '未标注') + ' | 核对 ' + (entry.check || 'none') + ' | 提取 ' + (entry.extraction || 'judgment');
    var tail = [];
    if (prov.file) tail.push(basename(prov.file) + ':' + (prov.line || '?'));
    var ref = prov.ref || '';
    if (ref && String(ref).indexOf('官方绑定') !== 0) tail.push(ref);  // 绑定行号已并入 tail，手册页才单独展示
    tail.push(prov.exported ? '导出表✓' : '导出表无背书');
    return '// [ ' + seg + ' ]  ' + tail.join(' · ');
  }

  function todoLine(fn, sdkFile) {
    if (sdkFile) {
      return '        // TODO[需核对厂商手册] ' + fn + ' —— 核对后更新 mcopilot 仓库 knowledge/sdks/' + sdkFile +
             ' 并重跑 scripts/annotate_provenance.py --check，然后重新生成本套件';
    }
    return '        // TODO[需核对厂商手册] ' + fn + '：签名未经外部真值钉扎 —— 按官方 C# 绑定逐字核对后再用';
  }

  // ---------- 核心装饰：解析 pinvoke 字面块，逐条 DllImport+extern 插真值链 ----------

  var EXTERN_RE = /^\s*(?:private|public|internal)\s+static\s+extern\s+([\w\.]+)\s+(\w+)\s*\((.*)\);\s*$/;

  function decorate(kb) {
    if (kb._truthAudit) return kb._truthAudit;  // 幂等：同一品牌 KB 只装饰一次
    var mc = loadMcopilot();
    var sdkName = BRAND_SDK[kb.id] || null;
    var sdk = sdkName ? mc.sdks[sdkName] : null;
    var sdkFile = sdk ? (sdk.sdk_file || (sdkName + '.json')) : null;
    var idx = sdk ? buildIndex(sdk) : null;

    var audit = {
      brandId: kb.id, label: kb.label, sdkFile: sdkFile, mcopilotRoot: mc.root,
      available: !!sdk, sourceNote: sdk ? ('mcopilot knowledge/sdks/' + sdkFile) : (mc.note || ('mcopilot 知识库无 ' + kb.label + ' 的绑定口径')),
      total: 0, matched: 0, missed: [], mismatched: [], noneList: [],
      chk: { manual: 0, hardware: 0, auto: 0, none: 0 },
      rows: []
    };

    var lines = String(kb.pinvoke || '').split('\n');
    var out = [];
    for (var i = 0; i < lines.length; i++) {
      var line = lines[i];
      var isDllImport = /^\s*\[DllImport/.test(line);
      var nextExt = (i + 1 < lines.length) ? lines[i + 1].match(EXTERN_RE) : null;

      if (isDllImport && nextExt) {
        emitChain(out, audit, idx, sdkFile, nextExt);
        out.push(line);
        out.push(lines[i + 1]);
        i++;
        continue;
      }
      var selfExt = line.match(EXTERN_RE);
      if (selfExt) { emitChain(out, audit, idx, sdkFile, selfExt); out.push(line); continue; }
      out.push(line);
    }

    var summary = buildSummary(audit);
    kb.pinvoke = summary + '\n' + out.join('\n');
    kb._truthAudit = audit;
    _runAudits.push(audit);
    return audit;
  }

  function emitChain(out, audit, idx, sdkFile, m) {
    var ret = m[1], fn = m[2];
    var argc = m[3].trim() === '' ? 0 : m[3].split(',').length;
    audit.total++;
    var entry = idx ? (idx.fn[fn] || idx.ep[fn]) : null;

    if (!entry) {
      audit.missed.push(fn);
      // 注意：未收录降级不进 noneList —— "未核对"专指钉扎了但 check=none 的条目，
      // 降级条目已在 missed 里单独统计，重复计入会让总汇总 56+2+2=60 这种账对不上。
      out.push('        // [ 源 未标注 | 核对 none | 提取 judgment ]  mcopilot 知识库未收录此函数');
      out.push(todoLine(fn, null));
      audit.rows.push({ fn: fn, status: '未收录·已降级', ours: ret + '(' + argc + '参)', theirs: '—', src: '—' });
      return;
    }

    audit.matched++;
    var check = entry.check || 'none';
    audit.chk[check] = (audit.chk[check] || 0) + 1;
    if (check === 'none') { audit.noneList.push(fn); out.push(todoLine(fn, sdkFile)); }

    var prov = entry.provenance || {};
    var mret = entry.ret || '';
    var margc = (entry.args || []).length;
    var consistent = (mret === ret && margc === argc);
    if (!consistent) {
      audit.mismatched.push({ fn: fn, ours: ret + '(' + argc + '参)', theirs: mret + '(' + margc + '参)' });
      out.push('        // 【口径不一致·用前必须人工裁决】' + fn + '：本套件 ' + ret + '/' + argc + '参 vs mcopilot ' + mret + '/' + margc + '参 —— 两边都可能有据，见 docs/TruthChain.md 对照表');
    }
    out.push('        ' + truthChainLine(entry));
    audit.rows.push({
      fn: fn, status: consistent ? (check === 'none' ? '已钉扎·未核对' : '已钉扎') : '口径不一致',
      ours: ret + '(' + argc + '参)', theirs: mret + '(' + margc + '参)',
      src: (prov.file ? basename(prov.file) + ':' + (prov.line || '?') : '—') + (prov.exported ? ' · 导出表✓' : ' · 导出表无背书')
    });
  }

  function buildSummary(audit) {
    var L = [];
    L.push('        // ── 真值链（每条 P/Invoke 上方：[ 源 | 核对 | 提取 ]）──');
    if (audit.available) {
      L.push('        //   数据源：' + audit.sourceNote + '（生成时实时读取，非复制）');
      L.push('        //   源=verified 这条结论从哪来 ｜ 核对=check 验到哪一步 ｜ 提取=extraction 签名对≠调用对时是否需人签字');
      L.push('        //   本套件 P/Invoke ' + audit.total + ' 条：知识库钉扎 ' + audit.matched +
             ' / 未收录(显式降级) ' + audit.missed.length +
             (audit.mismatched.length ? ' / 口径不一致 ' + audit.mismatched.length : ''));
      L.push('        //   核对进度（钉扎条目）：manual ' + audit.chk.manual + ' / hardware ' + audit.chk.hardware +
             ' / auto ' + audit.chk.auto + ' / none ' + audit.chk.none);
      if (audit.noneList.length) L.push('        //   ⚠ 未核对 ' + audit.noneList.length + ' 条：' + audit.noneList.join(', ') + ' —— 已标 TODO，不许当事实用');
      if (audit.mismatched.length) L.push('        //   ⚠ 口径不一致 ' + audit.mismatched.length + ' 条：' + audit.mismatched.map(function (m) { return m.fn; }).join(', '));
      L.push('        //   复核：mcopilot 仓库跑 scripts/annotate_provenance.py --check；重新生成本套件即自动继承最新口径');
    } else {
      L.push('        //   ⚠ ' + audit.sourceNote);
      L.push('        //   本套件全部 P/Invoke 按 [ 源 未标注 | 核对 none | 提取 judgment ] 处理 —— 上线前必须逐条按官方手册核对');
    }
    return L.join('\n');
  }

  // ---------- 运行级报告 ----------

  function resetRun() { _runAudits = []; }

  function runSummary() {
    if (!_runAudits.length) return null;
    var mc = loadMcopilot();
    if (!mc.available) {
      return 'mcopilot 知识库不可达，全部品牌 P/Invoke 已按未标注降级（' + mc.note + '）';
    }
    var t = 0, m = 0, mis = 0, none = 0, brands = [];
    for (var i = 0; i < _runAudits.length; i++) {
      var a = _runAudits[i];
      t += a.total; m += a.matched; mis += a.mismatched.length; none += a.noneList.length;
      brands.push(a.label.split(' ')[0] + ' ' + a.matched + '/' + a.total);
    }
    return '真值链：P/Invoke 共 ' + t + ' 条，mcopilot 知识库钉扎 ' + m + ' / 未收录降级 ' + (t - m) +
           (mis ? ' / 口径不一致 ' + mis : '') + ' / 未核对 ' + none +
           '（' + brands.join('，') + '；逐条对照见 docs/TruthChain.md）';
  }

  function docsFile() {
    if (!_runAudits.length) return null;
    var mc = loadMcopilot();
    var ts = new Date().toISOString().replace('T', ' ').substring(0, 19);
    var s = '';
    s += '# docs/TruthChain.md — P/Invoke 真值链审计\n\n';
    s += '> 数据源：' + (mc.available ? ('mcopilot 仓库 `knowledge/sdks/{dmc,gts,zmotion}.json`（生成时实时读取，非复制）\n') : '⚠ mcopilot 知识库不可达，本套件全部按未标注降级\n');
    s += '> 根目录：' + (mc.root || '—') + '　|　生成时间：' + ts + '\n\n';
    s += '**三根轴**：源 = verified（这条结论从哪来）｜核对 = check（验到哪一步：manual 人工 / hardware 真机 / auto 机器比对 / none 未核对）｜提取 = extraction（签名对是否等于调用对；judgment = 有语义陷阱，必须人签字）。\n\n';
    s += '**纪律（R2）**：judgment 条目必须 manual/hardware 签字才能当事实用；`check=none` 的条目生成代码里已标 TODO[需核对厂商手册]。\n';
    s += '**接缝说明**：本工厂的知识库不另设真值副本 —— mcopilot 侧 `scripts/annotate_provenance.py --check` 复核后，重新生成本套件即自动继承最新口径。\n\n';

    for (var i = 0; i < _runAudits.length; i++) {
      var a = _runAudits[i];
      s += '## ' + a.label + '\n\n';
      s += '- 数据源：' + a.sourceNote + '\n';
      s += '- P/Invoke ' + a.total + ' 条：钉扎 ' + a.matched + ' / 未收录(降级) ' + a.missed.length +
           ' / 口径不一致 ' + a.mismatched.length + '；核对进度 manual ' + a.chk.manual + ' / hardware ' +
           a.chk.hardware + ' / auto ' + a.chk.auto + ' / none ' + a.chk.none + '\n\n';
      if (!a.rows.length) { s += '（本品牌无 P/Invoke extern：' + (a.brandId === 'acs' ? '托管库模式，引用官方 .NET DLL' : '骨架品牌，待官方一手件') + '）\n\n'; continue; }
      s += '| P/Invoke | 本套件口径 | mcopilot 口径 | 状态 | 来源 |';
      s += '\n|---|---|---|---|---|\n';
      for (var r = 0; r < a.rows.length; r++) {
        var row = a.rows[r];
        s += '| ' + row.fn + ' | ' + row.ours + ' | ' + row.theirs + ' | ' + row.status + ' | ' + row.src + ' |\n';
      }
      if (a.missed.length) s += '\n**未收录（已显式降级 TODO）**：' + a.missed.join(', ') + '\n';
      if (a.mismatched.length) {
        s += '\n**口径不一致（两边都可能有据，用前必须人工裁决）**：\n';
        for (var mm = 0; mm < a.mismatched.length; mm++) {
          var d = a.mismatched[mm];
          s += '- ' + d.fn + '：本套件 ' + d.ours + ' vs mcopilot ' + d.theirs + '\n';
        }
      }
      s += '\n';
    }
    return s;
  }

  // ---------- 接缝 3：共享行为评测集（docs/JointEval.md） ----------

  function jointEvalFile() {
    var mc = loadMcopilot();
    var s = '# docs/JointEval.md — 共享行为评测集对照（接缝 3）\n\n';
    if (!mc.available) return s + '⚠ mcopilot 知识库不可达，无法读取共享评测集。\n';
    var fs = tryRequire('fs');
    var path = tryRequire('path');
    var f = path.join(mc.root, 'knowledge', 'shared_eval.json');
    var ev;
    try { ev = JSON.parse(fs.readFileSync(f, 'utf8')); }
    catch (e) { return s + '⚠ 共享评测集缺失（' + f + '）——mcopilot 仓库更新后重新生成。\n'; }

    s += '> 数据源：mcopilot `knowledge/shared_eval.json`（生成时实时读取，非复制）\n';
    s += '> 实测对照：由 mcopilot `scripts/joint_eval.py` 真跑两边后生成对照矩阵；本文件只回答「本套件对哪些行为断言负责」。\n\n';
    s += '**规则**：工厂端到端 PASS 只有落在共享评测集的断言上才算有背书——';
    s += '空白项（本套件应验未验）是整改清单，n/a 项（接口无此概念）是能力矩阵差异，不许算 FAIL。\n\n';

    var grp = {};
    for (var i = 0; i < ev.cases.length; i++) {
      var c = ev.cases[i];
      var k = (c.factory && c.factory.status) || 'blank';
      (grp[k] = grp[k] || []).push(c);
    }
    var stat = { pass: '已验', partial: '部分（打印无断言）', blank: '**空白（整改清单）**', 'n/a': '接口无此概念' };
    for (var key in grp) {
      s += '## ' + (stat[key] || key) + '\n\n';
      s += '| 断言 | 域 | 工厂侧验证点 | mcopilot 侧验证点 |\n|---|---|---|---|\n';
      for (var j = 0; j < grp[key].length; j++) {
        var c2 = grp[key][j];
        s += '| ' + c2.id + ' ' + c2.name + ' | ' + c2.domain + ' | ' +
             (c2.factory ? c2.factory.ref : '—') + ' | ' + (c2.mcopilot ? c2.mcopilot.ref : '—') + ' |\n';
      }
      s += '\n';
    }
    if (ev.divergences && ev.divergences.length) {
      s += '## 合法行为差异（不是 FAIL）\n\n';
      s += '| 议题 | mcopilot | 工厂 | 说明 |\n|---|---|---|---|\n';
      for (var d = 0; d < ev.divergences.length; d++) {
        var dv = ev.divergences[d];
        s += '| ' + dv.id + ' ' + dv.topic + ' | ' + dv.mcopilot + ' | ' + dv.factory + ' | ' + dv.note + ' |\n';
      }
      s += '\n';
    }
    s += '---\n\n本评测集与 P/Invoke 真值链同一来源纪律：数据在 mcopilot 仓库，工厂生成时实时读取，绝不另设副本。\n';
    return s;
  }

  return {
    decorate: decorate,
    resetRun: resetRun,
    runSummary: runSummary,
    docsFile: docsFile,
    jointEvalFile: jointEvalFile,
    loadMcopilot: loadMcopilot,
    BRAND_SDK: BRAND_SDK
  };
});
