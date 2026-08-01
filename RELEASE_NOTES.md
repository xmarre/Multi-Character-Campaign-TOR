# Multi-Character Campaign - TOR v1.2.1

Released: 1 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Fixed: unusable battle intervention alert

- Version 1.2.0 placed a full custom troop roster inside the multi-selection inquiry body. In sufficiently varied TOR battles, that text consumed the inquiry layout and pushed the two selectable action rows out of the usable window.
- Version 1.2.1 removes the roster dump from the inquiry body. The alert is compact and again exposes both selectable actions:
  - **Take control and continue the battle**
  - **Send the current party to reinforce**
- The existing **Apply selected actions** button works with either action or both actions together.
- The predicted result and the two native side-strength values remain visible in the compact alert.

## Native Bannerlord troop tooltip

- Hover either enabled intervention action to open Bannerlord's native `MapEvent` battle tooltip.
- The hotfix invokes Bannerlord 1.3.15's registered tooltip presenter with the active map event instead of recreating its troop viewer in inquiry text.
- The native tooltip owns battle-side aggregation, party and hero presentation, troop rows, wounded information, hidden-information rules, and TOR troop/formation data.
- Disabled actions continue to show their specific reason for being unavailable.
- The native tooltip is closed when the hover ends, the inquiry closes, another alert opens, or the campaign changes.

## Fixed: Return from settlement manager

- Opening **Manage shared characters** from a village, town, castle, camp, or settlement and selecting **Return** no longer drops directly to the campaign map while the active party remains settlement-bound and invisible.
- The manager records the exact source menu and returns to it, preserving Bannerlord's normal settlement lifecycle.
- Opening the manager with **Ctrl+R** from the unobstructed campaign map still returns directly to the campaign map.
- No character switch is required to restore the active party.

## Existing behavior retained

- Optional predicted-loss-only alerts remain available and save normally.
- Late battle reinforcements still trigger event-driven forecast reevaluation.
- Takeover, reinforcement, combined takeover-plus-reinforcement, encounter cleanup, and the independent-party treasury fix remain unchanged.
- No recurring party scans, tooltip polling, campaign reconciliation, or new save data were added.

## Validation

- Complete six-project Release build against Bannerlord 1.3.15 reference assemblies: zero warnings and zero errors.
- CI verifies the exact native tooltip, hint-hover, map-event, side-strength, encounter, reinforcement, and game-menu APIs used by the hotfix.
- A .NET Framework/Harmony smoke runner loads the built module and confirms installation of the prediction settings, prediction flow, priority bridge, compact alert/native-tooltip bridge, and manager-return bridge.
- Actual Gauntlet rendering and settlement-menu behavior require the first in-game test. Delete the old module folder before installing, back up the save, and use a new save slot for that test.
