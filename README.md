# Tech Leveler

A RimWorld 1.6 and 1.7 mod. Your faction's tech level goes up as you finish key research, and research above your level costs more.

Player guide: https://bambaryla-mods.github.io/RimworldTechLeveler/

Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3828217997

## What it does

- Each tier (Neolithic, Medieval, Industrial, Spacer, Ultra, Archotech, plus tiers added by other mods) has a list of key research. Finish it and your faction moves up a tier, with a letter.
- Research costs 50% of its base cost extra for every tier it sits above you. Vanilla stops counting at Industrial; Tech Leveler doesn't, so Spacer and Ultra research cost more if you jump ahead.
- A tier you just reached is "proto" at first: its own research costs up to 25% extra until you finish 20% of its key research.
- A Tech tiers tab on every research bench shows each tier's key research and what is still missing. Clicking a project opens it in the research window (vanilla, Research Tree, Nice Research Tab or YART).
- Tiers, key research and tier tags are plain XML, so other mods can patch them. `Common/Patches/_Example_AddResearch.xml.example` is a patch to copy.
- Built-in patches add Solar, Quasar, Tenebral and Umbral for Beyond Our Reach :: Redux, an Advanced Spacer tier for the All-Bamba armors and guns, and key research from a number of other mods.

Requires Harmony. Not compatible with Tech Advancing (GHXX) or Vanilla Factions Expanded - Tribals.

## Building

The C# source is in `Source/`. `TechLeveler.csproj` builds `1.6/Assemblies/TechLeveler.dll` (net472) with `dotnet build Source/TechLeveler.csproj -c Release`, and `1.7/Assemblies/TechLeveler.dll` (net10.0) with `-p:GameVersion=1.7`. `UIRethemedCompat/TechLeveler.UIRethemed.csproj` builds the optional [LFS] UI Rethemed theme into `Compat/UIRethemed/<version>/Assemblies`.

## License

[CC BY-NC-SA 4.0](LICENSE)
