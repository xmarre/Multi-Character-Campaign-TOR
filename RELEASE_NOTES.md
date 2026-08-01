# Multi-Character Campaign - TOR v1.2.0

Released: 1 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

Validated source merge: `09c85968587f46fce403ddff7c3a6770bd073977`.

## Optional predicted-loss alerts

- Shared-character battle alerts can now be limited to battles that Bannerlord currently predicts the character's side will lose.
- The existing behavior remains the default: every eligible shared-character battle produces an alert.
- Change the policy from **Manage shared characters** using either:
  - **Battle alerts: notify only for predicted losses**
  - **Battle alerts: notify for every eligible battle**
- The selected policy is stored in the campaign save.
- Prediction uses Bannerlord 1.3.15's native side-relative strength result for the active `MapEvent`. It is a current forecast, not a guaranteed result.
- A forecast counts as a predicted loss only when the shared character's complete battle side has less native strength than the opposing side.
- If Bannerlord cannot provide a valid forecast, the alert is shown as a safety fallback.
- When another party joins the active map event, loss-only mode re-evaluates that exact battle. A newly unfavorable forecast can therefore produce an alert even when the fight originally looked favorable.

## Strength and troop composition

The alert now shows both battle sides' current:

- ready troop count;
- wounded troop count;
- native combat-strength estimate;
- infantry, ranged, cavalry, horse-archer, and TOR-specific formation composition;
- most numerous troop types.

Hovering either intervention action displays a longer troop breakdown. Counts aggregate all currently involved parties on each side, not only the endangered shared character's individual party.

## Existing intervention flow retained

- **Take control** still switches into the endangered character and opens Bannerlord's native attack/fight encounter immediately.
- **Send the current party to reinforce** remains available independently.
- Both actions can still be selected together, sending the original AI-controlled party toward the fight after control moves.
- The v1.1.2 encounter cleanup and post-battle attack fix remain unchanged.
- The v1.1.1 independent-party treasury fix remains unchanged.

## Performance and save scope

- Initial detection remains event-driven on map-event start.
- Forecast re-evaluation occurs only when another party joins that same map event.
- No recurring global party scan, hero scan, or campaign-tick prediction loop was added.
- One new boolean save value stores the selected alert policy. Existing saves default to alerting for every eligible battle.

## Validation

- Complete six-project Release build against Bannerlord 1.3.15 reference assemblies.
- CI verifies the exact native side-strength, side-party enumeration, troop-roster, formation-class, encounter, and reinforcement APIs used by the feature.
- The first live test should confirm the management-menu toggle, alert suppression in a clearly favorable battle, alert appearance in a clearly unfavorable battle, and the displayed TOR troop names/formations. Use a backup save and a new save slot for that first test.
