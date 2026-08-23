# Multi-Character Campaign - TOR v1.3.8

Released: 23 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Fixed hideout-to-boss TOR missile-AI transition crash

The TOR 1.16 runtime was inspected directly for the reported crash in `MissileCastingBehavior.UpdateTarget(Target)`.

The native method dereferences `CurrentTarget.Formation` before checking whether mission teardown has already invalidated that target state, and later dereferences the incoming target as well. During mission replacement, including the hideout-to-boss transition, those references can disappear before the casting behavior itself is discarded.

v1.3.8 adds a narrowly scoped compatibility guard to that exact method. If `CurrentTarget`, `CurrentTarget.Formation`, or the incoming `Target` is already null, MCC returns the incoming target unchanged and skips the invalid native update. Valid target state continues through TOR's original implementation.

This is not a blanket exception handler and does not replace TOR missile selection, line-of-sight, aiming, or ordinary battle behavior.

## Fixed mixed-race additional character creation

Additional playable characters are not restricted to the campaign founder's race. TOR's own character-creation handler supports culture-driven race and body initialization.

The failure came from an ordering difference in MCC's in-campaign NativeCreation transaction:

- Bannerlord's `CharacterCreationContent.SetSelectedCulture(...)` records the selected culture.
- Bannerlord normally applies `Hero.MainHero.Culture = SelectedCulture` later from the normal campaign-start finalization path.
- MCC intentionally suppresses that full finalization path because it also mutates existing clan, campaign, and map state.
- TOR's `OnCultureSelected()` runs earlier and determines race/body data from the active player's current culture.
- The temporary MCC candidate therefore still exposed the previous active hero's culture when TOR initialized its race. Same-race creation hid the stale value; Human-to-Dawi, Human-to-Greenskin, and other cross-race combinations exposed it as the wrong body/skeleton.

v1.3.8 commits only the already-selected culture to the temporary active creation candidate before TOR's native race/body callback runs. TOR remains responsible for the actual race mapping, body properties, equipment, and character-creation content.

The fix does not run Bannerlord's complete `ApplyFinalEffects()`, change the persistent player-clan culture, hard-code TOR race IDs, or replay new-campaign initialization.

## Save and performance scope

- Existing saves remain compatible; no migration is required.
- The mixed-race repair runs only when the player selects a culture during an active MCC NativeCreation session.
- The missile guard performs only constant-time target-state checks inside TOR's existing missile target update.
- No campaign-map scans, global hero/party scans, mission-tick polling, or recurring reconciliation were added.

## Validation

v1.3.8 is gated by:

- Bannerlord 1.3.15 full build/API validation;
- Lib.Harmony 2.3.3 build and runtime-install smoke validation;
- full-solution Lib.Harmony 2.4.2 compatibility validation;
- direct Bannerlord member-shape validation for `CharacterCreationContent.SelectedCulture`, deferred `ApplyCulture`/`ApplyFinalEffects`, and `Hero.Culture`;
- dedicated regression guards requiring the MCC NativeCreation candidate/MainHero identity check and narrow culture assignment;
- dedicated regression guards requiring the TOR `CurrentTarget`, `Formation`, and incoming-target transition checks without broad `NullReferenceException` suppression;
- the retained v1.3.7 WizardAI inherited-agent regression, v1.3.6 NativeCreation snapshot regression, and existing battle-intervention, reinforcement, settlement-switch, career, and package-output checks.

Real in-game rendering and mission transitions remain the final runtime validation surface: cross-race creation should be checked with Human/Dawi/Greenskin combinations, and the hideout-to-boss transition should be exercised alongside a normal field battle to confirm unchanged stable-state missile AI.
