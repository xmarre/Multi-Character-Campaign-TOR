# Multi-Character Campaign - TOR

Multi-Character Campaign is intended as a general Bannerlord multi-character campaign framework. This package is the **The Old Realms edition** and includes TOR-specific character creation, career, prayer, finance, party-limit, and Career Uniques integration.

Target:
- Mount & Blade II: Bannerlord 1.3.15
- The Old Realms: War in the Mountains 1.16

## Features

- Create additional persistent playable heroes inside an existing campaign.
- Newly created shared heroes use Bannerlord and TOR's native personal-character creation stages and begin with the normal fresh-character progression state.
- Switch the real Bannerlord player character between registered heroes in the current main party or in separate player-clan parties anywhere on the campaign map.
- Receive an immediate paused alert when a registered shared character's independent party becomes the defender in a map battle. The alert can transfer control into that existing battle or order the current party to move toward it as reinforcements.
- Press **Ctrl+R** on the open campaign map to open **Manage shared characters** without visiting a settlement.
- Activate existing companions through the management menu or the `[Multi-Character Campaign] Take control of this character` dialogue line.
- Inactive registered heroes may remain in the main party or lead independent AI-controlled clan parties.
- When an independent party disbands, a registered inactive non-companion playable hero who is waiting in a town is exposed through the tavern for consistent recovery, without changing the hero into a companion.
- Shared campaign purse.
- The shared player inventory follows the controlled `MainParty`; troops, prisoners, ships, locations, and each hero's equipment remain attached to their physical party or hero.
- One persistent campaign-wide stash.
- Campaign-wide quests.
- Automatic successor selection when the active character dies.
- Native TOR character creation and per-hero TOR career state.
- Campaign-wide Career Uniques acquisition state.

## Defensive battle intervention

Version 1.1.0 listens to Bannerlord's native map-event start and end dispatchers. When a registered inactive shared character is physically present in an independent player-clan party that becomes the defending side of a new map event, the campaign is paused and a one-time alert identifies the character, party, and attacking side.

The alert provides three actions:

- **Take control and continue the battle** runs the existing transactional remote-party identity and inventory handoff while explicitly permitting only the exact defensive map event that triggered the alert. It then rebuilds Bannerlord's native `PlayerEncounter` for the existing defender and attacker leader parties. Troops, prisoners, ships, army membership, party positions, and the map event itself remain attached to their original parties.
- **Move the current party to reinforce** issues Bannerlord's native engage-party movement order toward the endangered party. The simulated battle can still finish before the current party arrives.
- **Dismiss** closes the alert without changing campaign state.

The control-transfer option is disabled for sieges, raids, settlement-bound parties, naval/port transitions, invalid party ownership, stale/ended battles, or whenever the current player party is already in another battle. Alert discovery is event-driven; the application-tick hook only drains queued events and deferred encounter continuation work.

## Independent party control

Version 1.0.40 lets control follow a registered hero's existing physical party. The selected hero may already lead that party or may be another registered member of it. If Oloec leads Party A and Sylvanwynth leads Party B, switching from Oloec to Sylvanwynth makes Party B the real `MobileParty.MainParty`. Oloec remains in Party A at its existing position with its troops, prisoners, ships, and AI state. The shared player inventory moves to Party B, while Party B's former AI inventory moves to Party A. Switching back reverses that handoff. Selecting another registered member of Party A later makes that hero Party A's leader only when control actually moves there.

After a remote handoff commits, the outgoing former MainParty is reclassified through Bannerlord's native party-visibility lifecycle. The reclassification runs again once when the management menu exits, allowing the active map view to create that party's ordinary AI banner and troop-count label. The destination MainParty retains its normal player-party presentation. Neither operation runs on campaign ticks.

The ordinary non-trading inventory opened while travelling uses the closest town's market by direct campaign-map distance for its displayed item values. Bannerlord's original helper instead performs route/path-distance work against every town; on a newly controlled remote party that cold lookup accounted for a measured 4.717-second first-open stall. Trading, barter, settlement inventory, stash, warehouse, and loot flows retain their native market selection.

Bannerlord 1.3.15's native `ChangePlayerCharacterAction` assumes a succession-style transfer. When the target belongs to another party, it moves ships from the outgoing party to the new MainParty, copies naval anchor data, and changes the outgoing lord-party owner to the new MainHero. Version 1.0.34 uses a dedicated remote transaction that performs Bannerlord's required player identity, MainParty, camera, trait, roster-version, wage, and campaign-event changes while preserving both physical parties.

The remote transaction captures Party B's pre-switch AI inventory, allows required player-change callbacks to run, then rebinds the complete outgoing shared inventory to the new `MainParty` in one roster assignment. Party B's saved AI inventory is assigned to the outgoing party. Callback-populated temporary inventory is discarded as a whole; the mod never clears and reconstructs the large shared roster. Troop roster, prisoner roster, fleet, position, settlement, and army references are still verified after every switch. It adds no periodic party scans or campaign-tick reconciliation.

## Party disbanding and town presence

Bannerlord 1.3.15 treats the two MCC character origins differently after an independent party disbands. A converted companion remains a player companion and the native hero-location model places that waiting hero in the town tavern. The original campaign founder remains a player-clan lord; when that no-party lord is waiting in a town and lord's-hall access is available, the native model routes the hero to the lord's hall instead. This produces inconsistent recovery behavior for registered playable characters.

Version 1.0.41 normalizes only this MCC-specific state. If a registered inactive hero is alive, active, free, belongs to the player clan, has no party, is already waiting in the town being entered, is not the current `Hero.MainHero`, and is not a player companion, the settlement-presence repair routes the native `PlayerClanMember` placement to that town's tavern. If the native model returned no location for the same state, the tavern is supplied as the fallback. The hero keeps the `PlayerClanMember` location-detail classification, so Bannerlord still creates the hero with the native player-clan-member/companion behavior set rather than reclassifying the hero as a wanderer.

The repair does not change `CompanionOf`, `IsPlayerCompanion`, clan membership, party-disband behavior, hero position, or settlement state. It does not affect the current player, converted companions, ordinary clan lords, prisoners, party members, castles, villages, or unrelated native location results. It performs no recurring scans or campaign-tick work.

## Campaign-map shortcut

Press **Ctrl+R** while the normal campaign map is open to enter the management menu. It works while the party is travelling or stationary. The shortcut is ignored while another menu, map conversation, battle simulation, mission, encounter, barter, or inventory transaction owns the campaign state. The normal **Return** option closes the manager back to the map.

The supplied Bannerlord/TOR assemblies contain no hard-coded Ctrl+R action. Another mod or a custom key binding can still claim the same combination.

## Native TOR character creation

**Create a new playable character** opens Bannerlord's native `CharacterCreationState` with TOR WiTM 1.16's real content handler. The mod retains the personal-character stages and removes the campaign-start-only stages that would rename or reconfigure the existing clan.

Loaded campaigns are hosted by `SandBoxGameManager` on this build, while TOR's launch helper is private to `TorCampaignGameManager`. The mod therefore performs TOR's exact state-launch sequence directly and registers TOR's real content handler on the character-creation initialization event.

The flow includes the currently loaded native/TOR stages:

- culture selection;
- sex, race, body, and face editing;
- origin;
- growth;
- profession and TOR career setup;
- TOR specialization;
- naming and review.

The flow ends from the review stage. Banner editing, clan naming, and campaign difficulty/options are deliberately skipped because those settings belong to the already-running campaign rather than the new playable hero.

The mod no longer searches random NPC, troop, wanderer, or player templates to determine a new character's progression. Bannerlord still requires an allocated `Hero`/`CharacterObject` before the state can open, so the current player character is used only as the structural allocation seed. `CharacterCreationContent` immediately clears inherited skill XP, focus, attributes, and progression. TOR's culture stage then replaces race, body properties, battle equipment, civilian equipment, and stealth equipment before finalization.

TOR's own content handler performs profession, specialization, attributes, resources, equipment, and `HeroExtendedInfo` initialization. There is no separate post-creation career approximation.

The native new-game finalizer normally replaces the map state and broadcasts the global campaign-start completion event. In an existing campaign that would finalize the live map screen and rerun campaign-start listeners. The mod instead pops only the stacked character-creation state, completes its own session directly, and restores the existing clan's renown and tier, culture, names, banner and colors, influence, home-settlement data, campaign options, main-party position, shared purse, and inventory. Clan names are deep-copied before native content initializes, preventing the removed naming stage from mutating the snapshot itself. The newly controlled hero remains clan leader. The map camera is then recentered on the restored party position.

## Party-screen presentation

Bannerlord stores the left-side Party-screen model as a `PartyVM` selection. Character switching changes the campaign player, party leader, and party owner first. Version 1.0.7 also selects the new active hero's roster entry when the Party screen opens.

The presentation bridge:
- waits for the Party screen and its roster to exist;
- retries while the roster is still loading;
- selects only once after a switch or save restoration;
- leaves subsequent manual roster selection alone;
- logs the exact runtime method or member used.

## Character-screen progression

Version 1.0.34 keeps the v1.0.33 identity invariant active throughout remote-party handoffs. The active remote party leader remains the authoritative `Hero.MainHero` and `Game.PlayerTroop`, including after save/load and Character-screen construction.

Version 1.0.33 closes the remaining split-brain identity path behind the Character-screen regression. Bannerlord derives `Hero.MainHero` from `Game.PlayerTroop`, and vanilla code can reassign that property to the hard-coded original `main_hero` even while the shared campaign's real active hero remains the main-party leader. The mod now rejects only that stale-founder reassignment when a different registered shared hero is still the authoritative main-party leader. The same invariant is repaired before campaign-state initialization and immediately before Character-screen state construction, allowing already-affected saves to recover before stale founder state can be normalized again. This keeps the active hero present and selected without interfering with deliberate character switches.

Version 1.0.32 repairs the vanilla Character screen's initial hero selection for shared-character campaigns. Bannerlord carries an `InitialSelectedHero` on `CharacterDeveloperState`; when a registered shared hero is active but that state is created with the saved campaign founder or no initial hero, the mod substitutes the current `Hero.MainHero` for that initial selection. An explicitly requested non-founder hero is preserved.

The repair runs only when the Character screen state is constructed. It does not continuously force the selector, so after the screen opens the normal dropdown remains usable. Attribute points, focus points, skills, perks, and level progression remain attached to each hero's own `HeroDeveloper`.

Version 1.0.32 also repairs the campaign rebind failure that previously stopped before the main-party roster version and visual refresh completed. This allows UI state created after loading or switching to observe the completed active-character identity transaction.

## Companion careers

TOR companions can possess spells, lores, priest attributes, or other archetype state without having a TOR career. The mod validates that existing state before assigning a career.

Examples:
- Empire companion with `SpellCaster` -> Imperial Magister
- Wood Elf companion with `SpellCaster` -> Spellsinger
- Eonir companion with `SpellCaster` -> Grey Lord Wizard
- Orc companion with `SpellCaster` -> Orc Shaman
- `PriestSigmar` -> Warrior Priest of Sigmar
- `PriestUlric` -> Warrior Priest of Ulric
- `PriestLady` -> Damsel of the Lady

A career assigned by an earlier build can be repaired through **Review active companion career**. Repair affects only that hero's career ID, career-choice tree, and career-tier markers. Existing spells, lores, normal skills, equipment, and custom resources remain on that hero.
Existing companions receive the validated career ID and root career node without rerunning TOR `InitialCareerSetup`; that setup contains transformations intended for newly created player characters. Newly created shared heroes still use TOR's complete native career initialization path.

## TOR career points

TOR has no separate career-XP pool. The Career screen calculates progression from the active hero's own data:

- Total available points: `min(hero.Level, TORConfig.MaximumNumberOfCareerPerkPoints)`
- Spent points: `CareerChoices.Count - 1`
- Free points: total available minus spent points

The root career node occupies the first `CareerChoices` entry and does not consume a displayed point.

A level 14 companion with a newly assigned career and a ten-point configured cap therefore displays ten free career points. Another hero can display the same number because both reached the cap. Their career IDs and selected choices remain separate in each hero's `HeroExtendedInfo`.

## UsefulCompanions compatibility

UsefulCompanions grants ordinary skill XP to companions and role holders with `Hero.AddSkillXp`. Bannerlord converts normal skill progression into normal hero levels. TOR then derives that hero's career-point allowance from the hero level.

No UsefulCompanions patch is required for TOR career points. An inactive registered character can gain normal skill XP and levels through Bannerlord, battle participation, party roles, and UsefulCompanions. Their increased level provides additional career points when that hero is controlled again. Their career choices remain saved on that hero while inactive.

## TOR battle career abilities

Promoted companions with a TOR career retain that career when made the active shared character. Version 1.0.32 retains v1.0.31's mission-time player-identity, `AbilityUser`, and `CareerAbility` repairs and enforces TOR's required `AbilityComponent.CareerAbility` invariant at the mission lifecycle boundaries where the active agent can change. This covers career abilities such as the Waywatcher's **Lethal Shot**, its bottom-left charge display, and the `Ability.IsDisabled` path used by the battle HUD.

When TOR has already created an `AbilityComponent`, the repair keeps that exact component and restores only the missing career-ability state using TOR's own factory, native event handlers, and native ability ordering. Existing ability selection, cooldowns, charge state, selected spells, item-bound abilities, and event subscriptions are preserved. A completely missing component is created only in missions that TOR itself classifies as casting-capable.

The repair is event-driven at agent creation, ability-component construction, main-agent/controller changes, HUD binding, and mission teardown. It adds no campaign-map ticks, mission-tick polling, or recurring scans.

## Battle-prayer safety

TOR's prayer screen requires a priest career, a matching priest attribute, a valid prayer list, and a resolvable religion. Version 1.0.7 validates those prerequisites before TOR opens `BattlePrayersVM`. Invalid migrated career assignments are blocked and directed to the career-repair menu instead of reaching the crash path.

## Finance compatibility

Bannerlord assumes that the controlled player, player-clan leader, main-party leader, and main-party owner are the same hero. Version 1.0.24 keeps those live identities synchronized whenever control changes. The founder ID remains available for save continuity but no longer overrides the active clan leader.

Without a compatibility guard, `DefaultClanFinanceModel` mistakes `MobileParty.MainParty` for a secondary clan war party and adds its internal party balance to **Caravan and Party Income**. Version 1.0.9 excludes only the actual player main party from that secondary-party calculation. Real caravans and independently led clan parties are still counted normally.

Bannerlord also charges wealthy AI vassal clans an amount labelled **Kingdom Budget Expense** when their clan leader is not `Hero.MainHero`. Version 1.0.24 prevents that AI-only path naturally by making the controlled shared hero the actual player-clan leader. AI clans and all ordinary vassal finances remain unchanged.

Vassal status does not provide a universal daily wage. Its normal finances come from sources such as fiefs and tribute and can still be negative after party, garrison, tribute, policy, or other expenses. TOR career-driven faction changes, including Blood Dragon starts, are deliberately preserved.

Fiefs belong to the clan rather than to whichever shared hero is currently active. Bannerlord's finance model enumerates `Clan.Fiefs` when calculating settlement taxes, tariffs, projects, and bound-village income. Because every shared hero uses the same player clan and purse, all fief revenue remains available to the active character without transferring settlements between heroes. The mod deliberately does not trigger settlement-owner changes when switching characters; those changes would invoke political, quest, notification, and governor-related campaign systems unnecessarily.

The shared purse remains one balance. Character switching synchronizes that balance across registered heroes.

## Lord recruitment and relations

Relations remain personal to each hero. Version 1.0.26 makes one narrow clan-representation exception: while Bannerlord constructs the persuasion tasks for recruiting an outside lord's clan, the initial trust relation is the highest relation that lord has with any registered shared playable hero.

Only registered playable heroes participate. Ordinary companions, spouses, children, and other unregistered clan members do not. The replacement occurs only inside `LordDefectionCampaignBehavior.GetPersuasionTasksForDefection`; greetings, quests, courtship, crime, rivalries, and every other relation-driven system continue using the active hero's own relation. Political strength, traits, Charm, current-liege relations, persuasion results, and barter remain native and can still prevent recruitment.

## UnlimitedCAP compatibility

UnlimitedCAP 2.1.0 applies its limits only when `Clan.PlayerClan.Leader.IsHumanPlayerCharacter` is true. Version 1.0.24 keeps that identity true for every active shared character.

The compatibility bridge still understands UnlimitedCAP's current MCM configuration:

- **Definite** companion and party limits replace the base result with the configured value.
- **Progressive** limits add the configured value to Bannerlord's normal result.
- The bridge remains dormant when UnlimitedCAP is absent or its corresponding option is disabled.
- It remains dormant whenever UnlimitedCAP's native active-leader condition succeeds, so values are not doubled.

## Main-party troop capacity

Bannerlord's `DefaultPartySizeLimitModel` assumes the hero leading the player main party is also the player clan leader. Older releases separated those roles, causing a non-founder active character to lose several campaign-administrative contributions:

- clan-tier contribution is calculated as an ordinary clan party leader (`15 × tier`) instead of the player clan leader (`25 × tier`);
- the faction-leader `+20` contribution can disappear;
- Noble Retinues `+40` and Royal Guard `+60` can disappear despite the founder still satisfying their campaign conditions.

Version 1.0.24 now satisfies Bannerlord's native leader identity directly. The older targeted correction remains guarded and therefore does not double native contributions. TOR culture factors and unit-weight handling retain their normal order.

The following values intentionally continue to vary between active characters:

- Steward and party-role skill effects;
- personal Bannerlord perks;
- TOR career choices and passives;
- race and culture modifiers;
- Wood Elf symbols and Oak upgrades where TOR ties them to the active player;
- Greenskin, Vampire, Dwarf, Wood Elf, and monstrous-unit rules.

UnlimitedCAP's **party limit** is a different value: it controls how many clan parties may exist. It does not set the troop capacity of `MobileParty.MainParty`.

## Inventory and equipment

- `MobileParty.MainParty` inventory is shared by every playable character.
- Each hero retains individual battle and civilian equipment.
- The shared chest is separate campaign storage.
- A same-party switch retains the established inventory snapshot protection.
- A remote-party switch moves the complete shared inventory roster to the newly controlled `MainParty` and moves that party's former AI inventory to the outgoing party. The large player roster is never cleared and rebuilt.

## Career Uniques compatibility

Career Uniques relic and set-piece recovery remains campaign-wide. A piece recovered by one character cannot drop again for another character in the same campaign. Character switching only refreshes effects for the newly active hero's equipped set.

## Rename and update from v1.0.10 or earlier

The install folder and DLL were renamed in v1.0.11. Delete the old folder before installing this version:

`Modules\TORSharedCharacterCampaign`

Install the new folder:

`Modules\MultiCharacterCampaignTOR`

Do not leave both folders installed. They intentionally share the legacy Bannerlord module ID so existing saves continue recognizing the mod.

All existing `tor_shared_campaign_*` save keys remain unchanged.

## Usage

1. Start or load a TOR campaign.
2. Press **Ctrl+R** on the open campaign map, or open **Camp**, a town, castle, or village menu.
3. Choose **Manage shared characters** when using a settlement or Camp menu.
4. Create a character, activate a companion, switch characters, review the active companion's career, or open the shared chest.

## Rules

- Normal switching runs only on the campaign map.
- A same-party target must be alive, active, free, and present in the current main party.
- A remote target must be alive, active, free, physically present in an active normal lord party belonging to the player clan, and registered as a shared character. The target does not have to be that party's current leader.
- Switching is blocked during missions, encounters, captivity, barter, and inventory transactions.
- Remote switching is also blocked for active map events, battles, sieges, raids, TOR hireling service, settlement interiors, and embarkation/disembarkation or port-navigation transitions.
- The active shared hero becomes clan leader, main-party leader, and main-party owner on each successful switch.
- The outgoing remote party retains its previous leader, owner, troops, prisoners, ships, location, and legal army membership, then resumes clan-party AI. It receives the destination party's former AI inventory as the shared player inventory moves to the newly controlled party.
- Skills, attributes, perks, equipment, appearance, health, TOR career, career choices, spells, devotion, and TOR resources remain per hero.
- Gold, quests, shared inventory, shared chest contents, and Career Uniques acquisition progress are campaign-wide. Independent AI parties keep the non-player inventory exchanged during the most recent control handoff.

## Configuration

This release has no MCM page. Management is performed through the in-game **Multi-Character Campaign - TOR** menu.

## Diagnostic log

Primary path:

`Documents\Mount and Blade II Bannerlord\Configs\ModLogs\MultiCharacterCampaignTOR.log`

Fallback:

`%TEMP%\MultiCharacterCampaignTOR.log`

The log records menu callbacks, native TOR character-creation launch/finalization, companion discovery, character switching, main-party income exclusion, UnlimitedCAP limit application, administrative party-size correction, remote inventory handoff counts, Party-screen presentation selection, career compatibility and repair, per-hero career-point calculations, prayer validation, rollback, and nested exceptions, and one-shot settlement-presence repair activations.

## Existing-companion activation and career handling

Existing registered heroes found in Bannerlord's stale `NotSpawned` state are repaired before activation. This compatibility repair applies to interrupted saves from older versions and is separate from the native new-character flow.

TOR 1.16 career availability for existing companions is resolved from the hero's culture and established companion role. The mod does not use `CareerObject.IsConditionsMet` as a general eligibility test because most TOR careers have no condition delegate. Recognized companion roles receive fitting candidates, including Waywatcher, Warden for Glade Captains, Imperial Magister for Empire wizards, and Grey Lord Wizard for Eonir Guardian Mages. Career progression remains stored per hero.
