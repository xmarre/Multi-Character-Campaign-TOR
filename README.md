# Multi-Character Campaign - TOR

Multi-Character Campaign support for **Mount & Blade II: Bannerlord 1.3.15** and **The Old Realms: War in the Mountains 1.16**.

The mod lets one campaign contain multiple persistent playable heroes. Registered characters can remain in the main party, lead independent player-clan parties, or become the active player character through the campaign management interface.

## v1.1.0

Version 1.1.0 adds event-driven defensive battle intervention:

- pauses and alerts when a registered shared character's independent party is attacked;
- can transfer control to that character and reopen the existing battle through Bannerlord's native player encounter;
- can order the current main party to move toward the endangered party as reinforcements;
- preserves physical party state, army membership, ships, troops, prisoners, ownership, and the shared-inventory handoff invariants;
- adds no campaign-wide recurring scans.

See [`module/README.md`](module/README.md) for the full feature and compatibility documentation and [`module/CHANGELOG.md`](module/CHANGELOG.md) for release history.

## Build

The maintained v1.1.0 change is the `IdentityGuard` sidecar. CI builds it against the Bannerlord 1.3.15 reference assemblies and validates the exact map-event and player-encounter method signatures used by the intervention path.

The remaining source is reconstructed development source for the v1.0.41 runtime baseline. Its provenance and limitations are documented in the source directories and `module/SOURCE_INFO.md`.

## Installation

Extract the release archive into the Bannerlord installation directory so the module is placed at:

```text
Modules/MultiCharacterCampaignTOR
```

Delete an older `Modules/MultiCharacterCampaignTOR` folder before installing a new release.
