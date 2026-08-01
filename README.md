# Multi-Character Campaign - TOR

Multi-Character Campaign support for **Mount & Blade II: Bannerlord 1.3.15** and **The Old Realms: War in the Mountains 1.16**.

The mod lets one campaign contain multiple persistent playable heroes. Registered characters can remain in the main party, lead independent player-clan parties, or become the active player character through the campaign management interface.

## v1.1.1

Version 1.1.1 fixes shared-party finance and expands battle intervention:

- keeps the active player's shared denars separate from independent shared-character party treasuries;
- prevents a mirrored player wallet from being converted into enormous `Caravan and Party Income`;
- preserves normal mercenary contract pay, workshops, caravans, legitimate party profits, wages, and garrison expenses;
- alerts when a registered shared-character party joins an already-running AI battle;
- supports intervention on either battle side, including helper parties temporarily attached to an AI side leader;
- preserves the existing physical-party, inventory, ownership, encounter, settlement, transition, siege, and rollback invariants;
- adds no campaign-wide recurring scans.

See [`module/README.md`](module/README.md) for the full feature and compatibility documentation, [`module/CHANGELOG-1.1.1.md`](module/CHANGELOG-1.1.1.md) for this release, and [`module/CHANGELOG.md`](module/CHANGELOG.md) for earlier release history.

## Build

The maintained fixes are contained in the `IdentityGuard` sidecar. CI builds the complete six-project solution against the Bannerlord 1.3.15 reference assemblies and validates the exact finance, map-event, party-join, and player-encounter method signatures used by v1.1.1.

The remaining source is reconstructed development source for the v1.0.41 runtime baseline. Its provenance and limitations are documented in the source directories and `module/SOURCE_INFO.md`.

## Installation

Extract the release archive into the Bannerlord installation directory so the module is placed at:

```text
Modules/MultiCharacterCampaignTOR
```

Delete an older `Modules/MultiCharacterCampaignTOR` folder before installing a new release.
