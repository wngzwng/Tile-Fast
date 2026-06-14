namespace Tile.Core.Common.Math;

/// <summary>
/// 为 <see cref="MathKit"/> 提供更顺手的扩展调用方式。
/// 这里只负责转发，不承载核心数学逻辑。
/// </summary>
public static class MathKitExtensions
{
    /// <summary>
    /// 尝试计算平均值；失败时返回 <c>false</c>，并输出 <c>0.0</c>。
    /// </summary>
    public static bool TryAverage(this Span<double> values, out double average)
        => MathKit.TryAverage(values, out average);

    /// <summary>
    /// 尝试计算平均值；失败时返回 <c>false</c>，并输出 <c>0.0</c>。
    /// </summary>
    public static bool TryAverage(this ReadOnlySpan<double> values, out double average)
        => MathKit.TryAverage(values, out average);

    /// <summary>
    /// 尝试计算平均值；失败时返回 <c>false</c>，并输出 <c>0.0</c>。
    /// </summary>
    public static bool TryAverage(this Span<int> values, out double average)
        => MathKit.TryAverage(values, out average);

    /// <summary>
    /// 尝试计算平均值；失败时返回 <c>false</c>，并输出 <c>0.0</c>。
    /// </summary>
    public static bool TryAverage(this ReadOnlySpan<int> values, out double average)
        => MathKit.TryAverage(values, out average);

    /// <summary>
    /// 尝试计算平均值；失败时返回 <c>false</c>，并输出 <c>0.0</c>。
    /// </summary>
    public static bool TryAverage(this double[] values, out double average)
        => MathKit.TryAverage(values, out average);

    /// <summary>
    /// 尝试计算平均值；失败时返回 <c>false</c>，并输出 <c>0.0</c>。
    /// </summary>
    public static bool TryAverage(this int[] values, out double average)
        => MathKit.TryAverage(values, out average);

    /// <summary>
    /// 返回一份新分配的 softmax 结果数组。
    /// </summary>
    public static double[] Softmax(this Span<double> scores, double temperature = 1.0)
        => MathKit.Softmax(scores, temperature);

    /// <summary>
    /// 返回一份新分配的 softmax 结果数组。
    /// </summary>
    public static double[] Softmax(this ReadOnlySpan<double> scores, double temperature = 1.0)
        => MathKit.Softmax(scores, temperature);

    /// <summary>
    /// 返回一份新分配的 softmax 结果数组。
    /// </summary>
    public static double[] Softmax(this double[] scores, double temperature = 1.0)
        => MathKit.Softmax(scores, temperature);

    /// <summary>
    /// 将 softmax 结果写入调用方提供的目标 Span，并返回该 Span 以便链式调用。
    /// </summary>
    public static Span<double> Softmax(
        this Span<double> scores,
        Span<double> destination,
        double temperature = 1.0)
    {
        MathKit.Softmax(scores, destination, temperature);
        return destination;
    }

    /// <summary>
    /// 将 softmax 结果写入调用方提供的目标 Span，并返回该 Span 以便链式调用。
    /// </summary>
    public static Span<double> Softmax(
        this ReadOnlySpan<double> scores,
        Span<double> destination,
        double temperature = 1.0)
    {
        MathKit.Softmax(scores, destination, temperature);
        return destination;
    }

    /// <summary>
    /// 将 softmax 结果写入调用方提供的目标 Span，并返回该 Span 以便链式调用。
    /// </summary>
    public static Span<double> Softmax(
        this double[] scores,
        Span<double> destination,
        double temperature = 1.0)
    {
        MathKit.Softmax(scores, destination, temperature);
        return destination;
    }

    /// <summary>
    /// 按权重随机选择一个索引；找不到合法候选时返回 <c>-1</c>。
    /// </summary>
    public static int WeightedChoice(this Span<double> weights, Random random)
        => MathKit.WeightedChoice(weights, random);

    /// <summary>
    /// 按权重随机选择一个索引；找不到合法候选时返回 <c>-1</c>。
    /// </summary>
    public static int WeightedChoice(this ReadOnlySpan<double> weights, Random random)
        => MathKit.WeightedChoice(weights, random);

    /// <summary>
    /// 按权重随机选择一个索引；找不到合法候选时返回 <c>-1</c>。
    /// </summary>
    public static int WeightedChoice(this double[] weights, Random random)
        => MathKit.WeightedChoice(weights, random);
}
