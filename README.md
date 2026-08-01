# Multi-Character Campaign - TOR

Multi-Character Campaign support for **Mount & Blade II: Bannerlord 1.3.15** and **The Old Realms: War in the Mountains 1.16**.

The mod lets one campaign contain multiple persistent playable heroes. Registered characters can remain in the main party, lead independent player-clan parties, or become the active player character through the campaign management interface.

## v1.3.0

Version 1.3.0 expands and hardens shared-character battle intervention:

- replaces the binary every-battle/predicted-loss-only setting with any whole friendly-strength-share threshold from 0% to 100%;
- supports close and difficult predicted victories through practical thresholds such as 55%, 60%, 67%, and 75%;
- migrates existing saves automatically to 50% or 100%;
- continues to alert when Bannerlord cannot produce a valid forecast;
- fixes the standalone reinforcement order moving briefly and then stopping;
- routes reinforcements to the active battle site and invokes native interaction on arrival;
- prevents an AI-controlled outgoing party from selecting unrelated objectives during a combined takeover/reinforcement trip;
- releases every temporary AI lock when the order completes, becomes invalid, or the battle ends;
- checks only active reinforcement orders and adds no global party scan.

The compact selectable alert, native battle tooltip, immediate takeover encounter, independent-party treasury fix, post-battle encounter cleanup, and settlement-manager Return fix remain active.

See [`module/CHANGELOG-1.3.0.md`](module/CHANGELOG-1.3.0.md) for this release and [`module/CHANGELOG.md`](module/CHANGELOG.md) for earlier release history.

## Build

The maintained fixes are contained in the `IdentityGuard` sidecar. CI builds the complete six-project solution against the Bannerlord 1.3.15 reference assemblies, validates the exact movement, interaction, AI-lock, strength-query, map-event, tooltip, finance, and encounter surfaces, and runs a .NET Framework/Harmony patch-installation smoke test.

The remaining source is reconstructed development source for the v1.0.41 runtime baseline. Its provenance and limitations are documented in the source directories and `module/SOURCE_INFO.md`.

## Installation

Extract the release archive into the Bannerlord installation directory so the module is placed at:

```text
Modules/MultiCharacterCampaignTOR
```

Delete an older `Modules/MultiCharacterCampaignTOR` folder before installing a new release.
