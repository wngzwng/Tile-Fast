using NUnit.Framework;
using Tile.Core.Common.BitSet;
using Tile.Core.Core.Mapping;
using Tile.Core.Core.Types;
using Tile.Core.Core.Zones;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Tests.Core;

public sealed class StagingAreaTests
{
    [Test]
    public void ToString_ContainsCurrentStateSummary()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1]);

        var text = staging.ToString();

        Assert.That(text, Does.Contain("StagingArea("));
        Assert.That(text, Does.Contain("Used=0/7"));
        Assert.That(text, Does.Contain("Available=7"));
        Assert.That(text, Does.Contain("IsFull=False"));
        Assert.That(text, Does.Contain("IsEmpty=True"));
        Assert.That(text, Does.Contain("Tiles=[]"));
        Assert.That(text, Does.Contain("SuitBits=0"));
        Assert.That(text, Does.Contain("SuitCounts=[]"));

        staging.Enter(0);
        staging.Enter(1);
        staging.Enter(2);

        text = staging.ToString();

        Assert.That(text, Does.Contain("Used=3/7"));
        Assert.That(text, Does.Contain("Available=4"));
        Assert.That(text, Does.Contain("IsEmpty=False"));
        Assert.That(text, Does.Contain("Tiles=[t0_s1, t2_s1, t1_s2]"));
        Assert.That(text, Does.Contain($"SuitBits={SuitBits(1, 2)}"));
        Assert.That(text, Does.Contain("SuitCounts=[s1_c2, s2_c1]"));
    }

    [Test]
    public void Add_WhenSuitAlreadyExists_InsertsAfterSameSuitGroup()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1, 3, 2]);

        staging.Enter(0);
        staging.Enter(1);
        staging.Enter(2);
        staging.Enter(3);
        staging.Enter(4);

        Assert.That(staging.Tiles.ToArray(), Is.EqualTo(new[] { 0, 2, 1, 4, 3 }));
        Assert.That(staging.GetSuitCount(1), Is.EqualTo(2));
        Assert.That(staging.GetSuitCount(2), Is.EqualTo(2));
        Assert.That(staging.GetSuitCount(3), Is.EqualTo(1));
        Assert.That(staging.SuitBits, Is.EqualTo(SuitBits(1, 2, 3)));
    }

    [Test]
    public void Remove_WhenLastSuitTileRemoved_UpdatesSuitOrderAndCounts()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1, 3]);

        staging.Enter(0);
        staging.Enter(1);
        staging.Enter(2);
        staging.Enter(3);

        staging.Leave(1);

        Assert.That(staging.Tiles.ToArray(), Is.EqualTo(new[] { 0, 2, 3 }));
        Assert.That(staging.GetSuitCount(2), Is.Zero);
        Assert.That(staging.SuitBits, Is.EqualTo(SuitBits(1, 3)));
    }

    [Test]
    public void TryMatch_RemovesFirstMatchingSuitGroup()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1, 1], LevelRuleSpec.TripleTile);

        staging.Enter(0);
        staging.Enter(1);
        staging.Enter(2);
        staging.Enter(3);

        var matched = staging.TryMatch(1, out var matchedTileIds);

        Assert.That(matched, Is.True);
        Assert.That(matchedTileIds, Is.EqualTo(new[] { 0, 2, 3 }));
        Assert.That(staging.Tiles.ToArray(), Is.EqualTo(new[] { 1 }));
        Assert.That(staging.GetSuitCount(1), Is.Zero);
        Assert.That(staging.SuitBits, Is.EqualTo(SuitBits(2)));
    }

    [Test]
    public void GetSuitTiles_ReturnsTilesWithRequestedSuitInSlotOrder()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1, 3, 2]);

        staging.Enter(0);
        staging.Enter(1);
        staging.Enter(2);
        staging.Enter(3);
        staging.Enter(4);

        Assert.That(staging.GetSuitTiles(1), Is.EqualTo(new[] { 0, 2 }));
        Assert.That(staging.GetSuitTiles(2), Is.EqualTo(new[] { 1, 4 }));
        Assert.That(staging.GetSuitTiles(4), Is.Empty);
    }

    [Test]
    public void GetSuitTileBits_FillsProvidedBitBuffer()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1, 3, 2]);
        ulong[] tileBits = [ulong.MaxValue];

        staging.Enter(0);
        staging.Enter(1);
        staging.Enter(2);
        staging.Enter(3);
        staging.Enter(4);

        var count = staging.GetSuitTileBits(2, tileBits);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(BitSetOperations.Get(tileBits, 1), Is.True);
        Assert.That(BitSetOperations.Get(tileBits, 4), Is.True);
        Assert.That(BitSetOperations.Get(tileBits, 0), Is.False);
        Assert.That(BitSetOperations.Get(tileBits, 2), Is.False);
        Assert.That(BitSetOperations.Get(tileBits, 3), Is.False);
    }

    [Test]
    public void SetCapacity_ChangesCapacityAndKeepsState()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1]);

        staging.Enter(0);
        staging.Enter(1);
        staging.SetCapacity(5);

        Assert.That(staging.Capacity, Is.EqualTo(5));
        Assert.That(staging.UsedCapacity, Is.EqualTo(2));
        Assert.That(staging.AvailableCapacity, Is.EqualTo(3));
        Assert.That(staging.Tiles.ToArray(), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(staging.SuitBits, Is.EqualTo(SuitBits(1, 2)));
        Assert.That(staging.GetSuitCount(1), Is.EqualTo(1));
        Assert.That(staging.GetSuitCount(2), Is.EqualTo(1));
    }

    [Test]
    public void SetCapacity_WhenSmallerThanUsedCapacity_Throws()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1]);

        staging.Enter(0);
        staging.Enter(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => staging.SetCapacity(1));
    }

    [Test]
    public void Reset_ClearsTilesAndSuitStateButKeepsCapacity()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1]);

        staging.Enter(0);
        staging.Enter(1);
        staging.SetCapacity(5);

        staging.Reset();

        Assert.That(staging.Capacity, Is.EqualTo(5));
        Assert.That(staging.UsedCapacity, Is.Zero);
        Assert.That(staging.AvailableCapacity, Is.EqualTo(5));
        Assert.That(staging.IsEmpty, Is.True);
        Assert.That(staging.Tiles.ToArray(), Is.Empty);
        Assert.That(staging.SuitBits, Is.Zero);
        Assert.That(staging.GetSuitCount(1), Is.Zero);
        Assert.That(staging.GetSuitCount(2), Is.Zero);
    }

    [Test]
    public void RemoveSuitGroup_RemovesAllTilesWithRequestedSuit()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1, 3, 2]);

        staging.Enter(0);
        staging.Enter(1);
        staging.Enter(2);
        staging.Enter(3);
        staging.Enter(4);

        var removed = staging.RemoveSuitGroup(2);

        Assert.That(removed, Is.EqualTo(new[] { 1, 4 }));
        Assert.That(staging.Tiles.ToArray(), Is.EqualTo(new[] { 0, 2, 3 }));
        Assert.That(staging.GetSuitCount(2), Is.Zero);
        Assert.That(staging.SuitBits, Is.EqualTo(SuitBits(1, 3)));
    }

    [Test]
    public void RemoveSuitGroup_WhenSuitMissing_ReturnsEmptyAndKeepsState()
    {
        var staging = CreateStagingArea(suits: [1, 2, 1]);

        staging.Enter(0);
        staging.Enter(1);
        staging.Enter(2);

        var removed = staging.RemoveSuitGroup(3);

        Assert.That(removed, Is.Empty);
        Assert.That(staging.Tiles.ToArray(), Is.EqualTo(new[] { 0, 2, 1 }));
        Assert.That(staging.SuitBits, Is.EqualTo(SuitBits(1, 2)));
    }

    private static ulong SuitBits(params int[] suits)
    {
        var bits = 0UL;

        foreach (var suit in suits)
            bits |= 1UL << suit;

        return bits;
    }

    private static StagingArea CreateStagingArea(int[] suits, LevelRuleSpec? ruleSpec = null)
    {
        var tiles = new Tile[suits.Length];

        for (var i = 0; i < suits.Length; i++)
        {
            var tile = new Tile(i, (i * 2, 0, 0).PackXyz());
            tile.SetSuit(suits[i]);
            tiles[i] = tile;
        }

        var mapping = TileMappingTable.Create(
            tiles,
            LockRuleTypeEnum.Tile,
            maxCol: suits.Length * 2,
            maxRow: 2,
            maxLayer: 1);

        ruleSpec ??= LevelRuleSpec.TripleTile;

        return new StagingArea(
            mapping,
            ruleSpec.MatchRequireCount,
            ruleSpec.SlotCapacity);
    }
}
