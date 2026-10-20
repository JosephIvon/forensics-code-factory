// concat.js - 合并 gen/*.js 到 factory_ui.html -> 上位机代码工厂.html（单文件交付）
const fs = require('fs');
const parts = ['gen/zip.js', 'gen/core.js', 'gen/adapter_common.js', 'gen/adapters_kb.js', 'gen/modbus.js', 'gen/index.js'];
let scripts = parts.map((p) => fs.readFileSync(p, 'utf8')).join('\n;\n');
const ui = fs.readFileSync('factory_ui.html', 'utf8');
const out = ui.replace('/*__GEN_SCRIPTS__*/', () => scripts);
fs.writeFileSync('上位机代码工厂.html', out, 'utf8');
console.log('OK ->', '上位机代码工厂.html', Math.round(out.length / 1024) + ' KB');
