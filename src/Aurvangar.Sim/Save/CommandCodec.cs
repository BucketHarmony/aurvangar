using Aurvangar.Sim.Commands;

namespace Aurvangar.Sim.Save;

/// <summary>Command log serialization (SAV-01): the tag, then the command's fields. Every command type the game can
/// enqueue must be listed here in the task that adds it; saving an unknown tag fails loudly instead of losing it.</summary>
internal static class CommandCodec
{
    public static void Write(BinaryWriter w, ICommand command)
    {
        w.Write(command.Tag);
        switch (command)
        {
            case DesignateDig c: w.Write(c.A); w.Write(c.B); break;
            case DesignateChop c: w.Write(c.X0); w.Write(c.Z0); w.Write(c.X1); w.Write(c.Z1); break;
            case CancelDesignation c: w.Write(c.A); w.Write(c.B); break;
            case DesignateDeconstructBlocks c: w.Write(c.A); w.Write(c.B); break;
            case DesignateFarm c: w.Write(c.X0); w.Write(c.Z0); w.Write(c.X1); w.Write(c.Z1); break;
            case PlaceBuilding c: w.Write(c.DefId); w.Write(c.Origin); w.Write(c.Rotation); break;
            case Deconstruct c: w.Write(c.Building.Value); break;
            case DesignateBuild c:
                w.Write((byte)c.Shape); w.Write(c.A); w.Write(c.B); w.Write(c.Height); w.Write((byte)c.Block); w.Write(c.Plan);
                w.Write((byte)c.Form.Shape); w.Write(c.Form.Rotation);   // save v7 (M11-T10): raw, so a rejected form replays as rejected
                break;
            case ReleasePlan c: w.Write(c.A); w.Write(c.B); break;
            case SetWorkshopOrder c:   // save v8 (M11-T4): the mode byte raw, so a bad mode replays as rejected
                w.Write(c.Workshop.Value); w.Write(c.Recipe); w.Write((byte)c.Mode); w.Write(c.Count);
                break;
            default:
                throw new NotSupportedException(
                    $"SaveGame: command '{command.Tag}' ({command.GetType().Name}) has no save codec; add it to CommandCodec.");
        }
    }

    public static ICommand Read(BinaryReader r)
    {
        string tag = r.ReadString();
        return tag switch
        {
            nameof(DesignateDig) => new DesignateDig(r.ReadInt3(), r.ReadInt3()),
            nameof(DesignateChop) => new DesignateChop(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32()),
            nameof(CancelDesignation) => new CancelDesignation(r.ReadInt3(), r.ReadInt3()),
            nameof(DesignateDeconstructBlocks) => new DesignateDeconstructBlocks(r.ReadInt3(), r.ReadInt3()),
            nameof(DesignateFarm) => new DesignateFarm(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32()),
            nameof(PlaceBuilding) => new PlaceBuilding(r.ReadString(), r.ReadInt3(), r.ReadInt32()),
            nameof(Deconstruct) => new Deconstruct(new Core.BuildingId(r.ReadInt32())),
            nameof(DesignateBuild) => new DesignateBuild(r.ReadEnum<Blocks.BuildShape>("build shape"), r.ReadInt3(), r.ReadInt3(),
                r.ReadInt32(), (World.BlockId)r.ReadByte(), r.ReadBoolean(),
                new World.BlockForm((World.BlockShape)r.ReadByte(), r.ReadByte())),
            nameof(ReleasePlan) => new ReleasePlan(r.ReadInt3(), r.ReadInt3()),
            nameof(SetWorkshopOrder) => new SetWorkshopOrder(new Core.BuildingId(r.ReadInt32()), r.ReadInt32(),
                (Buildings.OrderMode)r.ReadByte(), r.ReadInt32()),
            _ => throw new InvalidDataException($"Save file contains unknown command tag '{tag}'."),
        };
    }
}
