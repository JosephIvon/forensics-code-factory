using System;

namespace MotionControl.Modbus
{
    /// <summary>Modbus CRC16 (多项式 0xA001)。自检: "123456789" -> 0x4B37</summary>
    public static class Crc16
    {
        public static ushort Compute(byte[] data, int offset, int count)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + count && i < data.Length; i++)
            {
                crc ^= data[i];
                for (int b = 0; b < 8; b++)
                {
                    if ((crc & 1) != 0) { crc >>= 1; crc ^= 0xA001; }
                    else { crc >>= 1; }
                }
            }
            return crc;
        }
    }
}
