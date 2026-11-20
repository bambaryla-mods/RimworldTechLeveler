using System.Collections;
using System.Reflection;
using RimWorld;
using Verse;

namespace TechLeveler;

// Opens the research window on a project and selects it. Vanilla, Fluffy's ResearchTree and Nice Research Tab
// replace the window class; the latter two are reached by reflection (no hard dependency on their assemblies).
// Nice Research Tab builds its tree widget and nodes after the tab opens, so a failed attempt is retried every
// frame (see Update) until it works or the tab is closed.
public static class ResearchTabCompat
{
	const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
	const int MaxPendingFrames = 600;

	static ResearchProjectDef pending;
	static int pendingFrames;

	public static void Open(ResearchProjectDef r)
	{
		// Fluffy's ResearchTree swaps the Research button for its own MainButtonDef inside ToggleTab, so the
		// window to use is whatever tab is open afterwards, not MainButtonDefOf.Research.TabWindow.
		// Skip the toggle when a research window is already open, otherwise it would close it.
		if (!IsResearchTab(Find.MainTabsRoot.OpenTab)) Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
		pending = null;
		var result = TrySelect(r);
		if (result == Result.Retry) { pending = r; pendingFrames = 0; }
		else if (result == Result.Unsupported) Find.WindowStack.Add(new Dialog_InfoCard(r));
	}

	// Called every frame by the game component.
	public static void Update()
	{
		if (pending == null) return;
		if (!IsResearchTab(Find.MainTabsRoot.OpenTab) || ++pendingFrames > MaxPendingFrames)
		{
			if (IsResearchTab(Find.MainTabsRoot.OpenTab))
				Log.Warning($"[TechLeveler] Gave up selecting {pending.defName} in {Find.MainTabsRoot.OpenTab.TabWindow?.GetType().FullName}");
			pending = null;
			return;
		}
		var result = TrySelect(pending);
		if (result == Result.Retry) return;
		if (result == Result.Unsupported) Find.WindowStack.Add(new Dialog_InfoCard(pending));
		pending = null;
	}

	enum Result { Done, Retry, Unsupported }

	static bool IsResearchTab(MainButtonDef def)
	{
		if (def == null) return false;
		if (def == MainButtonDefOf.Research) return true;
		return def.tabWindowClass?.FullName is "FluffyResearchTree.MainTabWindow_ResearchTree" or "NiceResearchTab.MainTabWindow_ResearchTree";
	}

	static Result TrySelect(ResearchProjectDef r)
	{
		var window = (IsResearchTab(Find.MainTabsRoot.OpenTab) ? Find.MainTabsRoot.OpenTab : MainButtonDefOf.Research).TabWindow;
		try
		{
			if (window is MainTabWindow_Research vanilla) { vanilla.Select(r); return Result.Done; }
			switch (window.GetType().FullName)
			{
				case "FluffyResearchTree.MainTabWindow_ResearchTree": return SelectFluffy(window, r);
				case "NiceResearchTab.MainTabWindow_ResearchTree": return SelectNice(window, r);
				case "YART.MainTabWindow_YART":
					// Applied by YART on its next frame, once its graph is built.
					window.GetType().GetMethod("RequestOpenAt", All).Invoke(window, new object[] { r });
					return Result.Done;
			}
		}
		catch (Exception e)
		{
			Log.Warning($"[TechLeveler] Could not select {r.defName} in {window?.GetType().FullName}: {e}");
		}
		return Result.Unsupported;
	}

	// Mirrors what Fluffy's own search menu does when you pick a result: put the label in the search box
	// (highlights the match) and centre the tree on the node.
	static Result SelectFluffy(object window, ResearchProjectDef r)
	{
		var type = window.GetType();
		var tree = type.Assembly.GetType("FluffyResearchTree.Tree");
		var node = tree?.GetMethod("ResearchToNode", All)?.Invoke(null, new object[] { r });
		if (node == null) return Result.Retry;

		if (type.GetField("_quickSearchWidget", All)?.GetValue(window) is QuickSearchWidget search)
		{
			search.filter.Text = r.LabelCap;
			type.GetMethod("updateSearchResults", All)?.Invoke(window, new object[] { default(UnityEngine.Rect) });
			if (type.GetField("_matchingProjects", All)?.GetValue(window) is HashSet<ResearchProjectDef> matching)
			{
				matching.Clear();
				matching.Add(r);
			}
		}
		type.GetMethod("CenterOn", All).Invoke(window, new[] { node });
		Log.Message($"[TechLeveler] Fluffy: centred on {r.defName}");
		return Result.Done;
	}

	static Result SelectNice(object window, ResearchProjectDef r)
	{
		var widget = window.GetType().GetField("researchTree", All)?.GetValue(window);
		if (widget == null) return Result.Retry;
		var widgetType = widget.GetType();
		if (widgetType.GetField("Nodes", All)?.GetValue(widget) is not ICollection { Count: > 0 }) return Result.Retry;
		var node = widgetType.GetMethod("FindByProject", All)?.Invoke(widget, new object[] { r });
		if (node == null) return Result.Unsupported;
		widgetType.GetMethod("Select", All).Invoke(widget, new[] { node, true, false, false });
		return Result.Done;
	}
}
