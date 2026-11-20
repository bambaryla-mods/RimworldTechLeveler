using RimWorld;
using Verse;

namespace TechLeveler;

[StaticConstructorOnStartup]
public static class TechTierDatabase
{
	// Extra cost per ladder step, approximating vanilla's per-level research penalty.
	public const float CostPerStep = 0.5f;

	public static readonly List<TechTierDef> Tiers;
	static readonly Dictionary<ResearchProjectDef, TechTierDef> tagged = new();
	static readonly Dictionary<FactionDef, TechLevel> originalLevels = new();
	static readonly Dictionary<TechLevel, TechTierDef> canonicalByLevel = new();

	static TechTierDatabase()
	{
		Tiers = DefDatabase<TechTierDef>.AllDefsListForReading.OrderBy(t => t.order).ToList();
		for (int i = 0; i < Tiers.Count; i++) Tiers[i].ladderIndex = i;

		// Several tiers can share a vanilla level. The one named after the level (TL_Ultra) is the canonical one,
		// even when virtual tiers sit before it on the ladder; otherwise fall back to the first by order.
		foreach (var group in Tiers.GroupBy(t => t.vanillaLevel))
			canonicalByLevel[group.Key] = group.FirstOrDefault(t => t.defName == "TL_" + group.Key) ?? group.First();

		foreach (var f in DefDatabase<FactionDef>.AllDefs)
			if (f.isPlayer) originalLevels[f] = f.techLevel;

		foreach (var tier in Tiers)
		{
			Resolve(tier);
			foreach (var name in tier.tierResearch)
			{
				var r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
				if (r == null) { Warn(tier, name); continue; }
				Tag(r, tier);
			}
		}

		foreach (var r in DefDatabase<ResearchProjectDef>.AllDefs)
		{
			var ext = r.GetModExtension<ResearchTierExtension>();
			if (ext?.tier != null) Tag(r, ext.tier);
		}

		Log.Message($"[TechLeveler] {Tiers.Count} tiers: " + string.Join(", ",
			Tiers.Select(t => t.defName + (t.locked ? "(locked)" : ""))));
	}

	static void Tag(ResearchProjectDef r, TechTierDef tier)
	{
		if (tagged.TryGetValue(r, out var prev) && prev != tier)
			Log.Warning($"[TechLeveler] Research {r.defName} is tagged for both {prev.defName} and {tier.defName}; using the higher tier.");
		if (prev == null || tier.order > prev.order) tagged[r] = tier;
	}

	static void Warn(TechTierDef tier, string name) =>
		Log.Warning($"[TechLeveler] {tier.defName}: research '{name}' not found, skipped.");

	static void Resolve(TechTierDef tier)
	{
		foreach (var name in tier.keyResearch)
		{
			var r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
			if (r == null) Warn(tier, name); else tier.keyResolved.Add(r);
		}
		bool isLast = Tiers.Count > 0 && Tiers[Tiers.Count - 1] == tier;
		tier.locked = !isLast && tier.keyResolved.Count == 0;
	}

	public static void RestoreOriginalLevels()
	{
		foreach (var kv in originalLevels) kv.Key.techLevel = kv.Value;
	}

	public static TechLevel OriginalLevel(FactionDef def) =>
		originalLevels.TryGetValue(def, out var l) ? l : def.techLevel;

	public static TechTierDef TierOf(ResearchProjectDef r)
	{
		if (tagged.TryGetValue(r, out var t)) return t;
		return ForVanillaLevel(r.techLevel);
	}

	public static TechTierDef ForVanillaLevel(TechLevel level) =>
		canonicalByLevel.TryGetValue(level, out var t) ? t : null;

	public static int IndexOf(TechTierDef t) => t.ladderIndex;

	// Share of a tier's projects that must be finished to stop being "proto" in it, and how much of the
	// per-step penalty a fresh arrival still pays on own-tier projects.
	public const float ProtoShare = 0.2f;
	public const float ProtoPenaltyFactor = 0.5f;

	static readonly Dictionary<TechTierDef, int> doneCache = new();

	public static void InvalidateProgress() => doneCache.Clear();

	// Only the tier's key research counts towards ending the proto malus.
	public static int ProtoNeeded(TechTierDef tier) =>
		(int)Math.Ceiling(tier.keyResolved.Count * ProtoShare);

	public static int ProtoDone(TechTierDef tier)
	{
		if (!doneCache.TryGetValue(tier, out var n))
			doneCache[tier] = n = tier.keyResolved.Count(r => r.IsFinished);
		return n;
	}

	// Extra cost factor (above 1) the faction pays on projects of its own tier while proto in it.
	public static float ProtoMalus(TechTierDef tier)
	{
		int needed = ProtoNeeded(tier);
		if (needed <= 0) return 0f;
		float left = 1f - Math.Min(1f, ProtoDone(tier) / (float)needed);
		return ProtoPenaltyFactor * CostPerStep * left;
	}
}
