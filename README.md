# Multi-Character Campaign - TOR

Multi-Character Campaign support for **Mount & Blade II: Bannerlord 1.3.15** and **The Old Realms: War in the Mountains 1.16**.

The mod lets one campaign contain multiple persistent playable heroes. Registered characters can remain in the main party, lead independent player-clan parties, or become the active player character through the campaign management interface.

## v1.3.8

Version 1.3.8 fixes two TOR integration failures found in current 1.3.15/1.16 play:

- TOR missile AI can retain partially torn-down target state while a mission is being replaced, notably during the hideout-to-boss transition. MCC now guards the exact `MissileCastingBehavior.UpdateTarget(Target)` invalid-state boundary while leaving valid TOR targeting unchanged.
- Additional characters created with a race/culture different from the currently controlled hero could receive the previous hero's body/skeleton. MCC now commits Bannerlord's selected culture to the temporary active creation candidate before TOR performs its own native race/body initialization. Mixed-race shared campaigns are supported; MCC does not hard-code TOR race mappings.

The release retains the v1.3.7 WizardAI controller-transition repair, v1.3.6 NativeCreation snapshot fix, registered non-spellcaster TOR career abilities, first-spawn AI career prerequisites, Greater Harbinger controller safety, companion dialogue activation, career-button rebinding, Harmony 2.4.x loader compatibility, defensive-battle intervention, reinforcement orders, and settlement character switching.

See [`module/CHANGELOG-1.3.8.md`](module/CHANGELOG-1.3.8.md) for the current release and [`module/CHANGELOG.md`](module/CHANGELOG.md) for the retained historical changelog.

## Build

CI builds the complete six-project solution against the Bannerlord 1.3.15 reference assemblies, validates the exact movement, interaction, AI-lock, strength-query, map-event, tooltip, finance, encounter, inquiry, game-menu, character-creation culture, and runtime compatibility surfaces, and separately verifies full-solution compatibility with Lib.Harmony 2.4.2. Current release guards cover the NativeCreation mixed-race transaction, TOR missile transition safety, the v1.3.7 inherited `WizardAIComponent` agent-field path, and the v1.3.6 NativeCreation snapshot repair.

Most of the core source is reconstructed development source for the v1.0.41 runtime baseline. Its provenance and limitations are documented in the source directories and `module/SOURCE_INFO.md`.

## Installation

Extract the release archive into the Bannerlord installation directory so the module is placed at:

```text
Modules/MultiCharacterCampaignTOR
```

Delete an older `Modules/MultiCharacterCampaignTOR` folder before installing a new release.
