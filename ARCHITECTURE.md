# Witch Game Architecture

## Current status

This file describes the implemented three-day prototype as of October 28, 2026. The fourteen-day documents remain the release target; features explicitly listed as deferred below are not claimed as implemented.

Unity project facts:

- Unity `6000.6.5f1`, URP 2D, Input System, Unity Test Framework.
- macOS 12 minimum, Apple Silicon (`arm64`) development build, Metal through URP.
- Four enabled production scenes: Bootstrap, MainMenu, Shop, RewindMinigame.
- Editor-authored uGUI scenes and reusable herb/card prefabs; no scene per day or card difficulty.
- Every production scene contains an authored Camera. Main Menu, Shop, and Rewind also contain their editable Canvas and EventSystem hierarchies.

## Dependency model

```text
Presentation ------> Application ------> Domain
                           ^                ^
                           |                |
Infrastructure ------------+----------------+

Bootstrap creates and connects concrete services.
```

| Assembly | Implemented responsibility | Project references |
|---|---|---|
| `Witch.Domain` | Recipes, exact pairs, payments, suspicion, timer/expense/rewind rules, immutable run state | none |
| `Witch.Application` | Legal phases and use cases for start/load, arrival, preparation, serving, timer, rewind, expense, retry, and advancement | Domain |
| `Witch.Infrastructure` | ScriptableObject content adapter/validation, JSON persistence, atomic file replacement, clock, random, scene loading, audio, lifecycle | Application, Domain |
| `Witch.Presentation` | Main menu/settings, Shop/guidebook/tutorial/pause, card board, result screens, input, audio requests | Application, Domain, Unity UI/Input |
| `Witch.Bootstrap` | Composition root, object lifetime, content-to-view mapping, scene injection | all four assemblies |

Domain and Application assembly definitions use `noEngineReferences: true`. Source checks and Unity compilation verify neither imports Unity APIs.

## Runtime composition

`GameCompositionRoot` persists from Bootstrap and:

1. validates and maps `GameContentCatalog`;
2. creates JSON run/settings repositories and platform adapters;
3. creates Application use cases with Domain rules;
4. loads Main Menu;
5. injects Main Menu, Shop, or Rewind presenters whenever a production scene loads.

Scenes and prefabs own visual hierarchy, layout, colors, fonts, and component references. Presentation scripts bind authored controls, dispatch interactions, and render state; they do not create GameObjects, add visual components, or calculate layout at runtime. Twenty-four reusable herb slots and sixteen reusable rewind-card slots are authored in advance and activated/configured from content. Recipes, orders, timers, payments, expenses, unlocks, guidebook pages, rewind symbols, and rewind difficulty come from content definitions or Domain configuration, not scene objects.

The current implementation creates no GameObjects or components at runtime. Bootstrap, fixed-aspect controller, Cameras, audio channels, interface hierarchy, herb slots, and maximum rewind-card slots are all authored. Scripts only configure content, state, activity, and behavior. Each loaded Canvas uses its scene Camera so the whole interface stays inside a centered `16:9` landscape viewport; wider displays show black pillarboxes.

## Implemented content and folders

```text
Assets/
├── Audio/{BGM,SFX,Licenses}
├── Content/{Customers,Days,Economy,Guidebook,Herbs,Orders,Recipes,Rewind,Unlocks}
├── Editor/GameBuild.cs
├── Prefabs/{HerbButton,RewindCard}.prefab
├── Scenes/{Bootstrap,MainMenu,Shop,RewindMinigame}.unity
├── Scripts/{Domain,Application,Infrastructure,Presentation,Bootstrap}
├── Settings/WitchInputActions.inputactions
└── Tests/{EditMode,PlayMode,Fixtures}
```

The catalog contains 25 herbs, three exact recipes, 15 orders, three days, two guidebook entries, two unlock sets, eight rewind symbols, prototype economy, and run configuration. The current three-day progression unlocks a subset of those herbs. Day 3 applies Soothe unlocks before building its first Shop view.

No active script, content asset, or production scene references newspaper gameplay, Hunter customers/deduction, magical-herb variants, hexes, or finite herb stock.

## State and persistence

- `RunState`: run ID, current day, money, unlock IDs, suspicion, rewind count, threshold, and persisted completion flag.
- `DayStartSnapshot`: immutable start-of-day run, timer, expense, save version/time.
- `DayAttemptState`: active seed, timer, nonrepeating used-order IDs, active encounter, earnings, workbench.
- `ActiveAttemptMetadata`: immediately persisted suspicion, rewind count, pending rewind, board seed, active order seed, run/day identity.

`JsonRunRepository` writes one current prototype run through temporary file, flush, atomic replace, and previous-copy recovery. `settings.json` is separate and stores master/music/SFX volume plus reduced card mismatch delay. Old prototype save schemas are intentionally unsupported.

## Principal flows

### Shop

Day 1 shows three short opening beats before customer arrival. Prototype 1 temporarily admits customers automatically. Prototype 2 will add a player-operated bell/button; first successful admission starts the timer once. The presenter dispatches grind, pot, brew, discard, and serve commands. Domain returns correct, wrong-valid, or invalid-brew outcomes. Correct pays `10`; wrong-valid pays `0` and adds suspicion; invalid pairs auto-discard without suspicion. Each seeded order bag uses every available template once before reshuffling; repeated templates receive unique runtime order IDs so the queue can continue until closing time.

Timer runs during customer/order, guidebook, and preparation. Pause freezes it. At zero, no new encounter starts; an active encounter remains completable. If the authored queue is already empty, zero transitions directly to closing.

### Rewind

Suspicion `3` is persisted as pending before scene navigation. Rewind reconstructs a deterministic content-configured symbol board from saved metadata. There is no timer or failure. Mismatches flip back after the selected accessibility delay. Completion atomically restores day-start money/timer/workbench, resets suspicion, increments rewind count, changes order seed, and reloads Shop.

### Expense and progression

Closing resolves the configured expense. Success deducts the full amount; failure deducts nothing and is the only game-over route. Retry restores the same day and seed. Days 1–2 create the next snapshot with timer progression and unlocks. Paying Day 3 expense persists completion before entering prototype victory and playing its medieval-fanfare placeholder cue.

## Audio and accessibility

`UnityAudioService` owns independent Music, SFX, Ambience, and UI sources routed through the authored `WitchAudio` mixer groups. It prevents rapid repeated-cue stacking, applies persistent volume settings, and validates every required cue during Bootstrap startup. All current clips are original generated WAV placeholders; there is no runtime synthesis fallback and no downloaded audio ships. Ownership is recorded in `../Witch Vaults/AUDIO_LICENSE.md`; future CC0 recommendations remain in the current game specification.

All uGUI buttons are Selectables for keyboard navigation. Herb pointer clicks do not load the grinder; pointer users drag, while focused herb controls retain explicit keyboard Submit support. Rewind card identity uses authored text/symbol IDs in addition to color. Settings provide master/music/SFX volumes and reduced mismatch delay. Pause stops the authoritative Shop timer; guidebook does not.

## Verification

Edit Mode covers Domain rules, Application guards/transactions, content validation/mapping, persistence, recovery, settings separation, and platform adapters. Play Mode covers scene order, authored view references, Cameras, automatic arrival, guidebook timer behavior, preparation/service outcomes, seeded order-bag refill, tutorial/pause, rewind/reload/reset, expense retry, and three-day victory.

`Witch.Editor.GameBuild.BuildMacArm64` validates the four enabled scenes and creates a clean Apple Silicon macOS development player at `/private/tmp/WitchGamePrototype.app`.

## Deliberately deferred release features

- Days 4–14 and final balance/content.
- Load Selection, save branches, hints, cosmetics, purchases, and Game Center.
- Final art, animation, authored BGM/SFX, localization, signing, notarization, and TestFlight/App Store work.
- Persisted narrative/tutorial flags and optional repeated-mismatch assistance.

These are release-roadmap items, not holes in the three-day prototype contract.
