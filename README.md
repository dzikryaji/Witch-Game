# Witch Game

Witch Game is a single-player, mouse-first puzzle and deduction game about a witch hiding behind a medieval herbalist shop. Across a fourteen-day witch hunt, the player treats customers, studies village news, manages limited herbs and money, and decides when profitable magic is worth the risk of discovery.

This repository currently contains a fresh Unity 2D URP project and the default sample scene. The gameplay systems described below are planned, not yet implemented.

## Project status

- Unity: `6000.6.5f1`
- Target: macOS 12.0 or newer on Apple Silicon (`arm64`)
- Player: macOS `.app` using Metal, mouse, and keyboard
- Rendering: Universal Render Pipeline 2D
- Input: Unity Input System
- Tests: Unity Test Framework installed; no project tests yet
- Enabled scene: `Assets/Scenes/SampleScene.unity`
- Gameplay code, production UI, content assets, prefabs, saves, and CI: not yet implemented
- Current milestone: build a thin Day 1 vertical slice before entering all fourteen days of content
- Optional platform enhancement: Apple Game Center after the core release candidate is stable; Apple packages are not installed yet

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
- macOS with Apple Silicon for the required player build and smoke test
- Git

### Open the project

1. Clone this repository.
2. In Unity Hub, choose **Add project from disk** and select this directory.
3. Open the project with Unity `6000.6.5f1`.
4. Let Unity restore packages.
5. Open `Assets/Scenes/SampleScene.unity`.
6. Enter Play Mode.

The current result is only the Unity 2D template. These steps must be updated when the Bootstrap scene and first playable slice exist.

## macOS target

The confirmed release target is macOS desktop. Current baseline:

- minimum macOS version: 12.0;
- required architecture: Apple Silicon (`arm64`);
- rendering API: Metal through URP 2D;
- primary input: mouse and keyboard;
- output: `Witch Game.app`;
- required verification: build, launch, save, quit, relaunch, windowed mode, fullscreen mode, and player-log review on Apple Silicon hardware.

Intel Mac support is optional until the team configures and tests a Universal `arm64` and `x86_64` build. Before distribution, replace the template bundle identifier and company name, then configure signing and notarization or Mac App Store submission.

### Current compatibility result

On October 20, 2026, the current template project successfully compiled as `StandaloneOSX`, produced an `arm64` macOS application with a macOS 12.0 minimum, and launched the enabled sample scene on Apple Silicon. This proves the current Unity setup can build and start on macOS. It does not yet prove production gameplay, save behavior, signing, notarization, Intel support, or release readiness because those systems do not exist yet.

Current release blockers:

- bundle identifier is still `com.DefaultCompany.2D-URP`;
- company name is still the Unity template default;
- generated app is not validly distribution-signed or notarized;
- only the template sample scene is included;
- window resizing is not yet enabled;
- production gameplay, saves, tests, and build automation do not exist yet.

## Release milestones

- By October 24: confirm permanent App ID, matching App Store Connect macOS record, signing path, tester groups, account roles, and release owners.
- October 26–30: upload, process, install, and smoke-test first playable internal TestFlight build.
- By November 10: upload content-integrated TestFlight build for Playtest 2.
- By November 16: freeze and archive release candidate.
- November 17: publish final TestFlight update and submit external Beta App Review when required.
- By November 22: finish store metadata, screenshots, privacy, age rating, pricing, review notes, and final build selection.
- November 23: submit macOS version to App Review; reserve November 24–27 for blockers or resubmission.

Use one permanent bundle ID and a unique increasing build string for every upload. Apple controls build processing, external Beta App Review, App Review approval, and storefront timing; team deadlines cover upload, internal installation, and submission.

## Good-to-have: Apple Game Center

Apple Game Center is optional and must not delay core gameplay, local saves, testing, signing, or the November 16 release candidate. Its four-hour isolated feasibility gate belongs inside the October 19–24 save, Apple platform, and TestFlight research task. Local files remain authoritative; Game Center is not a save dependency. Continue into production only if the gate passes, the first TestFlight upload succeeds, and required work remains on schedule. Recommended first scope is non-blocking local-player authentication, one native dashboard button, and at most two achievements: completing the Day 1 tutorial and completing the fourteen-day run. The current game has no approved comparable score, so leaderboards do not fit.

Current researched package baseline:

- Apple.GameKit `4.1.0`;
- Apple.Core `3.3.0` dependency;
- declared Unity minimum `2022.3`;
- declared macOS minimum `12.0`;
- current project uses Unity `6000.6.5f1` and macOS `12.0`, so compatibility looks valid but remains unverified until a signed project spike passes.

Keep Game Center behind `IGameCenterService`. Infrastructure wraps Apple.GameKit, Bootstrap selects the real or no-op implementation, Presentation shows optional status and dashboard UI, and Domain remains platform-independent. Signed-out, declined, offline, restricted, and error states must continue into normal local play.

Do not include leaderboards, multiplayer, matchmaking, challenges, activities, cloud saves, or gameplay rewards in the current scope. Local saves remain authoritative.

Optional integration must be fully green by November 13. Otherwise final builds use the no-op service and show no Game Center controls.

Official starting points:

- [Apple Unity Plug-ins](https://github.com/apple/unityplugins)
- [Apple Unity Plug-in Quick Start](https://github.com/apple/unityplugins/blob/main/Documentation/Quickstart.md)
- [Apple.GameKit Unity guide](https://github.com/apple/unityplugins/blob/main/plug-ins/Apple.GameKit/Apple.GameKit_Unity/Assets/Apple.GameKit/Documentation~/Apple.GameKit.md)
- [GameKit setup](https://developer.apple.com/documentation/gamekit/initializing-and-configuring-game-center)
- [App Store Connect Game Center setup](https://developer.apple.com/help/app-store-connect/configure-game-center/overview-of-game-center)

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

Unity Package Manager dependencies such as Apple.Core and Apple.GameKit remain package dependencies rather than copied team assets.

See [ARCHITECTURE.md](ARCHITECTURE.md) for boundaries, dependency rules, game flow, save design, and testing strategy.

## Design constraints

Do not turn unresolved proposals into fixed code or content. Herb identity, property count, recipe structure, restrictions, sound evidence, final-potion rules, final herb count, exact prices, stock limits, and UI technology still require decisions.

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
