# Changelog

## 1.1.0

- Added event-driven alerts when a registered shared character's independent player-clan party becomes the defender in a new map battle.
- Added a paused intervention inquiry with actions to take control of the endangered character, order the current MainParty to reinforce them, or dismiss the alert.
- Extended the existing transactional remote-party switch only for the exact defensive `MapEvent` that triggered the alert; unrelated and stale battles remain blocked.
- After a successful battle-time control transfer, reconstructs Bannerlord's native `PlayerEncounter` from the existing defender and attacker leader parties so the player can continue the same fight.
- Preserves party positions, troop and prisoner rosters, ships, army membership, ownership, shared inventory handoff, and active-character identity invariants during the battle-time transfer.
- Added explicit stale-event, siege/raid, settlement, transition, current-party battle, and ownership guards.
- Added native engage-party reinforcement orders without teleporting either party or altering the battle simulation.
- Kept detection event-driven. The application-tick hook performs only constant-time queue/deferred-action work and no global campaign scans.
- Added reproducible GitHub Actions build, packaged ZIP, SHA-256 artifact, and stable release workflow for Bannerlord 1.3.15 reference assemblies.

## 1.0.41

- Fixed the post-disband town-presence mismatch for registered inactive non-companion playable heroes, especially the original campaign founder. Bannerlord 1.3.15 classifies the founder as a player-clan lord and normally routes a no-party lord to the lord's hall, while converted companions use the tavern path. MCC now gives registered inactive playable heroes one consistent recovery surface in towns.
- The repair is narrowly scoped to `DefaultHeroAgentLocationModel.GetLocationForHero`: when a registered, alive, free, non-companion player-clan hero has no party, is already waiting in the town being entered, and is not the current `Hero.MainHero`, only the native `PlayerClanMember`/unresolved placement is normalized to that town's tavern. The hero keeps the native `PlayerClanMember` behavior classification.
- Does not reclassify the founder as a companion, alter `CompanionOf`, touch party-disband logic, move heroes, change clan state, run settlement scans, or add campaign ticks. Unrelated native location results are preserved.
- Added the uniquely named `MultiCharacterCampaignTOR.SettlementPresence.v141.dll` sidecar so the fix is a distinct v1.0.41 runtime assembly.
- No save migration or new serialized state.

## 1.0.40

- Fixed the v1.0.39 packaging failure that left the core DLL unable to resolve its runtime repair dependency. The compatibility module is loaded before the core module, so initialization, Harmony installation, and remote-party switches can complete.
- Renamed the user-facing repair module to `MultiCharacterCampaignTOR.RuntimeCompatibility.v140.dll`. Its TOR career-ability invariant logic resolves each active hero's actual career and contains no career-path-specific selection.
- Added a separately loaded `MultiCharacterCampaignTOR.IdentityGuard.v140.dll`, avoiding an accidental loader dependency between the runtime compatibility and identity modules.
- Removed the CPU regression from continuous party-screen UI probing. Party-screen rebinding is dormant until Bannerlord constructs a PartyState, and Ctrl+R input is queried only on the unobstructed campaign map.
- Preserved the event-driven outgoing-party map-presentation handoff and the non-destructive shared-inventory roster handoff. Added one-shot `InventoryLogic.Initialize` timing so any remaining first-open delay is attributed to the exact stage without recurring campaign-map work.

## 1.0.39

- Fixed v1.0.38 not actually loading in the reported test session. The log reached the inherited v1.0.37 inventory timer but contained none of v1.0.38's mandatory outgoing-party, post-menu, or fast-market messages, proving that Bannerlord/.NET had resolved the older identity sidecar.
- Renamed the identity sidecar to `MultiCharacterCampaignTOR.IdentityGuard.v139.dll` and rebuilt the Waywatcher/identity loader as `MultiCharacterCampaignTOR.WaywatcherFix.v139.dll` against that exact unique assembly name. The module manifest now loads the uniquely named loader, preventing a stale or leftover old sidecar with the original assembly name from satisfying the dependency.
- Added unmistakable startup diagnostics: a correct installation logs `[RemotePartySwitch v1.0.39]` and `[IdentityGuard] v1.0.39 uniquely named sidecar loaded` before campaign launch.
- Retains v1.0.38's actual outgoing former-MainParty nameplate lifecycle fix and scoped plain-map-inventory pathfinding optimization unchanged.
- No save keys, campaign behavior data, recurring ticks, or gameplay rules changed.

## 1.0.38

- Corrected v1.0.37's map diagnosis: the missing banner and troop-count label belonged to the **outgoing former MainParty**, not the newly controlled destination party.
- After the outgoing party becomes AI-controlled, the mod now reevaluates its visibility from the new player position and raises Bannerlord's native party-visibility lifecycle event. This lets the map view create the ordinary AI-party banner/count presentation that the former MainParty did not previously own.
- Repeats that reclassification once after the shared-character management menu actually exits. The map nameplate layer is active at that point; v1.0.37 refreshed it too early while the menu still owned `MapState`.
- The v1.0.37 timing diagnostic measured 4.717 seconds inside Bannerlord's plain inventory-opening call. The deferred party weight/capacity cache was not the cause.
- Removed the expensive part of the non-trading map-inventory opening: Bannerlord normally pathfinds from the player party to every town merely to choose market data for displayed item values. The plain inventory now selects the closest town market by direct campaign-map distance. It retains local market-dependent displayed values while avoiding the cold all-towns navigation pass after a remote switch.
- Trade, barter, stash, warehouse, loot, settlement inventory, and actual item transactions retain their native market and navigation logic. The optimization is scoped to the ordinary non-trading inventory opened on the campaign map.
- All new work is event-driven on a remote switch, menu exit, or inventory opening. No campaign ticks, recurring party scans, per-item background work, or periodic reconciliation were added.

## 1.0.37

> Superseded by v1.0.38: this release refreshed the destination presentation instead of recreating the outgoing former MainParty's ordinary nameplate, and its inventory cache warm-up did not affect the measured stall.

- Fixed the newly controlled party's campaign-map banner and troop-count label remaining absent after a remote switch. After the identity, inventory, owner, leader, and AI handoff has committed, the mod now invokes Bannerlord 1.3.15's own one-shot `MapState` presentation refresh used by native army-state changes.
- Resolves the newly paired `MainParty`/shared-inventory weight and capacity caches once during the switch transaction. This removes those deferred calculations from the first inventory opening without clearing, copying, or rebuilding the shared player roster.
- Added one one-shot diagnostic for the first inventory opening after a remote switch. It records the duration of Bannerlord's `InventoryLogic` initialization and inventory-state push, allowing any remaining character-tableau/rendering delay to be distinguished from inventory data work.
- The refresh, cache warm-up, and diagnostic arm only after a successful remote switch. No campaign-map scans, party scans, per-item tick work, periodic reconciliation, or recurring logging were added.
- Save keys and the v1.0.36 shared-inventory handoff remain unchanged.

## 1.0.36

- Withdraws v1.0.35's destructive remote inventory restoration. That implementation misclassified Bannerlord/TOR's callback-populated new-MainParty inventory as corruption, cleared it, and rebuilt the destination from its tiny pre-switch AI roster. This could make most of the shared inventory disappear and force an expensive deferred inventory-screen rebuild.
- Remote switching now performs the intended shared-inventory handoff. The outgoing active `ItemRoster` object is attached directly to the new `MainParty`; the destination party's small pre-switch AI roster is copied once and attached to the outgoing party.
- Callback-populated temporary destination inventory is discarded by replacing its roster reference. The mod never clears or re-adds the large shared player inventory, preventing both item loss and repeated additive duplication.
- Invalidates only the two affected parties' inventory-derived weight and speed caches after the one-shot handoff. No periodic inventory work, scans, or reconciliation were added.
- Preserves v1.0.35's Ctrl+R campaign-map manager shortcut and expanded remote candidate list.
- Existing save keys remain unchanged. Because deleted items cannot be reconstructed without a trustworthy baseline, load a save made before the first v1.0.35 remote switch.

## 1.0.35

> Withdrawn: its remote inventory restoration was destructive. Use v1.0.36 or later.

- Fixed remote switches reporting `destination party inventory count changed` and leaving callback-added inventory entries in the destination party. The remote transaction now applies the same exact inventory restoration principle as the established same-party path, independently restoring both outgoing and destination rosters before invariant validation.
- Fixed the switch list showing only the hero who led the former main party after moving control elsewhere. Any registered, active, non-prisoner hero physically present in an eligible player-clan lord party can now be selected; that hero becomes the destination party's leader and owner only when the handoff commits.
- Updated the switch inquiry text and remote status hints so they no longer claim that candidates must be in the current main party or already lead an independent party.
- Added **Ctrl+R** to open **Manage shared characters** directly from the normal campaign map while travelling or stationary. It is gated out during menus, map conversations, simulations, missions, encounters, barter, inventory, and other unsafe identity-change states.
- Audited the supplied Bannerlord 1.3.15 and TOR WiTM 1.16 assemblies for hard-coded Ctrl+R usage; none was found. Other mods and user-remapped controls remain outside the module's control.
- Kept all save keys unchanged. No migration is required from v1.0.34.
- Added only constant-time key-state reads to the existing application-tick input path. No periodic party scan, hero scan, inventory scan, or campaign reconciliation was added.

## 1.0.34

- Added dedicated cross-party control handoff for registered playable heroes leading separate player-clan parties anywhere on the campaign map.
- Preserved the v1.0.33 same-party transaction unchanged; remote targets take a separate preflight and commit path.
- Verified the Bannerlord 1.3.15 root incompatibility in `ChangePlayerCharacterAction.Apply`: native succession logic transfers the outgoing party's ships to the new MainParty, copies naval anchor state, and assigns the outgoing lord party to the new MainHero. The remote path now performs the required PlayerTroop, campaign MainParty, camera, trait, roster-version, wage, and player-change event sequence without those succession-only mutations.
- Remote switching now tracks the outgoing and destination `MobileParty` by stable reference for the full transaction. Inventory, member roster, prisoner roster, fleet, map position, settlement, and army references are captured for both parties and verified after the handoff.
- Party inventory remains attached to each physical party. The existing inventory restoration logic continues to protect the single-party switch path and is never pointed at a different destination MainParty.
- The outgoing independent party retains its leader and owner, has AI enabled, clears an obsolete player click-to-move command when independent, and requests an AI decision refresh. Existing army objectives and attachment are retained for army parties.
- The destination hero must already lead and own an active normal lord party belonging to the player clan. Remote switching does not move a hero into another party and does not create a replacement party.
- Preserved source and destination army membership. A remote party may lead an army or remain a legal member of another army when it becomes MainParty.
- Added fleet-safe naval switching for stable parties. Ships remain with their original physical parties. Switching is blocked during embarkation, disembarkation, port-navigation transitions, battles, sieges, raids, TOR hireling service, and other unstable map-event states.
- Remote targets inside settlements are intentionally blocked because Bannerlord 1.3.15's native player-character transition forcibly calls `LeaveSettlementAction` for the new MainParty.
- Extended death succession to registered heroes leading safe remote parties, preserving registration order and the existing deferred succession behavior for battles and encounters.
- Kept all existing save keys unchanged. No new serialized state or save migration is required for v1.0.33 campaigns.
- Preserved the v1.0.33 founder/PlayerTroop identity guard and Character-screen recovery; the remote transaction holds the same switch guard across all campaign event dispatch.
- Added transaction diagnostics only at switch time. No campaign tick, periodic party scan, global hero scan, or hot-path reconciliation was added.

## 1.0.33

- Fixed the remaining Character-screen identity regression where the currently controlled shared hero could disappear from the selector and the original campaign founder would become selected again after later engine activity.
- Root cause: Bannerlord's `Hero.MainHero` is derived directly from `Game.PlayerTroop`. Vanilla code paths can reassign `PlayerTroop` to the hard-coded original `main_hero`; the v1.0.32 Character-screen repair then read that already-stale `Hero.MainHero` as its source of truth, so it could not recover the active shared hero.
- Added a narrowly scoped invariant guard on `Game.PlayerTroop`. When Bannerlord tries to restore the saved founder while a different registered shared hero still owns the main-party leader slot, the assignment is immediately corrected back to that active shared hero before the caller continues. Legitimate character switches are excluded while the mod's switch transaction is in progress.
- Added Character-screen pre-construction recovery using the same invariant, so an already-drifted session is repaired before `CharacterDeveloperState` and its hero selector are built. The existing v1.0.32 initial-selection postfix then receives the correct `Hero.MainHero`.
- Added pre-`InitializeState` recovery for saves already affected by the stale-founder split. When the persisted main-party leader is a different registered shared hero, both the live identity and any stale founder-valued pending active-character restore ID are corrected before initialization can normalize the founder back into authority.
- Repairs `_activeHeroId` only when this exact stale-founder split-brain state is detected, preventing the existing hourly synchronization from persisting the transient founder rebound into a later save.
- The guard is event-driven on the rare `PlayerTroop` setter, campaign initialization, and Character-screen construction paths. No campaign-map polling, recurring scans, mission ticks, or global hero iteration were added.

## 1.0.32

- Fixed the CLR `InvalidProgramException` in `TORBridge.RefreshAfterSwitch` that aborted campaign-state initialization, normal character switching, and the rollback path after `ChangePlayerCharacterAction` had already changed live identity state.
- Replaced the malformed emitted refresh body with a verifier-safe sidecar bridge. TOR career cache refresh and the required `AbilityUser` repair now fail safely and can no longer abort the authoritative Bannerlord character switch.
- Restored completion of `RebindMainPartyIdentity`, including its normal member-roster version refresh and party visual invalidation, after load and after every successful switch.
- Fixed the malformed lord-recruitment Harmony transpiler. The v1.0.30/v1.0.31 body referenced runtime-incompatible generic `List<CodeInstruction>` members and caused the entire shared-campaign Harmony installation sequence to fail before `_installed` was committed. The transpiler now delegates to compiler-emitted Harmony 2.4.2-compatible code and preserves the original relation call if the narrow replacement target cannot be resolved.
- Added a one-shot Character-screen initial-selection repair. When a registered shared hero is the real `Hero.MainHero` but `CharacterDeveloperState` is created with the saved founder or no initial hero, only that stale/default initial selection is rebound to the active shared hero. Explicitly requested non-founder heroes remain untouched.
- Added load-time recovery for inactive converted companions left in clan-member classification by an interrupted pre-v1.0.32 switch. The repair reuses the mod's existing `RestoreInactiveCompanion` invariant and excludes the current active hero.
- Preserved v1.0.31's TOR `CareerAbility`/HUD repair unchanged apart from versioned logging and installation of the new runtime repair bridge.
- Added no campaign-map polling, periodic scans, or mission-tick work. New checks run only during existing initialization/rebind events or Character-screen state construction.

## 1.0.31

- Fixed the TOR battle `NullReferenceException` in `Ability.IsDisabled` when the current main agent has a valid TOR career but its `AbilityComponent.CareerAbility` was never initialized.
- Preserves the v1.0.30 pre-construction identity and `AbilityUser` repairs, then validates the career-ability invariant again after TOR creates the agent's `AbilityComponent`, before HUD rebinding, on main-agent transitions, and on main-agent controller changes.
- Repairs an existing `AbilityComponent` in place. A missing `CareerAbility` is created through TOR's own `AbilityFactory`, wired to the component's native `OnCastStart` / `OnCastComplete` handlers, and inserted in TOR's native career-first ability order.
- If the career ability already exists in the known-ability list but the component property is stale, restores the property to the existing object instead of creating a duplicate.
- Creates a missing `AbilityComponent` only when TOR's own `IsCastingMission()` classification permits ability components; non-casting/friendly mission behavior is left unchanged.
- Never replaces a valid existing `AbilityComponent`, preserving current ability selection, cooldowns, charge state, selected spells, item-bound abilities, and existing event subscriptions.
- Adds race-safe duplicate-component prevention and transactional rollback if career-ability injection fails partway through.
- Clears cached mission-manager state at mission end so no mission-lifetime reference can leak into the next mission.
- Adds no campaign-map ticks, mission ticks, polling loops, or global scans. All repair work is event-driven at existing TOR lifecycle boundaries.

## 1.0.30

- Fixed the remaining TOR career-ability initialization path for promoted companions used as the active player character.
- Repairs TOR's `AbilityUser` prerequisite immediately before `AbilityManagerMissionLogic.OnAgentCreated` evaluates the spawned hero.
- Repairs the second TOR invariant at the exact `AbilityComponent` constructor boundary: the active main-party leader is re-established as `Game.PlayerTroop` / `Hero.MainHero` before TOR decides whether to create the career ability.
- Rebinds TOR's career HUD once after `AbilityHUDMissionView` finishes initialization so an already-established `Agent.Main` cannot miss the original `OnMainAgentChanged` binding.
- Does not rebuild or replace `AbilityComponent` instances after construction, avoiding stale manager references and duplicated ability event handlers.
- Adds one-shot mission diagnostics to `MultiCharacterCampaignTOR.log` showing the main agent hero, `Hero.MainHero`, main-party leader, career, `AbilityComponent`, and `CareerAbility` state if further diagnosis is required.
- Adds no campaign-map ticks, polling, global scans, or recurring diagnostic work.

## 1.0.29

- Fixed the v1.0.28 save-load crash (`MethodAccessException`) in `CareerUniquesBridge.LogStatus`.
- Corrected the accessibility of the existing `HarmonyBridge.PatchPrefix` helper used by the new mission-initialization career-ability patch.
- Preserves the v1.0.28 one-time `AbilityManagerMissionLogic.OnBehaviorInitialize` fix without adding any recurring mission or campaign-map work.

## 1.0.28

- Fixed TOR career ability initialization still being missed when TOR rebuilt battle state after the campaign-map character repair.
- Added a one-time Harmony prefix on `AbilityManagerMissionLogic.OnBehaviorInitialize`.
- Reasserts the active career hero's required `AbilityUser` attribute immediately before battle agents are created.
- Preserves the existing save-load and character-switch repairs from v1.0.27.
- Adds no mission ticks, per-agent scans, recurring campaign-map work, or new save data.

## 1.0.27

- Fixed promoted companions with a valid TOR career spawning in battle without their career ability or bottom-left charge indicator.
- Repairs TOR's required `AbilityUser` attribute for the active career hero during one-shot campaign identity rebinding and after character switches.
- Existing saves are repaired automatically on session launch; no career reselection is required.
- Retains each hero's existing career, choices, spells, lores, skills, resources, and equipment.
- No recurring mission work, campaign-map scans, or save keys were added.

## 1.0.26

- Fixed the startup `FileNotFoundException` in v1.0.25 for `System.Private.CoreLib, Version=8.0.0.0`.
- Rebuilt the lord-recruitment transpiler metadata entirely against Bannerlord's existing .NET Framework `mscorlib` types. The packaged module no longer references the .NET 8 runtime.
- Retains the narrowly scoped highest-registered-character base relation for lord-recruitment trust, clan-wide fief income, active-character clan leadership, UnlimitedCAP compatibility, and all v1.0.24 character-creation fixes.
- No save keys or recurring campaign-map work were added.

## 1.0.25

- Lord-recruitment trust now uses the highest relation that the target lord has with any registered shared playable hero.
- The change is injected only into `LordDefectionCampaignBehavior.GetPersuasionTasksForDefection`; personal dialogue, quests, courtship, crime, rivalries, and every other relation lookup remain character-specific.
- Ordinary companions, spouses, children, and other unregistered clan members are not included in the maximum.
- Settlement income remains native and clan-wide. Player fiefs are already enumerated from `Clan.Fiefs` by `DefaultClanFinanceModel`, so switching the active clan leader continues paying their income into the shared clan purse without transferring settlement ownership or firing owner-change events.
- No save keys or recurring campaign-map work were added.

## 1.0.24

- Fixed `InvalidProgramException` in v1.0.23's character-creation activation prefix.
- Replaced the rejected hand-encoded multi-argument `Type.GetMethod` sequence with the native bridge assembly's existing `FindCompatibleStaticMethod` resolver and its already-used zero-argument `MethodBase.Invoke` pattern.
- Retains activation-time clan-identity restoration, active-character clan leadership, the kingdom-budget correction, UnlimitedCAP compatibility, unchanged save keys, and zero recurring campaign-map work.

## 1.0.23

- Fixed the native character-creation default clan name still appearing in early TOR allegiance messages even though the final vassalage sequence and live clan name were restored correctly.
- Moved campaign-identity restoration from immediately after queued `PushState` to the character-creation activation prefix. Bannerlord initializes the queued state later, so the previous restore ran before its campaign-start stages assigned their temporary defaults.
- The activation prefix now removes the campaign-only stages first and then restores the captured campaign identity before any remaining TOR career or specialization stage can emit an allegiance event.
- Retains active-character clan leadership, the kingdom-budget correction, UnlimitedCAP compatibility, unchanged save keys, and zero recurring campaign-map work.

## 1.0.22

- Fixed `MethodAccessException` when starting character creation in v1.0.21. The newly reached leader synchronizer had attempted to call a private helper owned by another class.
- Replaced that illegal cross-class call with the public .NET `Type.GetField` reflection API using explicit instance/public/non-public binding flags, followed by the existing one-shot field assignment.
- Retains the v1.0.21 branch convergence, early clan-identity restoration, UnlimitedCAP compatibility, unchanged save keys, and zero recurring campaign-map work.

## 1.0.21

- Fixed the active-character clan-leader update being skipped whenever the main party exposed no owner party component. The no-component path now converges on the same one-shot leader synchronization as every other rebind path.
- This makes the newly created or selected shared hero the actual `Clan.PlayerClan.Leader`, restores clan-leader UI permissions, and prevents Bannerlord's wealthy-AI-clan **Kingdom Budget Expense** path from treating the active player clan as AI-led.
- Restores the captured clan and campaign identity immediately after the native character-creation state is constructed. Campaign-start stage constructors can therefore no longer leave their temporary default clan name active until completion or use it in later TOR allegiance notifications.
- Retains v1.0.20's verifier-safe legacy bridge, UnlimitedCAP compatibility, TOR career-driven allegiance changes, unchanged save keys, and zero recurring campaign-map work.

## 1.0.20

- Fixed the `BadImageFormatException` that prevented native character creation from starting in v1.0.19.
- Corrected the injected clan-name snapshot helper metadata. Its dictionary argument now uses a verifier-safe object signature and an explicit runtime cast instead of an invalid `ELEMENT_TYPE_CLASS`/`TypeSpec` method-signature encoding.
- Retains active-character clan leadership, clan-name preservation, the finance correction, UnlimitedCAP compatibility, and TOR career-driven allegiance changes without recurring map work or save-key changes.

## 1.0.19

- Changed live clan leadership to follow the currently controlled shared hero. The founder ID remains saved for continuity and succession bookkeeping but is no longer forced to remain the administrative clan leader.
- Fixes Bannerlord treating a non-founder player character as an AI-led player clan, which caused the wealthy-clan **Kingdom Budget Expense** levy and prevented leader-gated systems from recognizing the active player.
- Restored the complete existing Harmony installation sequence after the v1.0.18 nested finance installer could stop later party-size and UnlimitedCAP hooks from being installed.
- UnlimitedCAP now sees the active shared hero as `Clan.PlayerClan.Leader.IsHumanPlayerCharacter`; the existing compatibility bridge also remains available without double-applying configured limits.
- Fixed the removed clan-naming stage silently mutating the running clan through a shared `TextObject` reference. `Name` and `InformalName` are now deep-copied before native creation and restored afterward.
- Native creation no longer restores the former founder into `Clan._leader`, so the newly created and currently controlled hero remains clan leader after completion.
- TOR career-driven kingdom changes, including Blood Dragon allegiance, remain unchanged.
- Leadership synchronization runs only during a real character rebind or one-time campaign initialization. No recurring map callback, scan, or save key was added.

## 1.0.18

- Fixed Bannerlord charging the shared player clan the AI-only **Kingdom Budget Expense** when a non-founder shared hero is active. The founder remains the administrative clan leader, so the native finance model had mistaken the player clan for an AI vassal clan.
- Corrects both the explained daily balance and, during committed finance calculations, the matching amount credited to the kingdom budget wallet.
- Applies only to `Clan.PlayerClan` while a registered shared hero is active and differs from the administrative clan leader. AI clans remain unchanged.
- Preserves TOR career-driven faction changes, including the Blood Dragon start automatically joining its intended kingdom. Normal fief income, tribute, party and garrison costs, mercenary contracts, and other vassal finances remain native.
- Removed the switch-only identity and finance-input diagnostic messages whose method space now hosts the targeted compatibility bridge. No recurring campaign-map callback or scan was added.
- No save keys changed.

## 1.0.17

- Fixed the v1.0.16 stage trimmer retaining both banner editors and clan naming. The runtime places those campaign-identity stages before Review, so trimming only stages after Review could remove only campaign options.
- The in-campaign flow now preserves the complete personal-character sequence through TOR specialization, removes every intervening campaign-identity stage, retains Review, and removes every stage after Review.
- Supports any number of banner-editor stages between TOR specialization and Review without relying on their implementation type names.
- Fixed `MissingMethodException` during completion cleanup. Bannerlord 1.3.15's `CharacterCreationScreen` implements `ICharacterCreationStateHandler.OnCharacterCreationFinalized` explicitly; the bridge now resolves that exact non-public metadata method.
- Retained the targeted `PopState` completion path, existing campaign restoration, and suppression of the global campaign-start completion event.
- No save keys or recurring campaign-map work changed.

## 1.0.16

- Removed all campaign-start-only stages after the native TOR review screen. In-campaign character creation no longer opens banner editing, clan naming, or campaign difficulty/options.
- Fixed the crash when confirming the final character-creation stage. Bannerlord's native `CharacterCreationState.FinalizeCharacterCreationState` uses `CleanAndPushState`, which attempts to destroy and replace the already-running campaign map.
- Added an in-campaign-only finalization path that unregisters the character-creation state, pops only that stacked state, restores the existing campaign, and returns to the preserved map.
- Prevented the global `OnCharacterCreationIsOver` campaign-start event from being broadcast again inside an existing campaign. This avoids resetting campaign-start behavior state or removing listeners that are intended to initialize only once.
- TOR's real culture, appearance, origin, growth, profession, career, specialization, naming, review, fresh progression initialization, equipment, resources, and `HeroExtendedInfo` setup remain active.
- Existing clan identity, banner, names, difficulty settings, campaign progress, party position, inventory, shared gold, and save keys remain unchanged.
- No recurring campaign-map callback, scan, or diagnostic loop was added.

## 1.0.15

- Fixed an access violation when opening native TOR character creation from an active campaign menu.
- Replaced the new-campaign-only `GameStateManager.CleanAndPushState` transition with `GameStateManager.PushState` for the in-campaign character-creation launch.
- Keeps the live campaign map state underneath the native `CharacterCreationState`, matching Bannerlord's normal in-campaign party, inventory, career, spellbook, and other stacked-screen transitions.
- Prevents the current map screen from being finalized while its menu consequence callback is still executing.
- TOR's real `TORCharacterCreationContentHandler`, native stages, progression reset, career setup, and final return to a fresh map state remain unchanged.
- Startup rollback and unfinished-candidate cleanup remain unchanged.
- No campaign save keys changed.

## 1.0.14

- Fixed native TOR character creation failing immediately on loaded campaigns with `MissingMethodException: SandBoxGameManager.LaunchCharacterCreation()`.
- Removed the invalid assumption that the active loaded-campaign game manager exposes TOR's private `LaunchCharacterCreation` method.
- Reproduced TOR's actual launch sequence directly: register TOR's real character-creation content handler, create `CharacterCreationState`, and push it through `GameStateManager.CleanAndPushState`.
- Selects the exact inherited generic `GameStateManager.CreateState<T>()` method instead of accepting an arbitrary zero-argument overload.
- Preserves an existing TOR handler at priority 0 and skips duplicate registration. The temporary registration listener runs after existing character-creation listeners and is removed immediately after state construction.
- Added complete rollback cleanup for an unfinished candidate if native state startup fails: restore the original active hero, remove the candidate from shared-character state, remove it from the party and player clan, and disable it so no ghost character is left in the save.
- The level shown for the temporary allocation seed before the native state opens is not retained. Bannerlord's native `CharacterCreationContent` clears the candidate's progression during state construction before TOR choices are applied.
- No campaign save keys changed.

## 1.0.13

- Replaced the custom inquiry-based shared-character creator with TOR's actual native `TorCampaignGameManager.LaunchCharacterCreation` pipeline.
- Restored the complete Bannerlord/TOR character-creation sequence, including culture, sex and appearance, origin, growth, profession, TOR specialization, naming, review, banner/clan stages, and campaign options supplied by the currently loaded game and TOR version.
- Removed random NPC/player-template ranking from the active new-character path. A new hero object is allocated from the current player character only as an engine seed; Bannerlord's native `CharacterCreationContent` then clears inherited progression before any choices are applied.
- New characters now receive the same baseline initialization as campaign-start characters: cleared skill XP and focus state, base attributes, native narrative effects, and TOR's normal profession/specialization finalization.
- TOR now creates `HeroExtendedInfo` through its own native character-creation handler. The separate post-creation career inquiry and its `TOR did not create HeroExtendedInfo` failure are no longer used for newly created heroes.
- Preserved the existing campaign while the native new-game state runs by snapshotting and restoring player-clan renown and tier, culture, leadership, names, banner and banner colors, influence, home settlement, faction midpoint, initial home settlement, campaign difficulty/options, main-party position, shared gold, and main-party inventory.
- Recentered the campaign camera on the restored main-party position after the native state returns to the campaign map.
- Prevented duplicate TOR character-creation handler registration by removing the game manager's stale initialization listener before relaunch and removing the temporary listener immediately after state construction.
- Fixed the Party-screen presentation bridge throwing `NullReferenceException` while `MainPartyTroops` was not yet available during screen initialization.
- Existing shared heroes and save keys are unchanged.

## 1.0.12

- Fixed newly created in-campaign characters inheriting the level of the selected Bannerlord/TOR hero template, which could make a fresh character start at level 11 or another non-default level.
- Changed new-character template resolution to prioritize valid level-1 templates before higher-level candidates while retaining the existing culture, sex, player-template, character-creation-template, basic-troop, and wanderer ranking rules.
- Added a creation-only level invariant that sets `Hero.Level` to 1 and calls Bannerlord's `HeroDeveloper.SetInitialLevel(1)` after background application.
- Reasserted the same invariant after TOR career initialization so career hooks cannot leave a newly created character above level 1.
- Existing heroes and saved characters are not modified. No save keys changed.

## 1.0.11

- Renamed the mod to **Multi-Character Campaign - TOR**.
- Renamed the assembly, implementation namespace, project, source file, DLL, module folder, log file, Harmony owner ID, and campaign-menu identifiers to the new Multi-Character Campaign identity.
- Added a legacy `TORSharedCharacterCampaign.SharedCampaignBehavior` wrapper so behavior-scoped data from v1.0.0-v1.0.10 continues loading under the same runtime type.
- Retained the legacy module ID `TORSharedCharacterCampaign` because Bannerlord stores enabled module IDs in campaign saves.
- Retained every `tor_shared_campaign_*` save key. Registered heroes, active hero, founder, shared purse, shared chest, companion classification, and career migrations therefore remain compatible.
- This package remains the TOR edition and continues requiring `TOR_Core`.
- Installation folder is now `Modules\MultiCharacterCampaignTOR`; remove the old `Modules\TORSharedCharacterCampaign` folder before installing to avoid loading both builds.

## 1.0.10

- Fixed the main-party troop capacity losing campaign-leader bonuses whenever a non-founder shared character was active.
- Restored the clan-tier leader premium that Bannerlord downgrades from `25 × clan tier` to `15 × clan tier` when the main-party leader is not also `Clan.PlayerClan.Leader`.
- Restored the campaign founder's faction-leader `+20` bonus when the founder remains the actual faction leader.
- Restored the Noble Retinues `+40` and Royal Guard `+60` policy bonuses when their normal campaign conditions are met.
- Applies the correction inside `DefaultPartySizeLimitModel.CalculateBaseMemberSize`, before TOR culture and monstrous-unit modifiers. TOR factors therefore scale the restored bonuses in the same order as native bonuses.
- Leaves character-specific party-size inputs character-specific: Steward, personal perks, TOR career passives, race/culture modifiers, symbols, attributes, and equipment effects still follow the active hero.
- Added detailed logging of each administrative correction and its exact before/after contribution.
- Clarified that UnlimitedCAP's **party limit** controls the number of clan parties, not the troop capacity of the currently controlled party.
- No save keys or campaign progression data changed.

## 1.0.9

- Fixed the player main party being counted as a secondary **Caravan and Party Income** source whenever a shared character led the party while the founder remained clan leader.
- Added a targeted finance-model guard that returns zero only when `DefaultClanFinanceModel.AddIncomeFromParty` is asked to process `MobileParty.MainParty` for `Clan.PlayerClan`. Genuine caravans and secondary war parties remain unchanged.
- Added before/after switch logging for clan influence, mercenary award multiplier, mercenary-service state, active hero, main-party leader, and clan leader. This distinguishes actual contract-input changes from the fixed main-party accounting error.
- Added compatibility with UnlimitedCAP 2.1.0 when a shared character is active. Its original patches require `Clan.PlayerClan.Leader.IsHumanPlayerCharacter`, which becomes false because administrative clan leadership remains with the founder.
- Replicates UnlimitedCAP's configured **Definite** and **Progressive** companion-limit behavior only while its own condition is false, preventing double application when the founder is active.
- Added the equivalent compatibility bridge for UnlimitedCAP's party limit, which used the same clan-leader assumption.
- No save keys or campaign data were changed. Shared purse, per-hero careers, campaign-wide quests, and campaign-wide Career Uniques acquisition remain unchanged.

## 1.0.8

- Fixed new shared-character creation failing after the background step. `HeroCreator.CreateSpecialHero` creates heroes in Bannerlord's `NotSpawned` state; the mod now activates the hero before adding and switching to them.
- Added load-time repair for previously registered shared heroes that remained `NotSpawned` after an interrupted v1.0.7 creation attempt.
- Replaced the invalid TOR `CareerObject.IsConditionsMet` eligibility filter. In TOR 1.16 most careers intentionally have no condition delegate, and that method returns false for them.
- Added culture-aware new-character career pools.
- Added role-aware companion career mapping, including Waywatcher -> Waywatcher, Glade Captain -> Warden, Empire wizard -> Imperial Magister, and Eonir Guardian Mage -> Grey Lord Wizard.
- Preserved per-hero career IDs, choices, points, spells, lores, attributes, resources, and equipment.
- Added detailed career-candidate and new-hero activation logging.
- Added a one-time save migration that reopens career selection for v1.0.7 converted companions incorrectly marked as handled after the broken zero-candidate check.


## 1.0.7

- Fixed the Party screen retaining the outgoing hero in its left-side character presentation after the campaign identity had already switched successfully. A one-shot runtime bridge now selects the active hero's `PartyCharacterVM` when the Party screen opens.
- Added delayed Party-screen roster handling. The bridge retries while `MainPartyTroops` is still being populated, stops after a bounded number of attempts, and logs the exact runtime selection route or unsupported selection surface.
- Fixed the unsafe companion-career chooser introduced in 1.0.6. Existing companions are now offered careers compatible with their established TOR archetype, culture, magic attributes, priest attributes, and race state.
- Added specialist mappings for existing Empire spellcasters, Wood Elf spellcasters, Eonir spellcasters, Orc spellcasters, Sigmar/Ulric/Lady priests, runesmiths, and necromancers.
- Added automatic migration for companions given an incompatible career by 1.0.6. The first safe campaign-map tick opens a repair selector for that active companion.
- Added **Review active companion career** to the Multi-Character Campaign - TOR menu for manual validation and repair.
- Added a targeted guard for TOR's `CareerScreenVM.OpenBattlePrayers`. Invalid priest assignments are blocked before `BattlePrayersVM` dereferences missing religion or prayer-list state.
- Career repairs clear or replace only the selected hero's `CareerID`, `CareerChoices`, and career-tier markers. Spells, lores, skills, attributes unrelated to the career tree, equipment, and custom resources remain attached to that hero.
- Existing-companion career assignment writes the validated career ID and root node directly and does not rerun TOR `InitialCareerSetup`, avoiding destructive race, religion, spell, skill, or attribute initialization intended for brand-new player characters.
- Documented and logged TOR's actual career-point calculation: available career points come from each hero's own normal level, capped by `TORConfig.MaximumNumberOfCareerPerkPoints`; spent points come from that hero's own `CareerChoices`.
- Confirmed that UsefulCompanions requires no compatibility patch for career points. It grants normal skill XP through `Hero.AddSkillXp`; any resulting hero levels automatically increase that hero's TOR career-point allowance.
- Preserved full career selection for newly created heroes. Companion archetype restrictions apply only to converting existing TOR companions.
- Added the additive save key `tor_shared_campaign_career_repair_handled_ids`. All prior save keys remain unchanged.
- Career Uniques recovery and set-piece acquisition remain one campaign-wide state and were not partitioned by character.

## 1.0.6

- Rebound the main-party leader and party-component owner after each successful player-character change.
- Added detailed post-switch identity diagnostics.
- Added exact main-party inventory snapshots and restoration to remove switch-time equipment duplication while preserving each hero's equipped loadout.
- Added initial TOR career selection for careerless activated companions.
- Added a scoped quest-manager guard so shared-character switches do not cancel campaign-wide quests.

## 1.0.5

- Fixed both character and companion selectors failing before display with `MissingMethodException`.
- Removed the compile-time call to the incompatible `InquiryElement(object, string, TaleWorlds.Core.ImageIdentifier, bool, string)` signature.
- Resolves and invokes Bannerlord's actual runtime constructor, whose image parameter is `TaleWorlds.Core.ImageIdentifiers.ImageIdentifier` on the tested 1.3.15 installation.
- Preserved direct calls to the verified `MultiSelectionInquiryData` and `MBInformationManager.ShowMultiSelectionInquiry` APIs.

## 1.0.4

- Fixed a .NET Framework 4.8 `MissingMethodException` in the exception logger.
- Made exception logging fail-safe.
- Isolated companion discovery sources and added the main-party roster as a fallback.

## 1.0.3

- Added detailed click-to-callback logging for campaign menu actions and inquiry dispatch.
- Corrected campaign-menu inquiry pause state.

## 1.0.2

- Corrected Bannerlord 1.3.15 inquiry API ownership and restored selection windows.

## 1.0.1

- Fixed inherited-property reflection ambiguity and expanded companion discovery.

## 1.0.0

- Added persistent shared-character registration, native player switching, character creation, companion activation, shared purse, shared chest, succession, TOR integration, campaign-wide quests, and Career Uniques compatibility.
