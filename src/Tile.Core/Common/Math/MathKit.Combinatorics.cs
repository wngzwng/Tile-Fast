using System.Collections.Generic;

namespace Tile.Core.Common.Math;

public static partial class MathKit
{
    /// <summary>
    /// 枚举“从 n 个元素里选 k 个元素”的所有 bit mask。
    /// 非法范围会返回空序列。
    /// 特例：
    /// <c>n == 0 且 k == 0</c> 时，会返回一个掩码 <c>0</c>。
    /// </summary>
    public static IEnumerable<ulong> EnumerateChooseMasks(int n, int k)
    {
        if (n < 0 || k < 0 || k > n || n > 64)
            yield break;

        if (k == 0)
        {
            yield return 0UL;
            yield break;
        }

        if (k == 64)
        {
            yield return ulong.MaxValue;
            yield break;
        }

        var limit = 1UL << n;
        var mask = (1UL << k) - 1UL;

        while (mask < limit)
        {
            yield return mask;

            // Gosper's hack：在相同置位数量下生成下一个组合 mask。
            var c = mask & (0UL - mask);
            var r = mask + c;

            if (r == 0)
                yield break;

            mask = (((r ^ mask) >> 2) / c) | r;
        }
    }
}
