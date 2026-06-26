using NUnit.Framework;
using Tile.Core.Metrices;

namespace Tile.Core.Tests.Metrices;

public sealed class MetricBagTests
{
    [Test]
    public void MetricKey_StoresNameAndValueType()
    {
        var key = new MetricKey<int>("success_count");

        Assert.That(key.Name, Is.EqualTo("success_count"));
        Assert.That(key.ValueType, Is.EqualTo(typeof(int)));
        Assert.That(key.ToString(), Is.EqualTo("success_count"));
    }

    [Test]
    public void MetricKey_WhenNameIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => _ = new MetricKey<int>(""));
        Assert.Throws<ArgumentException>(() => _ = new MetricKey<int>(" "));
    }

    [Test]
    public void ScalarValues_CanSetAddReadAndGetDefault()
    {
        var bag = new MetricBag();

        bag.Set(MetricKeys.SuccessCount, 10);
        bag.Add(MetricKeys.SuccessCount, 2);
        bag.Set(MetricKeys.FailRate, 0.25);
        bag.Add(MetricKeys.FailRate, 0.5);
        bag.Set(MetricKeys.IsSuccess, true);
        bag.Set(MetricKeys.LevelId, "level-001");

        Assert.That(bag.TryRead(MetricKeys.SuccessCount, out var successCount), Is.True);
        Assert.That(successCount, Is.EqualTo(12));
        Assert.That(bag.GetOrDefault(MetricKeys.FailureCount, -1), Is.EqualTo(-1));

        Assert.That(bag.TryRead(MetricKeys.FailRate, out var failRate), Is.True);
        Assert.That(failRate, Is.EqualTo(0.75));
        Assert.That(bag.GetOrDefault(MetricKeys.SuccessRate, 1.0), Is.EqualTo(1.0));

        Assert.That(bag.TryRead(MetricKeys.IsSuccess, out var isSuccess), Is.True);
        Assert.That(isSuccess, Is.True);
        Assert.That(bag.GetOrDefault(MetricKeys.HasDeadEnd, true), Is.True);

        Assert.That(bag.TryRead(MetricKeys.LevelId, out var levelId), Is.True);
        Assert.That(levelId, Is.EqualTo("level-001"));
        Assert.That(bag.GetOrDefault(MetricKeys.FailReason, "none"), Is.EqualTo("none"));
    }

    [Test]
    public void StringValue_WhenMissing_ReturnsEmptyOnTryRead()
    {
        var bag = new MetricBag();

        var ok = bag.TryRead(MetricKeys.LevelId, out var value);

        Assert.That(ok, Is.False);
        Assert.That(value, Is.EqualTo(string.Empty));
    }

    [Test]
    public void StringValue_WhenNull_Throws()
    {
        var bag = new MetricBag();

        Assert.Throws<ArgumentNullException>(() => bag.Set(MetricKeys.LevelId, null!));
    }

    [Test]
    public void ListValues_CanSetAppendReadAndGetEmpty()
    {
        var bag = new MetricBag();
        var intList = new List<int> { 1, 2 };
        var doubleList = new List<double> { 0.25, 0.5 };

        bag.Set(MetricKeys.CandidateCountList, intList);
        bag.Append(MetricKeys.CandidateCountList, 3);
        bag.Set(MetricKeys.SelectedScoreList, doubleList);
        bag.Append(MetricKeys.SelectedScoreList, 0.75);

        Assert.That(bag.TryRead(MetricKeys.CandidateCountList, out var counts), Is.True);
        Assert.That(counts, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(bag.GetOrEmpty(MetricKeys.ClearStepList), Is.Empty);

        Assert.That(bag.TryRead(MetricKeys.SelectedScoreList, out var scores), Is.True);
        Assert.That(scores, Is.EqualTo(new[] { 0.25, 0.5, 0.75 }));
        Assert.That(bag.GetOrEmpty(MetricKeys.ScoreDeltaList), Is.Empty);
    }

    [Test]
    public void ListValues_WhenNull_Throws()
    {
        var bag = new MetricBag();

        Assert.Throws<ArgumentNullException>(() => bag.Set(MetricKeys.CandidateCountList, null!));
        Assert.Throws<ArgumentNullException>(() => bag.Set(MetricKeys.SelectedScoreList, null!));
    }

    [Test]
    public void ResetValues_ClearsScalarsAndKeepsListContainers()
    {
        var bag = new MetricBag();
        var list = new List<int> { 1, 2, 3 };

        bag.Set(MetricKeys.SuccessCount, 10);
        bag.Set(MetricKeys.CandidateCountList, list);

        bag.ResetValues();

        Assert.That(bag.TryRead(MetricKeys.SuccessCount, out _), Is.False);
        Assert.That(bag.TryRead(MetricKeys.CandidateCountList, out var values), Is.True);
        Assert.That(values, Is.Empty);

        bag.Append(MetricKeys.CandidateCountList, 4);

        Assert.That(ReferenceEquals(list, bag.GetOrEmpty(MetricKeys.CandidateCountList)), Is.True);
        Assert.That(list, Is.EqualTo(new[] { 4 }));
    }

    [Test]
    public void Clear_RemovesAllValuesAndListContainers()
    {
        var bag = new MetricBag();

        bag.Set(MetricKeys.SuccessCount, 10);
        bag.Append(MetricKeys.CandidateCountList, 1);
        bag.Append(MetricKeys.SelectedScoreList, 0.5);

        bag.Clear();

        Assert.That(bag.TryRead(MetricKeys.SuccessCount, out _), Is.False);
        Assert.That(bag.TryRead(MetricKeys.CandidateCountList, out _), Is.False);
        Assert.That(bag.TryRead(MetricKeys.SelectedScoreList, out _), Is.False);
    }
}
