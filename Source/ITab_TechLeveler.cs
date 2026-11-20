using RimWorld;
using UnityEngine;
using Verse;

namespace TechLeveler;

// Inspector tab on research benches: pick a tier, see which of its key research is done and what is missing.
public class ITab_TechLeveler : ITab
{
	const float ListWidth = 150f;
	const float RowHeight = 28f;
	const float LineHeight = 24f;

	static readonly Color DoneColor = new(0.6f, 0.9f, 0.6f);
	static readonly Color MissingColor = new(1f, 0.4f, 0.4f);

	// Set by the optional UI Rethemed compat assembly (Compat/UIRethemed) to draw the whole tab in that theme.
	// Draws the vanilla look while unset; a themed draw that throws is dropped for good and the vanilla look returns.
	public static Action<ITab_TechLeveler, Rect> ThemedFill;

	// State shared with the themed draw.
	public TechTierDef selected;
	public Vector2 scroll;
	public Vector2 listScroll;
	public float viewHeight;
	public Vector2 Size => size;

	public ITab_TechLeveler()
	{
		size = new Vector2(640f, 420f);
		labelKey = "TechLeveler_TabLabel";
	}

	protected override void FillTab()
	{
		var tiers = TechTierDatabase.Tiers;
		var current = GameComponent_TechTier.Instance?.current;
		if (tiers.Count == 0 || current == null) return;
		if (selected == null || !tiers.Contains(selected)) selected = current;
		int currentIndex = tiers.IndexOf(current);

		if (ThemedFill != null)
		{
			try
			{
				ThemedFill(this, new Rect(0f, 0f, size.x, size.y));
				return;
			}
			catch (Exception e)
			{
				ThemedFill = null;
				Log.Warning($"[TechLeveler] The UI Rethemed look for the Tech tiers tab failed, using the vanilla look: {e}");
			}
		}

		var inRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
		DrawTierList(inRect, tiers, current, currentIndex);
		DrawTierPane(new Rect(inRect.x + ListWidth + 10f, inRect.y, inRect.width - ListWidth - 10f, inRect.height),
			tiers.IndexOf(selected), currentIndex);
	}

	public static string StatusText(int index, int currentIndex) =>
		index < currentIndex ? "TechLeveler_Reached".Translate()
		: index == currentIndex ? "TechLeveler_Current".Translate()
		: "TechLeveler_NotReached".Translate();

	public static bool IsFinal(TechTierDef tier) => TechTierDatabase.Tiers[TechTierDatabase.Tiers.Count - 1] == tier;

	void DrawTierList(Rect inRect, List<TechTierDef> tiers, TechTierDef current, int currentIndex)
	{
		for (int i = 0; i < tiers.Count; i++)
		{
			var tier = tiers[i];
			var row = new Rect(inRect.x, inRect.y + i * (RowHeight + 2f), ListWidth, RowHeight);
			if (tier == selected) Widgets.DrawHighlightSelected(row);
			GUI.color = i < currentIndex ? DoneColor : i == currentIndex ? Color.white : Color.gray;
			if (Widgets.ButtonText(row, tier.LabelCap, drawBackground: tier != selected)) selected = tier;
			GUI.color = Color.white;
		}
	}

	void DrawTierPane(Rect pane, int selectedIndex, int currentIndex)
	{
		var tier = selected;
		Text.Font = GameFont.Medium;
		Widgets.Label(new Rect(pane.x, pane.y, pane.width, 32f), tier.LabelCap);
		Text.Font = GameFont.Small;

		Widgets.Label(new Rect(pane.x, pane.y + 32f, pane.width, 24f), StatusText(selectedIndex, currentIndex));

		var outRect = new Rect(pane.x, pane.y + 60f, pane.width, pane.height - 60f);
		var viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
		Widgets.BeginScrollView(outRect, ref scroll, viewRect);
		float y = 0f;

		if (!string.IsNullOrEmpty(tier.description))
		{
			float h = Text.CalcHeight(tier.description, viewRect.width);
			Widgets.Label(new Rect(0f, y, viewRect.width, h), tier.description);
			y += h + 8f;
		}

		if (IsFinal(tier))
			y = DrawNote(y, viewRect.width, "TechLeveler_FinalTier".Translate());
		else if (tier.locked)
			y = DrawNote(y, viewRect.width, "TechLeveler_NoKeys".Translate());
		else
		{
			y = DrawNote(y, viewRect.width, "TechLeveler_KeysHeader".Translate());
			foreach (var r in tier.keyResolved)
				y = DrawResearch(y, viewRect.width, r);
		}

		viewHeight = y;
		Widgets.EndScrollView();
	}

	static float DrawNote(float y, float width, string text)
	{
		float h = Text.CalcHeight(text, width);
		Widgets.Label(new Rect(0f, y, width, h), text);
		return y + h + 2f;
	}

	static float DrawResearch(float y, float width, ResearchProjectDef r)
	{
		var rect = new Rect(0f, y, width, LineHeight);
		bool done = r.IsFinished;
		GUI.color = done ? DoneColor : MissingColor;
		string label = done ? r.LabelCap.ToString() : $"{r.LabelCap} ({r.ProgressPercent.ToStringPercent("F0")})";
		Widgets.Label(new Rect(rect.x + 12f, rect.y, rect.width - 12f, rect.height), label);
		GUI.color = Color.white;
		if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
		TooltipHandler.TipRegion(rect, r.description);
		if (Widgets.ButtonInvisible(rect)) ResearchTabCompat.Open(r);
		return y + LineHeight;
	}
}
