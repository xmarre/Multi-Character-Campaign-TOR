# Multi-Character Campaign - TOR

Multi-Character Campaign support for **Mount & Blade II: Bannerlord 1.3.15** and **The Old Realms: War in the Mountains 1.16**.

The mod lets one campaign contain multiple persistent playable heroes. Registered characters can remain in the main party, lead independent player-clan parties, or become the active player character through the campaign management interface.

## v1.3.7

Version 1.3.7 fixes the TOR 1.16 AI career controller-transition guard failing to install because `WizardAIComponent.Agent` was reflected as a property. TOR exposes the agent through the inherited Bannerlord `AgentComponent.Agent` field; MCC now resolves that field through the component hierarchy and restores the intended AI/player controller-transition protection.

It also includes the v1.3.6 root fix for in-campaign **Create a new playable character**, where a decompiler-corrupted NativeCreation snapshot helper attempted to cast clan member-name strings to `FieldInfo` before TOR's native creation state could open.

Additional current compatibility work includes registered non-spellcaster TOR career abilities, first-spawn AI career prerequisites, Greater Harbinger controller safety, companion dialogue activation, career-button rebinding, Harmony 2.4.x loader compatibility, defensive-battle intervention, reinforcement orders, and settlement character switching.

See [`module/CHANGELOG-1.3.7.md`](module/CHANGELOG-1.3.7.md) for the current release and [`module/CHANGELOG.md`](module/CHANGELOG.md) for the retained historical changelog.

## Build

CI builds the complete six-project solution against the Bannerlord 1.3.15 reference assemblies, validates the exact movement, interaction, AI-lock, strength-query, map-event, tooltip, finance, encounter, inquiry, and game-menu surfaces, and separately verifies full-solution compatibility with Lib.Harmony 2.4.2. Current release guards also validate the NativeCreation snapshot repair and the TOR 1.16 inherited `WizardAIComponent` agent-field access path.

Most of the core source is reconstructed development source for the v1.0.41 runtime baseline. Its provenance and limitations are documented in the source directories and `module/SOURCE_INFO.md`.

## Installation

Extract the release archive into the Bannerlord installation directory so the module is placed at:

```text
Modules/MultiCharacterCampaignTOR
```

Delete an older `Modules/MultiCharacterCampaignTOR` folder before installing a new release.
