# Multi-Character Campaign - TOR v1.1.2

Released: 1 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Fixed: battle takeover lifecycle

- Fixed taking control of a shared character's engaged party leaving Bannerlord with an incomplete player encounter.
- Version 1.1.1 used the low-level `PlayerEncounter.RestartPlayerEncounter` method. That method creates the encounter object and assigns the parties, but it does not run the native initialization that joins the player to the existing map event, opens the encounter menu, and establishes the normal cleanup lifecycle.
- Version 1.1.2 uses `EncounterManager.RestartPlayerEncounter(attackerParty, defenderParty)`, the high-level Bannerlord entry point used by ordinary campaign-map encounters.
- After the switch completes, the native attack/fight encounter window opens automatically on the next application tick. No additional click on the enemy party is required.
- Completing or leaving the battle now follows Bannerlord's normal encounter lifecycle, so the newly controlled party is not left unable to initiate later attacks.

## Added: combined takeover and reinforcement

- The battle alert now allows **Take control** and **Send the current party to reinforce** to be selected together.
- When both are selected, the original MainParty is captured before the switch, transferred to AI control, and ordered toward the selected battle after the switch transaction finishes.
- The reinforcement order is applied after the outgoing-party handoff sets its normal temporary hold state, preventing that handoff from erasing the order.
- Bannerlord 1.3.15's actual `SetMoveEngageParty(MobileParty, NavigationType)` signature is used with `NavigationType.Default`.
- The outgoing AI party's immediate behavior refresh is cleared after the engage order so the selected target remains authoritative.

## Unchanged

- The v1.1.1 independent-party treasury and excessive `Caravan and Party Income` fixes remain active.
- No recurring campaign-party scan, global hero scan, new serialized save key, or save migration was added.

## Validation

- Complete six-project Release build against Bannerlord 1.3.15 reference assemblies.
- CI verifies the high-level `EncounterManager.RestartPlayerEncounter(PartyBase attackerParty, PartyBase defenderParty)` API.
- CI verifies the two-parameter `MobileParty.SetMoveEngageParty(MobileParty party, NavigationType navigationType)` API and the `NavigationType.Default` enum value.
- The live encounter menu, battle completion, and AI reinforcement arrival require an in-game campaign test. Keep a backup save and use a new save slot for the first test.
