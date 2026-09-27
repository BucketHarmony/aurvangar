using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Content;

/// <summary>CRF-04 (M11-T4): a recipe resolved against the items, with its workshop and its index in the workshop's
/// recipe list (the "recipe index" orders and Craft steps use). One cycle takes <see cref="InputCount"/> of
/// <see cref="Input"/> and makes <see cref="OutputCount"/> of <see cref="Output"/> in <see cref="WorkTicks"/>.</summary>
public sealed record Recipe(BuildingDef Workshop, int Index, RecipeDef Def, ItemId Input, int InputCount, ItemId Output,
    int OutputCount)
{
    public string Id => Def.Id;
    public string Name => Def.Name;
    public int WorkTicks => Def.WorkTicks;
}
