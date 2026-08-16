# Multi-Character Campaign - TOR v1.3.3

Released: 16 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## AI-controlled shared heroes can use their TOR career abilities

Registered MCC heroes now retain and use their own TOR career ability while another shared character is player-controlled.

The compatibility layer now:

- creates the correct TOR `CareerAbility` for each registered shared AI hero;
- keeps career ownership and career-choice lookups bound to that hero instead of the current `Hero.MainHero`;
- allows TOR's existing Wizard AI to evaluate and cast the ability while preserving normal cooldown, charge, weapon/mount, routed/dead, and mission-limit checks;
- credits direct damage dealt, damage taken, and kill charge to the correct AI hero's career ability;
- rebuilds TOR casting behavior state across AI/player controller handoffs so an AI cannot keep autonomously casting after MCC gives that hero to the player;
- restores the normal player ability ordering when the hero becomes player-controlled.

TOR does not provide a native Wizard-AI mapping for `CareerAbilityEffect`, so v1.3.3 also handles the career-specific cases that cannot safely use TOR's generic missile fallback. This includes local/self career abilities, Fey Paths, Greater Harbinger, targeted ground abilities, and moving career projectiles. AI Grail Damsel teleportation is also prevented from fading the actual player's camera.

## Fixed remaining Harmony-loader regressions from v1.3.2

The v1.3.2 core startup-crash fix was correct, but community retesting on the affected loader path exposed older assembly-qualified Harmony lookups in reconstructed auxiliary MCC components.

These stale lookups affected:

- `CareerAbilityRepair`, producing `Harmony 0Harmony assembly is unavailable` in the log;
- IdentityGuard initialization before `CampaignMapHotkey`, leaving Ctrl+R unavailable;
- the NativeCreation bridge used by `Create a new playable character`.

RuntimeCompatibility is the first MCC submodule loaded and already links the runtime `0Harmony` assembly directly. It now establishes that linked assembly as the resolver for the legacy auxiliary lookup shape before later MCC sidecars initialize. Resolver validation is failure-contained and cannot abort campaign startup.

This fixes the common loader-context root cause for CareerAbilityRepair, Ctrl+R, NativeCreation, and other reconstructed auxiliary patch installers that use the same legacy lookup form.

## Fixed stale TOR career buttons after character switches

TOR registers the party-screen career-button delegates when its `PartyCharacterVMExtension` is constructed. MCC can switch `Hero.MainHero` while the same party-screen VM remains alive, leaving the button handler and displayed state bound to the previous character.

Version 1.3.3 now rebinds TOR's current career button on MCC's existing `TORBridge.RefreshAfterSwitch()` path and refreshes an already-open party screen through TOR's own `PartyVMExtension.ViewModelInstance.RefreshValues()` mechanism.

This covers both reported cases:

- Mercenary -> Waywatcher no longer retains the previous Mercenary button state;
- a character whose career has no TOR party button, such as Grey Lord, no longer prevents a later Waywatcher from receiving the Waywatcher button after switching.

## Save, behavior, and performance scope

- Existing saves remain compatible; no save migration was added.
- No recurring campaign-map scan, global hero scan, global party scan, or new campaign tick work was added.
- The Harmony compatibility resolver runs once during RuntimeCompatibility initialization.
- Career-button rebinding runs only on MCC's existing TOR identity/career refresh path.
- AI career support is mission/event driven and limited to registered MCC shared heroes.

## Validation

The release is gated by:

- Bannerlord 1.3.15 full build/API validation;
- full-solution Lib.Harmony 2.4.2 compatibility build;
- the existing Lib.Harmony 2.3.3 build/runtime surface;
- runtime patch-installation smoke coverage;
- an expanded Harmony regression guard covering the first-loaded auxiliary resolver/load-order invariant.

The affected community loader path remains the most valuable runtime confirmation for Ctrl+R, in-campaign character creation, and the auxiliary Harmony resolution fix.
