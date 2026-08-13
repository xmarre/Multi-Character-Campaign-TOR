# Multi-Character Campaign - TOR

Multi-Character Campaign support for **Mount & Blade II: Bannerlord 1.3.15** and **The Old Realms: War in the Mountains 1.16**.

The mod lets one campaign contain multiple persistent playable heroes. Registered characters can remain in the main party, lead independent player-clan parties, or become the active player character through the campaign management interface.

## v1.3.2

Version 1.3.2 fixes a community-reported crash while entering a new campaign world with current Bannerlord.Harmony / Lib.Harmony 2.4.2.x:

- Harmony bootstrap now resolves `Harmony` and `HarmonyMethod` through MCC's linked `0Harmony` reference instead of brittle assembly-qualified string lookup;
- MCC's core Harmony compatibility patches no longer get silently skipped on the affected loader/runtime path;
- the auxiliary TOR active-equipment refresh hook is guarded so a missing reflected TOR surface cannot abort campaign startup;
- full-solution CI now covers both the existing Harmony 2.3.3 path and Harmony 2.4.2, plus the existing runtime patch-installation smoke test;
- save data, character switching, party handling, finance, career selection, battle intervention, reinforcement, and campaign-map behavior are unchanged.

Version 1.3.1 fixed settlement character switching, including deferred inquiry teardown and automatic return to the exact native settlement menu.

See [`module/CHANGELOG-1.3.2.md`](module/CHANGELOG-1.3.2.md) for this release and [`module/CHANGELOG.md`](module/CHANGELOG.md) for earlier release history.

## Build

CI builds the complete six-project solution against the Bannerlord 1.3.15 reference assemblies, validates the exact movement, interaction, AI-lock, strength-query, map-event, tooltip, finance, encounter, inquiry, and game-menu surfaces, runs a .NET Framework/Harmony patch-installation smoke test, and separately verifies full-solution compatibility with Lib.Harmony 2.4.2.

Most of the core source is reconstructed development source for the v1.0.41 runtime baseline. Its provenance and limitations are documented in the source directories and `module/SOURCE_INFO.md`.

## Installation

Extract the release archive into the Bannerlord installation directory so the module is placed at:

```text
Modules/MultiCharacterCampaignTOR
```

Delete an older `Modules/MultiCharacterCampaignTOR` folder before installing a new release.
