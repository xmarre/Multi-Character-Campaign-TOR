// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using MultiCharacterCampaignTOR.NativeCreation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;

namespace MultiCharacterCampaignTOR
{
	public class MultiCharacterCampaignBehavior : CampaignBehaviorBase
	{
		private const string MenuId = "tor_shared_character_campaign";

		private List<string> _sharedHeroIds = new List<string>();

		private List<string> _originalCompanionIds = new List<string>();

		private List<string> _careerPromptHandledHeroIds = new List<string>();

		private List<string> _careerRepairHandledHeroIds = new List<string>();

		private string _activeHeroId = string.Empty;

		private string _founderHeroId = string.Empty;

		private string _pendingConversationSwitchId = string.Empty;

		private string _pendingCareerPromptHeroId = string.Empty;

		private string _loadedActiveHeroId = string.Empty;

		private int _sharedGold;

		private ItemRoster _sharedChest = new ItemRoster();

		private bool _switchInProgress;

		private bool _deathTransition;

		private bool _pendingDeathSuccession;

		private bool _suppressNextGameOver;

		private bool _careerEligibilityMigration108Done;

		public static MultiCharacterCampaignBehavior Instance { get; private set; }

		internal bool IsIdentitySwitchInProgress => _switchInProgress;

		public MultiCharacterCampaignBehavior()
		{
			Instance = this;
		}

		internal bool IsRegisteredSharedHero(Hero hero)
		{
			if (hero == null || _sharedHeroIds == null)
			{
				return false;
			}
			return Contains(_sharedHeroIds, Reflection.IdOf(hero));
		}

		public override void RegisterEvents()
		{
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
			CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
			CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
			CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
			CampaignEvents.TickEvent.AddNonSerializedListener(this, OnCampaignTick);
			CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
			CampaignEvents.BeforeHeroKilledEvent.AddNonSerializedListener(this, OnBeforeHeroKilled);
			CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
			CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, OnGameOver);
		}

		public override void SyncData(IDataStore dataStore)
		{
			if (dataStore.IsSaving && Hero.MainHero != null)
			{
				_sharedGold = Hero.MainHero.Gold;
				_activeHeroId = Reflection.IdOf(Hero.MainHero);
				SynchronizeGold(_sharedGold);
			}
			dataStore.SyncData("tor_shared_campaign_hero_ids", ref _sharedHeroIds);
			dataStore.SyncData("tor_shared_campaign_original_companion_ids", ref _originalCompanionIds);
			dataStore.SyncData("tor_shared_campaign_career_prompt_handled_ids", ref _careerPromptHandledHeroIds);
			dataStore.SyncData("tor_shared_campaign_career_repair_handled_ids", ref _careerRepairHandledHeroIds);
			dataStore.SyncData("tor_shared_campaign_active_hero_id", ref _activeHeroId);
			dataStore.SyncData("tor_shared_campaign_founder_hero_id", ref _founderHeroId);
			dataStore.SyncData("tor_shared_campaign_gold", ref _sharedGold);
			dataStore.SyncData("tor_shared_campaign_chest", ref _sharedChest);
			dataStore.SyncData("tor_shared_campaign_career_eligibility_migration_108_done", ref _careerEligibilityMigration108Done);
			if (dataStore.IsLoading)
			{
				_loadedActiveHeroId = _activeHeroId ?? string.Empty;
			}
			if (_sharedHeroIds == null)
			{
				_sharedHeroIds = new List<string>();
			}
			if (_originalCompanionIds == null)
			{
				_originalCompanionIds = new List<string>();
			}
			if (_careerPromptHandledHeroIds == null)
			{
				_careerPromptHandledHeroIds = new List<string>();
			}
			if (_careerRepairHandledHeroIds == null)
			{
				_careerRepairHandledHeroIds = new List<string>();
			}
			if (_sharedChest == null)
			{
				_sharedChest = new ItemRoster();
			}
		}

		private void OnNewGameCreated(CampaignGameStarter starter)
		{
			InitializeState(newGame: true);
		}

		private void OnGameLoaded(CampaignGameStarter starter)
		{
			InitializeState(newGame: false);
		}

		private void OnSessionLaunched(CampaignGameStarter starter)
		{
			InitializeState(newGame: false);
			RestoreSavedActiveCharacter();
			RegisterMenus(starter);
			RegisterConversation(starter);
			HarmonyBridge.TryInstall();
			CareerUniquesBridge.LogStatus();
			PartyScreenSelectionBridge.RequestSelection("campaign session launched");
			QueueMissingCareerPromptForActiveOriginalCompanion();
		}

		private void InitializeState(bool newGame)
		{
			try
			{
				Hero mainHero = Hero.MainHero;
				if (mainHero != null)
				{
					bool flag = _sharedHeroIds != null && _sharedHeroIds.Count > 0 && !string.IsNullOrEmpty(_founderHeroId);
					string text = Reflection.IdOf(mainHero);
					if (string.IsNullOrEmpty(_founderHeroId))
					{
						_founderHeroId = text;
					}
					AddUnique(_sharedHeroIds, text);
					NormalizeRegisteredHeroStates();
					MigrateCareerEligibilityState108();
					if (newGame || string.IsNullOrEmpty(_loadedActiveHeroId))
					{
						_activeHeroId = text;
					}
					if (newGame || !flag)
					{
						_sharedGold = mainHero.Gold;
					}
					SynchronizeGold(_sharedGold);
					PruneMissingIds();
					RebindMainPartyIdentity(mainHero);
				}
			}
			catch (Exception ex)
			{
				Log.Error("State initialization failed", ex);
			}
		}

		private void MigrateCareerEligibilityState108()
		{
			if (_careerEligibilityMigration108Done)
			{
				return;
			}
			int num = 0;
			string[] array = ((_originalCompanionIds != null) ? _originalCompanionIds.ToArray() : new string[0]);
			foreach (string heroId in array)
			{
				Hero hero = ResolveHero(heroId);
				if (hero == null || !string.IsNullOrEmpty(TORBridge.GetCareerId(hero)))
				{
					continue;
				}
				if (_careerPromptHandledHeroIds != null)
				{
					int num2 = _careerPromptHandledHeroIds.RemoveAll((string x) => string.Equals(x, heroId, StringComparison.Ordinal));
					if (num2 > 0)
					{
						num++;
					}
				}
				if (_careerRepairHandledHeroIds != null)
				{
					_careerRepairHandledHeroIds.RemoveAll((string x) => string.Equals(x, heroId, StringComparison.Ordinal));
				}
			}
			_careerEligibilityMigration108Done = true;
			Log.Info("Applied v1.0.8 career-eligibility migration. Reopened career selection for " + num + " careerless converted companion(s).");
		}

		private void NormalizeRegisteredHeroStates()
		{
			if (_sharedHeroIds == null || _sharedHeroIds.Count == 0)
			{
				return;
			}
			string[] array = _sharedHeroIds.ToArray();
			foreach (string text in array)
			{
				Hero hero = ResolveHero(text);
				if (hero == null || !hero.IsAlive)
				{
					continue;
				}
				try
				{
					if (hero.PartyBelongedTo == MobileParty.MainParty || Reflection.IsHeroInMainPartyRoster(hero))
					{
						Reflection.EnsureHeroActive(hero);
						Reflection.EnsureHeroInMainParty(hero);
						Log.Info("Normalized registered shared hero. Hero=" + text + "; active=" + hero.IsActive + "; party=" + Reflection.IdOf(hero.PartyBelongedTo) + "; rosterContains=" + Reflection.IsHeroInMainPartyRoster(hero) + ".");
					}
				}
				catch (Exception ex)
				{
					Log.Error("Could not normalize registered shared hero=" + text, ex);
				}
			}
		}

		private void RestoreSavedActiveCharacter()
		{
			if (string.IsNullOrEmpty(_loadedActiveHeroId))
			{
				return;
			}
			string loadedActiveHeroId = _loadedActiveHeroId;
			_loadedActiveHeroId = string.Empty;
			Hero mainHero = Hero.MainHero;
			if (mainHero != null && Reflection.IdOf(mainHero) == loadedActiveHeroId)
			{
				_activeHeroId = loadedActiveHeroId;
				return;
			}
			Hero hero = ResolveHero(loadedActiveHeroId);
			if (hero != null && CanSwitchTo(hero, emergency: true, out var _) && TrySwitch(hero, emergency: true, notify: false))
			{
				Log.Info("Restored saved active character " + loadedActiveHeroId + " after campaign load.");
				return;
			}
			_activeHeroId = ((mainHero != null) ? Reflection.IdOf(mainHero) : string.Empty);
			Log.Info("Saved active character " + loadedActiveHeroId + " could not be restored; retained " + _activeHeroId + ".");
		}

		private void RegisterMenus(CampaignGameStarter starter)
		{
			try
			{
				starter.AddGameMenu("multi_character_campaign_tor", "Multi-Character Campaign - TOR\n\nManage playable characters, create a new character, activate a companion, or access the campaign-wide chest.", OnMenuInit);
				AddMenuOption(starter, "multi_character_campaign_tor", "mcc_tor_create", "Create a new playable character", CanUseManager, delegate
				{
					StartCharacterCreation();
				}, leave: false, 0);
				AddMenuOption(starter, "multi_character_campaign_tor", "mcc_tor_switch", "Switch active character", CanSwitchMenu, delegate
				{
					OpenSwitchSelection();
				}, leave: false, 1);
				AddMenuOption(starter, "multi_character_campaign_tor", "mcc_tor_companion", "Register or activate a companion", CanRegisterCompanion, delegate
				{
					OpenCompanionSelection();
				}, leave: false, 2);
				AddMenuOption(starter, "multi_character_campaign_tor", "mcc_tor_chest", "Open shared chest", CanUseManager, delegate
				{
					OpenSharedChest();
				}, leave: false, 3);
				AddMenuOption(starter, "multi_character_campaign_tor", "mcc_tor_appearance", "Edit active character appearance", CanUseManager, delegate
				{
					OpenAppearanceEditor(Hero.MainHero);
				}, leave: false, 4);
				AddMenuOption(starter, "multi_character_campaign_tor", "mcc_tor_career", "Review active companion career", CanReviewActiveCompanionCareer, delegate
				{
					OpenActiveCompanionCareerReview();
				}, leave: false, 5);
				AddMenuOption(starter, "multi_character_campaign_tor", "mcc_tor_status", "Show shared campaign status", CanUseManager, delegate
				{
					ShowStatus();
				}, leave: false, 6);
				AddMenuOption(starter, "multi_character_campaign_tor", "mcc_tor_leave", "Return", Always, delegate
				{
					GameMenu.ExitToLast();
				}, leave: true, 100);
				string[] array = new string[5] { "camp", "town", "castle", "village", "settlement" };
				string[] array2 = array;
				foreach (string text in array2)
				{
					try
					{
						AddMenuOption(starter, text, "mcc_tor_open_" + text, "Manage shared characters", CanUseManager, delegate
						{
							GameMenu.SwitchToMenu("multi_character_campaign_tor");
						}, leave: false, 90);
					}
					catch (Exception ex)
					{
						Log.Info("Parent menu not available: " + text + " (" + ex.GetType().Name + ")");
					}
				}
				Log.Info("Campaign menus registered.");
			}
			catch (Exception ex2)
			{
				Log.Error("Menu registration failed", ex2);
			}
		}

		private void AddMenuOption(CampaignGameStarter starter, string menu, string id, string text, GameMenuOption.OnConditionDelegate condition, Action consequence, bool leave, int index)
		{
			starter.AddGameMenuOption(menu, id, text, condition, delegate
			{
				Log.Info("MENU CLICK BEGIN id=" + id + "; menu=" + menu + "; text=" + text + "; mainHero=" + Reflection.IdOf(Hero.MainHero) + ".");
				try
				{
					consequence();
					Log.Info("MENU CLICK END id=" + id + ".");
				}
				catch (Exception ex)
				{
					Log.Error("Unhandled menu consequence failure id=" + id + "; menu=" + menu, ex);
					UI.Message("Multi-Character Campaign action failed. See MultiCharacterCampaignTOR.log at: " + Log.FilePath);
				}
			}, leave, index);
		}

		private void RegisterConversation(CampaignGameStarter starter)
		{
			try
			{
				starter.AddPlayerLine("mcc_tor_take_control", "hero_main_options", "close_window", "[Multi-Character Campaign] Take control of this character.", ConversationSwitchCondition, ConversationSwitchConsequence, 200);
				Log.Info("Companion activation dialogue registered.");
			}
			catch (Exception ex)
			{
				Log.Error("Conversation registration failed", ex);
			}
		}

		private void OnMenuInit(MenuCallbackArgs args)
		{
		}

		private bool Always(MenuCallbackArgs args)
		{
			return true;
		}

		private bool CanUseManager(MenuCallbackArgs args)
		{
			if (!(args.IsEnabled = CanChangeCampaignIdentity(out var reason, emergency: false)))
			{
				args.Tooltip = T(reason);
			}
			args.optionLeaveType = GameMenuOption.LeaveType.Manage;
			return true;
		}

		private bool CanSwitchMenu(MenuCallbackArgs args)
		{
			if (!(args.IsEnabled = CanChangeCampaignIdentity(out var reason, emergency: false) && EligibleSwitchTargets().Any()))
			{
				args.Tooltip = T((!string.IsNullOrEmpty(reason)) ? reason : "No other eligible registered character is in the main party.");
			}
			args.optionLeaveType = GameMenuOption.LeaveType.Manage;
			return true;
		}

		private bool CanRegisterCompanion(MenuCallbackArgs args)
		{
			if (!(args.IsEnabled = CanChangeCampaignIdentity(out var reason, emergency: false)))
			{
				args.Tooltip = T(reason);
			}
			else if (!DiscoverCompanions().Any())
			{
				args.Tooltip = T("No eligible companion is currently in the main party.");
			}
			args.optionLeaveType = GameMenuOption.LeaveType.Manage;
			return true;
		}

		private bool CanReviewActiveCompanionCareer(MenuCallbackArgs args)
		{
			Hero mainHero = Hero.MainHero;
			if (!(args.IsEnabled = CanChangeCampaignIdentity(out var reason, emergency: false) && mainHero != null && Contains(_originalCompanionIds, Reflection.IdOf(mainHero))))
			{
				args.Tooltip = T(string.IsNullOrEmpty(reason) ? "This option is available while controlling a companion registered by Multi-Character Campaign - TOR." : reason);
			}
			args.optionLeaveType = GameMenuOption.LeaveType.Manage;
			return true;
		}

		private bool ConversationSwitchCondition()
		{
			try
			{
				Hero oneToOneConversationHero = Hero.OneToOneConversationHero;
				if (oneToOneConversationHero == null || oneToOneConversationHero == Hero.MainHero)
				{
					return false;
				}
				if (!oneToOneConversationHero.IsAlive || !oneToOneConversationHero.IsActive || oneToOneConversationHero.IsPrisoner)
				{
					return false;
				}
				if (oneToOneConversationHero.PartyBelongedTo != MobileParty.MainParty)
				{
					return false;
				}
				return oneToOneConversationHero.CompanionOf == Clan.PlayerClan || Contains(_sharedHeroIds, Reflection.IdOf(oneToOneConversationHero));
			}
			catch
			{
				return false;
			}
		}

		private void ConversationSwitchConsequence()
		{
			Hero oneToOneConversationHero = Hero.OneToOneConversationHero;
			if (oneToOneConversationHero != null)
			{
				RegisterHero(oneToOneConversationHero, oneToOneConversationHero.CompanionOf == Clan.PlayerClan);
				_pendingConversationSwitchId = Reflection.IdOf(oneToOneConversationHero);
			}
		}

		private void OnConversationEnded(IEnumerable<CharacterObject> participants)
		{
			if (!string.IsNullOrEmpty(_pendingConversationSwitchId))
			{
				string pendingConversationSwitchId = _pendingConversationSwitchId;
				_pendingConversationSwitchId = string.Empty;
				Hero hero = ResolveHero(pendingConversationSwitchId);
				if (hero != null && TrySwitch(hero, emergency: false, notify: true))
				{
					OfferCareerForActivatedCompanion(hero);
				}
			}
		}

		private void OpenSharedChest()
		{
			try
			{
				if (_sharedChest == null)
				{
					_sharedChest = new ItemRoster();
				}
				InventoryScreenHelper.OpenScreenAsStash(_sharedChest);
				Log.Info("Shared chest opened.");
			}
			catch (Exception ex)
			{
				Log.Error("Opening shared chest failed", ex);
				UI.Message("Could not open the shared chest. See MultiCharacterCampaignTOR.log.");
			}
		}

		private void OpenSwitchSelection()
		{
			Log.Info("OpenSwitchSelection ENTER.");
			List<SelectionOption> list = (from h in EligibleSwitchTargets()
				select new SelectionOption(h, HeroLabel(h), HeroStatus(h), enabled: true)).ToList();
			Log.Info("OpenSwitchSelection option count=" + list.Count + ".");
			UI.SelectOne("Switch active character", "Select a registered character in the current main party.", list, delegate(SelectionOption o)
			{
				TrySwitch((Hero)o.Value, emergency: false, notify: true);
			});
			Log.Info("OpenSwitchSelection EXIT after SelectOne request.");
		}

		private void OpenCompanionSelection()
		{
			Log.Info("OpenCompanionSelection ENTER.");
			if (!CanChangeCampaignIdentity(out var reason, emergency: false))
			{
				Log.Warning("OpenCompanionSelection blocked: " + reason);
				UI.Message(reason);
				return;
			}
			List<Hero> list = (from h in Reflection.GetPlayerClanCompanions()
				where h != null
				select h).Distinct().ToList();
			Log.Info("Companion discovery raw count=" + list.Count + ".");
			foreach (Hero item in list)
			{
				try
				{
					Log.Info("Companion candidate id=" + Reflection.IdOf(item) + "; name=" + SafeName(item) + "; main=" + (item == Hero.MainHero) + "; alive=" + item.IsAlive + "; active=" + item.IsActive + "; prisoner=" + item.IsPrisoner + "; partyIsMain=" + (item.PartyBelongedTo == MobileParty.MainParty) + "; companionOfPlayerClan=" + (item.CompanionOf == Clan.PlayerClan) + ".");
				}
				catch (Exception ex)
				{
					Log.Error("Companion candidate diagnostic failed for id=" + Reflection.IdOf(item), ex);
				}
			}
			List<Hero> source = DiscoverCompanions().ToList();
			List<SelectionOption> list2 = source.Select((Hero h) => new SelectionOption(h, HeroLabel(h), "Register this companion and make them the active player character.", enabled: true)).ToList();
			Log.Info("Companion discovery eligible count=" + list2.Count + ".");
			if (list2.Count == 0)
			{
				UI.Message("No eligible companion is currently in the main party.");
				Log.Warning("Companion activation was requested, but no eligible main-party companion was discovered.");
				return;
			}
			UI.SelectOne("Activate companion", "The companion becomes playable. Their existing skills, equipment, appearance, and TOR progression are preserved.", list2, delegate(SelectionOption o)
			{
				Hero hero = (Hero)o.Value;
				Log.Info("Companion selection callback chose id=" + Reflection.IdOf(hero) + "; name=" + SafeName(hero) + ".");
				RegisterHero(hero, originalCompanion: true);
				if (TrySwitch(hero, emergency: false, notify: true))
				{
					OfferCareerForActivatedCompanion(hero);
				}
			});
			Log.Info("OpenCompanionSelection EXIT after SelectOne request.");
		}

		private void OfferCareerForActivatedCompanion(Hero hero)
		{
			if (hero != null && Hero.MainHero == hero)
			{
				if (string.Equals(_pendingCareerPromptHeroId, Reflection.IdOf(hero), StringComparison.Ordinal))
				{
					_pendingCareerPromptHeroId = string.Empty;
				}
				object career = TORBridge.GetCareer(hero);
				string careerId = TORBridge.GetCareerId(hero);
				if (career != null && TORBridge.IsCareerCompatibleWithExistingArchetype(hero, career))
				{
					AddUnique(_careerPromptHandledHeroIds, Reflection.IdOf(hero));
					AddUnique(_careerRepairHandledHeroIds, Reflection.IdOf(hero));
					Log.Info("Activated companion already has a compatible TOR career " + careerId + "; existing per-hero career state was preserved. " + TORBridge.GetCareerProgressSummary(hero));
				}
				else if (career != null || !string.IsNullOrEmpty(careerId))
				{
					OfferCareerRepair(hero, career, careerId);
				}
				else
				{
					OfferCareerSelectionForCareerlessCompanion(hero);
				}
			}
		}

		private void OfferCareerSelectionForCareerlessCompanion(Hero hero)
		{
			List<object> compatibleCareers = TORBridge.GetCompatibleCareers(hero);
			Log.Info("Activated companion has no TOR career. Hero=" + Reflection.IdOf(hero) + "; compatibleCareerCount=" + compatibleCareers.Count + "; archetype=" + TORBridge.DescribeArchetype(hero) + ".");
			if (compatibleCareers.Count == 0)
			{
				AddUnique(_careerPromptHandledHeroIds, Reflection.IdOf(hero));
				UI.Message(SafeName(hero) + " is now playable, but no TOR career can be assigned safely from this companion's existing archetype. Existing spells, lores, skills, and resources remain unchanged.");
				return;
			}
			List<SelectionOption> list = new List<SelectionOption>();
			list.Add(new SelectionOption(null, "Remain careerless", "No career is assigned. Existing spells, lores, skills, equipment, and resources remain attached to this character.", enabled: true));
			foreach (object item in compatibleCareers)
			{
				list.Add(new SelectionOption(item, Reflection.DisplayName(item), "Compatible with this companion's existing TOR archetype. Career progression remains stored only on this hero.", enabled: true));
			}
			UI.SelectOne("Choose a TOR career for " + SafeName(hero), "Only careers compatible with this companion's existing TOR attributes, culture, magic, and priest state are shown. Career points are derived from this hero's own level and choices.", list, delegate(SelectionOption option)
			{
				if (option.Value == null)
				{
					AddUnique(_careerPromptHandledHeroIds, Reflection.IdOf(hero));
					Log.Info("Career assignment skipped for activated companion hero=" + Reflection.IdOf(hero) + ".");
				}
				else
				{
					AssignCompanionCareer(hero, option.Value, repair: false);
				}
			});
		}

		private void OfferCareerRepair(Hero hero, object existingCareer, string existingCareerId)
		{
			if (hero == null || Hero.MainHero != hero)
			{
				return;
			}
			List<object> compatibleCareers = TORBridge.GetCompatibleCareers(hero);
			int careerChoiceCount = TORBridge.GetCareerChoiceCount(hero);
			string text = ((existingCareer != null) ? Reflection.DisplayName(existingCareer) : existingCareerId);
			string text2 = ((careerChoiceCount <= 1) ? " No spent career choices will be lost." : (" Changing careers removes this hero's " + (careerChoiceCount - 1) + " selected career choice(s)."));
			Log.Warning("Incompatible TOR career detected for activated companion. Hero=" + Reflection.IdOf(hero) + "; current=" + existingCareerId + "; archetype=" + TORBridge.DescribeArchetype(hero) + "; compatibleCount=" + compatibleCareers.Count + ".");
			List<SelectionOption> list = new List<SelectionOption>();
			foreach (object item in compatibleCareers)
			{
				list.Add(new SelectionOption(item, "Use " + Reflection.DisplayName(item), "Replaces only this hero's incompatible career and career-choice tree." + text2, enabled: true));
			}
			list.Add(new SelectionOption(null, "Remove the incompatible career and remain careerless", "Clears only this hero's CareerID and CareerChoices. Existing spells, lores, skills, equipment, and resources are retained." + text2, enabled: true));
			UI.SelectOne("Repair TOR career for " + SafeName(hero), "Current career: " + text + "\n\nThe current career does not match this companion's existing TOR archetype. This can expose career UI that expects missing priest, magic, race, or religion state and can crash. Select a compatible career or remove the invalid assignment." + text2, list, delegate(SelectionOption option)
			{
				if (option.Value == null)
				{
					try
					{
						TORBridge.ClearCareer(hero);
						AddUnique(_careerPromptHandledHeroIds, Reflection.IdOf(hero));
						AddUnique(_careerRepairHandledHeroIds, Reflection.IdOf(hero));
						TORBridge.RefreshAfterSwitch();
						UI.Message(SafeName(hero) + " is now careerless. Existing non-career TOR progression was retained.");
						return;
					}
					catch (Exception ex)
					{
						Log.Error("Could not clear incompatible TOR career for hero=" + Reflection.IdOf(hero), ex);
						UI.Message("Career repair failed. See MultiCharacterCampaignTOR.log.");
						return;
					}
				}
				AssignCompanionCareer(hero, option.Value, repair: true);
			});
		}

		private void AssignCompanionCareer(Hero hero, object career, bool repair)
		{
			try
			{
				TORBridge.AddCareer(hero, career);
				AddUnique(_careerPromptHandledHeroIds, Reflection.IdOf(hero));
				AddUnique(_careerRepairHandledHeroIds, Reflection.IdOf(hero));
				TORBridge.RefreshAfterSwitch();
				Log.Info(((!repair) ? "Assigned" : "Repaired") + " companion career. Hero=" + Reflection.IdOf(hero) + "; career=" + Reflection.IdOf(career) + "; " + TORBridge.GetCareerProgressSummary(hero));
				UI.Message(SafeName(hero) + " now follows the " + Reflection.DisplayName(career) + " career. Career points and choices are independent for this character.");
			}
			catch (Exception ex)
			{
				Log.Error("TOR career assignment failed for activated companion hero=" + Reflection.IdOf(hero), ex);
				UI.Message("The companion remains playable, but TOR career assignment failed: " + ex.Message + ". See MultiCharacterCampaignTOR.log.");
			}
		}

		private void OpenActiveCompanionCareerReview()
		{
			Hero mainHero = Hero.MainHero;
			if (mainHero == null || !Contains(_originalCompanionIds, Reflection.IdOf(mainHero)))
			{
				UI.Message("The active character was not registered from a companion.");
				return;
			}
			object career = TORBridge.GetCareer(mainHero);
			if (career == null)
			{
				OfferCareerSelectionForCareerlessCompanion(mainHero);
				return;
			}
			if (!TORBridge.IsCareerCompatibleWithExistingArchetype(mainHero, career))
			{
				OfferCareerRepair(mainHero, career, TORBridge.GetCareerId(mainHero));
				return;
			}
			UI.Message(SafeName(mainHero) + " currently follows " + Reflection.DisplayName(career) + ".\n\n" + TORBridge.GetCareerProgressSummary(mainHero) + "\n\nThis career is compatible with the companion's existing TOR archetype. Career state is stored on this hero only.");
		}

		private void QueueMissingCareerPromptForActiveOriginalCompanion()
		{
			Hero mainHero = Hero.MainHero;
			if (mainHero == null)
			{
				return;
			}
			string text = Reflection.IdOf(mainHero);
			if (!Contains(_originalCompanionIds, text))
			{
				return;
			}
			object career = TORBridge.GetCareer(mainHero);
			if (career == null)
			{
				if (!Contains(_careerPromptHandledHeroIds, text))
				{
					_pendingCareerPromptHeroId = text;
					Log.Info("Queued TOR career selection for previously activated careerless companion hero=" + text + ".");
				}
			}
			else if (!TORBridge.IsCareerCompatibleWithExistingArchetype(mainHero, career))
			{
				if (!Contains(_careerRepairHandledHeroIds, text))
				{
					_pendingCareerPromptHeroId = text;
					Log.Warning("Queued TOR career repair for active companion hero=" + text + "; currentCareer=" + TORBridge.GetCareerId(mainHero) + "; archetype=" + TORBridge.DescribeArchetype(mainHero) + ".");
				}
			}
			else
			{
				AddUnique(_careerPromptHandledHeroIds, text);
				AddUnique(_careerRepairHandledHeroIds, text);
				Log.Info("Active companion career is compatible. Hero=" + text + "; " + TORBridge.GetCareerProgressSummary(mainHero));
			}
		}

		private void ProcessPendingCareerPrompt()
		{
			if (!string.IsNullOrEmpty(_pendingCareerPromptHeroId) && !_switchInProgress && !_pendingDeathSuccession && !Reflection.IsMissionActive() && !Reflection.IsEncounterActive() && !Reflection.IsBarterOrInventoryActive())
			{
				string pendingCareerPromptHeroId = _pendingCareerPromptHeroId;
				Hero mainHero = Hero.MainHero;
				if (mainHero == null || Reflection.IdOf(mainHero) != pendingCareerPromptHeroId)
				{
					_pendingCareerPromptHeroId = string.Empty;
					return;
				}
				_pendingCareerPromptHeroId = string.Empty;
				OfferCareerForActivatedCompanion(mainHero);
			}
		}

		private void StartCharacterCreation()
		{
			NativeCharacterCreation.Start(this);
		}

		private void SelectSex(object culture)
		{
			List<SelectionOption> list = new List<SelectionOption>();
			list.Add(new SelectionOption(false, "Male", "Create a male hero.", enabled: true));
			list.Add(new SelectionOption(true, "Female", "Create a female hero.", enabled: true));
			UI.SelectOne("Create shared character: sex", "Select the character's sex.", list, delegate(SelectionOption o)
			{
				AskCharacterName(culture, (bool)o.Value);
			});
		}

		private void AskCharacterName(object culture, bool female)
		{
			string suggested = Reflection.RandomCultureName(culture, female);
			UI.AskText("Create shared character: name", "Enter the new hero's name.", suggested, delegate(string name)
			{
				string name2 = ((!string.IsNullOrWhiteSpace(name)) ? name.Trim() : suggested);
				SelectAge(culture, female, name2);
			});
		}

		private void SelectAge(object culture, bool female, string name)
		{
			int[] source = new int[8] { 18, 22, 25, 30, 35, 40, 50, 60 };
			List<SelectionOption> options = source.Select((int a) => new SelectionOption(a, a + " years", "Starting age " + a + ".", enabled: true)).ToList();
			UI.SelectOne("Create shared character: age", "Select a starting age.", options, delegate(SelectionOption o)
			{
				SelectBackground(culture, female, name, (int)o.Value);
			});
		}

		private void SelectBackground(object culture, bool female, string name, int age)
		{
			List<BackgroundProfile> list = new List<BackgroundProfile>();
			list.Add(new BackgroundProfile("martial", "Martial upbringing", "+20 One Handed, Two Handed, and Polearm; +1 Vigor; one focus in each skill.", "Vigor", "OneHanded", "TwoHanded", "Polearm"));
			list.Add(new BackgroundProfile("marksman", "Hunter and marksman", "+20 Bow, Crossbow, and Athletics; +1 Control; one focus in each skill.", "Control", "Bow", "Crossbow", "Athletics"));
			list.Add(new BackgroundProfile("rider", "Mounted retainer", "+20 Riding, Polearm, and Tactics; +1 Endurance; one focus in each skill.", "Endurance", "Riding", "Polearm", "Tactics"));
			list.Add(new BackgroundProfile("scout", "Wilderness scout", "+20 Scouting, Athletics, and Bow; +1 Cunning; one focus in each skill.", "Cunning", "Scouting", "Athletics", "Bow"));
			list.Add(new BackgroundProfile("courtier", "Court and command", "+20 Charm, Leadership, and Trade; +1 Social; one focus in each skill.", "Social", "Charm", "Leadership", "Trade"));
			list.Add(new BackgroundProfile("scholar", "Scholar and administrator", "+20 Medicine, Engineering, and Steward; +1 Intelligence; one focus in each skill.", "Intelligence", "Medicine", "Engineering", "Steward"));
			list.Add(new BackgroundProfile("balanced", "Balanced adventurer", "+20 Athletics, One Handed, and Steward; +1 Endurance; one focus in each skill.", "Endurance", "Athletics", "OneHanded", "Steward"));
			List<BackgroundProfile> source = list;
			List<SelectionOption> options = source.Select((BackgroundProfile b) => new SelectionOption(b, b.Name, b.Description, enabled: true)).ToList();
			UI.SelectOne("Create shared character: background", "Select a starting skill package. TOR career setup is applied afterward.", options, delegate(SelectionOption o)
			{
				CreateCharacter(culture, female, name, age, (BackgroundProfile)o.Value);
			});
		}

		private void CreateCharacter(object culture, bool female, string name, int age, BackgroundProfile background)
		{
			Hero mainHero = Hero.MainHero;
			try
			{
				CharacterObject characterObject = Reflection.FindHeroTemplate(culture, female);
				if (characterObject == null)
				{
					throw new InvalidOperationException("No usable character template exists for " + Reflection.DisplayName(culture) + ".");
				}
				Hero hero = HeroCreator.CreateSpecialHero(characterObject, (Clan.PlayerClan != null) ? Clan.PlayerClan.HomeSettlement : null, Clan.PlayerClan, null, age);
				if (hero == null)
				{
					throw new InvalidOperationException("HeroCreator returned null.");
				}
				hero.IsFemale = female;
				TextObject textObject = T(name);
				hero.SetName(textObject, textObject);
				hero.CompanionOf = null;
				hero.Clan = Clan.PlayerClan;
				Reflection.EnsureHeroActive(hero);
				Reflection.ApplyBackground(hero, background);
				Reflection.EnforceNewHeroLevelOne(hero);
				AddHeroToPartyAction.Apply(hero, MobileParty.MainParty, showNotification: false);
				Reflection.EnsureHeroInMainParty(hero);
				RegisterHero(hero, originalCompanion: false);
				SynchronizeGold(_sharedGold);
				Log.Info("New hero prepared for activation. Hero=" + Reflection.IdOf(hero) + "; active=" + hero.IsActive + "; alive=" + hero.IsAlive + "; party=" + Reflection.IdOf(hero.PartyBelongedTo) + "; rosterContains=" + Reflection.IsHeroInMainPartyRoster(hero) + ".");
				if (!CanSwitchTo(hero, emergency: false, out var reason))
				{
					throw new InvalidOperationException("The new hero was created but is not switchable: " + reason);
				}
				if (!TrySwitch(hero, emergency: false, notify: false))
				{
					throw new InvalidOperationException("The new hero was created and validated, but activation failed. See the preceding switch error in the log.");
				}
				Log.Info("Created shared character " + Reflection.IdOf(hero) + " named " + name + ".");
				SelectCareerForNewHero(hero);
			}
			catch (Exception ex)
			{
				Log.Error("Character creation failed", ex);
				UI.Message("Character creation failed: " + ex.Message + ". See MultiCharacterCampaignTOR.log.");
				if (mainHero != null && mainHero.IsAlive && mainHero.PartyBelongedTo == MobileParty.MainParty && Hero.MainHero != mainHero)
				{
					TrySwitch(mainHero, emergency: true, notify: false);
				}
			}
		}

		private void SelectCareerForNewHero(Hero hero)
		{
			List<object> eligibleCareers = TORBridge.GetEligibleCareers(hero);
			List<SelectionOption> list = new List<SelectionOption>();
			list.Add(new SelectionOption(null, "No TOR career", "Keep this hero without a career for now.", enabled: true));
			foreach (object item in eligibleCareers)
			{
				list.Add(new SelectionOption(item, Reflection.DisplayName(item), Reflection.IdOf(item), enabled: true));
			}
			UI.SelectOne("Create shared character: TOR career", "Choose a career. Career initialization runs while this hero is the real active player character, preserving TOR's MainHero-dependent setup.", list, delegate(SelectionOption o)
			{
				if (o.Value != null)
				{
					try
					{
						TORBridge.AddCareerForNewHero(hero, o.Value);
					}
					catch (Exception ex)
					{
						Log.Error("TOR career initialization failed", ex);
						UI.Message("The hero was created, but TOR career initialization failed: " + ex.Message);
					}
				}
				Reflection.EnforceNewHeroLevelOne(hero);
				OpenAppearanceEditor(hero);
			});
		}

		private void OpenAppearanceEditor(Hero hero)
		{
			try
			{
				if (hero != null)
				{
					Type type = Type.GetType("TaleWorlds.CampaignSystem.GameState.BarberState, TaleWorlds.CampaignSystem");
					Type type2 = Type.GetType("TaleWorlds.MountAndBlade.Module, TaleWorlds.MountAndBlade");
					if (type == null || type2 == null)
					{
						throw new InvalidOperationException("BarberState or Module type unavailable.");
					}
					object first = Activator.CreateInstance(type, hero.CharacterObject, null);
					object value = type2.GetProperty("CurrentModule", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
					object value2 = type2.GetProperty("GlobalGameStateManager", BindingFlags.Instance | BindingFlags.Public).GetValue(value, null);
					MethodInfo methodInfo = value2.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "PushState" && m.GetParameters().Length >= 1);
					if (methodInfo == null)
					{
						throw new MissingMethodException("GameStateManager.PushState");
					}
					methodInfo.Invoke(value2, BuildCallArguments(methodInfo.GetParameters(), first));
					Log.Info("Appearance editor opened for " + Reflection.IdOf(hero) + ".");
				}
			}
			catch (Exception ex)
			{
				Log.Error("Appearance editor failed", ex);
				UI.Message("The character is playable, but the appearance editor could not be opened. See MultiCharacterCampaignTOR.log.");
			}
		}

		public bool TrySwitch(Hero target, bool emergency, bool notify)
		{
			if (target == null || _switchInProgress)
			{
				return false;
			}
			Hero mainHero = Hero.MainHero;
			if (mainHero == target)
			{
				return true;
			}
			if (!CanSwitchTo(target, emergency, out var reason))
			{
				if (notify)
				{
					UI.Message(reason);
				}
				return false;
			}
			MobileParty mainParty = MobileParty.MainParty;
			PartyInventorySnapshot partyInventorySnapshot = ((mainHero == null || !mainHero.IsAlive) ? null : PartyInventorySnapshot.Capture(mainParty));
			_switchInProgress = true;
			try
			{
				if (mainHero != null && mainHero.IsAlive)
				{
					_sharedGold = mainHero.Gold;
				}
				RegisterHero(target, target.CompanionOf == Clan.PlayerClan);
				PrepareTargetForActivation(target);
				MobileParty mainParty2 = MobileParty.MainParty;
				if (mainParty2 == null)
				{
					throw new InvalidOperationException("The main party is unavailable.");
				}
				mainParty2.ChangePartyLeader(target);
				ChangePlayerCharacterAction.Apply(target);
				RebindMainPartyIdentity(target);
				_activeHeroId = Reflection.IdOf(target);
				SynchronizeGold(_sharedGold);
				RestoreInactiveCompanion(mainHero);
				partyInventorySnapshot?.RestoreIfChanged(MobileParty.MainParty);
				CareerUniquesBridge.RefreshAfterSwitch();
				TORBridge.RefreshAfterSwitch();
				PartyScreenSelectionBridge.RequestSelection("active character changed to " + Reflection.IdOf(target));
				QueueMissingCareerPromptForActiveOriginalCompanion();
				Log.Info("Player character changed from " + Reflection.IdOf(mainHero) + " to " + _activeHeroId + ". Shared gold=" + _sharedGold + ".");
				if (notify)
				{
					UI.Message("Now controlling " + SafeName(target) + ".");
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("Player-character switch failed", ex);
				if (notify)
				{
					UI.Message("Character switch failed: " + ex.Message + ". See MultiCharacterCampaignTOR.log.");
				}
				try
				{
					if (mainHero != null && mainHero.IsAlive)
					{
						PrepareTargetForActivation(mainHero);
						if (MobileParty.MainParty != null)
						{
							MobileParty.MainParty.ChangePartyLeader(mainHero);
						}
						if (Hero.MainHero != mainHero)
						{
							ChangePlayerCharacterAction.Apply(mainHero);
						}
						RebindMainPartyIdentity(mainHero);
						_activeHeroId = Reflection.IdOf(mainHero);
						SynchronizeGold(_sharedGold);
						PartyScreenSelectionBridge.RequestSelection("switch rollback restored " + Reflection.IdOf(mainHero));
					}
					if (target != Hero.MainHero)
					{
						RestoreInactiveCompanion(target);
					}
					partyInventorySnapshot?.RestoreIfChanged(MobileParty.MainParty);
				}
				catch (Exception ex2)
				{
					Log.Error("Switch rollback failed", ex2);
				}
				return false;
			}
			finally
			{
				_switchInProgress = false;
			}
		}

		private void RebindMainPartyIdentity(Hero target)
		{
			if (target == null)
			{
				return;
			}
			MobileParty mainParty = MobileParty.MainParty;
			if (mainParty == null)
			{
				throw new InvalidOperationException("The main party is unavailable while rebinding its active character.");
			}
			if (mainParty.LeaderHero != target)
			{
				mainParty.ChangePartyLeader(target);
			}
			object obj = Reflection.GetMember(mainParty, "LordPartyComponent") ?? Reflection.GetMember(mainParty, "PartyComponent");
			if (obj != null)
			{
				object objA = Reflection.GetMember(obj, "PartyOwner") ?? Reflection.GetMember(obj, "Owner");
				if (!object.ReferenceEquals(objA, target))
				{
					MethodInfo methodInfo = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "ChangePartyOwner" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(Hero)));
					if (methodInfo != null)
					{
						methodInfo.Invoke(obj, new object[1] { target });
					}
				}
			}
			SetActiveLeader(null, target, null);
			TORBridge.RefreshAfterSwitch();
			object member = Reflection.GetMember(mainParty, "MemberRoster");
			InvokeNoArgIfPresent(member, "UpdateVersion");
			object member2 = Reflection.GetMember(mainParty, "Party");
			InvokeNoArgIfPresent(member2, "SetVisualAsDirty");
		}

		private static void InvokeNoArgIfPresent(object instance, string methodName)
		{
			if (instance != null && !string.IsNullOrEmpty(methodName))
			{
				MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				if (method != null)
				{
					method.Invoke(instance, null);
				}
			}
		}

		private static void SetActiveLeader(string phase, Hero oldHero, Hero goldChange)
		{
			Clan playerClan = Clan.PlayerClan;
			if (playerClan != null && !object.ReferenceEquals(Reflection.GetMember(playerClan, "Leader"), oldHero))
			{
				((object)Clan.PlayerClan).GetType().GetField("_leader", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(Clan.PlayerClan, oldHero);
			}
		}

		private bool CanSwitchTo(Hero target, bool emergency, out string reason)
		{
			if (!emergency && !CanChangeCampaignIdentity(out reason, emergency: false))
			{
				return false;
			}
			if (!target.IsAlive)
			{
				reason = "That character is dead.";
				return false;
			}
			if (!target.IsActive)
			{
				reason = "That character is not active in the campaign.";
				return false;
			}
			if (target.IsPrisoner)
			{
				reason = "A prisoner cannot become the active character.";
				return false;
			}
			if (target.PartyBelongedTo != MobileParty.MainParty)
			{
				reason = "Character switching is limited to heroes in the current main party.";
				return false;
			}
			reason = string.Empty;
			return true;
		}

		private bool CanChangeCampaignIdentity(out string reason, bool emergency)
		{
			reason = string.Empty;
			if (_switchInProgress)
			{
				reason = "A character switch is already in progress.";
				return false;
			}
			if (Campaign.Current == null || Hero.MainHero == null || MobileParty.MainParty == null)
			{
				reason = "No active campaign or main party is available.";
				return false;
			}
			if (!emergency && Hero.MainHero.IsPrisoner)
			{
				reason = "Characters cannot be switched while the active character is imprisoned.";
				return false;
			}
			if (!emergency && Reflection.IsMissionActive())
			{
				reason = "Characters cannot be switched during a mission or battle.";
				return false;
			}
			if (!emergency && Reflection.IsEncounterActive())
			{
				reason = "Characters cannot be switched during a player encounter.";
				return false;
			}
			if (!emergency && Reflection.IsBarterOrInventoryActive())
			{
				reason = "Characters cannot be switched while another campaign transaction screen is active.";
				return false;
			}
			return true;
		}

		private void PrepareTargetForActivation(Hero hero)
		{
			if (hero != null)
			{
				hero.CompanionOf = null;
				hero.Clan = Clan.PlayerClan;
				hero.Gold = _sharedGold;
			}
		}

		private void RestoreInactiveCompanion(Hero hero)
		{
			if (hero == null || !hero.IsAlive || hero == Hero.MainHero)
			{
				return;
			}
			string text = Reflection.IdOf(hero);
			if (!Contains(_originalCompanionIds, text))
			{
				return;
			}
			try
			{
				hero.Clan = null;
				hero.CompanionOf = Clan.PlayerClan;
			}
			catch (Exception ex)
			{
				Log.Error("Could not restore inactive companion classification for " + text, ex);
			}
		}

		private void RegisterHero(Hero hero, bool originalCompanion)
		{
			if (hero != null)
			{
				string value = Reflection.IdOf(hero);
				AddUnique(_sharedHeroIds, value);
				if (originalCompanion)
				{
					AddUnique(_originalCompanionIds, value);
				}
			}
		}

		private IEnumerable<Hero> EligibleSwitchTargets()
		{
			string[] array = _sharedHeroIds.ToArray();
			foreach (string id in array)
			{
				Hero hero = ResolveHero(id);
				if (hero != null && hero != Hero.MainHero && CanSwitchTo(hero, emergency: true, out var _))
				{
					yield return hero;
				}
			}
		}

		private IEnumerable<Hero> DiscoverCompanions()
		{
			List<Hero> list = new List<Hero>();
			foreach (Hero playerClanCompanion in Reflection.GetPlayerClanCompanions())
			{
				if (playerClanCompanion == null)
				{
					continue;
				}
				try
				{
					if (playerClanCompanion != Hero.MainHero && playerClanCompanion.IsAlive && playerClanCompanion.IsActive && !playerClanCompanion.IsPrisoner && playerClanCompanion.PartyBelongedTo == MobileParty.MainParty && !list.Contains(playerClanCompanion))
					{
						list.Add(playerClanCompanion);
					}
				}
				catch (Exception ex)
				{
					Log.Error("Companion eligibility check failed for id=" + Reflection.IdOf(playerClanCompanion), ex);
				}
			}
			return list;
		}

		private void OnCampaignTick(float dt)
		{
			ProcessPendingCareerPrompt();
			if (_pendingDeathSuccession && !_switchInProgress && !Reflection.IsMissionActive() && !Reflection.IsEncounterActive() && !Reflection.IsBarterOrInventoryActive())
			{
				_pendingDeathSuccession = false;
				if (!TryEmergencySuccession(force: true))
				{
					Log.Info("Deferred death succession could not find an eligible successor; native game-over handling may continue.");
				}
			}
		}

		private void OnHourlyTick()
		{
			try
			{
				if (Hero.MainHero != null)
				{
					RegisterHero(Hero.MainHero, originalCompanion: false);
					_sharedGold = Hero.MainHero.Gold;
					SynchronizeGold(_sharedGold);
					if (_activeHeroId != Reflection.IdOf(Hero.MainHero))
					{
						_activeHeroId = Reflection.IdOf(Hero.MainHero);
					}
					CareerUniquesBridge.RefreshAfterSwitch();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Hourly synchronization failed", ex);
			}
		}

		private void SynchronizeGold(int amount)
		{
			_sharedGold = Math.Max(0, amount);
			string[] array = _sharedHeroIds.ToArray();
			foreach (string text in array)
			{
				Hero hero = ResolveHero(text);
				if (hero != null && hero.IsAlive)
				{
					try
					{
						hero.Gold = _sharedGold;
					}
					catch (Exception ex)
					{
						Log.Error("Could not synchronize shared gold to " + text, ex);
					}
				}
			}
		}

		private void OnBeforeHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
		{
			if (victim == Hero.MainHero)
			{
				_deathTransition = true;
			}
		}

		private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
		{
			try
			{
				if (victim == null)
				{
					return;
				}
				string text = Reflection.IdOf(victim);
				if (!Contains(_sharedHeroIds, text))
				{
					return;
				}
				Log.Info("Registered shared character died: " + text + ".");
				if (_deathTransition || victim == Hero.MainHero)
				{
					if (Reflection.IsMissionActive() || Reflection.IsEncounterActive() || Reflection.IsBarterOrInventoryActive())
					{
						_pendingDeathSuccession = EligibleSuccessors().Any();
						Log.Info((!_pendingDeathSuccession) ? "Active-character death occurred during an unsafe state and no eligible successor exists." : "Active-character death occurred during an unsafe state; succession was deferred until the campaign map resumes.");
					}
					else if (!TryEmergencySuccession(force: true))
					{
						Log.Info("No eligible successor remained; native campaign death handling continues.");
					}
					_deathTransition = false;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Death succession handler failed", ex);
			}
		}

		private void OnGameOver()
		{
			try
			{
				if (!_suppressNextGameOver && (Hero.MainHero == null || !Hero.MainHero.IsAlive || !Hero.MainHero.IsActive))
				{
					TryEmergencySuccession(force: false);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Game-over succession event failed", ex);
			}
		}

		public bool TryEmergencySuccession()
		{
			return TryEmergencySuccession(force: false);
		}

		private bool TryEmergencySuccession(bool force)
		{
			if (_switchInProgress)
			{
				return false;
			}
			if (Reflection.IsMissionActive() || Reflection.IsEncounterActive() || Reflection.IsBarterOrInventoryActive())
			{
				_pendingDeathSuccession = EligibleSuccessors().Any();
				return false;
			}
			Hero mainHero = Hero.MainHero;
			if (!force && mainHero != null && mainHero.IsAlive && mainHero.IsActive)
			{
				return false;
			}
			Hero hero = EligibleSuccessors().FirstOrDefault();
			if (hero == null)
			{
				return false;
			}
			bool flag = TrySwitch(hero, emergency: true, notify: false);
			if (flag)
			{
				_suppressNextGameOver = true;
				UI.Message(SafeName(hero) + " has become the active character after the previous character's death.");
				Log.Info("Emergency succession completed with " + Reflection.IdOf(hero) + ".");
			}
			return flag;
		}

		public bool ShouldSuppressGameOver()
		{
			if (_suppressNextGameOver)
			{
				_suppressNextGameOver = false;
				return true;
			}
			Hero mainHero = Hero.MainHero;
			if ((mainHero == null || !mainHero.IsAlive || !mainHero.IsActive) && (Reflection.IsMissionActive() || Reflection.IsEncounterActive() || Reflection.IsBarterOrInventoryActive()) && EligibleSuccessors().Any())
			{
				_pendingDeathSuccession = true;
				Log.Info("Suppressed native game over while succession waits for a safe campaign-map state.");
				return true;
			}
			if (TryEmergencySuccession(force: false))
			{
				_suppressNextGameOver = false;
				return true;
			}
			return false;
		}

		private IEnumerable<Hero> EligibleSuccessors()
		{
			string[] array = _sharedHeroIds.ToArray();
			foreach (string id in array)
			{
				Hero hero = ResolveHero(id);
				if (hero != null && hero != Hero.MainHero && hero.IsAlive && hero.IsActive && !hero.IsPrisoner && hero.PartyBelongedTo == MobileParty.MainParty)
				{
					yield return hero;
				}
			}
		}

		private void ShowStatus()
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			List<string> list = new List<string>();
			foreach (string sharedHeroId in _sharedHeroIds)
			{
				Hero hero = ResolveHero(sharedHeroId);
				if (hero == null || !hero.IsAlive)
				{
					num2++;
					list.Add("- " + sharedHeroId + ": dead or unavailable");
					continue;
				}
				num++;
				if (hero.IsPrisoner)
				{
					num3++;
				}
				if (hero.PartyBelongedTo != MobileParty.MainParty)
				{
					num4++;
				}
				list.Add("- " + SafeName(hero) + ((hero != Hero.MainHero) ? "" : " [ACTIVE]") + ": " + HeroStatus(hero) + "; career=" + ((!string.IsNullOrEmpty(TORBridge.GetCareerId(hero))) ? TORBridge.GetCareerId(hero) : "none") + "; " + TORBridge.GetCareerProgressSummary(hero));
			}
			string text = "Registered characters: " + _sharedHeroIds.Count + "\nAlive: " + num + "\nDead/unavailable: " + num2 + "\nPrisoners: " + num3 + "\nOutside main party: " + num4 + "\nShared purse: " + _sharedGold + "\n\n" + string.Join("\n", list.ToArray()) + "\n\nMod version: 1.0.7\nDiagnostic log: " + Log.FilePath + "\n\nQuests and TOR Career Uniques acquisition are campaign-shared. TOR career resources, skills, perks, spells, equipment, health, and appearance remain attached to each hero.";
			UI.Message(text);
		}

		private string HeroStatus(Hero hero)
		{
			if (hero == null)
			{
				return "unavailable";
			}
			if (!hero.IsAlive)
			{
				return "dead";
			}
			if (hero.IsPrisoner)
			{
				return "prisoner";
			}
			if (hero.PartyBelongedTo != MobileParty.MainParty)
			{
				return "outside the main party";
			}
			if (!hero.IsActive)
			{
				return "inactive";
			}
			return "ready";
		}

		private string HeroLabel(Hero hero)
		{
			return SafeName(hero) + " (age " + (int)hero.Age/*cast due to constrained. prefix*/ + ")";
		}

		private void PruneMissingIds()
		{
			_sharedHeroIds = _sharedHeroIds.Where((string id) => !string.IsNullOrEmpty(id) && ResolveHero(id) != null).Distinct().ToList();
			_originalCompanionIds = _originalCompanionIds.Where((string id) => !string.IsNullOrEmpty(id) && ResolveHero(id) != null).Distinct().ToList();
			_careerPromptHandledHeroIds = _careerPromptHandledHeroIds.Where((string id) => !string.IsNullOrEmpty(id) && ResolveHero(id) != null).Distinct().ToList();
			_careerRepairHandledHeroIds = _careerRepairHandledHeroIds.Where((string id) => !string.IsNullOrEmpty(id) && ResolveHero(id) != null).Distinct().ToList();
		}

		private static string SafeName(Hero hero)
		{
			try
			{
				return (hero != null && (object)hero.Name != null) ? hero.Name.ToString() : Reflection.IdOf(hero);
			}
			catch
			{
				return Reflection.IdOf(hero);
			}
		}

		private static Hero ResolveHero(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}
			try
			{
				return Hero.Find(id);
			}
			catch
			{
				return Reflection.FindHero(id);
			}
		}

		private static bool Contains(List<string> list, string value)
		{
			return list?.Any((string x) => string.Equals(x, value, StringComparison.Ordinal)) ?? false;
		}

		private static void AddUnique(List<string> list, string value)
		{
			if (list != null && !string.IsNullOrEmpty(value) && !Contains(list, value))
			{
				list.Add(value);
			}
		}

		private static TextObject T(string text)
		{
			return new TextObject(text ?? string.Empty);
		}

		private static object[] BuildCallArguments(ParameterInfo[] ps, object first)
		{
			object[] array = new object[ps.Length];
			if (ps.Length > 0)
			{
				array[0] = first;
			}
			for (int i = 1; i < ps.Length; i++)
			{
				array[i] = ((!ps[i].ParameterType.IsValueType) ? null : Activator.CreateInstance(ps[i].ParameterType));
			}
			return array;
		}

		public static float GetBestRegisteredRelationForDefection(Hero targetLord)
		{
			//IL_0096->IL0096: Incompatible stack heights: 1 vs 0
			if (targetLord == null)
			{
				return 0f;
			}
			float num = targetLord.GetUnmodifiedClanLeaderRelationshipWithPlayer();
			MultiCharacterCampaignBehavior instance = Instance;
			if (instance != null)
			{
				List<string> sharedHeroIds = instance._sharedHeroIds;
				if (sharedHeroIds != null)
				{
					for (int i = 0; i < sharedHeroIds.Count; i++)
					{
						Hero hero = ResolveHero(sharedHeroIds[i]);
						if (hero != null)
						{
							float num2 = targetLord.GetBaseHeroRelation(hero);
							if (!(num2 <= num))
							{
								num = num2;
							}
						}
					}
				}
			}
			return num;
		}
	}
}
