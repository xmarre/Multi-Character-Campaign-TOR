# Multi-Character Campaign - TOR v1.3.2

Released: 13 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

Validated crash-fix merge: `0b57fb6311cb200c081dc4a689f6a6a95aea348d`.

## Fixed new-campaign/session-launch crash with Harmony 2.4.2

Two independent community crash reports reproduced the same failure while entering a new campaign world on Bannerlord 1.3.15. Both reports had Bannerlord.Harmony / `0Harmony` 2.4.2 loaded and failed from `MultiCharacterCampaignTOR.CareerUniquesBridge.LogStatus()` with `ArgumentNullException: type` at `Activator.CreateInstance(Type, ...)`.

MCC was resolving `HarmonyLib.Harmony` and `HarmonyLib.HarmonyMethod` with assembly-qualified `Type.GetType("..., 0Harmony")` string lookups. On the affected loader/runtime path those lookups returned `null` even though Harmony was loaded. `HarmonyBridge.TryInstall()` silently skipped MCC's main Harmony compatibility patches when that happened, then `CareerUniquesBridge.LogStatus()` repeated the same lookup without a null guard and aborted `OnSessionLaunched`.

Version 1.3.2 now:

- resolves `Harmony` and `HarmonyMethod` through MCC's existing linked `0Harmony` reference instead of assembly-name string lookup;
- constructs the Harmony instance from that linked type, allowing MCC's core Harmony compatibility patches to install on the affected runtime path;
- guards the auxiliary TOR `AbilityManagerMissionLogic.OnBehaviorInitialize` refresh hook against missing or renamed reflected surfaces;
- contains unexpected failures from that optional refresh hook so they are logged without aborting campaign startup;
- keeps the existing optional TOR Career Uniques runtime refresh behavior unchanged when that mod is installed.

The supplied current `TOR_Core` surface still contains `TOR_Core.AbilitySystem.AbilityManagerMissionLogic.OnBehaviorInitialize`, so a missing ToR method was ruled out as the reported crash source.

## Save, behavior, and performance scope

- No save values or migration were added.
- Existing saves remain compatible.
- Character switching, remote-party transactions, party ownership, finance, career selection, battle intervention, reinforcement travel, settlement handling, and campaign-map behavior are unchanged.
- No recurring scan or campaign-tick work was added.

## Validation

- Existing Bannerlord 1.3.15 full build/API validation passed.
- Existing .NET Framework/Harmony runtime patch-installation smoke test passed.
- A new full-solution compatibility build against Lib.Harmony 2.4.2 passed, including a regression guard that rejects the brittle string-based Harmony bootstrap.
- The existing build path against Lib.Harmony 2.3.3 remains covered, preserving the older supported compile/runtime surface.
- The maintainer installation does not reproduce this loader-specific crash, so validation is based on the two matching community crash stacks, the exact failing code path, and dual-Harmony CI coverage. Community confirmation on an affected installation remains useful.
