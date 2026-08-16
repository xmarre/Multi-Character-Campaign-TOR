# Multi-Character Campaign - TOR v1.3.4

Released: 16 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Fixed in-campaign character creation on the affected Harmony 2.4 loader path

Community retesting of v1.3.3 showed that campaign startup, Ctrl+R, career buttons, and AI career abilities were working, but `Create a new playable character` could still fail on the same loader/runtime family that originally exposed the Harmony 2.4 startup problem.

The remaining NativeCreation wrapper still resolved `Harmony` and `HarmonyMethod` through assembly-qualified `Type.GetType("..., 0Harmony")` calls when the creation flow was first opened. Version 1.3.4 removes that dependency: NativeCreation now references the runtime `0Harmony` assembly directly and binds `Harmony` / `HarmonyMethod` through the linked types, matching the proven core bootstrap fix.

The same tester's log also showed a clear lifecycle distinction: the reconstructed `CareerAbilityRepair` failed when attempted during early `OnSubModuleLoad`, then installed successfully later during `OnGameStart` on the same installation. Those reconstructed campaign/runtime repairs are therefore no longer installed during the early module-loader phase. They now install at `OnGameStart`, before campaign missions can use them.

## Fixed AI Necromancer Greater Harbinger stealing player control

The v1.3.3 AI-career feature correctly allowed registered shared Necromancers to use Greater Harbinger, but TOR's native `SummonChampionScript` is written for the player career path. Its controller transition explicitly transfers player control to the summoned Harbinger and fades the player camera.

For a registered shared hero that is currently AI-controlled, v1.3.4 now suppresses that player-only controller transition. The AI Necromancer and summoned Harbinger remain AI-controlled and the actual player's controlled agent and camera are left untouched.

The native Greater Harbinger controller-switching behavior remains unchanged when the active player character casts it.

## Fixed companion activation through dialogue

The management-menu companion activation path already worked, but the MCC dialogue option used a reconstructed combination of `CompanionOf` and party membership that did not match Bannerlord 1.3.15's own companion-dialogue eligibility rules on the affected setup.

For unregistered companions, MCC now uses Bannerlord's native `HeroHelper.IsCompanionInPlayerParty(...)` predicate. Already registered shared heroes retain MCC's existing physical MainParty requirement for dialogue switching.

This restores the `[Multi-Character Campaign] Take control of this character.` dialogue route without changing the menu-based activation path.

## Confirmed v1.3.3 AI career support

Community testing of v1.3.3 covered the available TOR career abilities and confirmed that AI-controlled shared characters retain and use their career abilities successfully across the tested careers. The Greater Harbinger controller transfer above was the one identified player-control integration issue from that test pass.

The previously reported TOR party-screen career-button issue is also confirmed fixed by the same tester.

## Save, behavior, and performance scope

- Existing saves remain compatible; no save migration is required.
- No recurring campaign-map scan, global hero scan, global party scan, or recurring campaign reconciliation was added.
- NativeCreation's Harmony binding is resolved directly from its linked dependency rather than through repeated runtime lookup.
- The Greater Harbinger compatibility guard runs only on TOR's existing controller-transition methods for that ability.
- Companion dialogue eligibility is evaluated only when Bannerlord evaluates the conversation line.

## Validation

The release is gated by:

- Bannerlord 1.3.15 full build/API validation;
- full-solution Lib.Harmony 2.4.2 compatibility build;
- the existing Lib.Harmony 2.3.3 build/runtime surface;
- runtime patch-installation smoke coverage;
- regression guards rejecting the loader-sensitive Harmony lookup from NativeCreation and enforcing the corrected runtime-repair lifecycle.

The affected community installation remains the authoritative runtime confirmation for the loader-specific in-campaign creation fix, while the automated gates verify the exact build, patch, API, and packaging surfaces used by the release.
