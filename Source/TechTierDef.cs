using RimWorld;
using Verse;

namespace TechLeveler;

public class ResearchTierExtension : DefModExtension
{
	public TechTierDef tier;
}

public class TechTierDef : Def
{
	public int order;
	public TechLevel vanillaLevel = TechLevel.Undefined;
	public List<string> keyResearch = new();
	public List<string> tierResearch = new();

	// Resolved at startup by TechTierDatabase.
	[Unsaved] public List<ResearchProjectDef> keyResolved = new();
	[Unsaved] public bool locked;
	[Unsaved] public int ladderIndex;

	public bool IsSatisfied()
	{
		if (locked) return false;
		foreach (var r in keyResolved)
			if (!r.IsFinished) return false;
		return true;
	}

	public IEnumerable<ResearchProjectDef> AllResolved()
	{
		foreach (var r in keyResolved) yield return r;
	}
}
