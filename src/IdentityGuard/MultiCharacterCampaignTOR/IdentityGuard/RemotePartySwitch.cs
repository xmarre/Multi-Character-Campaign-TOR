// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class RemotePartySwitch
	{
		private sealed class InventoryHandoff
		{
			private readonly MobileParty _sourceParty;

			private readonly MobileParty _targetParty;

			private readonly object _sourcePartyBase;

			private readonly object _targetPartyBase;

			private readonly object _sourceInventory;

			private readonly object _targetInventoryOriginal;

			private readonly object _targetInventoryBackup;

			internal readonly int SourceElementCount;

			internal readonly int TargetElementCount;

			private InventoryHandoff(MobileParty sourceParty, MobileParty targetParty)
			{
				_sourceParty = sourceParty;
				_targetParty = targetParty;
				_sourcePartyBase = GetMember(sourceParty, "Party");
				_targetPartyBase = GetMember(targetParty, "Party");
				_sourceInventory = GetMember(_sourcePartyBase, "ItemRoster");
				_targetInventoryOriginal = GetMember(_targetPartyBase, "ItemRoster");
				if (_sourceInventory == null || _targetInventoryOriginal == null)
				{
					throw new InvalidOperationException("The source or destination inventory roster is unavailable.");
				}
				Type type = _targetInventoryOriginal.GetType();
				ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { type }, null);
				if (constructor == null)
				{
					throw new MissingMethodException(type.FullName, ".ctor(" + type.FullName + ")");
				}
				_targetInventoryBackup = constructor.Invoke(new object[1] { _targetInventoryOriginal });
				SourceElementCount = GetCount(_sourceInventory);
				TargetElementCount = GetCount(_targetInventoryOriginal);
			}

			internal static InventoryHandoff Capture(MobileParty sourceParty, MobileParty targetParty)
			{
				return new InventoryHandoff(sourceParty, targetParty);
			}

			internal void Commit()
			{
				bool flag = false;
				try
				{
					SetItemRoster(_targetPartyBase, _sourceInventory);
					flag = true;
					SetItemRoster(_sourcePartyBase, _targetInventoryBackup);
					InvalidateInventoryCaches(_sourceParty);
					InvalidateInventoryCaches(_targetParty);
				}
				catch
				{
					if (flag)
					{
						try
						{
							SetItemRoster(_targetPartyBase, _targetInventoryOriginal);
						}
						catch
						{
						}
					}
					throw;
				}
			}

			internal void AssertCommitted()
			{
				if (GetMember(_targetPartyBase, "ItemRoster") != _sourceInventory || GetMember(_sourcePartyBase, "ItemRoster") != _targetInventoryBackup)
				{
					throw new InvalidOperationException("The shared inventory handoff did not remain attached to the expected parties.");
				}
			}

			private static int GetCount(object roster)
			{
				object member = GetMember(roster, "Count");
				if (member != null)
				{
					return Convert.ToInt32(member);
				}
				return -1;
			}
		}

		private sealed class PartyState
		{
			internal readonly Hero Leader;

			internal readonly Hero Owner;

			internal readonly object Position;

			internal readonly object Army;

			internal readonly object Settlement;

			internal readonly List<object> Members;

			internal readonly List<object> Prisoners;

			internal readonly List<object> Ships;

			private PartyState(MobileParty party)
			{
				Leader = party?.LeaderHero;
				Owner = ((party == null) ? null : GetPartyOwner(party));
				Position = GetMember(party, "Position");
				Army = GetMember(party, "Army");
				Settlement = GetMember(party, "CurrentSettlement");
				Members = SnapshotEnumerable(GetMember(party, "MemberRoster"));
				Prisoners = SnapshotEnumerable(GetMember(party, "PrisonRoster"));
				Ships = SnapshotEnumerable(GetMember(party, "Ships"));
			}

			internal static PartyState Capture(MobileParty party)
			{
				return new PartyState(party);
			}

			internal void AssertPhysicalStateUnchanged(MobileParty party, string role)
			{
				if (party == null)
				{
					throw new InvalidOperationException("The " + role + " party reference became null.");
				}
				AssertEqual(Position, GetMember(party, "Position"), role + " party position");
				AssertSame(Army, GetMember(party, "Army"), role + " party army");
				AssertSame(Settlement, GetMember(party, "CurrentSettlement"), role + " party settlement");
				AssertSequence(Members, SnapshotEnumerable(GetMember(party, "MemberRoster")), role + " party member roster", referenceIdentity: false);
				AssertSequence(Prisoners, SnapshotEnumerable(GetMember(party, "PrisonRoster")), role + " party prisoner roster", referenceIdentity: false);
				AssertSequence(Ships, SnapshotEnumerable(GetMember(party, "Ships")), role + " party fleet", referenceIdentity: true);
			}

			private static List<object> SnapshotEnumerable(object value)
			{
				List<object> list = new List<object>();
				if (!(value is IEnumerable enumerable))
				{
					return list;
				}
				foreach (object item in enumerable)
				{
					list.Add(item);
				}
				return list;
			}

			private static void AssertSequence(List<object> before, List<object> after, string name, bool referenceIdentity)
			{
				if (before.Count != after.Count)
				{
					throw new InvalidOperationException(name + " count changed from " + before.Count + " to " + after.Count + ".");
				}
				for (int i = 0; i < before.Count; i++)
				{
					if (!(referenceIdentity ? (before[i] == after[i]) : object.Equals(before[i], after[i])))
					{
						throw new InvalidOperationException(name + " changed at element " + i + ".");
					}
				}
			}

			private static void AssertSame(object before, object after, string name)
			{
				if (before != after)
				{
					throw new InvalidOperationException(name + " changed.");
				}
			}

			private static void AssertEqual(object before, object after, string name)
			{
				if (!object.Equals(before, after))
				{
					throw new InvalidOperationException(name + " changed.");
				}
			}
		}

		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool _installed;

		private static Type _behaviorType;

		private static PropertyInfo _behaviorInstance;

		private static FieldInfo _switchInProgress;

		private static FieldInfo _sharedHeroIds;

		private static FieldInfo _activeHeroId;

		private static FieldInfo _sharedGold;

		private static MethodInfo _registerHero;

		private static MethodInfo _canChangeIdentity;

		private static MethodInfo _prepareTarget;

		private static MethodInfo _rebindIdentity;

		private static MethodInfo _synchronizeGold;

		private static MethodInfo _restoreInactiveCompanion;

		private static MethodInfo _careerUniquesRefresh;

		private static MethodInfo _torRefresh;

		private static MethodInfo _partyScreenRequest;

		private static MethodInfo _queueCareerPrompt;

		private static MethodInfo _uiMessage;

		private static MethodInfo _uiSelectOne;

		private static MethodInfo _logInfo;

		private static MethodInfo _logError;

		private static bool _profileNextInventoryOpen;

		private static int _plainInventoryOpenDepth;

		private static int _inventoryInitializeDepth;

		private static bool _loggedFastInventoryMarket;

		private static MobileParty _pendingOutgoingPresentationParty;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				ResolveMembers();
				Type type = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(type, "xmarre.multicharactercampaign.tor.remotepartyswitch.v139");
				Patch(harmony, type, harmonyMethodType, FindBehaviorMethod("CanSwitchTo", 3), GetPatchMethod("BeforeCanSwitchTo"), null);
				Patch(harmony, type, harmonyMethodType, FindBehaviorMethod("TrySwitch", 3), GetPatchMethod("BeforeTrySwitch"), null);
				Patch(harmony, type, harmonyMethodType, FindBehaviorMethod("EligibleSuccessors", 0), GetPatchMethod("BeforeEligibleSuccessors"), null);
				Patch(harmony, type, harmonyMethodType, FindBehaviorMethod("HeroStatus", 1), GetPatchMethod("BeforeHeroStatus"), null);
				Patch(harmony, type, harmonyMethodType, _uiSelectOne, GetPatchMethod("BeforeSwitchInquiry"), null);
				TryInstallInventoryProfiler(harmony, type, harmonyMethodType);
				TryInstallMapPresentationExitHook(harmony, type, harmonyMethodType);
				_installed = true;
				LogInfo("[RemotePartySwitch v1.1.0] Installed dedicated remote-party eligibility, switching, status, death-succession, outgoing-party presentation, and fast plain-inventory transactions.");
			}
			catch (Exception ex)
			{
				LogError("[RemotePartySwitch] Installation failed", ex);
			}
		}

		private static void TryInstallMapPresentationExitHook(object harmony, Type harmonyType, Type harmonyMethodType)
		{
			try
			{
				MethodInfo original = RequireMethod(RequireType("TaleWorlds.CampaignSystem.GameState.MapState, TaleWorlds.CampaignSystem"), "ExitMenuMode", 0, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				Patch(harmony, harmonyType, harmonyMethodType, original, null, GetPatchMethod("AfterMapMenuExited"));
			}
			catch (Exception ex)
			{
				LogError("[RemotePartySwitch] Outgoing-party post-menu presentation hook was unavailable", Unwrap(ex));
			}
		}

		private static void TryInstallInventoryProfiler(object harmony, Type harmonyType, Type harmonyMethodType)
		{
			try
			{
				Type type = RequireType("Helpers.InventoryScreenHelper, TaleWorlds.CampaignSystem");
				MethodInfo original = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "OpenScreenAsInventory" && m.GetParameters().Length == 1);
				Patch(harmony, harmonyType, harmonyMethodType, original, GetPatchMethod("BeforeInventoryOpen"), GetPatchMethod("AfterInventoryOpen"));
				MethodInfo original2 = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "GetCurrentMarketDataForPlayer" && m.GetParameters().Length == 0);
				Patch(harmony, harmonyType, harmonyMethodType, original2, GetPatchMethod("BeforePlainInventoryMarketLookup"), null);
				foreach (MethodInfo item in from m in RequireType("TaleWorlds.CampaignSystem.Inventory.InventoryLogic, TaleWorlds.CampaignSystem").GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					where m.Name == "Initialize"
					select m)
				{
					Patch(harmony, harmonyType, harmonyMethodType, item, GetPatchMethod("BeforeInventoryLogicInitialize"), GetPatchMethod("AfterInventoryLogicInitialize"));
				}
			}
			catch (Exception ex)
			{
				LogError("[RemotePartySwitch] Optional one-shot inventory timing hook was unavailable", Unwrap(ex));
			}
		}

		private static void ResolveMembers()
		{
			_behaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");
			_behaviorInstance = RequireProperty(_behaviorType, "Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_switchInProgress = RequireField(_behaviorType, "_switchInProgress");
			_sharedHeroIds = RequireField(_behaviorType, "_sharedHeroIds");
			_activeHeroId = RequireField(_behaviorType, "_activeHeroId");
			_sharedGold = RequireField(_behaviorType, "_sharedGold");
			_registerHero = FindBehaviorMethod("RegisterHero", 2);
			_canChangeIdentity = FindBehaviorMethod("CanChangeCampaignIdentity", 2);
			_prepareTarget = FindBehaviorMethod("PrepareTargetForActivation", 1);
			_rebindIdentity = FindBehaviorMethod("RebindMainPartyIdentity", 1);
			_synchronizeGold = FindBehaviorMethod("SynchronizeGold", 1);
			_restoreInactiveCompanion = FindBehaviorMethod("RestoreInactiveCompanion", 1);
			_queueCareerPrompt = FindBehaviorMethod("QueueMissingCareerPromptForActiveOriginalCompanion", 0);
			Type type = RequireType("MultiCharacterCampaignTOR.CareerUniquesBridge, MultiCharacterCampaignTOR");
			Type type2 = RequireType("MultiCharacterCampaignTOR.TORBridge, MultiCharacterCampaignTOR");
			Type type3 = RequireType("MultiCharacterCampaignTOR.PartyScreenSelectionBridge, MultiCharacterCampaignTOR");
			_careerUniquesRefresh = RequireMethod(type, "RefreshAfterSwitch", 0, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_torRefresh = RequireMethod(type2, "RefreshAfterSwitch", 0, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_partyScreenRequest = RequireMethod(type3, "RequestSelection", 1, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			Type type4 = RequireType("MultiCharacterCampaignTOR.UI, MultiCharacterCampaignTOR");
			_uiMessage = RequireMethod(type4, "Message", 1, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_uiSelectOne = RequireMethod(type4, "SelectOne", 4, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			Type type5 = RequireType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR");
			_logInfo = RequireMethod(type5, "Info", 1, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_logError = RequireMethod(type5, "Error", 2, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		}

		private static bool BeforeCanSwitchTo(object __instance, Hero target, bool emergency, ref string reason, ref bool __result)
		{
			if (target == null || target.PartyBelongedTo == MobileParty.MainParty)
			{
				return true;
			}
			__result = CanUseRemoteParty(__instance, target, emergency, out reason);
			return false;
		}

		private static bool BeforeTrySwitch(object __instance, Hero target, bool emergency, bool notify, ref bool __result)
		{
			if (target == null || target.PartyBelongedTo == MobileParty.MainParty)
			{
				return true;
			}
			__result = ExecuteRemoteSwitch(__instance, target, emergency, notify);
			return false;
		}

		private static bool BeforeEligibleSuccessors(object __instance, ref IEnumerable<Hero> __result)
		{
			List<Hero> list = new List<Hero>();
			if (_sharedHeroIds.GetValue(__instance) is IEnumerable<string> source)
			{
				string[] array = source.ToArray();
				for (int i = 0; i < array.Length; i++)
				{
					Hero hero = ResolveHero(array[i]);
					if (hero != null && hero != Hero.MainHero && CanUseAnyPartyForSuccession(hero))
					{
						list.Add(hero);
					}
				}
			}
			__result = list;
			return false;
		}

		private static bool BeforeHeroStatus(object __instance, Hero hero, ref string __result)
		{
			if (hero == null || hero.PartyBelongedTo == MobileParty.MainParty)
			{
				return true;
			}
			if (CanUseRemoteParty(__instance, hero, emergency: true, out var reason))
			{
				__result = "ready in another player-clan party";
			}
			else if (!hero.IsAlive)
			{
				__result = "dead";
			}
			else if (hero.IsPrisoner)
			{
				__result = "prisoner";
			}
			else if (!hero.IsActive)
			{
				__result = "inactive";
			}
			else
			{
				__result = "remote party unavailable: " + reason;
			}
			return false;
		}

		private static void BeforeSwitchInquiry(string title, ref string description)
		{
			if (string.Equals(title, "Switch active character", StringComparison.Ordinal))
			{
				description = "Select a registered character in this or another eligible player-clan party.";
			}
		}

		private static void BeforeInventoryOpen(ref long __state)
		{
			_plainInventoryOpenDepth++;
			__state = (_profileNextInventoryOpen ? Stopwatch.GetTimestamp() : 0);
		}

		private static void AfterInventoryOpen(long __state)
		{
			if (_plainInventoryOpenDepth > 0)
			{
				_plainInventoryOpenDepth--;
			}
			if (__state != 0L)
			{
				_profileNextInventoryOpen = false;
				LogInfo("[RemotePartySwitch] First post-switch inventory open completed in " + ((double)(Stopwatch.GetTimestamp() - __state) * 1000.0 / (double)Stopwatch.Frequency).ToString("0.0") + " ms (InventoryLogic initialization plus state push).");
			}
		}

		private static bool BeforePlainInventoryMarketLookup(ref IMarketData __result)
		{
			if (_plainInventoryOpenDepth <= 0 || MobileParty.MainParty == null || MobileParty.MainParty.CurrentSettlement != null)
			{
				return true;
			}
			try
			{
				Town town = null;
				float num = float.MaxValue;
				foreach (Town allTown in Town.AllTowns)
				{
					if (allTown != null && allTown.Settlement != null)
					{
						float num2 = MobileParty.MainParty.Position.DistanceSquared(allTown.Settlement.Position);
						if (num2 < num)
						{
							town = allTown;
							num = num2;
						}
					}
				}
				if (town == null || town.MarketData == null)
				{
					return true;
				}
				__result = town.MarketData;
				if (!_loggedFastInventoryMarket)
				{
					_loggedFastInventoryMarket = true;
					LogInfo("[RemotePartySwitch] Plain map inventory uses the closest local town market without the all-towns path-distance scan; town=" + IdOf(town.Settlement) + ".");
				}
				return false;
			}
			catch (Exception ex)
			{
				LogError("[RemotePartySwitch] Fast plain-inventory market lookup failed safely", Unwrap(ex));
				return true;
			}
		}

		private static void BeforeInventoryLogicInitialize(ref long __state)
		{
			_inventoryInitializeDepth++;
			__state = ((_profileNextInventoryOpen && _plainInventoryOpenDepth > 0) ? Stopwatch.GetTimestamp() : 0);
		}

		private static void AfterInventoryLogicInitialize(long __state)
		{
			if (_inventoryInitializeDepth > 0)
			{
				_inventoryInitializeDepth--;
			}
			if (__state != 0L)
			{
				LogInfo("[RemotePartySwitch] First post-switch InventoryLogic.Initialize completed in " + ((double)(Stopwatch.GetTimestamp() - __state) * 1000.0 / (double)Stopwatch.Frequency).ToString("0.0") + " ms.");
			}
		}

		private static void AfterMapMenuExited()
		{
			MobileParty pendingOutgoingPresentationParty = _pendingOutgoingPresentationParty;
			_pendingOutgoingPresentationParty = null;
			if (pendingOutgoingPresentationParty != null && MobileParty.MainParty != null)
			{
				ReclassifyOutgoingPartyPresentation(pendingOutgoingPresentationParty, MobileParty.MainParty);
				RefreshMapPresentation();
				LogInfo("[RemotePartySwitch] Completed outgoing-party presentation handoff after returning to the campaign map.");
			}
		}

		internal static IList<Hero> GetRegisteredSharedHeroes()
		{
			List<Hero> list = new List<Hero>();
			try
			{
				object value = _behaviorInstance == null ? null : _behaviorInstance.GetValue(null, null);
				if (value == null || _sharedHeroIds == null)
				{
					return list;
				}
				IEnumerable<string> enumerable = _sharedHeroIds.GetValue(value) as IEnumerable<string>;
				if (enumerable == null)
				{
					return list;
				}
				foreach (string id in enumerable.ToArray())
				{
					Hero hero = ResolveHero(id);
					if (hero != null)
					{
						list.Add(hero);
					}
				}
			}
			catch (Exception ex)
			{
				LogError("[RemotePartySwitch] Could not snapshot registered shared heroes", Unwrap(ex));
			}
			return list;
		}

		internal static object GetEffectiveMapEvent(MobileParty party)
		{
			if (party == null)
			{
				return null;
			}
			object mapEvent = GetMember(party, "MapEvent");
			if (mapEvent != null)
			{
				return mapEvent;
			}
			MobileParty attachedTo = party.AttachedTo;
			return attachedTo == null ? null : GetMember(attachedTo, "MapEvent");
		}

		internal static bool IsDefenderInMapEvent(MobileParty party, object mapEvent)
		{
			if (party == null || mapEvent == null || GetEffectiveMapEvent(party) != mapEvent)
			{
				return false;
			}
			object defenderSide = GetMember(mapEvent, "DefenderSide");
			object partyBase = GetMember(party, "Party");
			object partySide = GetMember(partyBase, "MapEventSide");
			if (defenderSide != null && partySide != null)
			{
				return partySide == defenderSide;
			}
			return SideContainsParty(defenderSide, party);
		}

		internal static bool CanTakeControlForBattle(Hero target, object expectedMapEvent, out string reason)
		{
			object behavior = _behaviorInstance == null ? null : _behaviorInstance.GetValue(null, null);
			if (behavior == null)
			{
				reason = "The shared-character campaign behavior is unavailable.";
				return false;
			}
			if (expectedMapEvent == null || target == null || !IsDefenderInMapEvent(target.PartyBelongedTo, expectedMapEvent))
			{
				reason = "That defensive battle is no longer active.";
				return false;
			}
			if (!CanUseRemoteParty(behavior, target, false, expectedMapEvent, out reason))
			{
				return false;
			}
			return CanContinueBattleAsPlayer(expectedMapEvent, out reason);
		}

		private static bool CanContinueBattleAsPlayer(object expectedMapEvent, out string reason)
		{
			reason = string.Empty;
			try
			{
				if (expectedMapEvent == null)
				{
					reason = "The defensive battle is no longer active.";
					return false;
				}
				object defenderParty = GetSideLeaderParty(GetMember(expectedMapEvent, "DefenderSide"));
				object attackerParty = GetSideLeaderParty(GetMember(expectedMapEvent, "AttackerSide"));
				if (defenderParty == null || attackerParty == null)
				{
					reason = "Bannerlord did not expose both battle-side leader parties.";
					return false;
				}
				Type encounterType = RequireType("TaleWorlds.CampaignSystem.Encounters.PlayerEncounter, TaleWorlds.CampaignSystem");
				MethodInfo restart = RequireMethod(encounterType, "RestartPlayerEncounter", 3, StaticFlags);
				ParameterInfo[] parameters = restart.GetParameters();
				if (parameters.Length != 3 || parameters[2].ParameterType != typeof(bool))
				{
					reason = "Bannerlord's player-encounter restart signature is incompatible.";
					return false;
				}
				RequireMethod(encounterType, "Init", 0, StaticFlags);
				RequireMethod(encounterType, "Start", 0, StaticFlags);
				return true;
			}
			catch (Exception ex)
			{
				Exception unwrapped = Unwrap(ex);
				reason = unwrapped.Message;
				LogError("[BattleIntervention] Native player-encounter API validation failed", unwrapped);
				return false;
			}
		}

		internal static bool TakeControlForBattle(Hero target, object expectedMapEvent, out string reason)
		{
			reason = string.Empty;
			object behavior = _behaviorInstance == null ? null : _behaviorInstance.GetValue(null, null);
			if (behavior == null)
			{
				reason = "The shared-character campaign behavior is unavailable.";
				return false;
			}
			if (!CanTakeControlForBattle(target, expectedMapEvent, out reason))
			{
				return false;
			}
			bool switched = ExecuteRemoteSwitch(behavior, target, false, true, expectedMapEvent);
			if (!switched)
			{
				reason = "The character-control handoff failed before the battle encounter could be resumed.";
				return false;
			}
			if (Hero.MainHero != target || MobileParty.MainParty != target.PartyBelongedTo || GetEffectiveMapEvent(MobileParty.MainParty) != expectedMapEvent)
			{
				reason = "Control changed, but the active party no longer belongs to the expected battle.";
				return false;
			}
			return true;
		}

		internal static bool CanOrderReinforcement(Hero target, object expectedMapEvent, out string reason)
		{
			reason = string.Empty;
			MobileParty mainParty = MobileParty.MainParty;
			MobileParty targetParty = target == null ? null : target.PartyBelongedTo;
			if (mainParty == null || targetParty == null || expectedMapEvent == null || GetEffectiveMapEvent(targetParty) != expectedMapEvent)
			{
				reason = "That battle is no longer active.";
				return false;
			}
			if (mainParty == targetParty)
			{
				reason = "You already control the endangered party.";
				return false;
			}
			if (GetEffectiveMapEvent(mainParty) != null)
			{
				reason = "The current party is already engaged in another battle.";
				return false;
			}
			if (PartyInSiegeOrRaid(mainParty))
			{
				reason = "The current party cannot leave an active siege or raid operation.";
				return false;
			}
			if (mainParty.CurrentSettlement != null)
			{
				reason = "Leave the current settlement before ordering a battle reinforcement.";
				return false;
			}
			if (mainParty.IsTransitionInProgress)
			{
				reason = "The current party is in a naval or port transition.";
				return false;
			}
			return true;
		}

		internal static bool OrderReinforcement(Hero target, object expectedMapEvent, out string reason)
		{
			if (!CanOrderReinforcement(target, expectedMapEvent, out reason))
			{
				return false;
			}
			MobileParty targetParty = target.PartyBelongedTo;
			try
			{
				MethodInfo method = MobileParty.MainParty.GetType().GetMethods(InstanceFlags).FirstOrDefault(delegate(MethodInfo m)
				{
					return m.Name == "SetMoveEngageParty" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(targetParty);
				});
				if (method == null)
				{
					throw new MissingMethodException(MobileParty.MainParty.GetType().FullName, "SetMoveEngageParty");
				}
				method.Invoke(MobileParty.MainParty, new object[1] { targetParty });
				if (MobileParty.MainParty.Ai != null)
				{
					MobileParty.MainParty.Ai.RethinkAtNextHourlyTick = false;
				}
				Message("Moving to reinforce " + SafeName(target) + " at " + SafePartyName(targetParty) + ". The battle may end before your party arrives.");
				LogInfo("[BattleIntervention] Ordered MainParty to engage endangered party=" + IdOf(targetParty) + "; hero=" + IdOf(target) + ".");
				return true;
			}
			catch (Exception ex)
			{
				Exception unwrapped = Unwrap(ex);
				reason = unwrapped.Message;
				LogError("[BattleIntervention] Reinforcement order failed", unwrapped);
				return false;
			}
		}

		internal static bool ContinueBattleAsPlayer(object expectedMapEvent, out string reason)
		{
			reason = string.Empty;
			try
			{
				if (!CanContinueBattleAsPlayer(expectedMapEvent, out reason))
				{
					return false;
				}
				MobileParty mainParty = MobileParty.MainParty;
				if (mainParty == null || GetEffectiveMapEvent(mainParty) != expectedMapEvent)
				{
					reason = "The controlled party is no longer in the expected battle.";
					return false;
				}
				object defenderParty = GetSideLeaderParty(GetMember(expectedMapEvent, "DefenderSide"));
				object attackerParty = GetSideLeaderParty(GetMember(expectedMapEvent, "AttackerSide"));
				if (defenderParty == null || attackerParty == null)
				{
					reason = "Bannerlord did not expose both battle-side leader parties.";
					return false;
				}
				Type encounterType = RequireType("TaleWorlds.CampaignSystem.Encounters.PlayerEncounter, TaleWorlds.CampaignSystem");
				MethodInfo restart = RequireMethod(encounterType, "RestartPlayerEncounter", 3, StaticFlags);
				restart.Invoke(null, new object[3] { defenderParty, attackerParty, true });
				if (!ReadStaticBool(encounterType, "IsActive"))
				{
					RequireMethod(encounterType, "Init", 0, StaticFlags).Invoke(null, null);
					RequireMethod(encounterType, "Start", 0, StaticFlags).Invoke(null, null);
				}
				if (!ReadStaticBool(encounterType, "IsActive") && !IsPlayerMapEvent(expectedMapEvent))
				{
					reason = "Bannerlord did not activate a player encounter for the defensive battle.";
					return false;
				}
				LogInfo("[BattleIntervention] Resumed defensive map event as the native player encounter; party=" + IdOf(mainParty) + ".");
				return true;
			}
			catch (Exception ex)
			{
				Exception unwrapped = Unwrap(ex);
				reason = unwrapped.Message;
				LogError("[BattleIntervention] Native player-encounter continuation failed", unwrapped);
				return false;
			}
		}

		internal static string HeroName(Hero hero)
		{
			return SafeName(hero);
		}

		internal static string PartyName(MobileParty party)
		{
			return SafePartyName(party);
		}

		internal static string OpposingLeaderPartyName(object mapEvent)
		{
			object partyBase = GetSideLeaderParty(GetMember(mapEvent, "AttackerSide"));
			object mobileParty = GetMember(partyBase, "MobileParty");
			object name = GetMember(mobileParty ?? partyBase, "Name");
			return name == null ? "an enemy party" : name.ToString();
		}

		internal static void Notify(string text)
		{
			Message(text);
		}

		internal static void Info(string text)
		{
			LogInfo(text);
		}

		internal static void Error(string text, Exception ex)
		{
			LogError(text, ex);
		}

		private static bool CanUseAnyPartyForSuccession(Hero hero)
		{
			if (hero == null || !hero.IsAlive || !hero.IsActive || hero.IsPrisoner)
			{
				return false;
			}
			if (hero.PartyBelongedTo == MobileParty.MainParty)
			{
				return true;
			}
			string reason;
			return CanUseRemoteParty(_behaviorInstance.GetValue(null, null), hero, emergency: true, out reason);
		}

		private static bool CanUseRemoteParty(object behavior, Hero target, bool emergency, out string reason)
		{
			return CanUseRemoteParty(behavior, target, emergency, null, out reason);
		}

		private static bool CanUseRemoteParty(object behavior, Hero target, bool emergency, object permittedMapEvent, out string reason)
		{
			reason = string.Empty;
			if (!emergency && permittedMapEvent == null)
			{
				object[] array = new object[2]
				{
					string.Empty,
					false
				};
				if (!Convert.ToBoolean(_canChangeIdentity.Invoke(behavior, array)))
				{
					reason = (array[0] as string) ?? "Campaign identity cannot be changed in the current state.";
					return false;
				}
			}
			if (target == null)
			{
				reason = "That character is unavailable.";
				return false;
			}
			if (!target.IsAlive)
			{
				reason = "That character is dead.";
				return false;
			}
			if (IsTORHirelingServiceActive())
			{
				reason = "Characters cannot be switched while serving as a TOR hireling; that system owns the MainParty attachment and settlement transitions.";
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
			MobileParty mainParty = MobileParty.MainParty;
			MobileParty partyBelongedTo = target.PartyBelongedTo;
			if (mainParty == null)
			{
				reason = "The current main party is unavailable.";
				return false;
			}
			if (partyBelongedTo == null)
			{
				reason = "That character is not leading a mobile party.";
				return false;
			}
			if (partyBelongedTo == mainParty)
			{
				return true;
			}
			if (!partyBelongedTo.IsActive)
			{
				reason = "That character's party is not active.";
				return false;
			}
			if (!IsLordParty(partyBelongedTo))
			{
				reason = "That character is not leading a normal lord party.";
				return false;
			}
			if (!PartyContainsHero(partyBelongedTo, target))
			{
				reason = "That character is not physically present in their reported party roster.";
				return false;
			}
			if (Clan.PlayerClan == null || partyBelongedTo.ActualClan != Clan.PlayerClan || (target.Clan != Clan.PlayerClan && target.CompanionOf != Clan.PlayerClan))
			{
				reason = "Remote switching is limited to parties belonging to the player clan.";
				return false;
			}
			object targetMapEvent = GetEffectiveMapEvent(partyBelongedTo);
			if (targetMapEvent != null && targetMapEvent != permittedMapEvent)
			{
				reason = "Cannot switch to " + SafeName(target) + " while their party is currently engaged in another battle.";
				return false;
			}
			if (permittedMapEvent != null && (targetMapEvent != permittedMapEvent || !IsDefenderInMapEvent(partyBelongedTo, permittedMapEvent)))
			{
				reason = "The defensive battle is no longer active for " + SafeName(target) + ".";
				return false;
			}
			if (PartyInSiegeOrRaid(partyBelongedTo))
			{
				reason = "Cannot switch to " + SafeName(target) + " during an active siege or raid operation.";
				return false;
			}
			if (partyBelongedTo.CurrentSettlement != null)
			{
				reason = "Cannot switch to " + SafeName(target) + " while their party is inside a settlement; Bannerlord's player-character transition forcibly removes the new main party from settlements.";
				return false;
			}
			if (partyBelongedTo.IsTransitionInProgress)
			{
				reason = "Cannot switch to " + SafeName(target) + " during an embarkation, disembarkation, or port transition.";
				return false;
			}
			if (partyBelongedTo.AttachedTo != null && partyBelongedTo.Army == null)
			{
				reason = "That party is attached to another party outside a valid army.";
				return false;
			}
			Hero mainHero = Hero.MainHero;
			if ((!emergency || permittedMapEvent != null) && mainHero != null && mainHero.IsAlive)
			{
				if (mainParty.LeaderHero != mainHero)
				{
					reason = "The current main-party leader does not match the active character.";
					return false;
				}
				if (PartyInMapEvent(mainParty))
				{
					reason = "Characters cannot be switched while the current party is engaged in battle.";
					return false;
				}
				if (PartyInSiegeOrRaid(mainParty))
				{
					reason = "Characters cannot be switched during an active siege or raid operation.";
					return false;
				}
				if (mainParty.CurrentSettlement != null)
				{
					reason = "Characters cannot be switched to a remote battle while the current party is inside a settlement.";
					return false;
				}
				if (mainParty.IsTransitionInProgress)
				{
					reason = "Characters cannot be switched during an embarkation, disembarkation, or port transition.";
					return false;
				}
			}
			return true;
		}

		private static bool ExecuteRemoteSwitch(object behavior, Hero target, bool emergency, bool notify)
		{
			return ExecuteRemoteSwitch(behavior, target, emergency, notify, null);
		}

		private static bool ExecuteRemoteSwitch(object behavior, Hero target, bool emergency, bool notify, object permittedMapEvent)
		{
			string reason;
			if (!CanUseRemoteParty(behavior, target, emergency, permittedMapEvent, out reason))
			{
				if (notify)
				{
					Message(reason);
				}
				return false;
			}
			Hero mainHero = Hero.MainHero;
			MobileParty mainParty = MobileParty.MainParty;
			MobileParty partyBelongedTo = target.PartyBelongedTo;
			InventoryHandoff inventoryHandoff = InventoryHandoff.Capture(mainParty, partyBelongedTo);
			PartyState partyState = PartyState.Capture(mainParty);
			PartyState partyState2 = PartyState.Capture(partyBelongedTo);
			bool flag = false;
			bool flag2 = false;
			_switchInProgress.SetValue(behavior, true);
			try
			{
				if (mainHero != null && mainHero.IsAlive)
				{
					_sharedGold.SetValue(behavior, mainHero.Gold);
				}
				_registerHero.Invoke(behavior, new object[2]
				{
					target,
					target.CompanionOf == Clan.PlayerClan
				});
				_prepareTarget.Invoke(behavior, new object[1] { target });
				LogInfo("[RemotePartySwitch] BEGIN oldHero=" + IdOf(mainHero) + "; targetHero=" + IdOf(target) + "; sourceParty=" + IdOf(mainParty) + "; targetParty=" + IdOf(partyBelongedTo) + ".");
				SetPlayerTroop(target);
				flag = true;
				if (Hero.MainHero != target)
				{
					throw new InvalidOperationException("Game.PlayerTroop did not establish the requested Hero.MainHero.");
				}
				DispatchBeforePlayerCharacterChanged(mainHero, target);
				if (!InvokeCampaignPlayerCharacterChanged() || MobileParty.MainParty != partyBelongedTo)
				{
					throw new InvalidOperationException("Campaign.OnPlayerCharacterChanged did not bind the target's existing party as MainParty.");
				}
				flag2 = true;
				if (permittedMapEvent != null && GetEffectiveMapEvent(partyBelongedTo) != permittedMapEvent)
				{
					throw new InvalidOperationException("The destination party left the expected battle during the identity handoff.");
				}
				DispatchPlayerCharacterChanged(mainHero, target, partyBelongedTo, mainPartyChanged: true);
				inventoryHandoff.Commit();
				WarmInventoryDerivedCaches(partyBelongedTo);
				MarkVisualDirty(partyBelongedTo);
				MarkVisualDirty(mainParty);
				SetCurrentCampaignMember("MainHeroIllDays", -1);
				_rebindIdentity.Invoke(behavior, new object[1] { target });
				_activeHeroId.SetValue(behavior, IdOf(target));
				_synchronizeGold.Invoke(behavior, new object[1] { (int)_sharedGold.GetValue(behavior) });
				_restoreInactiveCompanion.Invoke(behavior, new object[1] { mainHero });
				HandOffOutgoingPartyToAi(mainParty);
				if (permittedMapEvent == null)
				{
					ReclassifyOutgoingPartyPresentation(mainParty, partyBelongedTo);
					_pendingOutgoingPresentationParty = (IsMapMenuActive() ? mainParty : null);
				}
				else
				{
					_pendingOutgoingPresentationParty = null;
				}
				partyState.AssertPhysicalStateUnchanged(mainParty, "outgoing");
				partyState2.AssertPhysicalStateUnchanged(partyBelongedTo, "destination");
				inventoryHandoff.AssertCommitted();
				AssertFinalIdentity(target, mainParty, partyBelongedTo, partyState, partyState2);
				if (permittedMapEvent == null)
				{
					RefreshMapPresentation();
				}
				_profileNextInventoryOpen = true;
				_careerUniquesRefresh.Invoke(null, null);
				_torRefresh.Invoke(null, null);
				_partyScreenRequest.Invoke(null, new object[1] { "remote active character changed to " + IdOf(target) });
				_queueCareerPrompt.Invoke(behavior, null);
				string[] obj = new string[17]
				{
					"[RemotePartySwitch] COMMIT oldHero=",
					IdOf(mainHero),
					"; targetHero=",
					IdOf(target),
					"; sourceParty=",
					IdOf(mainParty),
					"; targetParty=",
					IdOf(partyBelongedTo),
					"; inventory/sourceToTarget=",
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null
				};
				int sourceElementCount = inventoryHandoff.SourceElementCount;
				obj[9] = sourceElementCount.ToString();
				obj[10] = "; inventory/targetToSource=";
				sourceElementCount = inventoryHandoff.TargetElementCount;
				obj[11] = sourceElementCount.ToString();
				obj[12] = "; ships/source=";
				obj[13] = partyState.Ships.Count.ToString();
				obj[14] = "; ships/target=";
				obj[15] = partyState2.Ships.Count.ToString();
				obj[16] = ".";
				LogInfo(string.Concat(obj));
				if (notify)
				{
					Message("Now controlling " + SafeName(target) + " in " + SafePartyName(partyBelongedTo) + ". " + SafeName(mainHero) + " remains with " + SafePartyName(mainParty) + ".");
				}
				return true;
			}
			catch (Exception ex)
			{
				Exception ex2 = Unwrap(ex);
				LogError("[RemotePartySwitch] Remote switch failed", ex2);
				if (flag && !flag2 && Hero.MainHero == target && MobileParty.MainParty == mainParty && mainHero != null && mainHero.IsAlive)
				{
					try
					{
						SetPlayerTroop(mainHero);
						if (Hero.MainHero != mainHero || MobileParty.MainParty != mainParty)
						{
							throw new InvalidOperationException("Pre-handoff rollback did not restore the original MainHero/MainParty pair.");
						}
						flag = false;
						LogInfo("[RemotePartySwitch] Rolled back PlayerTroop after campaign party rebinding failed before commit.");
					}
					catch (Exception rollbackException)
					{
						LogError("[RemotePartySwitch] Pre-handoff PlayerTroop rollback failed", Unwrap(rollbackException));
					}
				}
				if (flag && (flag2 || (Hero.MainHero == target && MobileParty.MainParty == partyBelongedTo)))
				{
					try
					{
						_activeHeroId.SetValue(behavior, IdOf(target));
						_restoreInactiveCompanion.Invoke(behavior, new object[1] { mainHero });
						HandOffOutgoingPartyToAi(mainParty);
					}
					catch (Exception ex3)
					{
						LogError("[RemotePartySwitch] Failure finalization also failed", Unwrap(ex3));
					}
					if (notify)
					{
						Message("Control moved to " + SafeName(target) + ", but a post-switch invariant failed: " + ex2.Message + ". Save in a new slot and inspect MultiCharacterCampaignTOR.log.");
					}
					return true;
				}
				try
				{
					_restoreInactiveCompanion.Invoke(behavior, new object[1] { target });
				}
				catch (Exception ex4)
				{
					LogError("[RemotePartySwitch] Pre-handoff target classification restoration failed", Unwrap(ex4));
				}
				if (notify)
				{
					Message("Character switch failed before control changed: " + ex2.Message + ". See MultiCharacterCampaignTOR.log.");
				}
				return false;
			}
			finally
			{
				_switchInProgress.SetValue(behavior, false);
			}
		}

		private static void HandOffOutgoingPartyToAi(MobileParty sourceParty)
		{
			if (sourceParty == null || !sourceParty.IsActive || sourceParty == MobileParty.MainParty)
			{
				return;
			}
			if (sourceParty.Ai != null)
			{
				sourceParty.Ai.EnableAi();
				sourceParty.Ai.RethinkAtNextHourlyTick = true;
				SetMember(sourceParty.Ai, "DefaultBehaviorNeedsUpdate", true);
			}
			if (sourceParty.Army == null)
			{
				sourceParty.SetMoveModeHold();
				if (sourceParty.Ai != null)
				{
					sourceParty.Ai.RethinkAtNextHourlyTick = true;
					SetMember(sourceParty.Ai, "DefaultBehaviorNeedsUpdate", true);
				}
			}
		}

		private static void ReclassifyOutgoingPartyPresentation(MobileParty sourceParty, MobileParty targetParty)
		{
			if (sourceParty != null && targetParty != null && sourceParty.IsActive && sourceParty != MobileParty.MainParty)
			{
				sourceParty.Party.UpdateVisibilityAndInspected(targetParty.Position);
				sourceParty.Party.OnVisibilityChanged(sourceParty.IsVisible);
				sourceParty.Party.SetVisualAsDirty();
				LogInfo("[RemotePartySwitch] Reclassified outgoing party map presentation; party=" + IdOf(sourceParty) + "; visible=" + sourceParty.IsVisible + "; inspected=" + sourceParty.IsInspected + "; banner=" + ((GetMember(sourceParty, "Banner") == null) ? "missing" : "present") + ".");
			}
		}

		private static void AssertFinalIdentity(Hero target, MobileParty sourceParty, MobileParty targetParty, PartyState sourceBefore, PartyState targetBefore)
		{
			if (Hero.MainHero != target || MobileParty.MainParty != targetParty || targetParty.LeaderHero != target)
			{
				throw new InvalidOperationException("Final MainHero/MainParty/party-leader identity did not converge on the requested target.");
			}
			if (GetPartyOwner(targetParty) != target)
			{
				throw new InvalidOperationException("The destination lord party is not owned by its active leader after rebinding.");
			}
			if (sourceParty != null && sourceParty.IsActive && sourceBefore.Leader != null && sourceParty.LeaderHero != sourceBefore.Leader)
			{
				throw new InvalidOperationException("The outgoing party leader changed during the remote handoff.");
			}
			if (sourceParty != null && sourceParty.IsActive && sourceBefore.Owner != null && GetPartyOwner(sourceParty) != sourceBefore.Owner)
			{
				throw new InvalidOperationException("The outgoing party owner changed during the remote handoff.");
			}
			if (targetBefore.Army != targetParty.Army || sourceBefore.Army != sourceParty.Army)
			{
				throw new InvalidOperationException("Army membership changed during the remote handoff.");
			}
		}

		private static void SetPlayerTroop(Hero target)
		{
			Type type = RequireType("TaleWorlds.Core.Game, TaleWorlds.Core");
			PropertyInfo propertyInfo = RequireProperty(type, "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			PropertyInfo propertyInfo2 = RequireProperty(type, "PlayerTroop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object value = propertyInfo.GetValue(null, null);
			object member = GetMember(target, "CharacterObject");
			if (value == null || member == null)
			{
				throw new InvalidOperationException("Game.Current or the target CharacterObject is unavailable.");
			}
			propertyInfo2.SetValue(value, member, null);
		}

		private static bool InvokeCampaignPlayerCharacterChanged()
		{
			Type type = RequireType("TaleWorlds.CampaignSystem.Campaign, TaleWorlds.CampaignSystem");
			object value = RequireProperty(type, "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null);
			MethodInfo methodInfo = RequireMethod(type, "OnPlayerCharacterChanged", 1, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object[] array = new object[1] { false };
			methodInfo.Invoke(value, array);
			return Convert.ToBoolean(array[0]);
		}

		private static void DispatchBeforePlayerCharacterChanged(Hero oldHero, Hero target)
		{
			object campaignEventDispatcher = GetCampaignEventDispatcher();
			RequireMethod(campaignEventDispatcher.GetType(), "OnBeforePlayerCharacterChanged", 2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(campaignEventDispatcher, new object[2] { oldHero, target });
		}

		private static void DispatchPlayerCharacterChanged(Hero oldHero, Hero target, MobileParty targetParty, bool mainPartyChanged)
		{
			object campaignEventDispatcher = GetCampaignEventDispatcher();
			RequireMethod(campaignEventDispatcher.GetType(), "OnPlayerCharacterChanged", 4, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(campaignEventDispatcher, new object[4] { oldHero, target, targetParty, mainPartyChanged });
		}

		private static object GetCampaignEventDispatcher()
		{
			return RequireProperty(RequireType("TaleWorlds.CampaignSystem.CampaignEventDispatcher, TaleWorlds.CampaignSystem"), "Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null) ?? throw new InvalidOperationException("CampaignEventDispatcher.Instance is unavailable.");
		}

		private static void SetCurrentCampaignMember(string name, object value)
		{
			SetMember(RequireProperty(RequireType("TaleWorlds.CampaignSystem.Campaign, TaleWorlds.CampaignSystem"), "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null), name, value);
		}

		private static bool PartyInMapEvent(MobileParty party)
		{
			return GetEffectiveMapEvent(party) != null;
		}

		private static bool SideContainsParty(object side, MobileParty party)
		{
			if (side == null || party == null)
			{
				return false;
			}
			foreach (object item in SnapshotEnumerable(GetMember(side, "Parties")))
			{
				object partyBase = GetMember(item, "Party") ?? item;
				if (GetMember(partyBase, "MobileParty") == party || partyBase == GetMember(party, "Party"))
				{
					return true;
				}
			}
			return false;
		}

		private static object GetSideLeaderParty(object side)
		{
			if (side == null)
			{
				return null;
			}
			object leader = GetMember(side, "LeaderParty");
			if (leader != null)
			{
				return GetMember(leader, "Party") ?? leader;
			}
			foreach (object item in SnapshotEnumerable(GetMember(side, "Parties")))
			{
				object partyBase = GetMember(item, "Party") ?? item;
				if (partyBase != null)
				{
					return partyBase;
				}
			}
			return null;
		}

		private static bool ReadStaticBool(Type type, string name)
		{
			PropertyInfo property = type.GetProperty(name, StaticFlags);
			if (property != null && property.CanRead)
			{
				return Convert.ToBoolean(property.GetValue(null, null));
			}
			FieldInfo field = type.GetField(name, StaticFlags);
			return field != null && Convert.ToBoolean(field.GetValue(null));
		}

		private static bool IsPlayerMapEvent(object mapEvent)
		{
			if (mapEvent == null)
			{
				return false;
			}
			Type type = mapEvent.GetType();
			PropertyInfo property = type.GetProperty("PlayerMapEvent", StaticFlags);
			if (property != null)
			{
				return property.GetValue(null, null) == mapEvent;
			}
			return false;
		}

		private static bool PartyInSiegeOrRaid(MobileParty party)
		{
			if (party == null)
			{
				return false;
			}
			if (GetMember(party, "SiegeEvent") != null || GetMember(party, "BesiegerCamp") != null)
			{
				return true;
			}
			string text = Convert.ToString(GetMember(party, "DefaultBehavior"));
			if (!(text == "RaidSettlement") && !(text == "AssaultSettlement"))
			{
				return text == "BesiegeSettlement";
			}
			return true;
		}

		private static bool IsLordParty(MobileParty party)
		{
			object obj = GetMember(party, "LordPartyComponent") ?? GetMember(party, "PartyComponent");
			if (obj != null)
			{
				return obj.GetType().FullName == "TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent";
			}
			return false;
		}

		private static bool PartyContainsHero(MobileParty party, Hero hero)
		{
			object member = GetMember(hero, "CharacterObject");
			if (party == null || hero == null || member == null)
			{
				return false;
			}
			object member2 = GetMember(party, "MemberRoster");
			if (member2 == null)
			{
				return false;
			}
			MethodInfo methodInfo = member2.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "GetTroopCount" && m.GetParameters().Length == 1);
			if (methodInfo != null)
			{
				return Convert.ToInt32(methodInfo.Invoke(member2, new object[1] { member })) > 0;
			}
			foreach (object item in SnapshotEnumerable(member2))
			{
				if (GetMember(item, "Character") == member)
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsTORHirelingServiceActive()
		{
			try
			{
				Type type = Type.GetType("TOR_Core.Extensions.HeroExtensions, TOR_Core", throwOnError: false);
				MethodInfo methodInfo = ((type == null) ? null : type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SingleOrDefault((MethodInfo m) => m.Name == "IsEnlisted" && m.GetParameters().Length == 1));
				return methodInfo != null && Convert.ToBoolean(methodInfo.Invoke(null, new object[1] { Hero.MainHero }));
			}
			catch (Exception ex)
			{
				LogError("[RemotePartySwitch] TOR hireling-state preflight failed", Unwrap(ex));
				return true;
			}
		}

		private static Hero GetPartyOwner(MobileParty party)
		{
			object instance = GetMember(party, "LordPartyComponent") ?? GetMember(party, "PartyComponent");
			return (GetMember(instance, "PartyOwner") ?? GetMember(instance, "Owner")) as Hero;
		}

		private static void MarkVisualDirty(MobileParty party)
		{
			if (party != null)
			{
				InvokeNoArg(GetMember(party, "Party"), "SetVisualAsDirty");
			}
		}

		private static void WarmInventoryDerivedCaches(MobileParty party)
		{
			if (party == null)
			{
				return;
			}
			try
			{
				InvokeNoArg(party, "UpdateCommonCacheVersions");
				GetMember(party, "TotalWeightCarried");
				GetMember(party, "InventoryCapacity");
			}
			catch (Exception ex)
			{
				LogError("[RemotePartySwitch] One-shot inventory cache warm-up failed safely", Unwrap(ex));
			}
		}

		private static void RefreshMapPresentation()
		{
			try
			{
				object member = GetMember(GetMember(RequireProperty(RequireType("TaleWorlds.Core.Game, TaleWorlds.Core"), "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null), "GameStateManager"), "ActiveState");
				if (member != null && !(member.GetType().FullName != "TaleWorlds.CampaignSystem.GameState.MapState"))
				{
					InvokeNoArg(member, "OnJoinArmy");
					LogInfo("[RemotePartySwitch] Refreshed campaign-map presentation for the newly active MainParty.");
				}
			}
			catch (Exception ex)
			{
				LogError("[RemotePartySwitch] Campaign-map presentation refresh failed safely", Unwrap(ex));
			}
		}

		private static bool IsMapMenuActive()
		{
			try
			{
				object member = GetMember(GetMember(RequireProperty(RequireType("TaleWorlds.Core.Game, TaleWorlds.Core"), "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null), "GameStateManager"), "ActiveState");
				return member != null && member.GetType().FullName == "TaleWorlds.CampaignSystem.GameState.MapState" && Convert.ToBoolean(GetMember(member, "AtMenu"));
			}
			catch
			{
				return false;
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
				return null;
			}
		}

		private static string IdOf(object value)
		{
			object member = GetMember(value, "StringId");
			if (member != null)
			{
				return member.ToString();
			}
			return string.Empty;
		}

		private static string SafeName(Hero hero)
		{
			if (hero == null)
			{
				return "the previous character";
			}
			object member = GetMember(hero, "Name");
			if (member != null)
			{
				return member.ToString();
			}
			return IdOf(hero);
		}

		private static string SafePartyName(MobileParty party)
		{
			object member = GetMember(party, "Name");
			if (member != null)
			{
				return member.ToString();
			}
			return IdOf(party);
		}

		private static void Message(string message)
		{
			try
			{
				_uiMessage.Invoke(null, new object[1] { message });
			}
			catch
			{
			}
		}

		private static void LogInfo(string message)
		{
			try
			{
				_logInfo.Invoke(null, new object[1] { message });
			}
			catch
			{
			}
		}

		private static void LogError(string message, Exception ex)
		{
			try
			{
				_logError.Invoke(null, new object[2] { message, ex });
			}
			catch
			{
			}
		}

		private static Exception Unwrap(Exception ex)
		{
			Exception ex2 = ex;
			while (ex2 is TargetInvocationException && ex2.InnerException != null)
			{
				ex2 = ex2.InnerException;
			}
			return ex2;
		}

		private static object GetMember(object instance, string name)
		{
			if (instance == null)
			{
				return null;
			}
			Type type = instance.GetType();
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanRead)
			{
				return property.GetValue(instance, null);
			}
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (!(field == null))
			{
				return field.GetValue(instance);
			}
			return null;
		}

		private static List<object> SnapshotEnumerable(object value)
		{
			List<object> list = new List<object>();
			if (!(value is IEnumerable enumerable))
			{
				return list;
			}
			foreach (object item in enumerable)
			{
				list.Add(item);
			}
			return list;
		}

		private static void SetMember(object instance, string name, object value)
		{
			if (instance == null)
			{
				return;
			}
			Type type = instance.GetType();
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanWrite)
			{
				property.SetValue(instance, value, null);
				return;
			}
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				field.SetValue(instance, value);
			}
		}

		private static void SetItemRoster(object partyBase, object roster)
		{
			if (partyBase == null || roster == null)
			{
				throw new InvalidOperationException("A party inventory roster could not be rebound.");
			}
			PropertyInfo property = partyBase.GetType().GetProperty("ItemRoster", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo obj = ((property == null) ? null : property.GetSetMethod(nonPublic: true));
			if (obj == null)
			{
				throw new MissingMethodException(partyBase.GetType().FullName, "set_ItemRoster");
			}
			obj.Invoke(partyBase, new object[1] { roster });
		}

		private static void InvalidateInventoryCaches(MobileParty party)
		{
			if (party != null)
			{
				SetMember(party, "_itemRosterVersionNo", -1);
				SetMember(party, "_partyWeightLastCheckVersionNo", -1);
				SetMember(party, "_partyPureSpeedLastCheckVersion", -1);
			}
		}

		private static void InvokeNoArg(object instance, string name)
		{
			if (instance != null)
			{
				MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				if (method != null)
				{
					method.Invoke(instance, null);
				}
			}
		}

		private static MethodInfo FindBehaviorMethod(string name, int parameterCount)
		{
			return RequireMethod(_behaviorType, name, parameterCount, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			return RequireMethod(typeof(RemotePartySwitch), name, -1, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		}

		private static Type RequireType(string name)
		{
			Type type = Type.GetType(name, throwOnError: false);
			if (type == null)
			{
				throw new TypeLoadException(name);
			}
			return type;
		}

		private static PropertyInfo RequireProperty(Type type, string name, BindingFlags flags)
		{
			PropertyInfo property = type.GetProperty(name, flags);
			if (property == null)
			{
				throw new MissingMemberException(type.FullName, name);
			}
			return property;
		}

		private static FieldInfo RequireField(Type type, string name)
		{
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
			{
				throw new MissingFieldException(type.FullName, name);
			}
			return field;
		}

		private static MethodInfo RequireMethod(Type type, string name, int parameterCount, BindingFlags flags)
		{
			MethodInfo methodInfo = type.GetMethods(flags).SingleOrDefault((MethodInfo m) => m.Name == name && (parameterCount < 0 || m.GetParameters().Length == parameterCount));
			if (methodInfo == null)
			{
				throw new MissingMethodException(type.FullName, name);
			}
			return methodInfo;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix)
		{
			object obj = ((prefix == null) ? null : Activator.CreateInstance(harmonyMethodType, prefix));
			object obj2 = ((postfix == null) ? null : Activator.CreateInstance(harmonyMethodType, postfix));
			MethodInfo methodInfo = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "Patch" && m.GetParameters().Length >= 4);
			object[] array = new object[methodInfo.GetParameters().Length];
			array[0] = original;
			array[1] = obj;
			array[2] = obj2;
			methodInfo.Invoke(harmony, array);
		}
	}
}
