using System;
using System.Globalization;

namespace G42.TrajectoryCollection
{
    /// <summary>
    /// 手写 NumPy .npy v1.0 header(零第三方依赖)。只产出 dtype '&lt;f4'(小端 float32)、
    /// C-order(fortran_order=False)、二维 shape (rows, cols) 的 header。
    /// 与模型二读取侧约定一致:读取侧按 '&lt;f4' 解析、shape 第 1 维即步数、第 2 维即特征数(47)。
    /// </summary>
    internal static class NpyWriter
    {
        private static readonly byte[] MagicAndVersion = new byte[]
        {
            0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y', // magic
            0x01, 0x00,                                                   // version 1.0
        };

        // magic(6) + version(2) + header-len 字段(2) = 10 字节前导。
        private const int PreambleLength = 10;

        // v1.0:整个 header(前导 + dict 串)对齐到 64 字节边界(与当前 numpy 写出一致,读侧只认 header-len)。
        private const int Alignment = 64;

        /// <summary>
        /// 把 (rows, cols) 的 .npy header 写进 scratch,返回写入字节数。
        /// scratch 复用(单题多条轨迹共用一块),容量不足直接抛(定长小 header,正常 &lt;=128 字节)。
        /// </summary>
        public static int WriteHeaderToScratch(byte[] scratch, int rows, int cols)
        {
            if (rows < 0 || cols <= 0)
            {
                throw new ArgumentException("rows 必须 >=0、cols 必须 >0,实际 rows=" + rows + " cols=" + cols);
            }

            // dict 串严格用单引号、键序 descr/fortran_order/shape,与 numpy 写出格式一致。
            string dict = "{'descr': '<f4', 'fortran_order': False, 'shape': ("
                + rows.ToString(CultureInfo.InvariantCulture) + ", "
                + cols.ToString(CultureInfo.InvariantCulture) + "), }";

            // dict 末尾需以 '\n' 收尾;前导 + dict + 空格补齐 + '\n' 总长对齐到 64。
            int unpadded = PreambleLength + dict.Length + 1; // +1 为换行符
            int total = ((unpadded + Alignment - 1) / Alignment) * Alignment;
            int headerLen = total - PreambleLength; // 即 header-len 字段的值(dict + 空格 + '\n' 的长度)
            int spaces = headerLen - dict.Length - 1;

            if (total > scratch.Length)
            {
                throw new InvalidOperationException(
                    "npy header scratch 容量不足:需要 " + total + " 字节,实际 " + scratch.Length);
            }
            if (headerLen > ushort.MaxValue)
            {
                throw new InvalidOperationException("npy v1.0 header-len 超过 uint16 上限;rows 异常过大");
            }

            int pos = 0;
            Array.Copy(MagicAndVersion, 0, scratch, pos, MagicAndVersion.Length);
            pos += MagicAndVersion.Length;

            // header-len:uint16 小端。
            scratch[pos++] = (byte)(headerLen & 0xFF);
            scratch[pos++] = (byte)((headerLen >> 8) & 0xFF);

            for (int i = 0; i < dict.Length; i++)
            {
                scratch[pos++] = (byte)dict[i];
            }
            for (int i = 0; i < spaces; i++)
            {
                scratch[pos++] = (byte)' ';
            }
            scratch[pos++] = (byte)'\n';

            return pos; // == total
        }
    }
}
