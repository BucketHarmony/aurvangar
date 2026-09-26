using Colony.Sim.Core;

namespace Colony.Sim.Items;

/// <summary>A count of one item type. Empty when Item is invalid or Count is 0.</summary>
public readonly record struct ItemStack(ItemId Item, int Count)
{
    public static readonly ItemStack Empty = new(default, 0);
    public bool IsEmpty => !Item.IsValid || Count <= 0;
}
