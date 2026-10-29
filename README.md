# Witch Game

Witch Game is a mouse-and-keyboard order-deduction game about a witch hiding behind a medieval herbalist shop. This repository currently contains a playable three-day prototype with reusable placeholder presentation. The planned full game lasts fourteen days.

All customers are Villagers. Hunters are an outside narrative threat triggered by repeated wrong orders. Hunter identity deduction, Hunter customers, newspaper gameplay, magical-herb variants, arrests, and hexes are not part of the current game.

## Implemented prototype

- Three authored days with `4:00`, `4:20`, and `4:40` timers.
- Warm, Cool, and Day 3 Soothe recipes, guidebook clues, and 15 uniquely identified orders.
- Automatic bell/customer arrival, unlimited herbs, grind/pot/brew/discard/serve interactions, exact-pair checking, payment, and daily expense.
- Wrong valid orders pay `0` and add suspicion. Suspicion `3` persists a pending rewind and opens the separate memory-card scene.
- Guaranteed 3–8-pair rewind board with deterministic reload, mismatch flip-back, day reset, suspicion reset, erased attempt earnings, and changed order seed.
- Expense failure as the only game-over path, same-seed retry, and prototype victory after paying Day 3 expense.
- Persisted victory restoration and safe Main Menu fallback when Shop/Rewind scene loading fails.
- Centered `16:9` landscape gameplay/UI viewport with black pillarboxes on wider displays.
- Dark-comedy Day 1 opening/tutorial, keyboard navigation, pause behavior, readable symbolic cards, reduced mismatch-delay setting, and persistent master/music/SFX settings.
- Four authored Audio Mixer groups—Music, SFX, Ambience, UI—with copyright-safe original placeholder cues and Bootstrap validation for missing clips. CC0 replacement recommendations and rules live in the prototype design document.
- Atomic JSON current-run save with recovery copy and a separate settings file.
- Editor-authored Main Menu, Shop, and Rewind hierarchies with reusable herb/card prefabs. Presentation scripts handle behavior and state display instead of constructing the interface.

## Architecture

```text
Presentation -> Application -> Domain
Infrastructure -> Application -> Domain
Bootstrap connects all parts
```

`Witch.Domain` and `Witch.Application` are Unity-free assemblies. Immutable authored content uses ScriptableObjects; mutable run data uses plain C# state. Infrastructure owns files, time, random generation, scenes, audio, and lifecycle. Presentation renders typed results and does not calculate outcomes.

Every production scene contains a Camera. Bootstrap owns an authored fixed-aspect controller; loaded Canvases use their scene Camera so UI follows the centered `16:9` landscape viewport. Visible hierarchy, layout, styling, Canvas controls, and reusable card/herb visuals are authored in scenes and prefabs. Presentation code binds interactions and updates state; it does not build visual GameObjects at runtime.

Production uses exactly four enabled scenes:

1. `Assets/Scenes/Bootstrap.unity`
2. `Assets/Scenes/MainMenu.unity`
3. `Assets/Scenes/Shop.unity`
4. `Assets/Scenes/RewindMinigame.unity`

See [ARCHITECTURE.md](ARCHITECTURE.md) for the implementation map, [Production Constraints and Timeline](../Witch%20Vaults/Production%20Constraints%20and%20Timeline.md) for platform and performance targets, and [Current Game Specification](../Witch%20Vaults/Current%20Game%20Specification.md) for prototype rules. [Newspaper Plan](../Witch%20Vaults/Newspaper%20Plan.md) is retained as inactive reference only.

## Open and test

Requirements: Unity `6000.6.5f1`; macOS 12 or newer on Apple Silicon for the accepted player target.

1. Add this folder in Unity Hub.
2. Open it with Unity `6000.6.5f1`.
3. Open `Assets/Scenes/Bootstrap.unity`.
4. Enter Play Mode. Bootstrap loads Main Menu and composes the application.

Unity Test Framework suites are under `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`. Command-line Apple Silicon development builds use:

```text
-executeMethod Witch.Editor.GameBuild.BuildMacArm64
```

The build method validates the four-scene order and writes `/private/tmp/WitchGamePrototype.app`.

Local release-size verification uses `Witch.Editor.GameBuild.BuildMacArm64Release` and writes `/private/tmp/WitchGamePrototype-Release.app`.

## Prototype versus release target

The prototype ends after Day 3 and uses provisional fixed values: payment `10`, daily expense `20`, and one current-run save. The design target remains fourteen days with more content and balancing. Branching/load selection, hints, cosmetics, optional Game Center, final art, signed distribution, and replacement music/SFX are not implemented prototype features.

## Development rules

- Keep Domain and Application free of Unity APIs.
- Keep rule calculations out of UI and file I/O out of Presentation.
- Keep appearance, layout, and visual component composition in scenes/prefabs. Presentation scripts may control behavior and state, but must not construct the visual hierarchy at runtime.
- Store authored definitions in ScriptableObjects and mutable progress in plain state.
- Use stable IDs for saved content.
- Add a focused test for each behavior or regression.
- Keep Unity `.meta` files with assets; never commit `Library`, `Temp`, `Logs`, `Obj`, or `UserSettings`.
- Do not introduce newspaper or Hunter-customer mechanics into the active prototype.
