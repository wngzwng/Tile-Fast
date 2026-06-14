namespace Tile.Core.Common.Math;

public static partial class MathKit
{
    /// <summary>
     /// 按权重随机选择一个索引。
    /// 公式上等价于：先计算累计权重区间，再在 <c>[0, totalWeight)</c> 中采样一个随机值，
    /// 最后返回该随机值落入的区间索引。
     /// 当 <paramref name="weights"/> 为空，或所有权重都非法
     /// （NaN / Infinity / 小于等于 0）时，返回 <c>-1</c>。
     /// 非法权重会被忽略。
     /// </summary>
    public static int WeightedChoice(ReadOnlySpan<double> weights, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (weights.Length == 0)
            return -1;

        var total = 0.0;

        for (var i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];

            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0.0)
                continue;

            total += weight;
        }

        if (total <= 0.0)
            return -1;

        var target = random.NextDouble() * total;
        var cumulative = 0.0;

        for (var i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];

            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0.0)
                continue;

            cumulative += weight;

            if (target < cumulative)
                return i;
        }

        // 浮点累计可能让 target 落在最右边界，最后回退到最后一个有效权重。
        for (var i = weights.Length - 1; i >= 0; i--)
        {
            var weight = weights[i];

            if (!double.IsNaN(weight) && !double.IsInfinity(weight) && weight > 0.0)
                return i;
        }

        return -1;
    }
}
