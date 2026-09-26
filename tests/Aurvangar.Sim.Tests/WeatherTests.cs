using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M6-T4: WeatherSystem (ECO-17, ECO-18, WAT-09, ARCH-01 step 2, ADR-049).</summary>
public class WeatherTests
{
    private const int Day = SimClock.TicksPerDay;

    private static Simulation Flat() => new ScenarioBuilder().Ground(4).Source(new Int3(2, 5, 2)).Build();

    [Theory]
    [InlineData(0, Season.Wet, 5 * Day, 5)]
    [InlineData(1, Season.Wet, 5 * Day - 1, 5)]
    [InlineData(4 * Day, Season.Wet, Day, 1)]
    [InlineData(5 * Day - 1, Season.Wet, 1, 1)]
    [InlineData(5 * Day, Season.Drought, 2 * Day, 2)]
    [InlineData(6 * Day + 1, Season.Drought, Day - 1, 1)]
    [InlineData(7 * Day - 1, Season.Drought, 1, 1)]
    [InlineData(7 * Day, Season.Wet, 5 * Day, 5)]
    [InlineData(12 * Day, Season.Drought, 2 * Day, 2)]
    [InlineData(70 * Day + 3, Season.Wet, 5 * Day - 3, 5)]
    public void Schedule_Wet5Drought2(long tick, Season season, int ticksLeft, int daysLeft) // ECO-17, ECO-18
    {
        Assert.Equal(season, WeatherSystem.SeasonAt(tick));
        Assert.Equal(ticksLeft, WeatherSystem.TicksUntilChange(tick));
        Assert.Equal(daysLeft, WeatherSystem.DaysUntilChange(tick));
    }

    [Fact]
    public void SourceStrength_FollowsSeason() // ECO-17, WAT-09
    {
        var sim = Flat();
        Assert.Equal(100, sim.Water.SourceStrength);
        sim.Clock.Tick = 5 * Day - 1;
        sim.Tick();                                    // last Wet tick
        Assert.Equal(100, sim.Water.SourceStrength);
        sim.Tick();                                    // tick 5 days: drought begins
        Assert.Equal(0, sim.Water.SourceStrength);
        long added = sim.Water.Stats.SourceAdded;
        sim.Water.SetLevel(new Int3(2, 5, 2), 0);
        sim.RunTicks(50);
        Assert.Equal(added, sim.Water.Stats.SourceAdded);   // drought: sources add nothing

        sim.Clock.Tick = 7 * Day - 1;
        sim.Tick();
        Assert.Equal(0, sim.Water.SourceStrength);
        sim.Tick();                                    // tick 7 days: Wet again
        Assert.Equal(100, sim.Water.SourceStrength);
        sim.RunTicks(5);
        Assert.True(sim.Water.Stats.SourceAdded > added, "the source did not resume after the drought");
    }

    [Fact]
    public void SeasonChanged_EmittedOnlyOnTransitions() // ECO-17, ECO-18
    {
        var sim = Flat();
        var seen = new List<(long Tick, Season Season)>();
        void Run(int n)
        {
            for (int i = 0; i < n; i++)
            {
                long t = sim.Clock.Tick;
                sim.Tick();
                foreach (var e in sim.Events.Drain())
                    if (e is SeasonChanged s) seen.Add((t, s.Season));
            }
        }
        Run(3);                                        // tick 0 is not a transition
        sim.Clock.Tick = 5 * Day - 2; Run(4);
        sim.Clock.Tick = 7 * Day - 2; Run(4);
        sim.Clock.Tick = 12 * Day - 2; Run(4);
        Assert.Equal(new[] { (5L * Day, Season.Drought), (7L * Day, Season.Wet), (12L * Day, Season.Drought) }, seen);
    }

    [Fact]
    public void DroughtSource_SeepsAway_CountedAsDrained() // WAT-09, WAT-11, ADR-049
    {
        var cell = new Int3(2, 5, 2);
        var sim = new ScenarioBuilder().Ground(4).Water(cell, WaterGrid.Full).Source(cell).Build();
        long initial = sim.Water.TotalVolume();
        sim.Water.SourceStrength = 0;
        sim.Tick();
        Assert.Equal(0, sim.Water.GetLevel(cell));
        Assert.Equal(WaterGrid.Full, sim.Water.Stats.Drained);
        Assert.Equal(0, sim.Water.Stats.SourceAdded);
        for (int t = 0; t < 50; t++)
        {
            sim.Tick();
            var s = sim.Water.Stats;
            Assert.Equal(initial + s.SourceAdded - s.Drained - s.Evaporated - s.Pumped, sim.Water.TotalVolume());
        }
    }

    [Fact]
    public void ManualStrength_KeptUntilNextSeason() // ADR-049
    {
        var sim = Flat();
        sim.Water.SourceStrength = 30;
        sim.RunTicks(10);
        Assert.Equal(30, sim.Water.SourceStrength);
        sim.Clock.Tick = 5 * Day;
        sim.Tick();
        Assert.Equal(0, sim.Water.SourceStrength);
    }

    [Fact]
    public void SaveLoad_MidDrought_MatchesContinuousRun() // SAV-03, ECO-17
    {
        var sim = Flat();
        sim.Clock.Tick = 6 * Day;
        sim.Water.SourceStrength = 0;                  // as the step at 5 days would have set it
        sim.RunTicks(10);

        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(0, loaded.Water.SourceStrength);
        Assert.Equal(sim.StateHash(), loaded.StateHash());

        int toWet = WeatherSystem.TicksUntilChange(sim.Clock.Tick) + 20;
        sim.RunTicks(toWet);
        loaded.RunTicks(toWet);
        Assert.Equal(100, loaded.Water.SourceStrength);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
    }
}
