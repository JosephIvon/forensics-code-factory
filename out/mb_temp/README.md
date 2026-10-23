# TempControllerDriver - 温控器（示例点表）

> 生成工具: 上位机代码工厂 v1.0 | 2026-10-23 09:09:29 | 点位: 5

## 内容

- `src/ModbusTcpClient.cs` 自实现 Modbus TCP 主站（FC1/2/3/4/6/16, 零依赖）
- `src/ModbusRtuClient.cs` Modbus RTU 串口主站（可选）
- `src/TempControllerDriver.cs` 类型化驱动（属性=工程值, 已含缩放）
- `samples/Program.cs` 自检程序（CRC/解析，无设备也 PASS）
- `build/build_net48.bat` 零安装编译

## 使用

```csharp
var dev = new TempControllerDriver("192.168.0.50", 502, 1);
dev.Open();
dev.Poll();
Console.WriteLine(dev.Pv);
dev.Close();
```

## 接 MES/SPC（JD职责8）

Poll() 结果写入数据库或消息队列即可构成数据采集层；采样节拍建议 >=100ms。