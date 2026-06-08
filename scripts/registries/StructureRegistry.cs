using System.Collections.Generic;

public class StructureRegistry
{
	private static readonly Dictionary<string, StructureDefinition> Structures = new();

	public static void Register(StructureDefinition def)
	{
		def.StableId = DeterministicHash.String32(def.Id);
		Structures[def.Id] = def;
	}

	public static StructureDefinition Get(string id)
	{
		return Structures[id];
	}
}