# Multi-Character Campaign - TOR

Multi-Character Campaign support for **Mount & Blade II: Bannerlord 1.3.15** and **The Old Realms: War in the Mountains 1.16**.

The mod lets one campaign contain multiple persistent playable heroes. Registered characters can remain in the main party, lead independent player-clan parties, or become the active player character through the campaign management interface.

## v1.3.1

Version 1.3.1 fixes settlement character switching:

- registered-character selection is deferred until Bannerlord's inquiry has fully closed;
- the identity switch no longer leaves the custom manager attached to stale settlement input state;
- successful settlement switches automatically return to the exact native village, town, castle, camp, or settlement menu;
- the **Wait here for some time** workaround is no longer required;
- map/Ctrl+R switching and all v1.3.0 battle-intervention features remain unchanged.

Version 1.3.0 added granular 0–100% battle-alert strength thresholds and persistent player/AI reinforcement travel to active battles.

See [`module/CHANGELOG-1.3.1.md`](module/CHANGELOG-1.3.1.md) for this release and [`module/CHANGELOG.md`](module/CHANGELOG.md) for earlier release history.

## Build

The maintained fixes are contained in the `IdentityGuard` sidecar. CI builds the complete six-project solution against the Bannerlord 1.3.15 reference assemblies, validates the exact movement, interaction, AI-lock, strength-query, map-event, tooltip, finance, encounter, inquiry, and game-menu surfaces, and runs a .NET Framework/Harmony patch-installation smoke test.

The remaining source is reconstructed development source for the v1.0.41 runtime baseline. Its provenance and limitations are documented in the source directories and `module/SOURCE_INFO.md`.

## Installation

Extract the release archive into the Bannerlord installation directory so the module is placed at:

```text
Modules/MultiCharacterCampaignTOR
```

Delete an older `Modules/MultiCharacterCampaignTOR` folder before installing a new release.
