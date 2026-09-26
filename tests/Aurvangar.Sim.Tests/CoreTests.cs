using Aurvangar.Sim.Core;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class CoreTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(256, 1024)]
    [InlineData(512, 0)]
    [InlineData(768, -1024)]
    [InlineData(1024, 0)]
    [InlineData(-256, -1024)]
    public void FixedSin_KeyAngles(int angle, int expected) => Assert.Equal(expected, Fixed.Sin(angle));

    [Fact]
    public void FixedSin_IsOddAndBounded()
    {
        for (int a = -2048; a <= 2048; a++)
        {
            Assert.InRange(Fixed.Sin(a), -Fixed.One, Fixed.One);
            Assert.Equal(-Fixed.Sin(a), Fixed.Sin(-a));
        }
    }

    [Fact]
    public void FloorDiv_RoundsDown()
    {
        Assert.Equal(-1, Fixed.FloorDiv(-1, 4));
        Assert.Equal(0, Fixed.FloorDiv(3, 4));
        Assert.Equal(-2, Fixed.FloorDiv(-5, 4));
    }

    [Fact]
    public void Int3_RotateY_QuarterTurns()
    {
        var v = new Int3(2, 1, 0);
        Assert.Equal(new Int3(0, 1, 2), v.RotateY(90));
        Assert.Equal(new Int3(-2, 1, 0), v.RotateY(180));
        Assert.Equal(new Int3(0, 1, -2), v.RotateY(270));
        Assert.Equal(v, v.RotateY(360));
    }

    [Fact]
    public void Rng_SameSeedSameSequence()
    {
        var a = new Rng(42); var b = new Rng(42);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.NextU64(), b.NextU64());
    }

    [Fact]
    public void Rng_DeriveIsIndependentOfParentState()
    {
        var a = Rng.Derive(7, 3); var b = Rng.Derive(7, 3); var c = Rng.Derive(7, 4);
        Assert.Equal(a.NextU64(), b.NextU64());
        Assert.NotEqual(Rng.Derive(7, 3).NextU64(), c.NextU64());
    }

    [Fact]
    public void Rng_NextIntInRange()
    {
        var r = new Rng(1);
        for (int i = 0; i < 10_000; i++) Assert.InRange(r.NextInt(3, 9), 3, 8);
    }

    [Fact]
    public void StateHasher_OrderSensitive()
    {
        var a = StateHasher.Create(); a.Add(1); a.Add(2);
        var b = StateHasher.Create(); b.Add(2); b.Add(1);
        Assert.NotEqual(a.Value, b.Value);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(255, 100)]
    [InlineData(5_000, 2_000)]
    [InlineData(40_000, 1_048_576)]
    [InlineData(40_000, 50_000_000)]
    public void IndexSort_MatchesListSort(int count, int maxExclusive)
    {
        var rng = new Rng(7);
        var sorter = new IndexSort();
        for (int round = 0; round < 3; round++)                // reuse: scratch buffer and counts carry over
        {
            var list = new List<int>();
            for (int k = 0; k < count; k++) list.Add(rng.NextInt(0, maxExclusive));
            var expected = new List<int>(list);
            expected.Sort();
            sorter.Sort(list);
            Assert.Equal(expected, list);
        }
    }
}
