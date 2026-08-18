# Multi-Character Campaign - TOR v1.3.6

Released: 18 August 2026.

Target: Bannerlord 1.3.15 and The Old Realms: War in the Mountains 1.16.

## Fixed the remaining Create New Character startup failure

Aeen's v1.3.5 retest confirmed that in-campaign `Create a new playable character` still did not start. The supplied log did not contain the v1.3.5 NativeCreation launch/adapter diagnostics, which pointed to a failure before the character-creation event/state path.

A full audit of the recovered NativeCreation implementation found the root cause in `CaptureCampaignState()`. The decompiled helper used to snapshot player-clan members was corrupted: it attempted to cast the member-name string (for example `Renown`, `Name`, or `InformalName`) to `FieldInfo` while deciding whether to copy a `TextObject`. That throws before NativeCreation subscribes its initialization listener, allocates the candidate, or pushes `CharacterCreationState`.

v1.3.6 repairs that exact recovered helper at runtime. Clan values are read by their actual member names, and only `Name` / `InformalName` receive the intended `CopyTextObject()` copy. The existing v1.3.5 generic event-listener adaptation remains in place for the later creation stage.

## Fixed non-spellcaster AI career abilities

Aeen also isolated an asymmetry between spellcasting and non-spellcasting careers: careers such as Waywatcher could be used by AI only after taking direct control, and an RTS controller switch could reveal that the hero had no career ability at all, while spellcasters continued to work.

TOR's native `AbilityComponent` constructor creates `CareerAbility` automatically only for `Hero.MainHero`. AI spellcasters usually mask this because their selected spells still populate `KnownAbilitySystem`, allowing TOR's normal casting/WizardAI path to exist. A non-spellcaster career can instead have no selected spell/prayer abilities, leaving its career slot empty when controller timing misses MCC's AI-only repair.

v1.3.6 separates stable career identity from AI casting state. Every registered shared hero with a TOR career now receives/retains its `AbilityComponent` and `CareerAbility` during agent creation and controller changes regardless of whether the agent is currently player- or AI-controlled and regardless of whether the hero knows normal spells or prayers.

The existing AI-career layer still owns `WizardAIComponent` and AI-only casting behavior once Bannerlord reports the agent as AI-controlled. No companion is converted into a fake spellcaster, and no dummy lore, void spell, placeholder spell, or synthetic casting progression is added.

This also means an RTS switch to a registered non-caster career hero should expose the same career ability that belongs to that hero rather than an empty career slot.

## Preserved behavior

- v1.3.5's controller-independent `AbilityUser` first-spawn prerequisite remains active.
- v1.3.4's Greater Harbinger controller safety remains unchanged.
- Companion activation through dialogue remains unchanged.
- TOR career-button rebinding remains unchanged.
- Existing spellcaster AI career behavior remains unchanged.
- Personal perks/progression remain native per-hero state.

## Save and performance scope

- Existing saves remain compatible; no save migration is required.
- No campaign-map scan, global hero scan, global party scan, mission-tick polling, or recurring campaign reconciliation was added.
- Career identity repair runs only on TOR's existing agent-created/controller-changed callbacks.
- NativeCreation snapshot repair runs only when the recovered creation flow snapshots clan state.

## Validation

v1.3.6 is gated by:

- Bannerlord 1.3.15 full build/API validation;
- Lib.Harmony 2.3.3 build/runtime validation;
- full-solution Lib.Harmony 2.4.2 compatibility validation;
- runtime patch-installation smoke coverage;
- dedicated regression guards for the recovered NativeCreation `FieldInfo` cast corruption;
- dedicated regression guards ensuring registered career identity does not depend on `Agent.IsAIControlled`, normal spells, lores, or `WizardAIComponent` creation.

The affected community installation remains the runtime confirmation target for the repaired in-campaign creation path and non-spellcaster career behavior.
