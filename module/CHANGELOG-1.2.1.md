## 1.2.1
- Fixed the v1.2.0 battle intervention window hiding its selectable takeover and reinforcement rows after the embedded troop roster text exceeded the inquiry layout.
- Removed the full troop-roster dump from the inquiry body. The alert is compact again and always leaves the two selectable actions and Apply button in their normal usable positions.
- Hovering either enabled intervention action now opens Bannerlord 1.3.15's native `MapEvent` troop tooltip through `InformationManager.ShowTooltip`, using the same native battle-side and troop presentation path instead of a custom text imitation.
- Disabled actions retain their specific ordinary reason tooltip rather than opening the battle tooltip.
- Preserved predicted-loss filtering, side-strength calculation, late-reinforcement reevaluation, duplicate suppression, takeover, reinforcement, and combined takeover-plus-reinforcement behavior.
- Fixed opening **Manage shared characters** from a village, town, castle, camp, or settlement and then selecting **Return** leaving the active party settlement-bound while the campaign map was shown without a controllable party.
- The manager now records the exact menu it was opened from and returns to that menu. Opening it with Ctrl+R from the unobstructed campaign map still returns directly to the campaign map.
- Added no recurring party scans, tooltip polling, campaign reconciliation, or new save data.
