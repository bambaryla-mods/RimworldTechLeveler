using LudeonTK;
using RimWorld;
using Verse;

namespace TechLeveler;

public static class DebugActions_TechTier
{
	[DebugAction("TechLeveler", "Log tier status", allowedGameStates = AllowedGameStates.Playing)]
	static void LogStatus()
	{
		var comp = GameComponent_TechTier.Instance;
		Log.Message($"[TechLeveler] current={comp?.current?.defName} vanilla={Faction.OfPlayer.def.techLevel}");
		foreach (var t in TechTierDatabase.Tiers)
			Log.Message($"  {t.defName} order={t.order} locked={t.locked} satisfied={t.IsSatisfied()}");
	}

	[DebugAction("TechLeveler", "Log player tech level", allowedGameStates = AllowedGameStates.Playing)]
	static void LogPlayerTechLevel()
	{
		var def = Faction.OfPlayer.def;
		Log.Message($"[TechLeveler] Player faction '{def.defName}': techLevel={def.techLevel}, " +
			$"original={TechTierDatabase.OriginalLevel(def)}, tier={GameComponent_TechTier.Instance?.current?.defName}");
	}

	[DebugAction("TechLeveler", "Set tier...",allowedGameStates = AllowedGameStates.Playing)]
	static void SetTier()
	{
		var options = TechTierDatabase.Tiers.Select(t =>
			new DebugMenuOption(t.defName, DebugMenuOptionMode.Action,
				() => GameComponent_TechTier.Instance?.SetTier(t))).ToList();
		Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
	}
}
