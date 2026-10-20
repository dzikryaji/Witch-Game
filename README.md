# Witch Game

Witch Game is a single-player, mouse-first puzzle and deduction game about a witch hiding behind a medieval herbalist shop. Across a fourteen-day witch hunt, the player treats customers, studies village news, manages limited herbs and money, and decides when profitable magic is worth the risk of discovery.

This repository currently contains a fresh Unity 2D URP project and the default sample scene. The gameplay systems described below are planned, not yet implemented.

## Project status

- Unity: `6000.6.5f1`
- Rendering: Universal Render Pipeline 2D
- Input: Unity Input System
- Tests: Unity Test Framework installed; no project tests yet
- Enabled scene: `Assets/Scenes/SampleScene.unity`
- Gameplay code, production UI, content assets, prefabs, saves, and CI: not yet implemented
- Current milestone: build a thin Day 1 vertical slice before entering all fourteen days of content

## Core game loop

1. Read the daily newspaper.
2. Open the shop and receive one customer at a time.
3. Inspect the customer's appearance and automatic dialogue.
4. Use the guidebook to interpret the requested treatment.
5. Select, grind when required, and brew exactly one potion.
6. Serve before the sixty-second timer expires.
7. Resolve payment, Hunter inspection, arrest, and automatic hex protection.
8. After the final customer, meet the day's minimum-income target.
9. Restock herbs and buy available upgrades.
10. Autosave the next day's starting state before showing its newspaper.

The main risk is magical treatment. A potion containing at least one magical ingredient pays twice the normal amount, but serving it to a Hunter causes an arrest attempt. An available hex prevents one arrest and is consumed automatically.

## Getting started

### Requirements

- Unity Hub
- Unity Editor `6000.6.5f1`
- Git

### Open the project

1. Clone this repository.
2. In Unity Hub, choose **Add project from disk** and select this directory.
3. Open the project with Unity `6000.6.5f1`.
4. Let Unity restore packages.
5. Open `Assets/Scenes/SampleScene.unity`.
6. Enter Play Mode.

The current result is only the Unity 2D template. These steps must be updated when the Bootstrap scene and first playable slice exist.

## Planned project layout

Create folders only when real files need them.

```text
Assets/
└── Witch/
    ├── Art/
    ├── Audio/
    ├── Content/
    ├── Prefabs/
    ├── Scenes/
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

Imported packages belong under `Assets/ThirdParty/`. Team code and imported assets must not be mixed.

See [ARCHITECTURE.md](ARCHITECTURE.md) for boundaries, dependency rules, game flow, save design, and testing strategy.

## Design constraints

Do not turn unresolved proposals into fixed code or content. Herb identity, property count, recipe structure, restrictions, sound evidence, final-potion rules, final herb count, exact prices, stock limits, target platform, and UI technology still require decisions.

## Development rules

- Check `git status` before and after work.
- Preserve existing local changes.
- Coordinate before editing shared scenes, prefabs, assembly definitions, or project settings.
- Keep game rules out of Unity UI and animation code.
- Keep file I/O out of Presentation.
- Store authored definitions in ScriptableObjects.
- Store mutable run state in plain serializable C# objects.
- Use stable IDs for content referenced by saves.
- Add an Edit Mode or Play Mode test with each rule or bug fix.
- Commit Unity `.meta` files with their assets.
- Never commit `Library`, `Temp`, `Logs`, `Obj`, or `UserSettings`.

## First playable milestone

The first implementation target is one complete Day 1 path:

1. Start from Main Menu.
2. Create a Day 1 save.
3. Read one newspaper.
4. Serve one Villager and one Hunter through the real UI.
5. Exercise normal and magical potion outcomes, inspection, and hex use.
6. Check daily income and open end-of-day shopping.
7. Save Day 2 safely.
8. Restart and load the identical Day 2 state.

Only after this slice works should the team add the remaining content and save-branch UI.

## Team

The production plan assigns three programmers and two artists. Programmer ownership is summarized in [ARCHITECTURE.md](ARCHITECTURE.md#team-boundaries).
