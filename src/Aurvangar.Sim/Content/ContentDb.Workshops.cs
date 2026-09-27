
namespace Aurvangar.Sim.Content;

/// <summary>CRF-03/04 (M11-T4, ADR-082): workshop validation and the resolved recipes.</summary>
public sealed partial class ContentDb
{
    private readonly Dictionary<string, Recipe> _recipesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Recipe[]> _recipesByBuilding = new(StringComparer.Ordinal);

    /// <summary>CRF-04: the recipe with this id, with its workshop and recipe index.</summary>
    public Recipe Recipe(string id) =>
        _recipesById.TryGetValue(id, out var r) ? r : throw new KeyNotFoundException($"buildings.json: unknown recipe '{id}'");

    /// <summary>The recipes of a building in recipe-index order (empty for a building with no workshop block).</summary>
    public IReadOnlyList<Recipe> RecipesOf(BuildingDef def) =>
        _recipesByBuilding.TryGetValue(def.Id, out var list) ? list : Array.Empty<Recipe>();

    private void IndexRecipes()
    {
        foreach (var b in Buildings)
        {
            if (b.Workshop is null) continue;
            var list = new Recipe[b.Workshop.Recipes.Length];
            for (int i = 0; i < list.Length; i++)
            {
                var d = b.Workshop.Recipes[i];
                var (inKey, inN) = d.Input.Single();
                var (outKey, outN) = d.Output.Single();
                list[i] = new Recipe(b, i, d, _itemsByKey[inKey], inN, _itemsByKey[outKey], outN);
                _recipesById[d.Id] = list[i];
            }
            _recipesByBuilding[b.Id] = list;
        }
    }

    /// <summary>CRF-04. Each error names the file, the building and (for a recipe error) the recipe.</summary>
    private void ValidateWorkshops()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);   // lookups only
        foreach (var b in Buildings)
        {
            if (b.Workshop is not { } w) continue;
            string who = $"buildings.json: workshop '{b.Id}'";
            if (b.Workers != 1) throw new InvalidDataException($"{who} must have exactly 1 worker");
            if (b.Entrance is null) throw new InvalidDataException($"{who} needs an entrance");
            if (b.Storage is not null) throw new InvalidDataException($"{who} must not have a storage");
            if (b.Producer is not null) throw new InvalidDataException($"{who} must not have a producer");
            if (b.Placement == "waterEdge") throw new InvalidDataException($"{who} must not use waterEdge placement");
            if (w.Recipes is null || w.Recipes.Length == 0) throw new InvalidDataException($"{who} has no recipes");
            if (w.OutputBuffer < 1) throw new InvalidDataException($"{who} outputBuffer must be at least 1");
            if (w.HaulAt < 1 || w.HaulAt > w.OutputBuffer)
                throw new InvalidDataException($"{who} haulAt must be 1..outputBuffer ({w.OutputBuffer})");
            foreach (var r in w.Recipes)
            {
                string rwho = $"{who} recipe '{r.Id}'";
                if (string.IsNullOrWhiteSpace(r.Id)) throw new InvalidDataException($"{who} has a recipe with no id");
                if (!ids.Add(r.Id)) throw new InvalidDataException($"{rwho}: the recipe id is used twice");
                if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidDataException($"{rwho} needs a name");
                CheckOne(rwho, "input", r.Input, Agents.Agent.CarryCapacity);
                CheckOne(rwho, "output", r.Output, w.OutputBuffer);
                if (r.WorkTicks < 1) throw new InvalidDataException($"{rwho} needs workTicks >= 1");
            }
        }
    }

    private void CheckOne(string who, string what, Dictionary<string, int>? items, int max)
    {
        if (items is null || items.Count != 1) throw new InvalidDataException($"{who} must have exactly one {what} item");
        foreach (var (key, n) in items)
        {
            if (!_itemsByKey.ContainsKey(key)) throw new InvalidDataException($"{who} has unknown {what} item '{key}'");
            if (n < 1 || n > max) throw new InvalidDataException($"{who} {what} count {n} must be 1..{max}");
        }
    }
}
