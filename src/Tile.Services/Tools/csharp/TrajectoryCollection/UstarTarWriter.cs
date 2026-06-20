using System;

namespace G42.TrajectoryCollection
{
    /// <summary>
    /// 手写 USTAR(POSIX tar)格式(零第三方依赖,不用 System.Formats.Tar 以免挑 .NET 版本)。
    /// 一条轨迹一个 entry:512 字节 header + 数据 + 补齐到 512 倍数;整题末尾写两个全零块作为结束标记。
    /// 数值字段用八进制 ASCII,最大兼容 GNU tar / python tarfile。
    /// </summary>
    internal static class UstarTarWriter
    {
        public const int BlockSize = 512;

        // USTAR header 字段偏移(单位:字节)。
        private const int OffName = 0;       // 100
        private const int OffMode = 100;     // 8
        private const int OffUid = 108;      // 8
        private const int OffGid = 116;      // 8
        private const int OffSize = 124;     // 12
        private const int OffMtime = 136;    // 12
        private const int OffChksum = 148;   // 8
        private const int OffTypeflag = 156; // 1
        private const int OffMagic = 257;    // 6 "ustar\0"
        private const int OffVersion = 263;  // 2 "00"

        private const int NameFieldLength = 100;
        private const int FileMode0644 = 420; // 0o644

        /// <summary>
        /// 写一个 tar entry 的 512 字节 header。
        /// header 用调用方传入的 512 字节 scratch 拼好(复用,免每条轨迹分配),再拷进 buffer。
        /// </summary>
        public static void WriteEntryHeader(ByteBuffer buffer, byte[] headerScratch, string name, long size, long mtimeUnixSeconds)
        {
            if (headerScratch.Length < BlockSize)
            {
                throw new ArgumentException("headerScratch 至少 512 字节,实际 " + headerScratch.Length);
            }
            if (size < 0)
            {
                throw new ArgumentException("entry size 不能为负:" + size);
            }

            Array.Clear(headerScratch, 0, BlockSize);

            PutAscii(headerScratch, OffName, NameFieldLength, name);
            PutOctal(headerScratch, OffMode, 8, FileMode0644);
            PutOctal(headerScratch, OffUid, 8, 0);
            PutOctal(headerScratch, OffGid, 8, 0);
            PutOctal(headerScratch, OffSize, 12, size);
            PutOctal(headerScratch, OffMtime, 12, mtimeUnixSeconds);
            headerScratch[OffTypeflag] = (byte)'0'; // 普通文件

            headerScratch[OffMagic + 0] = (byte)'u';
            headerScratch[OffMagic + 1] = (byte)'s';
            headerScratch[OffMagic + 2] = (byte)'t';
            headerScratch[OffMagic + 3] = (byte)'a';
            headerScratch[OffMagic + 4] = (byte)'r';
            headerScratch[OffMagic + 5] = 0; // "ustar\0"
            headerScratch[OffVersion + 0] = (byte)'0';
            headerScratch[OffVersion + 1] = (byte)'0';

            WriteChecksum(headerScratch);

            buffer.WriteBytes(headerScratch, 0, BlockSize);
        }

        /// <summary>entry 数据写完后,把数据长度补齐到 512 的整数倍。</summary>
        public static void WriteEntryPadding(ByteBuffer buffer, long dataSize)
        {
            int remainder = (int)(dataSize % BlockSize);
            if (remainder != 0)
            {
                buffer.WriteZeros(BlockSize - remainder);
            }
        }

        /// <summary>整题结束:写两个全零块作为 tar 结束标记。</summary>
        public static void WriteTrailer(ByteBuffer buffer)
        {
            buffer.WriteZeros(BlockSize * 2);
        }

        private static void PutAscii(byte[] header, int offset, int fieldLength, string value)
        {
            if (value.Length > fieldLength)
            {
                throw new InvalidOperationException(
                    "tar 字段写不下:字段 " + fieldLength + " 字节,值长度 " + value.Length + " (" + value + ")");
            }
            for (int i = 0; i < value.Length; i++)
            {
                header[offset + i] = (byte)value[i];
            }
            // 余下字节保持 0(header 已 Array.Clear)。
        }

        // 数值字段:(fieldLength-1) 位八进制 ASCII,前导补 0,末位写 NUL。
        private static void PutOctal(byte[] header, int offset, int fieldLength, long value)
        {
            int digits = fieldLength - 1;
            long remaining = value;
            for (int i = digits - 1; i >= 0; i--)
            {
                header[offset + i] = (byte)('0' + (int)(remaining & 7));
                remaining >>= 3;
            }
            header[offset + digits] = 0; // NUL
            if (remaining != 0)
            {
                throw new InvalidOperationException(
                    "tar 八进制字段溢出:值 " + value + " 超过 " + digits + " 位八进制(字段 " + fieldLength + " 字节)");
            }
        }

        // checksum:把 chksum 字段先当作 8 个空格,对整 512 字节求无符号和;
        // 结果写成 6 位八进制 + NUL + 空格(最兼容的形式)。
        private static void WriteChecksum(byte[] header)
        {
            for (int i = 0; i < 8; i++)
            {
                header[OffChksum + i] = (byte)' ';
            }
            int sum = 0;
            for (int i = 0; i < BlockSize; i++)
            {
                sum += header[i];
            }
            // 6 位八进制
            for (int i = 5; i >= 0; i--)
            {
                header[OffChksum + i] = (byte)('0' + (sum & 7));
                sum >>= 3;
            }
            header[OffChksum + 6] = 0;        // NUL
            header[OffChksum + 7] = (byte)' '; // 空格
        }
    }
}
