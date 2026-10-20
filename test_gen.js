// test_gen.js - 端到端生成测试（node）：输出解包目录 + zip
// 用法: node test_gen.js [brand|all]
//   brand: gts | leadshine | zmotion | gaochuan | softwin | acs | virtual
//   all  : 全品牌套件（各品牌适配器 + 虚拟控制器，不含 ACS）
const fs = require('fs');
const path = require('path');
const gen = require('./gen/index.js');

function emit(dir, result, zipName) {
  fs.rmSync(dir, { recursive: true, force: true });
  for (const f of result.files) {
    const p = path.join(dir, f.path);
    fs.mkdirSync(path.dirname(p), { recursive: true });
    fs.writeFileSync(p, f.content, 'utf8');
  }
  fs.writeFileSync(path.join('out', zipName), Buffer.from(result.zip));
  for (const s of (result.steps || [{ tag: 'OK', msg: result.files.length + ' files' }])) console.log(`  [${s.tag}] ${s.msg}`);
  console.log(`  => ${result.files.length} files -> ${dir} + ${zipName}`);
}

fs.mkdirSync('out', { recursive: true });

const brand = process.argv[2] || 'leadshine';
const isAll = brand === 'all';
// 全品牌套件的目标品牌：各品牌适配器都会进套件，目标品牌只影响套件抬头
const targetBrand = isAll ? 'leadshine' : brand;
const kitLabel = isAll ? '全品牌套件（固高/雷赛/正运动 + 软赢/高川骨架）' : null;

console.log('=== 适配层工程生成: ' + brand + ' ===');
const res = gen.generate({
  brandId: targetBrand,
  axisCount: 4,
  process: 'dispensing',
  includeAllBrands: isAll,
  brandLabel: kitLabel
});
if (!res.ok) { console.error(res.errors); process.exit(1); }
emit(isAll ? 'out/mc_all' : 'out/mc_' + brand, res,
     isAll ? 'MotionControlKit_all.zip' : 'MotionControlKit_' + brand + '.zip');

console.log('\n=== Modbus 驱动生成: 温控器 ===');
const csv = [
  '# 地址,名称,类型,访问,功能码,缩放,单位,描述',
  '100,PV,f32,RO,04,0.1,C,当前温度(过程值)',
  '200,SV,f32,RW,03,0.1,C,设定温度',
  '210,RUN,u16,RW,03,1,,运行/停止',
  '220,ALARM,u16,RO,04,1,,报警码',
  '230,HEAT_POWER,u16,RO,04,0.1,%,加热功率百分比'
].join('\n');
const mb = gen.modbus.generate({ name: 'temp_controller', title: '温控器（示例点表）', ip: '192.168.0.50', port: 502, slaveId: 1 }, csv, { includeRtu: true });
if (!mb.ok) { console.error(mb.errors); process.exit(1); }
emit('out/mb_temp', mb, 'ModbusDeviceKit.zip');
console.log('\nDONE');
