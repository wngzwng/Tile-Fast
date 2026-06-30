using NUnit.Framework;
using Tile.Core.Metrices;

namespace Tile.Core.Tests.Metrices;

public sealed class MetricBagTests
{
    private sealed record DetailPayload(string Reason, int Step);

    private static readonly MetricKey<int> SuccessCount =
        new("success_count");

    private static readonly MetricKey<int> FailureCount =
        new("failure_count");

    private static readonly MetricKey<double> FailRate =
        new("fail_rate");

    private static readonly MetricKey<double> SuccessRate =
        new("success_rate");

    private static readonly MetricKey<bool> IsSuccess =
        new("is_success");

    private static readonly MetricKey<bool> HasDeadEnd =
        new("has_dead_end");

    private static readonly MetricKey<string> LevelId =
        new("level_id");

    private static readonly MetricKey<string> FailReason =
        new("fail_reason");

    private static readonly MetricKey<string> DisplayName =
        new("display_name");

    private static readonly MetricKey<List<int>> CandidateCountList =
        new("candidate_count_list");

    private static readonly MetricKey<List<int>> ClearStepList =
        new("clear_step_list");

    private static readonly MetricKey<List<double>> SelectedScoreList =
        new("selected_score_list");

    private static readonly MetricKey<List<double>> ScoreDeltaList =
        new("score_delta_list");

    private static readonly MetricKey<object> Detail =
        new("detail");

    private static readonly MetricKey<object> MissingDetail =
        new("missing_detail");

    private static readonly MetricKey<List<object>> DetailList =
        new("detail_list");

    private static readonly MetricKey<List<object>> MissingDetailList =
        new("missing_detail_list");

    private static readonly MetricKey<DetailPayload> DetailPayloadKey =
        new("detail_payload");

    private static readonly MetricKey<DetailPayload> MissingDetailPayloadKey =
        new("missing_detail_payload");

    private static readonly MetricKey<List<DetailPayload>> DetailPayloadList =
        new("detail_payload_list");

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

        bag.Set(SuccessCount, 10);
        bag.Add(SuccessCount, 2);
        bag.Set(FailRate, 0.25);
        bag.Add(FailRate, 0.5);
        bag.Set(IsSuccess, true);
        bag.Set(LevelId, "level-001");

        Assert.That(bag.TryRead(SuccessCount, out var successCount), Is.True);
        Assert.That(successCount, Is.EqualTo(12));
        Assert.That(bag.GetOrDefault(FailureCount, -1), Is.EqualTo(-1));

        Assert.That(bag.TryRead(FailRate, out var failRate), Is.True);
        Assert.That(failRate, Is.EqualTo(0.75));
        Assert.That(bag.GetOrDefault(SuccessRate, 1.0), Is.EqualTo(1.0));

        Assert.That(bag.TryRead(IsSuccess, out var isSuccess), Is.True);
        Assert.That(isSuccess, Is.True);
        Assert.That(bag.GetOrDefault(HasDeadEnd, true), Is.True);

        Assert.That(bag.TryRead(LevelId, out var levelId), Is.True);
        Assert.That(levelId, Is.EqualTo("level-001"));
        Assert.That(bag.GetOrDefault(FailReason, "none"), Is.EqualTo("none"));
    }

    [Test]
    public void StringValue_WhenMissing_ReturnsEmptyOnTryRead()
    {
        var bag = new MetricBag();

        var ok = bag.TryRead(LevelId, out var value);

        Assert.That(ok, Is.False);
        Assert.That(value, Is.EqualTo(string.Empty));
    }

    [Test]
    public void StringValue_WhenNull_Throws()
    {
        var bag = new MetricBag();

        Assert.Throws<ArgumentNullException>(() => bag.Set(LevelId, null!));
    }

    [Test]
    public void ListValues_CanSetAppendReadAndGetEmpty()
    {
        var bag = new MetricBag();
        var intList = new List<int> { 1, 2 };
        var doubleList = new List<double> { 0.25, 0.5 };

        bag.Set(CandidateCountList, intList);
        bag.Append(CandidateCountList, 3);
        bag.Set(SelectedScoreList, doubleList);
        bag.Append(SelectedScoreList, 0.75);

        Assert.That(bag.TryRead(CandidateCountList, out var counts), Is.True);
        Assert.That(counts, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(bag.GetOrEmpty(ClearStepList), Is.Empty);

        Assert.That(bag.TryRead(SelectedScoreList, out var scores), Is.True);
        Assert.That(scores, Is.EqualTo(new[] { 0.25, 0.5, 0.75 }));
        Assert.That(bag.GetOrEmpty(ScoreDeltaList), Is.Empty);
    }

    [Test]
    public void ListValues_WhenNull_Throws()
    {
        var bag = new MetricBag();

        Assert.Throws<ArgumentNullException>(() => bag.Set(CandidateCountList, null!));
        Assert.Throws<ArgumentNullException>(() => bag.Set(SelectedScoreList, null!));
    }

    [Test]
    public void ObjectValues_CanSetReadAndGetDefault()
    {
        var bag = new MetricBag();
        var detail = new { Reason = "slot_full", Step = 4 };

        bag.Set(Detail, detail);

        Assert.That(bag.TryRead(Detail, out var found), Is.True);
        Assert.That(found, Is.SameAs(detail));
        Assert.That(bag.GetOrDefault(Detail), Is.SameAs(detail));
        Assert.That(bag.GetOrDefault(MissingDetail), Is.Null);
        Assert.That(bag.GetOrDefault(MissingDetail, "fallback"), Is.EqualTo("fallback"));
    }

    [Test]
    public void CustomObjectValues_AreStoredInObjectWarehouseAndReadByOriginalType()
    {
        var bag = new MetricBag();
        var detail = new DetailPayload("slot_full", 4);

        bag.Set(DetailPayloadKey, detail);

        Assert.That(bag.Contains(DetailPayloadKey), Is.True);
        Assert.That(bag.TryRead(DetailPayloadKey, out var found), Is.True);
        Assert.That(found, Is.EqualTo(detail));
        Assert.That(bag.GetOrDefault(DetailPayloadKey), Is.EqualTo(detail));
        Assert.That(
            bag.GetOrDefault(MissingDetailPayloadKey, new DetailPayload("fallback", -1)),
            Is.EqualTo(new DetailPayload("fallback", -1)));
    }

    [Test]
    public void ObjectValues_WhenNull_Throws()
    {
        var bag = new MetricBag();

        Assert.Throws<ArgumentNullException>(() => bag.Set(Detail, null!));
        Assert.Throws<ArgumentNullException>(() => bag.Append(DetailList, null!));
    }

    [Test]
    public void ObjectListValues_CanSetAppendReadAndGetEmpty()
    {
        var bag = new MetricBag();
        var list = new List<object> { "a", 2 };

        bag.Set(DetailList, list);
        bag.Append(DetailList, 3.5);

        Assert.That(bag.TryRead(DetailList, out var values), Is.True);
        Assert.That(values, Is.EqualTo(new object[] { "a", 2, 3.5 }));
        Assert.That(bag.GetOrEmpty(MissingDetailList), Is.Empty);
    }

    [Test]
    public void CustomObjectListValues_AreStoredInObjectListWarehouseAndReadByOriginalType()
    {
        var bag = new MetricBag();
        var first = new DetailPayload("a", 1);
        var second = new DetailPayload("b", 2);
        var third = new DetailPayload("c", 3);

        bag.Set(DetailPayloadList, new List<DetailPayload> { first, second });
        bag.Append(DetailPayloadList, third);

        Assert.That(bag.Contains(DetailPayloadList), Is.True);
        Assert.That(bag.TryRead(DetailPayloadList, out var values), Is.True);
        Assert.That(values, Is.EqualTo(new[] { first, second, third }));
    }

    [Test]
    public void ResetValues_ClearsScalarsAndKeepsListContainers()
    {
        var bag = new MetricBag();
        var list = new List<int> { 1, 2, 3 };

        bag.Set(SuccessCount, 10);
        bag.Set(CandidateCountList, list);
        bag.Set(Detail, "detail");
        bag.Set(DetailList, new List<object> { "a", 1 });

        bag.ResetValues();

        Assert.That(bag.TryRead(SuccessCount, out _), Is.False);
        Assert.That(bag.TryRead(Detail, out _), Is.False);
        Assert.That(bag.TryRead(CandidateCountList, out var values), Is.True);
        Assert.That(values, Is.Empty);
        Assert.That(bag.TryRead(DetailList, out var objectValues), Is.True);
        Assert.That(objectValues, Is.Empty);

        bag.Append(CandidateCountList, 4);
        bag.Append(DetailList, "b");

        Assert.That(ReferenceEquals(list, bag.GetOrEmpty(CandidateCountList)), Is.True);
        Assert.That(list, Is.EqualTo(new[] { 4 }));
        Assert.That(bag.GetOrEmpty(DetailList), Is.EqualTo(new object[] { "b" }));
    }

    [Test]
    public void Clear_RemovesAllValuesAndListContainers()
    {
        var bag = new MetricBag();

        bag.Set(SuccessCount, 10);
        bag.Append(CandidateCountList, 1);
        bag.Append(SelectedScoreList, 0.5);
        bag.Set(Detail, "detail");
        bag.Append(DetailList, "a");

        bag.Clear();

        Assert.That(bag.TryRead(SuccessCount, out _), Is.False);
        Assert.That(bag.TryRead(CandidateCountList, out _), Is.False);
        Assert.That(bag.TryRead(SelectedScoreList, out _), Is.False);
        Assert.That(bag.TryRead(Detail, out _), Is.False);
        Assert.That(bag.TryRead(DetailList, out _), Is.False);
    }

    [Test]
    public void Contains_AndTryReadText_ReadValuesByUntypedKey()
    {
        var bag = new MetricBag();

        bag.Set(SuccessCount, 12);
        bag.Set(FailRate, 0.25);
        bag.Set(IsSuccess, true);
        bag.Set(LevelId, "level-001");
        bag.Set(CandidateCountList, new List<int> { 1, 2, 3 });
        bag.Set(SelectedScoreList, new List<double> { 0.25, 0.5 });
        bag.Set(Detail, 3.5);
        bag.Set(DetailList, new List<object> { "a", 2, 0.5 });

        Assert.That(bag.Contains(SuccessCount), Is.True);
        Assert.That(bag.TryReadText(SuccessCount, out var success), Is.True);
        Assert.That(success, Is.EqualTo("12"));
        Assert.That(bag.TryReadText(FailRate, out var failRate), Is.True);
        Assert.That(failRate, Is.EqualTo("0.25"));
        Assert.That(bag.TryReadText(IsSuccess, out var isSuccess), Is.True);
        Assert.That(isSuccess, Is.EqualTo("true"));
        Assert.That(bag.TryReadText(LevelId, out var levelId), Is.True);
        Assert.That(levelId, Is.EqualTo("level-001"));
        Assert.That(bag.TryReadText(CandidateCountList, out var counts), Is.True);
        Assert.That(counts, Is.EqualTo("1|2|3"));
        Assert.That(bag.TryReadText(SelectedScoreList, out var scores), Is.True);
        Assert.That(scores, Is.EqualTo("0.25|0.5"));
        Assert.That(bag.TryReadText(Detail, out var detail), Is.True);
        Assert.That(detail, Is.EqualTo("3.5"));
        Assert.That(bag.TryReadText(DetailList, out var detailList), Is.True);
        Assert.That(detailList, Is.EqualTo("a|2|0.5"));

        Assert.That(bag.Contains(FailureCount), Is.False);
        Assert.That(bag.TryReadText(FailureCount, out var missing), Is.False);
        Assert.That(missing, Is.EqualTo(string.Empty));
    }

    [Test]
    public void TryReadObject_ReadsValuesByUntypedKey()
    {
        var bag = new MetricBag();
        var detail = new DetailPayload("slot_full", 4);
        var details = new List<DetailPayload> { detail };

        bag.Set(SuccessCount, 12);
        bag.Set(FailRate, 0.25);
        bag.Set(IsSuccess, true);
        bag.Set(LevelId, "level-001");
        bag.Set(CandidateCountList, new List<int> { 1, 2, 3 });
        bag.Set(DetailPayloadKey, detail);
        bag.Set(DetailPayloadList, details);

        Assert.That(bag.TryReadObject(SuccessCount, out var success), Is.True);
        Assert.That(success, Is.EqualTo(12));
        Assert.That(bag.TryReadObject(FailRate, out var failRate), Is.True);
        Assert.That(failRate, Is.EqualTo(0.25));
        Assert.That(bag.TryReadObject(IsSuccess, out var isSuccess), Is.True);
        Assert.That(isSuccess, Is.EqualTo(true));
        Assert.That(bag.TryReadObject(LevelId, out var levelId), Is.True);
        Assert.That(levelId, Is.EqualTo("level-001"));
        Assert.That(bag.TryReadObject(CandidateCountList, out var counts), Is.True);
        Assert.That(counts, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(bag.TryReadObject(DetailPayloadKey, out var foundDetail), Is.True);
        Assert.That(foundDetail, Is.EqualTo(detail));
        Assert.That(bag.TryReadObject(DetailPayloadList, out var foundDetails), Is.True);
        Assert.That(foundDetails, Is.EqualTo(details));

        Assert.That(bag.TryReadObject(FailureCount, out var missing), Is.False);
        Assert.That(missing, Is.Null);
    }

}
