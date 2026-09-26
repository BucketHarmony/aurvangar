using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;

namespace Aurvangar.Sim.Water;

public enum Season : byte { Wet = 0, Drought = 1 }

/// <summary>ECO-17 drought schedule, ARCH-01 step 2. The season is a pure function of the tick
/// (<c>Wet(5 days) → Drought(2 days) → Wet …</c>, Wet from tick 0), so the weather has no state of its own; the
/// source strength it sets lives in <see cref="WaterGrid.SourceStrength"/>, which is saved and hashed (ADR-049).
/// The strength is written only on the tick a season begins (never at tick 0, where the default 100 is the Wet
/// value), so a scenario or test that sets the strength by hand keeps it until the next change.</summary>
public static class WeatherSystem
{
    public const int WetDays = 5;
    public const int DroughtDays = 2;
    public const int WetTicks = WetDays * SimClock.TicksPerDay;
    public const int DroughtTicks = DroughtDays * SimClock.TicksPerDay;
    public const int CycleTicks = WetTicks + DroughtTicks;
    public const int WetStrength = 100;
    public const int DroughtStrength = 0;

    public static Season SeasonAt(long tick) => tick % CycleTicks < WetTicks ? Season.Wet : Season.Drought;

    /// <summary>Ticks from <paramref name="tick"/> until the next season begins (1..season length).</summary>
    public static int TicksUntilChange(long tick)
    {
        int inCycle = (int)(tick % CycleTicks);
        return inCycle < WetTicks ? WetTicks - inCycle : CycleTicks - inCycle;
    }

    /// <summary>ECO-18 readout: whole days until the season changes, rounded up (1 on the change's last day).</summary>
    public static int DaysUntilChange(long tick) => (TicksUntilChange(tick) + SimClock.TicksPerDay - 1) / SimClock.TicksPerDay;

    public static int StrengthOf(Season season) => season == Season.Wet ? WetStrength : DroughtStrength;

    /// <summary>ARCH-01 step 2: on the first tick of a season, set the source strength and emit
    /// <see cref="SeasonChanged"/>.</summary>
    public static void Tick(Simulation sim)
    {
        long tick = sim.Clock.Tick;
        if (tick == 0) return;
        int inCycle = (int)(tick % CycleTicks);
        if (inCycle != 0 && inCycle != WetTicks) return;
        var season = SeasonAt(tick);
        sim.Water.SourceStrength = StrengthOf(season);
        sim.Events.Emit(new SeasonChanged(season));
    }
}
