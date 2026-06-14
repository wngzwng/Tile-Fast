namespace Tile.Core.Common.Math;

public static partial class MathKit
{
    /// <summary>
     /// 将 softmax 结果写入 <paramref name="destination"/>。
    /// 公式：<c>softmax(x_i) = exp(x_i / T) / sum(exp(x_j / T))</c>，其中 <c>T</c> 是 temperature。
     /// 非法分数（NaN / Infinity）会被忽略，对应位置保持为 0。
     /// 当不存在任何有效分数时，<paramref name="destination"/> 会被清成全 0。
    /// 实现时会先减去最大值，再做 <c>exp</c>，以减少数值溢出风险。
     /// </summary>
    public static void Softmax(
        ReadOnlySpan<double> scores,
        Span<double> destination,
        double temperature = 1.0)
    {
        if (scores.Length != destination.Length)
            throw new ArgumentException("Destination length must match scores length.", nameof(destination));

        if (temperature <= 0.0 || double.IsNaN(temperature) || double.IsInfinity(temperature))
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be a finite positive number.");

        if (scores.Length == 0)
            return;

        destination.Clear();

        var max = double.NegativeInfinity;

        for (var i = 0; i < scores.Length; i++)
        {
            var score = scores[i];

            if (double.IsNaN(score) || double.IsInfinity(score))
                continue;

            if (score > max)
                max = score;
        }

        if (double.IsNegativeInfinity(max))
            return;

        var total = 0.0;

        for (var i = 0; i < scores.Length; i++)
        {
            var score = scores[i];

            if (double.IsNaN(score) || double.IsInfinity(score))
                continue;

            // 先减去 max，避免 exp 在大分数输入时过大。
            var weight = System.Math.Exp((score - max) / temperature);
            destination[i] = weight;
            total += weight;
        }

        if (total <= 0.0 || double.IsNaN(total) || double.IsInfinity(total))
        {
            destination.Clear();
            return;
        }

        for (var i = 0; i < destination.Length; i++)
            destination[i] /= total;
    }

    /// <summary>
     /// 分配并返回一份 softmax 结果数组。
     /// 当 <paramref name="scores"/> 为空时，返回空数组。
     /// </summary>
    public static double[] Softmax(ReadOnlySpan<double> scores, double temperature = 1.0)
    {
        if (scores.Length == 0)
            return Array.Empty<double>();

        var result = new double[scores.Length];
        Softmax(scores, result, temperature);
        return result;
    }
}
