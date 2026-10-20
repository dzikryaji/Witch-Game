# Witch Game Architecture

## Status and scope

This document describes the intended Unity architecture for Witch Game. It is a target design, not a description of systems that already exist.

Current repository state:

- Unity `6000.6.5f1` with Universal Render Pipeline 2D
- confirmed player target: macOS 12.0 or newer on Apple Silicon (`arm64`), using Metal
- Input System and Unity Test Framework installed
- only `Assets/Scenes/SampleScene.unity` enabled in build settings
- sample scene contains template camera and 2D light objects
- no production gameplay code, assembly definitions, UI, prefabs, tests, save system, or CI
- current template successfully builds and launches as an Apple Silicon macOS player, but its template bundle identity and invalid distribution signature make it unsuitable for release

This architecture preserves approved game and production rules while keeping unresolved design choices configurable or unimplemented. Code must not guess their outcomes.

## Architectural goals

- Keep deterministic game rules testable without loading Unity scenes.
- Keep Unity UI, animation, audio, and input replaceable without rewriting game rules.
- Keep persistence and platform behavior outside gameplay decisions.
- Give three programmers clear ownership and stable integration boundaries.
- Support exact day-start restoration and branch-preserving replay.
- Fit a two-month production schedule with one reusable shop location and one core loop.
- Produce a launchable, testable macOS `.app` from a clean checkout.

## Dependency model

```text
Presentation ------> Application ------> Domain
                           ^                ^
                           |                |
Infrastructure ------------+----------------+

Bootstrap creates and connects all concrete services.
```

Dependencies point toward game policy. Domain knows nothing about Unity, files, scenes, UI, or platform APIs.

### Domain

Pure C# rules and state transitions. Domain answers questions such as:

- whether selected ingredients match a recipe;
- whether a potion is normal or magical;
- whether ingredients and money can be spent;
- how much payment qualifies toward the daily target;
- whether a Hunter inspects inventory;
- whether an arrest occurs and a hex is consumed;
- whether the day target, victory, or a loss condition is satisfied.

Domain must not reference `UnityEngine` or call infrastructure interfaces directly.

### Application

Use cases that coordinate Domain rules and define legal action order. Examples:

- start or load a run;
- start a day or customer encounter;
- select and commit ingredients;
- grind, brew, discard, serve, or expire a timer;
- finish a day;
- buy stock, unlocks, or upgrades;
- request save and load operations.

Application owns service interfaces such as save repositories, clocks, random sources, and scene-transition requests. It returns typed results for Presentation to display. It must not draw UI or write files directly.

### Presentation

Unity-facing views, presenters, input, animation, visual effects, and display-only timing. Planned surfaces include:

- Main Menu and Story/Load screens;
- newspaper and guidebook;
- customer portrait and dialogue;
- inventory and potion preparation;
- sixty-second timer;
- result, arrest, hex, game-over, and victory feedback;
- end-of-day summary, restocking, and upgrades.

Presentation forwards actions to Application and renders returned results. It must not calculate recipes, payment, Hunter identity, income targets, or save outcomes.

### Infrastructure

Adapters for Unity and device services:

- local file save repository;
- system clock;
- deterministic random source;
- scene loading;
- audio playback and mixer settings;
- application pause, resume, and quit callbacks.

Infrastructure implements Application interfaces. It must not decide game outcomes.

Infrastructure also owns macOS-specific adapters and release integration: `Application.persistentDataPath`, player logs, application pause and quit callbacks, build output, signing hooks, and packaging.

### Bootstrap

Composition root that creates concrete services and connects every layer. Bootstrap owns object lifetime and startup order, not gameplay rules.

## Assembly boundaries

Create assembly definitions when their first production files are added.

| Assembly | Allowed project references | Unity reference |
| --- | --- | --- |
| `Witch.Domain` | none | no |
| `Witch.Application` | `Witch.Domain` | no |
| `Witch.Presentation` | `Witch.Application`, `Witch.Domain` | yes |
| `Witch.Infrastructure` | `Witch.Application`, `Witch.Domain` | yes |
| `Witch.Bootstrap` | all assemblies | yes |

Tests should mirror these boundaries. Edit Mode tests cover Domain and Application first; Play Mode tests cover Unity integration.

## Planned filesystem layout

```text
Assets/Witch/
├── Art/
│   ├── Characters/
│   ├── Environment/
│   ├── Herbs/
│   └── UI/
├── Audio/
├── Content/
│   ├── Customers/
│   ├── Days/
│   ├── Economy/
│   ├── Guidebook/
│   ├── Herbs/
│   ├── Newspapers/
│   ├── Recipes/
│   └── Upgrades/
├── Prefabs/
├── Scenes/
│   ├── Bootstrap.unity
│   ├── MainMenu.unity
│   ├── Shop.unity
│   └── Development/
├── Scripts/
│   ├── Domain/
│   ├── Application/
│   ├── Presentation/
│   ├── Infrastructure/
│   └── Bootstrap/
├── Settings/
├── Tests/
│   ├── EditMode/
│   ├── PlayMode/
│   └── Fixtures/
└── UI/
```

Add folders only when real files need them. Imported assets belong under `Assets/ThirdParty/`.

## Data model

### Authored definitions

Use ScriptableObjects for team-authored, immutable-at-runtime content:

- `HerbDefinition`
- `RecipeDefinition`
- `CustomerDefinition`
- `DayDefinition`
- `NewspaperDefinition`
- `UpgradeDefinition`
- `EconomyDefinition`

Definitions may contain stable IDs, display text, artwork references, prices, recipe requirements, dialogue, and authored schedules. They must not contain current money, inventory quantities, purchased upgrades, or completed-day state.

### Mutable state

Use plain serializable C# objects for player progress:

- `RunState`: run ID, current day, money, inventory, unlocks, upgrades, hexes, and narrative flags;
- `DayState`: qualifying income, deterministic customer order, current encounter, and day-specific flags;
- `PotionDraft`: selected and committed ingredients plus preparation state;
- `SaveNode`: complete beginning-of-day snapshot and parent metadata;
- `SaveIndex`: runs, branches, node relationships, and latest markers;
- `GlobalSettings`: audio, language, display, accessibility, and controls, stored separately from run saves.

Persist stable content IDs. Never persist list indexes, display names, Unity instance IDs, or direct GameObject references as identity.

## State machines

### Top-level flow

```text
Boot
Main Menu
Story/Load
Newspaper
Shop Opening
Customer Encounter
Income Check
End-of-Day Shopping
Saving Next Day
Victory or Game Over
```

Each Application action validates the current phase. Invalid actions return an explicit error and do not mutate state.

### Customer flow

```text
Present customer and automatic dialogue
Start 60-second timer when player gains control
Select and prepare ingredients
Serve one potion, or expire timer
Resolve payment, inspection, arrest, and hex use
Display result
Advance to next customer
```

Ingredients are consumed once intentionally committed to a brew, discard, or service. UI must provide confirmation or an undo opportunity before an accidental mouse action can cause irreversible consumption.

### End-of-day flow

```text
Resolve final customer
Check qualifying potion-payment income
Game Over when below target
Victory after successful Day 14
Otherwise show summary and shopping
Finalize purchases
Build next day with income reset to zero
Write and validate autosave
Show next newspaper only after save succeeds
```

Speed tips increase money but never count toward the daily income target. Meeting a target never deducts money.

## Potion and encounter resolution

Current fixed rules:

- Every current recipe uses exactly two herb units and may use two units of the same herb.
- Normal and magical variants satisfy the same recipe requirement.
- Any magical ingredient makes the completed potion magical.
- Correct normal potion pays the normal amount.
- Correct magical potion pays double.
- Wrong potion pays nothing.
- Served and discarded ingredients remain consumed.
- Villagers never expose or arrest the player.
- A Hunter leaves after correct normal treatment without inspecting inventory.
- A Hunter attempts arrest after magical treatment.
- A Hunter inspects inventory after wrong treatment or timeout; magical stock then causes an arrest attempt.
- An available hex automatically prevents one arrest and is consumed.

Hunter identity follows the two-characteristic rule. One characteristic is insufficient. Each designed Hunter-like appearance trait counts as one; dialogue contradicting the current newspaper counts as one. Villagers may contradict the newspaper, and Hunters may tell the truth.

Keep these rules in Domain and test every outcome as a decision table. Do not duplicate them in UI callbacks.

## Save architecture

Saving follows the checkpoint, branching, safety, and compatibility rules below.

### Checkpoint model

- Create one complete autosave at the beginning of each day.
- Do not provide manual or mid-day saves.
- Leaving or losing during a day restores that day's starting snapshot.
- Replaying an earlier node preserves existing descendants.
- Completing the replay creates a new branch from the selected ancestor.
- Starting a separate new run resets progression without deleting existing runs.

### Repository boundary

Application owns an `ISaveRepository` contract. Infrastructure provides an in-memory implementation for early tests and a file-backed implementation for production.

Repository operations should return explicit success or error results. UI must never infer success from a missing exception.

### Safe write sequence

1. Serialize complete save data to a temporary file.
2. Validate required fields, stable references, and save-data version.
3. Flush completed data.
4. Replace primary file or index only after validation succeeds.
5. Keep a recoverable previous copy until replacement completes.

Next-day gameplay cannot begin until this operation succeeds. Failed writes leave the previous valid day-start save intact.

### Compatibility

Store save-data version separately from displayed game version. Migrate supported versions only after creating a backup. Corrupt or unsupported saves produce a clear error; they must never silently become a new game.

## Scene strategy

Use three production scenes:

- `Bootstrap`: long-lived services and composition root;
- `MainMenu`: title, settings, credits, and Story/Load;
- `Shop`: all fourteen playable days.

Do not create one scene per day. Change newspapers, customers, props, weather, damage, lighting, and sound through data and reusable layers.

UI Toolkit is the recommended prototype choice for document-heavy screens. Confirm it during the Day 1 slice. If it cannot support shop interactions cleanly, choose one alternative before production UI expands; do not maintain parallel UI Toolkit and uGUI versions.

## macOS platform architecture

- Unity build target: `StandaloneOSX`.
- Minimum supported system: macOS 12.0.
- Required architecture: Apple Silicon (`arm64`).
- Rendering: Metal through URP 2D.
- Input: mouse and keyboard through Unity Input System.
- Display: windowed and fullscreen, including Retina displays.
- Output: `Witch Game.app`.
- Persistence: file-backed repository rooted at `Application.persistentDataPath`.
- Diagnostics: Unity player log plus explicit save and migration error results.
- Distribution: stable bundle identifier, version, icon, code signing, and notarization or Mac App Store packaging.

Run Edit Mode tests in the macOS Editor. Run Play Mode and integration smoke tests in both Editor and built player where supported. Release acceptance requires launch, save, quit, relaunch, window/fullscreen, input, audio, and log checks on Apple Silicon hardware.

Intel support is not part of the current acceptance baseline. Supporting Intel requires a Universal `arm64` and `x86_64` build plus separate compatibility testing.

## Determinism and content validation

Reloading a day must reproduce its customer order and Hunter placement. Persist an authored schedule or deterministic seed sufficient to recreate the exact day.

Before entering all fourteen days, add an Editor validation tool that reports:

- missing or duplicate stable IDs;
- wrong customer, Hunter, or Villager counts;
- wrong daily income targets;
- recipes using unavailable herbs;
- orders requiring locked guidebook information;
- Hunters lacking two visible or readable characteristics;
- unlocks occurring after dependent orders;
- missing newspaper facts or dialogue links;
- invalid save references.

Random or hybrid scheduling must never generate an impossible order through no player fault.

## Testing strategy

### Edit Mode

- correct and wrong recipes;
- ingredient consumption on serve and discard;
- normal and magical potion classification;
- every Villager and Hunter outcome;
- timeout, inspection, arrest, and hex use;
- speed-tip exclusion from qualifying income;
- all fourteen fixed income targets;
- resource exhaustion and Day 14 victory priority;
- deterministic schedules;
- save-node ancestry, branching, and deletion;
- state-machine action guards.

### Play Mode and integration

- Bootstrap creates each service once;
- production scenes load correctly;
- UI sends one command per action and displays returned results;
- timer starts only after automatic dialogue;
- save survives application restart;
- failed writes preserve previous data;
- Day 1 completes, saves Day 2, and reloads identical state;
- replaying an earlier day creates a visible branch without damaging the original path.

## Team boundaries

| Area | Owner | Main reviewer |
| --- | --- | --- |
| Domain rules, state, flow, and content validation | Programmer A | Programmer C |
| Application commands and results | Programmer A | Programmer B |
| UI, input, scenes, and presentation prefabs | Programmer B | Programmer A or C |
| Save files, branching, migrations, and recovery | Programmer C | Programmer A |
| Bootstrap, technical services, and builds | Programmer C | Programmer A or B |

Coordinate before changing public interfaces, saveable state, ScriptableObject schemas, scene names, shared scenes, prefabs, assembly definitions, or project settings.

## Implementation sequence

1. Create Domain and Application assemblies with plain state objects.
2. Test inventory, potion drafts, recipe matching, payment, Hunter outcomes, and hex use.
3. Add one customer Application flow and typed results.
4. Create Bootstrap with an in-memory save repository and test adapters.
5. Build one customer UI, then the complete Day 1 flow.
6. Add daily target, shopping, next-day state, and safe local persistence.
7. Verify restart restores an identical Day 2 state.
8. Add Story/Load branching and deletion.
9. Add content validation, all fourteen days, final art, and release automation.

## Unresolved decisions

Keep these configurable or unimplemented until the team decides:

- herb identity and magical-variant model;
- five or six treatment properties;
- exact basic and compound recipe structure;
- detailed unlock schedule;
- whether sound counts as Hunter evidence;
- whether a final potion is required;
- hard-order restriction rules;
- final herb count;
- exact prices and stock limits;
- save-branch limit;
- final UI technology after prototype testing.

Three-or-more-unit recipes are also not approved. Current recipes remain exactly two units until an explicit design revision.

## Architecture acceptance criteria

- Assembly references enforce dependency direction.
- Domain and Application compile without `UnityEngine`.
- Bootstrap is the only composition root.
- ScriptableObjects hold definitions, never mutable run state.
- One customer flows end to end through Presentation, Application, and Domain.
- Day 1 saves Day 2 and reloads exactly.
- Rule tests and content validation pass.
- Save failure cannot expose a newspaper for a checkpoint that does not exist.
- No unresolved design decision is hard-coded.
- Clean checkout restores packages, runs tests, and starts the game using documented steps.
