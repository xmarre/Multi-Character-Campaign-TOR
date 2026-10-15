## v1.3.9

- Guarded TOR `MindControlScript.OnAfterTick` for AI-controlled registered MCC shared heroes so an absent, inactive, or teamless caster cannot enter unchecked enemy-target selection. The script is stopped at that boundary rather than permitting a null caster/team dereference.
- Reconciled Mind Control's private caster cache against `AbilityScript.CasterAgent` on valid casts. The fix preserves TOR's targeting, career keystones, success/miss behavior, and spell effects when caster state is valid.
- Restricted the patch to registered shared-character career owners. Unrelated TOR heroes, normal caster behavior, and other ability scripts remain on their original paths; the general career-script Harmony finalizer still restores its context and propagates unrelated exceptions.
- Added compiled runtime-smoke regression tests exercising the actual Harmony prefix with valid, missing, removed, and teamless casters, cache reconciliation, and the unregistered-TOR-owner bypass.
- Retained the v1.3.8 mixed-race creation and missile transition fixes, v1.3.7 WizardAI repair, and the previous six-project / Harmony compatibility checks.
- Existing saves remain compatible. No save migration, campaign-map scans, periodic mission polling, or AI career-choice changes.
- The reported siege crash is addressed at its identified invalid caster-lifecycle boundary. Large-battle in-game confirmation remains necessary; separate reported freezes and native access violations are not attributed to this fix.
