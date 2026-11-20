using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TechLeveler;

// YART (Yet Another Research Tree) groups research into era bands by vanilla TechLevel. This makes it use the
// mod's tier ladder instead: every research gets a pseudo TechLevel of PseudoBase + tier.order, which sorts
// correctly against other pseudo levels, gets its own band, colour and label. Real levels stay below PseudoBase.
// YART is reached by reflection only (no hard dependency); everything is skipped when it is not loaded.
public static class YartCompat
{
	const int PseudoBase = 100;
	const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	static PropertyInfo nodeDef;
	static MethodInfo originalEra;
	static Func<TechLevel, Color> vanillaColor;

	// How far each tier fades towards the next level's colour. The colours themselves stay live: YART owns them.
	static readonly Dictionary<TechTierDef, float> eraFades = new();

	public static void Init(Harmony harmony)
	{
		var node = AccessTools.TypeByName("YART.Data.ResearchNode");
		var colors = AccessTools.TypeByName("YART.Data.ActiveColors");
		var window = AccessTools.TypeByName("YART.MainTabWindow_YART");
		if (node == null || colors == null || window == null) return;

		try
		{
			nodeDef = node.GetProperty("Def", All);
			originalEra = colors.GetMethod("Era", All);
			vanillaColor = AccessTools.MethodDelegate<Func<TechLevel, Color>>(originalEra);
			harmony.Patch(node.GetProperty("TechLevel", All).GetGetMethod(), postfix: new HarmonyMethod(typeof(YartCompat), nameof(NodeTechLevel)));
			harmony.Patch(originalEra, prefix: new HarmonyMethod(typeof(YartCompat), nameof(EraColor)));
			harmony.Patch(window.GetMethod("TechLevelLabel", All), prefix: new HarmonyMethod(typeof(YartCompat), nameof(EraLabel)));
			harmony.Patch(window.GetMethod("CostFactorWarningText", All), prefix: new HarmonyMethod(typeof(YartCompat), nameof(CostWarning)));
			Log.Message("[TechLeveler] YART detected: research tree bands follow Tech Leveler tiers.");
		}
		catch (Exception e)
		{
			Log.Warning($"[TechLeveler] Could not patch YART, its bands stay vanilla: {e}");
		}
	}

	static bool IsPseudo(TechLevel level) => (int)level >= PseudoBase;

	static TechTierDef TierOfPseudo(TechLevel level)
	{
		int order = (int)level - PseudoBase;
		foreach (var t in TechTierDatabase.Tiers)
			if (t.order == order) return t;
		return null;
	}

	// ResearchNode.TechLevel getter. Proxy and dummy nodes copy an already-remapped level, so only real levels are touched.
	static void NodeTechLevel(object __instance, ref TechLevel __result)
	{
		if (IsPseudo(__result) || __result == TechLevel.Undefined) return;
		if (nodeDef.GetValue(__instance) is not ResearchProjectDef def) return;
		var tier = TechTierDatabase.TierOf(def);
		if (tier != null) __result = (TechLevel)(PseudoBase + tier.order);
	}

	// YART's "your faction is X-level, the research is Y-level" note: use tier names instead of vanilla level names.
	static bool CostWarning(ResearchProjectDef def, ref string __result)
	{
		var player = GameComponent_TechTier.Instance?.current;
		var proj = TechTierDatabase.TierOf(def);
		if (player == null || proj == null || def.IsFinished) return true;
		float costFactor = def.CostFactor(Faction.OfPlayer.def.techLevel);
		if (costFactor == 1f) { __result = null; return false; }

		string head = proj == player && GameComponent_TechTier.Instance.proto
			? "TechLeveler_ProtoNote".Translate(proj.LabelCap).ToString()
			: "TechLevelTooLow".Translate(player.LabelCap, proj.LabelCap, (1f / costFactor).ToStringPercent()).ToString();
		__result = head + " " + "ResearchCostComparison".Translate(def.Cost.ToString("F0"), def.CostApparent.ToString("F0"));
		return false;
	}

	static bool EraLabel(TechLevel tl, ref string __result)
	{
		if (!IsPseudo(tl)) return true;
		__result = TierOfPseudo(tl)?.LabelCap.ToString() ?? tl.ToString();
		return false;
	}

	// Tiers that share a vanilla level (e.g. Ultra and its virtual successors) fade from that level's colour
	// towards the next real level's colour, so neighbouring bands stay distinguishable.
	static bool EraColor(TechLevel tl, ref Color __result)
	{
		if (!IsPseudo(tl)) return true;
		var tier = TierOfPseudo(tl);
		if (tier == null) { __result = Color.gray; return false; }

		if (!eraFades.TryGetValue(tier, out float fade))
		{
			var same = TechTierDatabase.Tiers.Where(t => t.vanillaLevel == tier.vanillaLevel).ToList();
			fade = (float)same.IndexOf(tier) / same.Count;
			eraFades[tier] = fade;
		}
		var next = (TechLevel)Math.Min((int)tier.vanillaLevel + 1, (int)TechLevel.Archotech);
		__result = Color.Lerp(vanillaColor(tier.vanillaLevel), vanillaColor(next), fade);
		return false;
	}
}
