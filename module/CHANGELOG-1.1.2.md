## 1.1.2

- Fixed battle takeover leaving an incomplete `PlayerEncounter` active. Version 1.1.1 called the low-level `PlayerEncounter.RestartPlayerEncounter`, which creates an encounter object and assigns parties but does not run Bannerlord's native encounter initialization, join, menu, and cleanup lifecycle.
- Battle takeover now enters the existing map event through `EncounterManager.RestartPlayerEncounter(attackerParty, defenderParty)`. Bannerlord performs the native side join and immediately opens the correct attack/fight encounter window without requiring another map click.
- Prevented the incomplete encounter object from blocking the newly controlled party from initiating later encounters after the battle.
- The intervention inquiry now allows **Take control** and **Send the current party to reinforce** to be selected together.
- When both actions are selected, the original MainParty is captured before the character switch, handed to AI, and given its engage-party order after the switch transaction has applied its normal hold state. This prevents the handoff from erasing the reinforcement order.
- Corrected reinforcement movement for Bannerlord 1.3.15's actual two-parameter `SetMoveEngageParty(MobileParty, NavigationType)` API and explicitly uses `NavigationType.Default`.
- Keeps the issued AI reinforcement target from being immediately replaced by the outgoing party's post-switch behavior refresh.
- Existing finance fixes from v1.1.1 remain unchanged.
- No recurring campaign-party scan, new save key, or save migration was added.
