# Multi-Character Campaign - TOR

Multi-Character Campaign support for **Mount & Blade II: Bannerlord 1.3.15** and **The Old Realms: War in the Mountains 1.16**.

The mod lets one campaign contain multiple persistent playable heroes. Registered characters can remain in the main party, lead independent player-clan parties, or become the active player character through the campaign management interface.

## v1.2.1

Version 1.2.1 fixes the two v1.2.0 UI/lifecycle regressions:

- restores the selectable takeover and reinforcement rows in the shared-character battle alert;
- removes the oversized custom roster dump from the inquiry body;
- opens Bannerlord's native `MapEvent` battle troop tooltip when either enabled action is hovered;
- keeps the compact native strength prediction visible in the alert;
- returns from **Manage shared characters** to the exact village, town, castle, camp, or settlement menu from which it was opened;
- retains normal direct map return when the manager was opened with Ctrl+R;
- adds no recurring scans, tooltip polling, save migration, or new save data.

The optional predicted-loss-only alert policy, combined takeover and reinforcement flow, encounter cleanup, and independent-party treasury fix remain active.

See [`module/CHANGELOG-1.2.1.md`](module/CHANGELOG-1.2.1.md) for this release and [`module/CHANGELOG.md`](module/CHANGELOG.md) for earlier release history.

## Build

The maintained fixes are contained in the `IdentityGuard` sidecar. CI builds the complete six-project solution against the Bannerlord 1.3.15 reference assemblies, validates the exact tooltip, hint-hover, map-event, game-menu, finance, and player-encounter surfaces, and runs a .NET Framework/Harmony patch-installation smoke test.

The remaining source is reconstructed development source for the v1.0.41 runtime baseline. Its provenance and limitations are documented in the source directories and `module/SOURCE_INFO.md`.

## Installation

Extract the release archive into the Bannerlord installation directory so the module is placed at:

```text
Modules/MultiCharacterCampaignTOR
```

Delete an older `Modules/MultiCharacterCampaignTOR` folder before installing a new release.
