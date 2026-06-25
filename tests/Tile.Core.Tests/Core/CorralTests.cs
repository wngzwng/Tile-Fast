using NUnit.Framework;
using Tile.Core.Core.Zones;

namespace Tile.Core.Tests.Core;

public sealed class CorralTests
{
    [Test]
    public void ToString_ContainsCurrentStateSummary()
    {
        var corral = new Corral(capacity: 3);

        var text = corral.ToString();

        Assert.That(text, Does.Contain("Corral("));
        Assert.That(text, Does.Contain("Count=0"));
        Assert.That(text, Does.Contain("Capacity=3"));
        Assert.That(text, Does.Contain("IsEmpty=True"));
        Assert.That(text, Does.Contain("Tiles=[]"));

        corral.Push(10);
        corral.Push(20);

        text = corral.ToString();

        Assert.That(text, Does.Contain("Count=2"));
        Assert.That(text, Does.Contain("IsEmpty=False"));
        Assert.That(text, Does.Contain("Tiles=[t10, t20]"));
    }

    [Test]
    public void ToString_WithMatchRequireCount_ShowsOnlyLastDoubleMatchRequireCountTiles()
    {
        var corral = new Corral(capacity: 8);

        for (var tileIndex = 0; tileIndex < 8; tileIndex++)
            corral.Push(tileIndex);

        var text = corral.ToString(matchRequireCount: 3);

        Assert.That(text, Does.Contain("Count=8"));
        Assert.That(text, Does.Contain("Tiles(last6)=[t2, t3, t4, t5, t6, t7]"));
    }

    [Test]
    public void ToString_WithMatchRequireCount_WhenStackIsShorter_ShowsAllTiles()
    {
        var corral = new Corral(capacity: 3);

        corral.Push(10);
        corral.Push(20);

        var text = corral.ToString(matchRequireCount: 3);

        Assert.That(text, Does.Contain("Tiles(last6)=[t10, t20]"));
    }

    [Test]
    public void PushPopAndPeek_FollowLifoOrder()
    {
        var corral = new Corral(capacity: 3);

        corral.Push(10);
        corral.Push(20);

        Assert.That(corral.Count, Is.EqualTo(2));
        Assert.That(corral.Peek(), Is.EqualTo(20));
        Assert.That(corral.Pop(), Is.EqualTo(20));
        Assert.That(corral.Pop(), Is.EqualTo(10));
        Assert.That(corral.IsEmpty, Is.True);
    }

    [Test]
    public void PopMany_WritesPoppedTilesInLifoOrder()
    {
        var corral = new Corral(capacity: 4);
        Span<int> buffer = stackalloc int[2];

        corral.Push(1);
        corral.Push(2);
        corral.Push(3);

        var count = corral.PopMany(2, buffer);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(buffer.ToArray(), Is.EqualTo(new[] { 3, 2 }));
        Assert.That(corral.Count, Is.EqualTo(1));
        Assert.That(corral.Peek(), Is.EqualTo(1));
    }

    [Test]
    public void DropMany_RemovesTailWithoutReturningTiles()
    {
        var corral = new Corral(capacity: 4);

        corral.Push(1);
        corral.Push(2);
        corral.Push(3);

        corral.DropMany(2);

        Assert.That(corral.Count, Is.EqualTo(1));
        Assert.That(corral.Peek(), Is.EqualTo(1));
    }

    [Test]
    public void Reset_ClearsStack()
    {
        var corral = new Corral(capacity: 2);

        corral.Push(1);
        corral.Push(2);

        corral.Reset();

        Assert.That(corral.Count, Is.Zero);
        Assert.That(corral.IsEmpty, Is.True);
    }

    [Test]
    public void Clone_CopiesStateIndependently()
    {
        var corral = new Corral(capacity: 3);
        corral.Push(1);
        corral.Push(2);

        var clone = corral.Clone();

        Assert.That(clone.Pop(), Is.EqualTo(2));
        Assert.That(corral.Pop(), Is.EqualTo(2));
        Assert.That(corral.Count, Is.EqualTo(1));
    }

    [Test]
    public void Push_WhenFull_Throws()
    {
        var corral = new Corral(capacity: 1);

        corral.Push(1);

        Assert.Throws<InvalidOperationException>(() => corral.Push(2));
    }

    [Test]
    public void PopAndPeek_WhenEmpty_Throw()
    {
        var corral = new Corral(capacity: 1);

        Assert.Throws<InvalidOperationException>(() => corral.Pop());
        Assert.Throws<InvalidOperationException>(() => corral.Peek());
    }

    [Test]
    public void PopMany_WhenBufferTooSmall_Throws()
    {
        var corral = new Corral(capacity: 2);
        int[] buffer = new int[1];

        corral.Push(1);
        corral.Push(2);

        Assert.Throws<ArgumentException>(() => corral.PopMany(2, buffer));
    }

    [Test]
    public void DropMany_WhenCountExceedsStack_Throws()
    {
        var corral = new Corral(capacity: 1);

        corral.Push(1);

        Assert.Throws<InvalidOperationException>(() => corral.DropMany(2));
    }
}
