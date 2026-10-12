# FICSIT in Space — Space Engineers × Satisfactory

**Tagline:** Fill FICSIT's Space Elevator order from a Space Engineers station, using Satisfactory's own production chain.

Space Engineers is the game you play. Satisfactory brings its rules: its parts
(Iron Plate, Iron Rod, Screw, Reinforced Iron Plate, Rotor, Smart Plating), its
recipe amounts and craft times, and their names and descriptions. All of these
are read at load time from **your own Satisfactory install**
(`CommunityResources/Docs/en-US.json`). Nothing from either game is shipped.

## What you get (v0.1, solo)
- A FICSIT starter kit the first time you spawn in a world.
- Six Satisfactory parts craftable in any Space Engineers Assembler, with
  Satisfactory's amounts and times (for example 3 Iron Ingot → 2 Iron Plate in 6 s,
  or 1 Reinforced Iron Plate + 1 Rotor → 1 Smart Plating in 30 s).
- Smelting stays Space Engineers' own: mine iron, refine it, then build up the chain.
- **Space Elevator:** name any cargo container "Space Elevator" and deliver
  Phase 1 (50 Smart Plating). Progress shows in the block's terminal info and
  on screen, and the phase completes with a FICSIT message.

## Requires
- Space Engineers (the game you play; the mod goes in `%AppData%/SpaceEngineers/Mods/FicsitInSpace`).
- Satisfactory (companion: its Docs file is linked to
  `FicsitInSpace/Satisfactory/Docs.json` when the mashup is set up).

## How it is built
- `sheets/`: the design (items, recipes, phases, kit, hooks). It is the source of truth.
- `tools/build.py`: preflight (every cell filled, every reference resolves),
  then generates the SBC definitions and `SheetData.cs`.
- `mod/FicsitInSpace/`: the Space Engineers mod (SBC + C# session/game-logic scripts;
  Space Engineers compiles the scripts itself when the world loads).
- `tests/TestDocs.cs`: offline test of the Satisfactory Docs reader.
- `tools/package.py`: builds `dist/FicsitInSpace-<version>.zip`.

## Status
See `MODLOG.md`. Source and offline tests are done. It has **not yet run in Space
Engineers**.
