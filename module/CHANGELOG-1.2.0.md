## 1.2.0

- Added an optional **predicted losses only** policy for shared-character battle alerts. The existing alert-every-battle behavior remains the default.
- Added persistent management-menu controls to switch between **notify for every eligible battle** and **notify only for predicted losses**. The choice is stored in the campaign save.
- Uses Bannerlord 1.3.15's native `MapEvent.GetStrengthsRelativeToParty` result for the current battle sides instead of a separate troop-count heuristic.
- Treats a side as predicted to lose only when its current native side strength is lower than the opposing side. Even/favorable forecasts are suppressed in loss-only mode.
- Preserves a safety fallback: when Bannerlord cannot provide a valid strength forecast, the alert is still shown rather than silently hiding a potentially dangerous battle.
- Re-evaluates the exact active map event when another party joins. If reinforcements change a previously favorable battle into a predicted loss, the shared-character alert is queued at that point.
- Added current friendly and opposing side details directly to the alert: ready troops, wounded troops, native side strength, formation composition, and the most numerous troop types.
- Added a longer troop breakdown to both action tooltips, aggregating every involved party on each battle side rather than showing only the shared character's individual party.
- Retained the v1.1.2 combined takeover/reinforcement actions and immediate native encounter window unchanged.
- Kept detection event-driven. No recurring global party scan, hero scan, or campaign-tick forecast loop was added.
