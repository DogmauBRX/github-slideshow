# MODLOG — FICSIT in Space

## Design (agreed with the creator)
- Host / primary: Space Engineers. Companion: Satisfactory. Solo.
- Start: a FICSIT starter kit plus the Space Elevator Phase 1 order (50 Smart Plating).
- Licence and remix choice: **not yet given by the creator**.

## Route
- Space Engineers: no loader needed. A local mod (SBC data plus C# scripts compiled by SE)
  in `%AppData%/SpaceEngineers/Mods/FicsitInSpace`.
- Satisfactory: an Unreal game, but the mashup only needs its plain-JSON Docs file
  (`<Satisfactory>/CommunityResources/Docs/en-US.json`, UTF-16 LE with a BOM), so no pak
  extraction or SML is needed. Melty maps that file to `FicsitInSpace/Satisfactory/Docs.json`,
  and the SE script reads it with `ReadFileInModLocation` (SE's script whitelist allows no
  other file access).
- Definitions are edited in `LoadData` and undone in `UnloadData`
  (`MyBlueprintDefinitionBase.Prerequisites/Results/BaseProductionTimeInSeconds` are public
  fields in KSH's published source).
- New items borrow a vanilla component's model and icon. The script copies them from the
  loaded vanilla definition, so the guessed SBC paths are only fallbacks.

## Verified
- Sheets preflight: 0 problems.
- `tests/TestDocs.cs` (mono) against a Docs-format fixture built from community data
  (SatisfactoryTools data1.0.json, kept in scratch and never committed): every item and
  recipe resolves, amounts match Satisfactory 1.0, and the reader still works if the file
  is decoded as 8-bit.
- The mod scripts compile as C# 6 against stand-in API declarations. These check syntax
  only; they are not the real SE API.

## Not yet verified (needs a PC with both games)
- That SE compiles and loads the scripts against its real ModAPI whitelist.
- That item names, recipes, kit and Space Elevator work in game.
- The one-click start: a world with this mod enabled (SE loads local mods per world).
- The Melty recipe, its file mapping, the release test and the screenshot. melty.gg is
  blocked by this cloud session's network policy.

## Gotchas
1. Docs is UTF-16. `SatDocs.Clean` strips NULs if a reader decodes it as 8-bit.
2. A component without a blueprint can crash SE, so the preflight requires a recipe for
   every new item.
