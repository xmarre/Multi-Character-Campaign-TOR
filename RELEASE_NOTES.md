# Multi-Character Campaign - TOR v1.3.0

Released: 2 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Granular battle-alert threshold

The previous binary choice has been replaced with one configurable threshold from 0% to 100%.

The value is the maximum share of the battle's total native strength that the shared character's complete side may have before an alert is suppressed. Examples:

- **50%**: predicted defeats and approximately even battles;
- **55%**: razor-close predicted victories and worse;
- **60%**: difficult victories with substantial casualty risk and worse;
- **67%**: alerts whenever the enemy retains at least roughly half of your side's strength;
- **75%**: broad intervention coverage;
- **100%**: every eligible battle.

Any whole percentage from 0 to 100 is accepted. The setting is available through **Manage shared characters > Configure battle alert threshold** and is stored in the campaign save.

This is deliberately described as a strength-share threshold rather than a casualty prediction. Bannerlord exposes a current side-relative combat-strength estimate, not a guaranteed casualty count. The 55–65% range is therefore a practical close/difficult-victory heuristic.

Existing saves migrate automatically: the old predicted-loss-only policy becomes 50%, while the old every-battle policy becomes 100%. Prediction failures still show an alert as a safety fallback. The configured threshold is re-evaluated when another party joins an active battle.

## Fixed reinforcement travel

The earlier reinforcement action used `SetMoveEngageParty` against the shared party already participating in the battle. An in-battle party is not a normal visible campaign target, so the movement order could be cancelled almost immediately, producing the reported short movement followed by a stop.

Version 1.3.0 instead:

- moves the reinforcing party to the active battle site;
- invokes Bannerlord's native party interaction when it reaches the native encounter distance;
- keeps the route while the selected battle remains active;
- permits the player to replace the movement order normally;
- stops and reports the order if the battle ends before arrival.

For a combined **take control + reinforce** action, the original party becomes AI-controlled after the handoff. It receives a temporary no-new-decisions lock tied only to that reinforcement order. The route is restored if ordinary AI processing replaces it, and the lock is released as soon as the party joins, the battle ends, the order becomes invalid, or the campaign changes. The party should therefore not select unrelated objectives on the way to the chosen battle.

## Existing intervention behavior retained

- The compact selectable alert and Bannerlord-native battle tooltip remain in place.
- Takeover still opens the existing fight encounter immediately.
- Takeover and reinforcement can still be selected together.
- Predicted-strength filtering remains event-driven.
- The independent-party treasury fix, encounter cleanup, post-battle attack fix, and settlement-manager Return fix remain unchanged.

## Performance and save scope

- No global party or hero scan was added.
- Only currently active reinforcement orders are checked, at a throttled interval.
- Two save values store and initialize the new threshold.
- No physical party, troop, prisoner, inventory, ship, or settlement ownership is transferred by the reinforcement system.

## Validation

- Complete six-project Release build against Bannerlord 1.3.15 reference assemblies.
- Exact native movement, party-interaction, AI-decision-lock, encounter-distance, strength-query, text-inquiry, and map-event callback surfaces are checked by CI.
- The .NET Framework/Harmony smoke runner loads the built module and installs both new runtime patches.
- Live campaign testing remains required for Gauntlet input rendering, player-party arrival interaction, and the AI-controlled outgoing party's full trip to a real battle.
