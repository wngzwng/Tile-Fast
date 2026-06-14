using Tile.Core.Common.Math;
using NUnit.Framework;

namespace Tile.Core.Tests.Common.Math;

public sealed class MathKitTests
{
    [Test]
    public void Sigmoid_AtZero_ReturnsHalf()
    {
        Assert.That(MathKit.Sigmoid(0.0), Is.EqualTo(0.5).Within(1e-12));
    }

    [Test]
    public void Sigmoid_ForLargeMagnitudeInput_RemainsStable()
    {
        Assert.That(MathKit.Sigmoid(1000.0), Is.GreaterThan(0.999));
        Assert.That(MathKit.Sigmoid(-1000.0), Is.LessThan(0.001));
    }

    [Test]
    public void Average_ForDouble_IgnoresNaNAndInfinity()
    {
        // 这里验证“非法 double 会被忽略”，而不是直接让平均值失效。
        var values = new[] { 1.0, double.NaN, 3.0, double.PositiveInfinity };

        var average = MathKit.Average(values);

        Assert.That(average, Is.EqualTo(2.0).Within(1e-12));
    }

    [Test]
    public void Average_ForDoubleEmptyOrAllInvalid_ReturnsZero()
    {
        // 约定：没有有效数据时，Average 返回 0.0，而不是抛异常。
        Assert.That(MathKit.Average(Array.Empty<double>()), Is.EqualTo(0.0));
        Assert.That(MathKit.Average(new[] { double.NaN, double.PositiveInfinity }), Is.EqualTo(0.0));
    }

    [Test]
    public void Average_ForInt_EmptyReturnsZero()
    {
        Assert.That(MathKit.Average(Array.Empty<int>()), Is.EqualTo(0.0));
    }

    [Test]
    public void TryAverage_ForDouble_FailureSetsAverageToZero()
    {
        // 约定：TryAverage 失败时返回 false，并把 out average 设为 0.0。
        var ok = MathKit.TryAverage(new[] { double.NaN, double.PositiveInfinity }, out var average);

        Assert.That(ok, Is.False);
        Assert.That(average, Is.EqualTo(0.0));
    }

    [Test]
    public void TryAverage_ForInt_EmptyReturnsFalse()
    {
        var ok = MathKit.TryAverage(Array.Empty<int>(), out var average);

        Assert.That(ok, Is.False);
        Assert.That(average, Is.EqualTo(0.0));
    }

    [Test]
    public void WeightedAverage_IgnoresInvalidPairs()
    {
        var values = new[] { 10.0, 100.0, 30.0 };
        var weights = new[] { 1.0, 0.0, 3.0 };

        var average = MathKit.WeightedAverage(values, weights);

        Assert.That(average, Is.EqualTo(25.0).Within(1e-12));
    }

    [Test]
    public void WeightedAverage_WhenNoValidPair_ReturnsZero()
    {
        var values = new[] { double.NaN, 5.0 };
        var weights = new[] { 1.0, 0.0 };

        Assert.That(MathKit.WeightedAverage(values, weights), Is.EqualTo(0.0));
    }

    [Test]
    public void WeightedAverage_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MathKit.WeightedAverage(new[] { 1.0, 2.0 }, new[] { 1.0 }));
    }

    [Test]
    public void UpdateAverage_ComputesIncrementalAverage()
    {
        var updated = MathKit.UpdateAverage(2.0, 3, 5.0);

        Assert.That(updated, Is.EqualTo(3.0).Within(1e-12));
    }

    [Test]
    public void UpdateAverage_InvalidCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathKit.UpdateAverage(0.0, 0, 1.0));
    }

    [Test]
    public void Softmax_WritesNormalizedWeights()
    {
        var destination = new double[3];

        MathKit.Softmax(new[] { 1.0, 2.0, 3.0 }, destination, temperature: 1.0);

        Assert.That(destination.Sum(), Is.EqualTo(1.0).Within(1e-10));
        Assert.That(destination[2], Is.GreaterThan(destination[1]));
        Assert.That(destination[1], Is.GreaterThan(destination[0]));
    }

    [Test]
    public void Softmax_IgnoresInvalidScores_AndKeepsTheirOutputZero()
    {
        // 约定：非法分数被忽略，并保持输出位为 0。
        var destination = new double[3];

        MathKit.Softmax(new[] { 1.0, double.NaN, 3.0 }, destination);

        Assert.That(destination[1], Is.EqualTo(0.0));
        Assert.That(destination.Sum(), Is.EqualTo(1.0).Within(1e-10));
    }

    [Test]
    public void Softmax_AllInvalidScores_ClearsDestination()
    {
        // 约定：如果没有任何有效分数，destination 会被清成全 0。
        var destination = new[] { 1.0, 2.0 };

        MathKit.Softmax(new[] { double.NaN, double.PositiveInfinity }, destination);

        Assert.That(destination, Is.EqualTo(new[] { 0.0, 0.0 }));
    }

    [Test]
    public void Softmax_InvalidTemperature_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MathKit.Softmax(new[] { 1.0, 2.0 }, new double[2], 0.0));
    }

    [Test]
    public void Softmax_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MathKit.Softmax(new[] { 1.0, 2.0 }, new double[1]));
    }

    [Test]
    public void WeightedChoice_EmptyOrAllInvalid_ReturnsMinusOne()
    {
        // 约定：没有任何合法候选时，WeightedChoice 返回 -1。
        var random = new Random(123);

        Assert.That(MathKit.WeightedChoice(Array.Empty<double>(), random), Is.EqualTo(-1));
        Assert.That(MathKit.WeightedChoice(new[] { 0.0, double.NaN, -1.0 }, random), Is.EqualTo(-1));
    }

    [Test]
    public void WeightedChoice_WithSingleValidWeight_ReturnsThatIndex()
    {
        var random = new Random(123);

        var index = MathKit.WeightedChoice(new[] { 0.0, 0.0, 5.0, double.NaN }, random);

        Assert.That(index, Is.EqualTo(2));
    }

    [Test]
    public void EnumerateChooseMasks_ForFiveChooseThree_YieldsTenMasks()
    {
        var masks = MathKit.EnumerateChooseMasks(5, 3).ToArray();

        Assert.That(masks.Length, Is.EqualTo(10));
        Assert.That(masks[0], Is.EqualTo(0b00111UL));
        Assert.That(masks.All(mask => System.Numerics.BitOperations.PopCount(mask) == 3), Is.True);
    }

    [Test]
    public void EnumerateChooseMasks_InvalidRange_ReturnsEmpty()
    {
        Assert.That(MathKit.EnumerateChooseMasks(5, -1), Is.Empty);
        Assert.That(MathKit.EnumerateChooseMasks(5, 6), Is.Empty);
        Assert.That(MathKit.EnumerateChooseMasks(65, 1), Is.Empty);
    }

    [Test]
    public void EnumerateChooseMasks_ZeroChooseZero_ReturnsSingleZeroMask()
    {
        Assert.That(MathKit.EnumerateChooseMasks(0, 0).ToArray(), Is.EqualTo(new[] { 0UL }));
    }

    [Test]
    public void Distance_Methods_ReturnExpectedValues()
    {
        Assert.That(MathKit.Manhattan2D(1, 2, 4, 6), Is.EqualTo(7));
        Assert.That(MathKit.Manhattan3D(1, 2, 3, 4, 6, 5), Is.EqualTo(9));
        Assert.That(MathKit.Euclidean2D(0, 0, 3, 4), Is.EqualTo(5.0).Within(1e-12));
        Assert.That(MathKit.Euclidean3D(0, 0, 0, 1, 2, 2), Is.EqualTo(3.0).Within(1e-12));
    }
}
