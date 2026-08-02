# Multi-Character Campaign - TOR v1.3.1

Released: 2 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

Validated source merge: `cc2577294cf9f111f60cf38bc1fa6faa8acb0d78`.

## Fixed settlement character-switch lock

Switching to another registered shared character from the MCC manager while inside a settlement could leave the custom manager attached to a stale Bannerlord settlement/menu input context. The only practical recovery was to enter **Wait here for some time**, which forced the native settlement flow to rebuild its state.

The switch callback was running synchronously while Bannerlord's character-selection inquiry was still completing its affirmative-action teardown. `ChangePlayerCharacterAction.Apply` could therefore replace the active campaign identity before the inquiry and settlement menu lifecycle had finished.

Version 1.3.1 now:

- intercepts only registered-character selections made from the MCC manager while the main party is inside a settlement;
- defers the original selection callback for two application ticks so the inquiry closes first;
- replays the existing switch transaction unchanged;
- exits the custom manager on the following tick after a successful switch;
- uses the existing `ManagerReturnHotfix` to return to the exact captured village, town, castle, camp, or settlement menu;
- removes the need to use the waiting option to recover controls.

Campaign-map/Ctrl+R switching, companion registration, career selection, battle takeover, reinforcement travel, finance handling, native troop tooltips, and granular alert thresholds are unchanged.

## Performance and save scope

- No recurring party, hero, settlement, or menu scan was added.
- The fix is active only while one deferred settlement switch is pending.
- No new save values were added.
- Existing saves remain compatible.

## Validation

- Complete six-project Release build against Bannerlord 1.3.15 reference assemblies: zero warnings and zero errors.
- The .NET Framework/Harmony smoke runner loaded the built module and installed `SettlementCharacterSwitchMenuFix` successfully.
- Pre-release CI artifact: `full-module-v1.3.0`, artifact ID `8827344019`, digest `05c5fb2594ad933bfa7e4f6da055eaf95ebf60c780d6a5d22d9e3e8dd77151b5`.
- Live testing remains required for the rendered selection inquiry and real village, town, and castle menu transitions.
