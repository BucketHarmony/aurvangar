using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.ViewCore.Hud;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M11-T6, VIEW-28: the workshop panel model. Flat stone to y = 4, the hall at (20,5,20) with no logs, a
/// complete sawmill at (10,5,10).</summary>
[Trait("Category", "Unit")]
public class WorkshopPanelTests
{
    private const int G = 5;

    private static (Simulation Sim, Building Mill, Building Hall) World()
    {
        var sim = new ScenarioBuilder().Ground(G - 1).Hub(new Int3(20, G, 20)).Build();
        var mill = sim.Buildings.PlacePrebuilt(sim.Content.Building("sawmill"), new Int3(10, G, 10), 0);
        return (sim, mill, sim.Buildings.All.First(b => b.Def.Id == "hub"));
    }

    /// <summary>Sends the command (it must not be null) and returns the rejections.</summary>
    private static List<CommandRejected> Send(Simulation sim, SetWorkshopOrder? command)
    {
        Assert.NotNull(command);
        sim.Events.Drain();
        sim.Enqueue(command!);
        sim.Tick();
        return sim.Events.Drain().OfType<CommandRejected>().ToList();
    }

    [Fact]
    public void Panel_ShowsStatusAndRecipes_SendsOrders()
    {
        var (sim, mill, hall) = World();
        var model = new WorkshopPanelModel();
        Assert.False(model.Open(hall));   // only workshops open the panel
        Assert.False(model.IsOpen);
        Assert.True(model.Open(mill));

        var panel = model.Build(sim)!;
        Assert.Equal("Sawmill", panel.Title);
        Assert.Equal("Sawmill: no orders", panel.StatusText);
        var row = Assert.Single(panel.Rows);
        Assert.Equal("Saw planks: 1 log -> 2 planks", row.Text);
        Assert.Equal((OrderMode.Make, false, 0, (string?)null), (row.Mode, row.HasOrder, row.Count, row.Progress));
        Assert.Equal("no order", row.ProgressText);

        // Nothing to lower, and Keep on a row with no order only picks the mode.
        Assert.Null(model.StepCount(sim, 0, -1));
        Assert.Null(model.Clear(sim, 0));
        Assert.Null(model.SetMode(sim, 0, OrderMode.Keep));
        Assert.Equal(OrderMode.Keep, model.Build(sim)!.Rows[0].Mode);
        Assert.Null(model.SetMode(sim, 0, OrderMode.Make));

        // + with Shift creates a Make 5 order: one command, applied by the sim.
        var make = model.StepCount(sim, 0, WorkshopPanelModel.StepSize(shift: true));
        Assert.Equal(new SetWorkshopOrder(mill.Id, 0, OrderMode.Make, 5), make);
        Assert.Empty(Send(sim, make));
        Assert.Equal((0, OrderMode.Make, 5, 0), mill.Orders.Select(o => (o.Recipe, o.Mode, o.Count, o.Done)).Single());
        row = model.Build(sim)!.Rows[0];
        Assert.Equal((OrderMode.Make, true, 5, "0/5"), (row.Mode, row.HasOrder, row.Count, row.Progress));
        Assert.Equal("done 0/5", row.ProgressText);
        Assert.Equal("Sawmill: needs log", model.Build(sim)!.StatusText);   // CRF-13 NoInput names the item
        Assert.Equal(WorkshopStatus.NoInput, model.Build(sim)!.Status);

        mill.Orders[0].Done = 2;
        Assert.Equal("2/5", model.Build(sim)!.Rows[0].Progress);

        // + without Shift, then the mode toggle re-sends the order with the same count.
        Assert.Empty(Send(sim, model.StepCount(sim, 0, WorkshopPanelModel.StepSize(shift: false))));
        Assert.Equal(6, mill.Orders.Single().Count);
        Assert.Null(model.SetMode(sim, 0, OrderMode.Make));   // already Make
        var keep = model.SetMode(sim, 0, OrderMode.Keep);
        Assert.Equal(new SetWorkshopOrder(mill.Id, 0, OrderMode.Keep, 6), keep);
        Assert.Empty(Send(sim, keep));
        row = model.Build(sim)!.Rows[0];
        Assert.Equal((OrderMode.Keep, true, 6, (string?)null), (row.Mode, row.HasOrder, row.Count, row.Progress));
        Assert.Equal("in stock 0", row.ProgressText);   // CRF-08 stock of planks

        // - lowers; Clear sends count 0 and the sim removes the order; the row keeps its mode.
        Assert.Empty(Send(sim, model.StepCount(sim, 0, -1)));
        Assert.Equal(5, mill.Orders.Single().Count);
        var clear = model.Clear(sim, 0);
        Assert.Equal(new SetWorkshopOrder(mill.Id, 0, OrderMode.Keep, 0), clear);
        Assert.Empty(Send(sim, clear));
        Assert.Empty(mill.Orders);
        row = model.Build(sim)!.Rows[0];
        Assert.Equal((OrderMode.Keep, false, 0), (row.Mode, row.HasOrder, row.Count));
        Assert.Equal("Sawmill: no orders", model.Build(sim)!.StatusText);

        // Stepping down to 0 also removes; the count is clamped to 999.
        Assert.Empty(Send(sim, model.StepCount(sim, 0, 1)));
        Assert.Equal(OrderMode.Keep, mill.Orders.Single().Mode);
        Assert.Empty(Send(sim, model.StepCount(sim, 0, -5)));
        Assert.Empty(mill.Orders);
        Assert.Empty(Send(sim, model.StepCount(sim, 0, 5000)));
        Assert.Equal(WorkshopOrder.MaxCount, mill.Orders.Single().Count);
        Assert.Null(model.StepCount(sim, 0, 1));

        // A gone building closes the panel's model output.
        model.Close();
        Assert.Null(model.Build(sim));
    }

    [Fact]
    public void Panel_OpensOnABlueprint_AndStatusTexts()
    {
        var (sim, _, _) = World();
        sim.Enqueue(new PlaceBuilding("stonecutter", new Int3(4, G, 4), 0));
        sim.Tick();
        var bp = sim.Buildings.All.Single(b => b.Def.Id == "stonecutter");
        var model = new WorkshopPanelModel();
        Assert.True(model.Open(bp));
        var panel = model.Build(sim)!;
        Assert.Equal("Stonecutter: not built yet", panel.StatusText);
        Assert.Equal("Cut stone: 1 stone -> 1 cut stone", panel.Rows.Single().Text);
        Assert.Empty(Send(sim, model.StepCount(sim, 0, 5)));   // CRF-07: orders on a blueprint
        Assert.Equal(5, bp.Orders.Single().Count);

        var c = sim.Content;
        var planks = c.Item("planks");
        Assert.Equal("Sawmill: output full (planks)", WorkshopPanelModel.StatusText(c, "Sawmill", new(WorkshopStatus.OutputFull, planks)));
        Assert.Equal("Sawmill: working", WorkshopPanelModel.StatusText(c, "Sawmill", new(WorkshopStatus.Working, default)));
        Assert.Equal("Sawmill: waiting for a crafter", WorkshopPanelModel.StatusText(c, "Sawmill", new(WorkshopStatus.Waiting, default)));
        Assert.Equal("Sawmill: orders met", WorkshopPanelModel.StatusText(c, "Sawmill", new(WorkshopStatus.Done, default)));
    }
}
