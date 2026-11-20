using HarmonyLib;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace TechLeveler.UIRethemed;

// Draws the Tech tiers tab with [LFS] UI Rethemed - Core's widgets so it matches the themed inspector tabs:
// rounded window, hex title bar, lit cards and the theme's palette. Mirrors how UI Rethemed - Inspector Tabs frames its
// own tabs (vanilla window background off, themed background, title bar, border and close button drawn over it).
// This assembly is only loaded when the theme is active; ITab_TechLeveler falls back to vanilla if anything here throws.
[StaticConstructorOnStartup]
public static class UIRethemedTab
{
	const int TabWindowId = 235086; // InspectTabBase.DoTabGUI's ImmediateWindow
	const float TitleHeight = 26f;
	const float Pad = 8f;
	const float ListWidth = 170f;
	const float TierRowHeight = 30f;
	const float ResearchRowHeight = 30f;
	const float Gap = 4f;
	// Not a const or field: nothing typed from UI Rethemed may sit in a field, as this assembly's types are listed
	// while mods load, possibly before UI Rethemed is (mod order).
	static Corners WindowCorners => Corners.Top | Corners.BottomRight;

	static readonly Texture2D TabIcon = CoreWidgets.Icon("flask");
	static readonly Texture2D ReachedIcon = CoreWidgets.Icon("check-circle");
	static readonly Texture2D CurrentIcon = CoreWidgets.Icon("map-pin");
	static readonly Texture2D LockedIcon = CoreWidgets.Icon("lock");
	static readonly Texture2D MissingIcon = CoreWidgets.Icon("test-tube");
	static readonly Texture2D FinalIcon = CoreWidgets.Icon("trophy");
	static readonly Texture2D WarningIcon = CoreWidgets.Icon("warning");

	static bool drawingOurs;
	static ITab_TechLeveler shownTab;
	static int shownFrame = -1;
	static float shownSince;

	static UIRethemedTab()
	{
		try
		{
			var harmony = new Harmony("bambaryla.techleveler.uirethemed");
			harmony.Patch(AccessTools.Method(typeof(InspectTabBase), nameof(InspectTabBase.DoTabGUI)),
				prefix: new HarmonyMethod(typeof(UIRethemedTab), nameof(DoTabGUIPrefix)),
				finalizer: new HarmonyMethod(typeof(UIRethemedTab), nameof(DoTabGUIFinalizer)));
			harmony.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.ImmediateWindow)),
				prefix: new HarmonyMethod(typeof(UIRethemedTab), nameof(ImmediateWindowPrefix)));
			ITab_TechLeveler.ThemedFill = Fill;
			Log.Message("[TechLeveler] UI Rethemed detected: the Tech tiers tab uses its theme.");
		}
		catch (Exception e)
		{
			Log.Warning($"[TechLeveler] Could not hook into UI Rethemed, the Tech tiers tab keeps the vanilla look: {e}");
		}
	}

	static void DoTabGUIPrefix(InspectTabBase __instance) =>
		drawingOurs = __instance is ITab_TechLeveler && ITab_TechLeveler.ThemedFill != null;

	static void DoTabGUIFinalizer() => drawingOurs = false;

	static void ImmediateWindowPrefix(int ID, ref bool doBackground)
	{
		if (ID == TabWindowId && drawingOurs) doBackground = false;
	}

	static void Fill(ITab_TechLeveler tab, Rect rect)
	{
		var tiers = TechTierDatabase.Tiers;
		var current = GameComponent_TechTier.Instance.current;
		int currentIndex = tiers.IndexOf(current);
		var closeRect = new Rect(rect.width - 22f, 4f, 18f, 18f); // vanilla's close button sits here and takes the click
		try
		{
			Rounded.WindowBackground(rect, WindowCorners);
			TitleBar(new Rect(0f, 0f, rect.width, TitleHeight), closeRect.x - Gap, Age(tab), current);

			var body = new Rect(Pad, TitleHeight + Pad, rect.width - Pad * 2f, rect.height - TitleHeight - Pad * 2f);
			TierList(tab, new Rect(body.x, body.y, ListWidth, body.height), tiers, currentIndex);
			TierPane(tab, new Rect(body.x + ListWidth + Pad, body.y, body.width - ListWidth - Pad, body.height),
				tiers.IndexOf(tab.selected), currentIndex);
		}
		finally
		{
			Theme.CloseScrolls();
			Theme.Reset();
		}
		Rounded.Border(rect, Rounded.WindowBorderColor, WindowCorners);
		CoreWidgets.IconButton(closeRect, CoreWidgets.CloseTex, "CloseButton".Translate(), iconSize: 14f);
		Theme.Reset();
	}

	// Same title bar as the themed pawn tabs: dark top, fading hex pattern, an accent line that grows when the tab opens.
	static void TitleBar(Rect bar, float right, float age, TechTierDef current)
	{
		Rounded.Fill(bar, Theme.BGDark, Corners.Top);
		Faces.FadingHex(bar, Theme.HighlightGreen, Theme.BGDark, 1f, 0.6f);
		CoreWidgets.Accent(bar, Theme.HighlightGreen, ThemeMotion.On ? age : 1f);

		float x = bar.x + 10f;
		GUI.color = Color.white;
		CoreWidgets.DrawIcon(new Rect(x, Mathf.Round(bar.center.y - 9f), 18f, 18f), TabIcon);
		x += 26f;

		right = CoreWidgets.MiniTag(right, bar.center.y, current.LabelCap, TierColor(current), "TechLeveler_Current".Translate());
		string title = "TechLeveler_TabLabel".Translate();
		CoreWidgets.Line(new Rect(x, bar.y, Mathf.Max(0f, right - x), bar.height), title, Color.white);
	}

	static void TierList(ITab_TechLeveler tab, Rect outRect, List<TechTierDef> tiers, int currentIndex)
	{
		float height = tiers.Count * (TierRowHeight + Gap) - Gap;
		var view = Theme.BeginScroll(outRect, ref tab.listScroll, height);
		for (int i = 0; i < tiers.Count; i++)
		{
			var row = new Rect(0f, i * (TierRowHeight + Gap), view.width, TierRowHeight);
			if (TierRow(row, tiers[i], tiers[i] == tab.selected, i, currentIndex))
			{
				tab.selected = tiers[i];
				tab.scroll = Vector2.zero;
			}
		}
		Theme.EndScroll();
	}

	static bool TierRow(Rect row, TechTierDef tier, bool selected, int index, int currentIndex)
	{
		var accent = TierColor(tier);
		CoreWidgets.Card(row, selected, accent);
		if (!selected) Rounded.Fill(new Rect(row.x + 3f, row.y + 6f, 3f, row.height - 12f), Faces.Fade(accent, 0.8f));

		Texture2D icon;
		Color iconColor, textColor;
		if (index < currentIndex) (icon, iconColor, textColor) = (ReachedIcon, Theme.TextGreen, Theme.TextBright);
		else if (index == currentIndex) (icon, iconColor, textColor) = (CurrentIcon, Color.white, Color.white);
		else (icon, iconColor, textColor) = (LockedIcon, Theme.TextFaint, Theme.TextDim);
		if (selected) textColor = iconColor = Color.white;

		var iconRect = new Rect(row.xMax - 24f, Mathf.Round(row.center.y - 8f), 16f, 16f);
		GUI.color = iconColor;
		CoreWidgets.DrawIcon(iconRect, icon);
		GUI.color = Color.white;
		CoreWidgets.Line(new Rect(row.x + 12f, row.y, iconRect.x - row.x - 16f, row.height), tier.LabelCap, textColor);

		CoreWidgets.Tip(row, tier.LabelCap, ITab_TechLeveler.StatusText(index, currentIndex), accent: accent);
		return Widgets.ButtonInvisible(row);
	}

	static void TierPane(ITab_TechLeveler tab, Rect pane, int selectedIndex, int currentIndex)
	{
		var tier = tab.selected;
		var accent = TierColor(tier);

		// Header: tier name in its era colour, status tag on the right.
		var header = new Rect(pane.x, pane.y, pane.width, 32f);
		Color statusColor = selectedIndex < currentIndex ? Theme.HighlightGreen
			: selectedIndex == currentIndex ? accent : Theme.HighlightGray;
		float right = CoreWidgets.MiniTag(header.xMax, header.center.y, ITab_TechLeveler.StatusText(selectedIndex, currentIndex), statusColor);
		CoreWidgets.Line(new Rect(header.x + 2f, header.y, right - header.x - 2f, header.height), tier.LabelCap, CoreWidgets.Bright(accent), GameFont.Medium);
		Theme.Divider(pane.x, header.yMax + 2f, pane.width);

		var outRect = new Rect(pane.x, header.yMax + 8f, pane.width, pane.yMax - header.yMax - 8f);
		var view = Theme.BeginScroll(outRect, ref tab.scroll, tab.viewHeight);
		float y = 0f;

		if (!string.IsNullOrEmpty(tier.description))
		{
			CoreWidgets.Wrapped(ref y, 2f, view.width - 4f, tier.description, Theme.TextDim);
			y += 10f;
		}

		if (ITab_TechLeveler.IsFinal(tier))
			y = Note(y, view.width, FinalIcon, "TechLeveler_FinalTier".Translate(), Theme.TextYellow);
		else if (tier.locked)
			y = Note(y, view.width, WarningIcon, "TechLeveler_NoKeys".Translate(), Theme.TextYellow);
		else
			y = KeyResearch(y, view.width, tier, accent);

		tab.viewHeight = y;
		Theme.EndScroll();
	}

	static float KeyResearch(float y, float width, TechTierDef tier, Color accent)
	{
		var keys = tier.keyResolved;
		int done = keys.Count(r => r.IsFinished);
		CoreWidgets.Section(ref y, 0f, width, "TechLeveler_KeysHeader".Translate(), $"{done} / {keys.Count}");
		Theme.Bar(new Rect(2f, y + 2f, width - 4f, 6f), keys.Count == 0 ? 0f : (float)done / keys.Count,
			done == keys.Count ? Theme.HighlightGreen : accent);
		y += 14f;

		foreach (var r in keys)
		{
			ResearchRow(new Rect(0f, y, width, ResearchRowHeight), r);
			y += ResearchRowHeight + Gap;
		}
		return y;
	}

	static void ResearchRow(Rect row, ResearchProjectDef r)
	{
		bool done = r.IsFinished;
		CoreWidgets.Card(row);
		if (done) CoreWidgets.EdgeGlow(row, Theme.HighlightGreen);

		GUI.color = done ? Theme.TextGreen : Theme.TextRed;
		CoreWidgets.DrawIcon(new Rect(row.x + 8f, Mathf.Round(row.center.y - 8f), 16f, 16f), done ? ReachedIcon : MissingIcon);
		GUI.color = Color.white;

		float labelRight = row.xMax - 8f;
		if (!done)
		{
			// Progress: a short bar with the percentage beside it.
			var pctRect = new Rect(row.xMax - 48f, row.y, 40f, row.height);
			CoreWidgets.Line(pctRect, r.ProgressPercent.ToStringPercent("F0"), Theme.TextDim, GameFont.Tiny, TextAnchor.MiddleRight);
			var barRect = new Rect(pctRect.x - 84f, Mathf.Round(row.center.y - 3f), 80f, 6f);
			Theme.Bar(barRect, r.ProgressPercent, Theme.HighlightYellow);
			labelRight = barRect.x - 8f;
		}
		CoreWidgets.Line(new Rect(row.x + 32f, row.y, labelRight - row.x - 32f, row.height), r.LabelCap, done ? Theme.TextGreen : Theme.TextBright);

		CoreWidgets.Tip(row, r.LabelCap, r.description);
		if (Widgets.ButtonInvisible(row)) ResearchTabCompat.Open(r);
	}

	static float Note(float y, float width, Texture2D icon, string text, Color color)
	{
		GUI.color = color;
		CoreWidgets.DrawIcon(new Rect(2f, y + 2f, 18f, 18f), icon);
		GUI.color = Color.white;
		CoreWidgets.Wrapped(ref y, 28f, width - 30f, text, color);
		return y + 6f;
	}

	// The theme's own era palette, by the vanilla level a tier stands for.
	static Color TierColor(TechTierDef tier) => Theme.TechColor(tier.vanillaLevel);

	// Seconds since the tab was last shown, for the title accent's grow-in.
	static float Age(ITab_TechLeveler tab)
	{
		if (tab != shownTab || Time.frameCount - shownFrame > 2)
		{
			shownTab = tab;
			shownSince = ThemeMotion.Now;
		}
		shownFrame = Time.frameCount;
		return ThemeMotion.Now - shownSince;
	}
}
