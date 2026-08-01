# Multi-Character Campaign - TOR v1.1.0

Released: 1 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Added

- Immediate paused alert when a registered shared character's independent player-clan party is attacked as the defender.
- **Take control and continue the battle** transfers the active player identity into that existing party and reopens the same map event through Bannerlord's native `PlayerEncounter` path.
- **Move the current party to reinforce** orders the current MainParty toward the endangered party without teleportation.
- **Dismiss** leaves the battle simulation unchanged.

## Safety and performance

- Takeover is permitted only for the exact still-active defensive map event that raised the alert.
- Blocks stale events, sieges, raids, settlement and port transitions, invalid ownership, and conflicting player encounters.
- Resets queued alert state when the campaign instance changes.
- Rolls `PlayerTroop` back if Bannerlord fails before rebinding `MainParty` during the remote identity transaction.
- Event-driven detection; no global campaign-party scan or recurring reconciliation.

## Validation

- Built against Bannerlord 1.3.15 reference assemblies.
- Verifies the exact runtime signatures for `OnMapEventStarted`, `OnMapEventEnded`, `RestartPlayerEncounter`, `Init`, and `Start` in CI.
- Packages the complete six-assembly module from the reconstructed v1.0.41 source baseline plus the maintained v1.1.0 intervention changes.

The new battle-transfer path has not been exercised in a live Bannerlord campaign in this environment. Keep a backup save and initially save to a new slot after testing a takeover.
