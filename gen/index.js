// index.js - 聚合入口: 自然语言解析 + 适配层工程生成
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory(require('./zip.js'), require('./core.js'), require('./adapters_kb.js'), require('./modbus.js'), require('./truth_kb.js'));
  } else {
    root.MCGen = factory(root.MCZip, root.MCCore, root.MCKb, root.MCModbus, root.MCTruth);
  }
})(typeof self !== 'undefined' ? self : this, function (MCZip, MCCore, MCKb, MCModbus, MCTruth) {

  // ---------- 品牌元数据（UI下拉 + 解析词表） ----------
  var BRANDS = [
    { id: 'virtual',   label: '虚拟控制器（仿真，零硬件跑通全流程）', kb: 'A' },
    { id: 'gts',       label: '固高 GTS/GTN 系列', kb: 'B' },
    { id: 'leadshine', label: '雷赛 DMC/EMC 系列 (LTDMC)', kb: 'B' },
    { id: 'zmotion',   label: '正运动 ZMC 系列 (EtherCAT)', kb: 'B' },
    { id: 'acs',       label: 'ACS SPiiPlus (高级EtherCAT)', kb: 'B' },
    { id: 'softwin',   label: '软赢（骨架+TODO清单）', kb: 'C' },
    { id: 'gaochuan',  label: '高川（骨架+TODO清单）', kb: 'C' }
  ];

  var PROCESSES = [
    { id: 'dispensing', label: '点胶（路径规划/出胶时序/SPC）' },
    { id: 'diebond',    label: '共晶贴片（拾放/压力/温度时序）' },
    { id: 'coupling',   label: '耦合对准（多轴对准/功率采集）' },
    { id: 'generic',    label: '通用运动流程' }
  ];

  // ---------- 自然语言需求解析（规则式，零API依赖） ----------
  function parseRequest(text) {
    var cfg = { brandId: 'virtual', axisCount: 3, process: 'generic', notes: [] };
    if (!text) return cfg;
    var t = text.toLowerCase();

    if (/固高|googol|gts|gtn/.test(t)) cfg.brandId = 'gts';
    else if (/雷赛|leadshine|dmc|emc/.test(t)) cfg.brandId = 'leadshine';
    else if (/正运动|zmotion|zmc/.test(t)) cfg.brandId = 'zmotion';
    else if (/acs|spiiplus/.test(t)) cfg.brandId = 'acs';
    else if (/软赢|softwin/.test(t)) cfg.brandId = 'softwin';
    else if (/高川/.test(t)) cfg.brandId = 'gaochuan';
    else if (/虚拟|仿真|virtual|无硬件/.test(t)) cfg.brandId = 'virtual';

    var m = t.match(/(\d+)\s*(轴|axis)/);
    if (m) { cfg.axisCount = Math.min(8, Math.max(1, parseInt(m[1], 10))); }

    if (/点胶|dispens|涂胶|胶路/.test(t)) cfg.process = 'dispensing';
    else if (/贴片|共晶|die\s*bond|diebond|贴装|吸嘴/.test(t)) cfg.process = 'diebond';
    else if (/耦合|coupl|对准|寻光|光功率/.test(t)) cfg.process = 'coupling';

    if (/xy/.test(t) && !m) cfg.axisCount = Math.max(2, cfg.axisCount);
    return cfg;
  }

  // ---------- 生成完整工程 ----------
  function generate(cfg) {
    var steps = [];
    var brandId = cfg.brandId || 'virtual';
    var kb = MCKb.kb[brandId];

    steps.push({ tag: '需求', msg: '品牌=' + kb.label + '，轴数=' + cfg.axisCount + '，工艺=' + (cfg.process || 'generic') });
    steps.push({ tag: '知识库', msg: '加载 ' + kb.label + ' API 映射（KB级别 ' + kb.kbLevel + '，' + ((kb.api ? kb.api.length : 0)) + ' 项核对条目）' });

    // 选中的适配器：目标品牌 + 全品牌（可选） + 虚拟（永远提供），按 id 去重
    var seen = {}, list = [kb];
    if (cfg.includeAllBrands) {
      list = [kb, MCKb.kb.gts, MCKb.kb.leadshine, MCKb.kb.zmotion, MCKb.kb.softwin, MCKb.kb.gaochuan];
      // ACS 不进全品牌套件：其适配器引用官方 ACS.SPiiPlusNET.dll，无该 DLL 时整个套件编不过；
      // 选 ACS 作为目标品牌时仍单独生成专用套件
      steps.push({ tag: '知识库', msg: '全品牌套件不含 ACS（需官方 ACS.SPiiPlusNET.dll 才能编译）；Brand.Acs 请单独生成专用套件' });
    }
    var adapters = [];
    for (var q = 0; q < list.length; q++) {
      if (!list[q] || seen[list[q].id]) continue;
      seen[list[q].id] = 1;
      adapters.push(list[q]);
    }
    adapters.push(MCKb.kb.virtual);

    steps.push({ tag: '架构', msg: '分层: Core(抽象) / Adapters(适配) / Virtual(仿真) / samples(工艺) / docs(文档) / build(两套编译)' });

    var files = [];
    MCTruth.resetRun();   // 真值链审计：本次生成从零累计
    var core = MCCore.coreFiles(cfg);
    for (var i = 0; i < core.length; i++) files.push(core[i]);
    steps.push({ tag: '代码', msg: '生成抽象层 Core（接口/配置/结果对象，业务唯一依赖）' });

    var adapterFiles = [];
    for (var a = 0; a < adapters.length; a++) {
      if (adapters[a].id === 'virtual') continue;
      var af = MCKb.buildFile(adapters[a].id);
      adapterFiles.push(af);
    }
    files = files.concat(adapterFiles);
    steps.push({ tag: '代码', msg: '生成适配器 x' + adapterFiles.length + '（P/Invoke + 单位换算 + 线程锁 + 错误映射）' });
    var tStat = MCTruth.runSummary();
    if (tStat) steps.push({ tag: '真值链', msg: tStat });

    files.push(MCCore.virtualFile());
    steps.push({ tag: '代码', msg: '生成 VirtualController（无硬件仿真，单元测试/CI 可用）' });

    files.push(MCCore.factoryFile(adapters));
    steps.push({ tag: '代码', msg: '生成 ControllerFactory（换卡=改一个枚举）' });

    files.push(MCCore.sampleFile(cfg));
    steps.push({ tag: '代码', msg: '生成工艺示例 Program.cs（含 SPC/Cpk 报表）' });

    var docs = MCCore.docFiles(cfg, cfg.includeAllBrands
      ? adapters.filter(function (a) { return a.id !== 'virtual'; })   // 全品牌套件：清单覆盖套件内全部品牌
      : [kb]);
    for (var d = 0; d < docs.length; d++) files.push(docs[d]);
    steps.push({ tag: '文档', msg: '生成能力差异矩阵 + P/Invoke 核对清单（清单与代码同源，k/n 按官方真值核对状态统计；依据须为厂商官方手册/头文件）' });

    var truthDoc = MCTruth.docsFile();
    if (truthDoc) {
      files.push({ path: 'docs/TruthChain.md', content: truthDoc });
      steps.push({ tag: '文档', msg: '生成 docs/TruthChain.md（P/Invoke 真值链审计：逐条对照 mcopilot 官方绑定口径）' });
    }

    var jointDoc = MCTruth.jointEvalFile();   // 接缝 3：共享行为评测集（本套件对哪些断言负责）
    if (jointDoc) {
      files.push({ path: 'docs/JointEval.md', content: jointDoc });
      steps.push({ tag: '文档', msg: '生成 docs/JointEval.md（共享行为评测集对照：与 mcopilot 自检断言逐条对齐，空白项=整改清单）' });
    }

    var builds = MCCore.buildFiles(cfg);
    for (var b = 0; b < builds.length; b++) files.push(builds[b]);
    files.push(MCCore.readme(cfg, { brandLabel: cfg.brandLabel || kb.label, axisCount: cfg.axisCount, processLabel: cfg.process }));
    steps.push({ tag: '构建', msg: '生成 build_net48.bat(零安装csc) + GlueDemo.csproj(dotnet)' });

    var zip = MCZip.makeZip(files);
    var lineCount = 0;
    for (var f = 0; f < files.length; f++) lineCount += files[f].content.split('\n').length;
    steps.push({ tag: '完成', msg: files.length + ' 个文件 / ' + lineCount + ' 行代码 / zip ' + Math.round(zip.length / 1024) + ' KB —— 双击 build_net48.bat 即可编译运行' });

    return { ok: true, files: files, zip: zip, steps: steps, stats: { files: files.length, lines: lineCount, kbBytes: zip.length } };
  }

  return {
    BRANDS: BRANDS,
    PROCESSES: PROCESSES,
    parseRequest: parseRequest,
    generate: generate,
    modbus: {
      parseCsv: MCModbus.parseCsv,
      pascal: MCModbus.pascal,
      generate: function (dev, csv, opts) {
        var r = MCModbus.generate(dev, csv, opts);
        if (r.ok) r.zip = MCZip.makeZip(r.files);
        return r;
      }
    }
  };
});
