using System;

namespace G42.TrajectoryCollection
{
    /// <summary>
    /// 单实例复用的可增长字节缓冲。用于在内存里拼好整条轨迹的 .npy 与整题的 tar,
    /// 再一次顺序写盘,避免每条轨迹反复分配小数组。
    ///
    /// 非线程安全:每个 <see cref="TrajectoryFeatures"/> 实例独占一个 ByteBuffer,
    /// 同事外层若并发采集,请每题 new 一个 TrajectoryFeatures(从而每实例独立一个缓冲)。
    /// </summary>
    internal sealed class ByteBuffer
    {
        // float ↔ uint 同址重解释:零分配地拿到 IEEE-754 位模式,再按小端逐字节写出。
        // 位模式整数值与机器端序无关(移位取字节),故下方 WriteFloatLittleEndian 在大小端机器上都产出小端字节。
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct FloatUIntUnion
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public float AsFloat;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint AsUInt;
        }

        private byte[] _buffer;
        private int _length;

        public ByteBuffer(int initialCapacity)
        {
            if (initialCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "初始容量必须 >=1");
            }
            _buffer = new byte[initialCapacity];
            _length = 0;
        }

        /// <summary>已写入的字节数。</summary>
        public int Length { get { return _length; } }

        /// <summary>底层数组(只读 [0,Length) 区间有效;勿在外部改写)。</summary>
        public byte[] RawArray { get { return _buffer; } }

        /// <summary>清空(保留已分配容量,供下一题复用)。</summary>
        public void Reset()
        {
            _length = 0;
        }

        private void EnsureCapacity(int additional)
        {
            // additional 由调用方传入定长(单步 4 字节、单 header 512 字节等),不会出现负值;
            // 万一发生整型溢出(超大轨迹)即数据/用法异常,直接崩出来定位。
            long required = (long)_length + additional;
            if (required > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "ByteBuffer 需求超过 int.MaxValue 字节;单题数据过大,请调小 maxTrajectoriesPerPuzzle 或检查上游");
            }
            if (required <= _buffer.Length)
            {
                return;
            }
            int newCapacity = _buffer.Length;
            while (newCapacity < required)
            {
                newCapacity = newCapacity < (int.MaxValue / 2) ? newCapacity * 2 : int.MaxValue;
            }
            byte[] grown = new byte[newCapacity];
            Array.Copy(_buffer, 0, grown, 0, _length);
            _buffer = grown;
        }

        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _buffer[_length++] = value;
        }

        public void WriteBytes(byte[] source, int offset, int count)
        {
            EnsureCapacity(count);
            Array.Copy(source, offset, _buffer, _length, count);
            _length += count;
        }

        /// <summary>写入 count 个 0 字节(tar 数据块补齐、末尾全零块用)。</summary>
        public void WriteZeros(int count)
        {
            EnsureCapacity(count);
            // 新区间在 EnsureCapacity 扩容时已是 0;但复用旧容量区间可能残留上一题数据,显式清零。
            Array.Clear(_buffer, _length, count);
            _length += count;
        }

        /// <summary>写入一个 ASCII 字符串(调用方保证全为 ASCII;非 ASCII 字符会被截成低字节)。</summary>
        public void WriteAscii(string value)
        {
            EnsureCapacity(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                _buffer[_length++] = (byte)value[i];
            }
        }

        /// <summary>按小端写入一个 float32(对齐 .npy 的 '&lt;f4')。零分配。</summary>
        public void WriteFloatLittleEndian(float value)
        {
            EnsureCapacity(4);
            FloatUIntUnion u = new FloatUIntUnion();
            u.AsFloat = value;
            uint bits = u.AsUInt;
            _buffer[_length++] = (byte)bits;
            _buffer[_length++] = (byte)(bits >> 8);
            _buffer[_length++] = (byte)(bits >> 16);
            _buffer[_length++] = (byte)(bits >> 24);
        }
    }
}
