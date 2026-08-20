# Multi-Character Campaign - TOR v1.3.7

Released: 20 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Fixed TOR 1.16 AI career transition guard installation

A v1.3.5 user report included a secondary startup warning:

`AI career-ability transition guard installation failed safely: ... WizardAIComponent.Agent not found.`

The warning was separate from the reported in-campaign character-creation failure, which was already fixed by v1.3.6. It did expose a real compatibility error in the AI career controller-transition guard.

The guard treated `TOR_Core.BattleMechanics.AI.CastingAI.Components.WizardAIComponent.Agent` as a property. In TOR WiTM 1.16 the component receives its agent from Bannerlord's `AgentComponent` base type through the inherited `Agent` field. The property lookup therefore failed while resolving runtime surfaces, causing the guard to exit through its safe-failure path before installing any of its Harmony patches.

v1.3.7 resolves the actual field across the component inheritance chain and reads it directly. This restores the existing transition behavior for registered shared heroes:

- a stale TOR `WizardAIComponent` is kept dormant while its hero agent is player-controlled;
- cached AI casting behavior is cleared when Bannerlord changes the agent controller;
- TOR career-ability ordering is normalized when direct control returns to the registered hero;
- compiler-generated TOR career-script context patches continue to install as before.

The change is limited to the incorrect reflected member resolution. It does not replace TOR AI logic or introduce a second controller system.

## Included previous fix

v1.3.7 includes the v1.3.6 root fix for `Create a new playable character`. That fix intercepts the decompiler-corrupted NativeCreation clan-snapshot helper which cast member-name strings such as `Name` and `InformalName` to `FieldInfo` before TOR character creation could open.

Users still on the Nexus v1.3.5 package should update directly to v1.3.7.

## Save and performance scope

- Existing saves remain compatible; no migration is required.
- No campaign-map scan, global hero scan, global party scan, mission-tick polling, or recurring reconciliation was added.
- The repaired lookup runs only while the existing AI-career transition guard installs.
- Controller handling remains event-driven through TOR/Bannerlord mission callbacks.

## Validation

v1.3.7 is gated by:

- Bannerlord 1.3.15 full build/API validation;
- Lib.Harmony 2.3.3 build validation;
- full-solution Lib.Harmony 2.4.2 compatibility validation;
- a dedicated regression assertion requiring the inherited `Agent` field resolver/access path and rejecting the invalid `WizardAIComponent.Agent` property lookup;
- the existing NativeCreation, career identity, Greater Harbinger, companion-dialogue, battle-intervention, reinforcement, settlement-switch, and package-output guards.
