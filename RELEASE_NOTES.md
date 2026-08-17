# Multi-Character Campaign - TOR v1.3.5

Released: 17 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Fixed AI career abilities requiring one manual player use first

Community testing of v1.3.4 confirmed the wider AI-career implementation and the Greater Harbinger controller fix, but exposed one initialization edge case: a registered companion could require the player to personally use that hero's career ability once before the AI could use it in later battles.

TOR decides whether to build an `AbilityComponent` during `AbilityManagerMissionLogic.OnAgentCreated` from the hero's persistent `AbilityUser` attribute. MCC previously established that prerequisite only when the agent already reported `IsAIControlled`. Agent creation can occur before the final controller assignment, so TOR could skip the component on that first AI spawn. Manually using the career ability as the hero persisted `AbilityUser`, which is why later AI battles then worked.

Version 1.3.5 establishes `AbilityUser` before TOR's native `OnAgentCreated` handling for every registered shared hero that has a TOR career, independent of transient controller state. The existing controller-change path still owns CareerAbility/WizardAI setup and AI/player handoffs.

A registered career companion should therefore be able to use its career ability as AI without ever having been personally controlled or having the ability manually activated first.

## Fixed another Bannerlord 1.3.15 NativeCreation compatibility defect

The v1.3.4 community report still showed `Create a new playable character` failing on the affected installation. The quoted `RuntimeFix132 Installed...` line was only a successful startup message and did not contain the actual creation exception, so v1.3.5 does not misidentify that line as the failure.

A source audit found another concrete issue in the reconstructed in-campaign creation bridge. It registered Bannerlord's generic `OnCharacterCreationInitializedEvent` using an `Action<object>`, while Bannerlord 1.3.15 exposes this as `MbEvent<CharacterCreationManager>` and therefore requires an `Action<CharacterCreationManager>` listener. The reconstructed reflection matcher rejects the otherwise-correct `AddNonSerializedListener` overload when the supplied delegate has the wrong closed generic type.

RuntimeCompatibility now adapts that legacy listener to the exact delegate type required by the runtime event before the legacy reflection call proceeds.

Because this particular installation has exposed several creation-path compatibility issues in succession, v1.3.5 also adds phase-specific NativeCreation diagnostics for wrapper entry, legacy entry, generic-listener adaptation, and escaped startup exceptions. If another installation-specific problem remains, the resulting log should identify the actual failing creation layer rather than showing only unrelated initialization output.

## TOR career-button troop effects across character switches

TOR Waywatcher special arrows and Runelord unit runes are stored in the physical party's `MobilePartyExtendedInfo.TroopAttributes`. MCC's career-button switch repair only rebinds the active career-button delegates and refreshes the Party VM; it does not remove those troop attributes. TOR's own button `Disable()` path likewise clears only the UI delegates.

Character switching therefore does not inherently remove those applied troop effects. They remain associated with the physical party/troop until TOR explicitly removes, replaces, or invalidates them.

## Save, behavior, and performance scope

- Existing saves remain compatible; no save migration is required.
- No new campaign-map scan, global hero scan, global party scan, mission-tick polling, or recurring reconciliation was added.
- The first-spawn career prerequisite runs only at TOR's existing agent-creation event for registered shared heroes with careers.
- NativeCreation compatibility is applied only around the in-campaign character-creation startup path.
- The v1.3.4 Greater Harbinger controller guard and companion-dialogue repair remain unchanged.

## Validation

The release is gated by:

- Bannerlord 1.3.15 full build/API validation;
- full-solution Lib.Harmony 2.4.2 compatibility build;
- the existing Lib.Harmony 2.3.3 build/runtime surface;
- runtime patch-installation smoke coverage;
- regression guards ensuring the registered-career `AbilityUser` prerequisite is not gated by transient `Agent.IsAIControlled`;
- regression guards retaining the exact NativeCreation generic-listener adapter and startup diagnostics;
- the existing linked-Harmony loader protections.

The affected community installation remains the authoritative runtime confirmation for the in-campaign creation path, while the automated gates verify the build, patch, API, and packaging surfaces used by v1.3.5.
