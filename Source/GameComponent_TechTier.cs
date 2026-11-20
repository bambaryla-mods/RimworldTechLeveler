using RimWorld;
using Verse;

namespace TechLeveler;

public class GameComponent_TechTier : GameComponent
{
	public TechTierDef current;

	public GameComponent_TechTier(Game game) { }

	public static GameComponent_TechTier Instance =>
		Current.ProgramState == ProgramState.Playing ? Current.Game?.GetComponent<GameComponent_TechTier>() : null;

	// True after arriving in `current` by progression; cleared by the starting tier and debug set-tier.
	public bool proto;

	public override void ExposeData()
	{
		Scribe_Defs.Look(ref current, "currentTier");
		Scribe_Values.Look(ref proto, "proto");
	}

	public override void FinalizeInit()
	{
		TechTierDatabase.InvalidateProgress();
		TechTierDatabase.RestoreOriginalLevels();
		var tiers = TechTierDatabase.Tiers;
		if (tiers.Count == 0) return;
		var faction = Faction.OfPlayerSilentFail;
		if (faction == null) return;

		if (current == null || !tiers.Contains(current))
		{
			var start = TechTierDatabase.OriginalLevel(faction.def);
			current = TechTierDatabase.ForVanillaLevel(start)
				?? tiers.LastOrDefault(t => t.vanillaLevel <= start)
				?? tiers[0];
			proto = false;
		}
		Apply(current);
		Evaluate();
	}

	public override void GameComponentUpdate() => ResearchTabCompat.Update();

	void Apply(TechTierDef tier)
	{
		var def = Faction.OfPlayerSilentFail?.def;
		if (def == null) return;
		var orig = TechTierDatabase.OriginalLevel(def);
		def.techLevel = (TechLevel)Math.Max((int)orig, (int)tier.vanillaLevel);
	}

	public void Evaluate()
	{
		var tiers = TechTierDatabase.Tiers;
		if (proto && current != null && TechTierDatabase.ProtoMalus(current) <= 0f)
		{
			proto = false;
			Find.LetterStack.ReceiveLetter("TechLeveler_ProtoDoneLabel".Translate(current.LabelCap),
				"TechLeveler_ProtoDoneText".Translate(current.LabelCap), LetterDefOf.PositiveEvent);
		}
		var left = new List<TechTierDef>();
		while (true)
		{
			int i = tiers.IndexOf(current);
			if (i < 0 || i + 1 >= tiers.Count) break;
			if (!current.IsSatisfied()) break;
			left.Add(current);
			current = tiers[i + 1];
		}
		if (left.Count == 0) return;
		proto = TechTierDatabase.ProtoMalus(current) > 0f;
		Apply(current);
		SendLetter(left);
	}

	public void SetTier(TechTierDef tier)
	{
		current = tier;
		proto = false;
		TechTierDatabase.RestoreOriginalLevels();
		Apply(tier);
	}

	void SendLetter(List<TechTierDef> left)
	{
		var names = left.SelectMany(t => t.AllResolved()).Distinct().Select(r => r.LabelCap.ToString());
		string text = "TechLeveler_LetterText".Translate(current.LabelCap,
			"TechLeveler_LetterUnlockedBy".Translate(string.Join(", ", names)));
		int needed = TechTierDatabase.ProtoNeeded(current);
		float malus = TechTierDatabase.ProtoMalus(current);
		if (needed > 0 && malus > 0f)
			text += "\n\n" + "TechLeveler_LetterProto".Translate(current.LabelCap, malus.ToStringPercent(), needed);
		Find.LetterStack.ReceiveLetter("TechLeveler_LetterLabel".Translate(current.LabelCap), text, LetterDefOf.PositiveEvent);
	}
}
