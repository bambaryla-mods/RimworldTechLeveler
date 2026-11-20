using HarmonyLib;
using RimWorld;
using Verse;

namespace TechLeveler;

[StaticConstructorOnStartup]
public static class TechLevelerMod
{
	static TechLevelerMod()
	{
		var harmony = new Harmony("bambaryla.techleveler");
		harmony.PatchAll();
		YartCompat.Init(harmony);
		AddResearchBenchTab();
	}

	// Done in code so benches from other mods get the tab too.
	static void AddResearchBenchTab()
	{
		var tab = InspectTabManager.GetSharedInstance(typeof(ITab_TechLeveler));
		foreach (var def in DefDatabase<ThingDef>.AllDefs)
		{
			if (def.thingClass == null || !typeof(Building_ResearchBench).IsAssignableFrom(def.thingClass)) continue;
			def.inspectorTabs ??= new List<Type>();
			if (!def.inspectorTabs.Contains(typeof(ITab_TechLeveler))) def.inspectorTabs.Add(typeof(ITab_TechLeveler));
			def.inspectorTabsResolved ??= new List<InspectTabBase>();
			if (!def.inspectorTabsResolved.Contains(tab)) def.inspectorTabsResolved.Add(tab);
		}
	}
}

// Replaces vanilla's cost factor (1 + 0.5 per level short, with the project's level capped at Industrial)
// with the same formula over the mod's full tier ladder, so Spacer, Ultra and the virtual tiers cost more too:
//   factor = 1 + max(0, projectTierIndex - playerTierIndex) * CostPerStep
// Below Industrial this gives vanilla's numbers (the ladder steps match the vanilla levels one to one).
[HarmonyPatch(typeof(ResearchProjectDef), nameof(ResearchProjectDef.CostFactor))]
public static class Patch_ResearchProjectDef_CostFactor
{
	public static void Postfix(ResearchProjectDef __instance, TechLevel researcherTechLevel, ref float __result)
	{
		var player = GameComponent_TechTier.Instance?.current;
		if (player == null) return;
		var faction = Faction.OfPlayerSilentFail;
		if (faction == null || researcherTechLevel != faction.def.techLevel) return;

		var proj = TechTierDatabase.TierOf(__instance);
		if (proj == null) return;
		int steps = TechTierDatabase.IndexOf(proj) - TechTierDatabase.IndexOf(player);
		__result = 1f + Math.Max(0, steps) * TechTierDatabase.CostPerStep;
			if (steps == 0 && GameComponent_TechTier.Instance.proto)
				__result += TechTierDatabase.ProtoMalus(player);
	}
}

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
public static class Patch_ResearchManager_FinishProject
{
	public static void Postfix()
	{
		TechTierDatabase.InvalidateProgress();
		GameComponent_TechTier.Instance?.Evaluate();
	}
}
