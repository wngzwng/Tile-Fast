namespace Tile.Core.Common.Math;

public static partial class MathKit
{
    /// <summary>
     /// 计算有限 <see cref="double"/> 值的平均值。
    /// 公式：<c>avg = sum / count</c>。
     /// 当输入为空，或所有值都是 NaN / Infinity 时，返回 <c>0.0</c>。
     /// </summary>
    public static double Average(ReadOnlySpan<double> values)
    {
        if (!TryAverage(values, out var average))
            return 0.0;

        return average;
    }

    /// <summary>
     /// 计算 <see cref="int"/> 序列的平均值。
    /// 公式：<c>avg = sum / count</c>。
     /// 当输入为空时，返回 <c>0.0</c>。
     /// </summary>
    public static double Average(ReadOnlySpan<int> values)
    {
        if (!TryAverage(values, out var average))
            return 0.0;

        return average;
    }

    /// <summary>
     /// 计算加权平均值。
    /// 公式：<c>weightedAvg = sum(value[i] * weight[i]) / sum(weight[i])</c>。
     /// 非法 pair（值或权重为 NaN / Infinity，或权重小于等于 0）会被忽略。
     /// 当不存在任何有效 pair 时，返回 <c>0.0</c>。
     /// </summary>
    public static double WeightedAverage(
        ReadOnlySpan<double> values,
        ReadOnlySpan<double> weights)
    {
        if (values.Length != weights.Length)
            throw new ArgumentException("Weights length must match values length.", nameof(weights));

        var weightedSum = 0.0;
        var totalWeight = 0.0;

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            var weight = weights[i];

            if (double.IsNaN(value) || double.IsInfinity(value) ||
                double.IsNaN(weight) || double.IsInfinity(weight) ||
                weight <= 0)
            {
                continue;
            }

            weightedSum += value * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0.0)
            return 0.0;

        return weightedSum / totalWeight;
    }

    /// <summary>
     /// 在“追加了一个新值”之后，增量更新平均值。
    /// 公式：<c>newAvg = oldAvg + (newValue - oldAvg) / newCount</c>。
     /// <paramref name="newCount"/> 表示加入新值之后的总数量。
     /// </summary>
    public static double UpdateAverage(double currentAverage, int newCount, double newValue)
    {
        if (newCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(newCount));

        if (newCount == 1)
            return newValue;

        return currentAverage + (newValue - currentAverage) / newCount;
    }

    /// <summary>
     /// 尝试计算有限 <see cref="double"/> 值的平均值。
    /// 公式：<c>avg = sum / count</c>。
     /// 当输入为空，或所有值都是 NaN / Infinity 时，返回 <c>false</c>，
     /// 并将 <paramref name="average"/> 设为 <c>0.0</c>。
     /// </summary>
    public static bool TryAverage(ReadOnlySpan<double> values, out double average)
    {
        average = 0.0;

        if (values.Length == 0)
            return false;

        var sum = 0.0;
        var count = 0;

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];

            if (double.IsNaN(value) || double.IsInfinity(value))
                continue;

            sum += value;
            count++;
        }

        if (count == 0)
            return false;

        average = sum / count;
        return true;
    }

    /// <summary>
     /// 尝试计算 <see cref="int"/> 序列的平均值。
    /// 公式：<c>avg = sum / count</c>。
     /// 当输入为空时，返回 <c>false</c>，并将 <paramref name="average"/> 设为 <c>0.0</c>。
     /// </summary>
    public static bool TryAverage(ReadOnlySpan<int> values, out double average)
    {
        average = 0.0;

        if (values.Length == 0)
            return false;

        long sum = 0;

        for (var i = 0; i < values.Length; i++)
            sum += values[i];

        average = (double)sum / values.Length;
        return true;
    }
}
