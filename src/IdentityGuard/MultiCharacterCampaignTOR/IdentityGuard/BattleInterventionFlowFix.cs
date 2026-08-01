using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleInterventionFlowFix
	{
		private sealed class FlowChoice
		{
			internal const int TakeControl = 1;
			internal const int Reinforce = 2;

			internal readonly int Action;
			internal readonly object Candidate;

			internal FlowChoice(int action, object candidate)
			{
				Action = action;
				Candidate = candidate;
			}
		}

		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool _installed;
		private static FieldInfo _candidateHeroField;
		private static FieldInfo _candidatePartyField;
		private static FieldInfo _candidateMapEventField;
		private static FieldInfo _inquiryOpenField;
		private static FieldInfo _pendingEncounterEventField;
		private static FieldInfo _encounterDelayTicksField;
		private static FieldInfo _suppressedEventsField;
		private static MethodInfo _isCandidateCurrentMethod;
		private static MethodInfo _partyInSiegeOrRaidMethod;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				ResolveAlertMembers();
				_partyInSiegeOrRaidMethod = typeof(RemotePartySwitch).GetMethod("PartyInSiegeOrRaid", StaticFlags);
				if (_partyInSiegeOrRaidMethod == null)
				{
					throw new MissingMethodException(typeof(RemotePartySwitch).FullName, "PartyInSiegeOrRaid");
				}

				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.battleinterventionflow.v112");

				MethodInfo showInquiry = typeof(BattleInterventionAlert).GetMethod("ShowInquiry", StaticFlags);
				MethodInfo continueBattle = typeof(RemotePartySwitch).GetMethod("ContinueBattleAsPlayer", StaticFlags);
				if (showInquiry == null || continueBattle == null)
				{
					throw new MissingMethodException("The v1.1.1 battle-intervention surfaces are unavailable.");
				}
				Patch(harmony, harmonyType, harmonyMethodType, showInquiry, GetPatchMethod("BeforeShowInquiry"), null);
				Patch(harmony, harmonyType, harmonyMethodType, continueBattle, GetPatchMethod("BeforeContinueBattleAsPlayer"), null);

				_installed = true;
				RemotePartySwitch.Info("[BattleInterventionFlow v1.1.2] Installed native encounter initialization, immediate encounter-menu continuation, and combined takeover/reinforcement actions.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionFlow] Installation failed", Unwrap(ex));
			}
		}

		private static bool BeforeShowInquiry(object __0)
		{
			object candidate = __0;
			try
			{
				Hero hero = CandidateHero(candidate);
				MobileParty party = CandidateParty(candidate);
				object mapEvent = CandidateMapEvent(candidate);
				if (hero == null || party == null || mapEvent == null)
				{
					SetInquiryOpen(false);
					return false;
				}

				string controlReason;
				string reinforceReason;
				bool canControl = RemotePartySwitch.CanTakeControlForBattle(hero, mapEvent, out controlReason);
				bool canReinforce = CanOrderPartyToBattle(MobileParty.MainParty, party, mapEvent, out reinforceReason);
				List<InquiryElement> elements = new List<InquiryElement>
				{
					CreateInquiryElement(new FlowChoice(FlowChoice.TakeControl, candidate), "Take control of " + RemotePartySwitch.HeroName(hero) + " and continue the battle", canControl, canControl ? "Switch control to this character and immediately open Bannerlord's native encounter window." : controlReason),
					CreateInquiryElement(new FlowChoice(FlowChoice.Reinforce, candidate), "Send the current party to reinforce " + RemotePartySwitch.PartyName(party), canReinforce, canReinforce ? "Order the current party toward this fight. This can be selected together with taking control." : reinforceReason)
				};
				string description = RemotePartySwitch.HeroName(hero) + " is involved in an active battle with " + RemotePartySwitch.OpposingLeaderPartyName(mapEvent) + ". Select either action or both actions together.";
				MultiSelectionInquiryData data = new MultiSelectionInquiryData("Shared character in battle", description, elements, true, 1, 2, "Apply selected actions", "Dismiss", delegate(List<InquiryElement> selected)
				{
					HandleSelection(candidate, selected);
				}, delegate(List<InquiryElement> selected)
				{
					SetInquiryOpen(false);
				}, string.Empty, false);

				SetInquiryOpen(true);
				MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
			}
			catch (Exception ex)
			{
				SetInquiryOpen(false);
				RemotePartySwitch.Error("[BattleInterventionFlow] Could not open the combined battle inquiry", Unwrap(ex));
				RemotePartySwitch.Notify("A shared-character battle was detected, but the intervention inquiry could not be opened. See MultiCharacterCampaignTOR.log.");
			}
			return false;
		}

		private static void HandleSelection(object candidate, List<InquiryElement> selected)
		{
			SetInquiryOpen(false);
			try
			{
				bool takeControl = false;
				bool reinforce = false;
				if (selected != null)
				{
					for (int i = 0; i < selected.Count; i++)
					{
						FlowChoice choice = ReadMember(selected[i], "Identifier") as FlowChoice ?? ReadMember(selected[i], "Id") as FlowChoice;
						if (choice == null || !object.ReferenceEquals(choice.Candidate, candidate))
						{
							continue;
						}
						takeControl |= choice.Action == FlowChoice.TakeControl;
						reinforce |= choice.Action == FlowChoice.Reinforce;
					}
				}
				if (!takeControl && !reinforce)
				{
					return;
				}
				if (!IsCandidateCurrent(candidate))
				{
					RemotePartySwitch.Notify("That battle ended or changed before the selected actions could be applied.");
					return;
				}

				Hero hero = CandidateHero(candidate);
				MobileParty targetParty = CandidateParty(candidate);
				object mapEvent = CandidateMapEvent(candidate);
				MobileParty outgoingParty = MobileParty.MainParty;
				bool actionApplied = false;

				if (takeControl)
				{
					string controlReason;
					if (!RemotePartySwitch.TakeControlForBattle(hero, mapEvent, out controlReason))
					{
						RemotePartySwitch.Notify("Could not take control of " + RemotePartySwitch.HeroName(hero) + ": " + controlReason);
					}
					else
					{
						actionApplied = true;
						if (reinforce)
						{
							string reinforcementReason;
							if (!OrderPartyToBattle(outgoingParty, targetParty, mapEvent, out reinforcementReason))
							{
								RemotePartySwitch.Notify("Control changed successfully, but the original party could not be sent to the battle: " + reinforcementReason);
							}
							else
							{
								RemotePartySwitch.Notify(RemotePartySwitch.PartyName(outgoingParty) + " is moving to reinforce the battle.");
							}
						}
						SuppressEvent(mapEvent);
						_pendingEncounterEventField.SetValue(null, mapEvent);
						_encounterDelayTicksField.SetValue(null, 0);
						return;
					}
				}

				if (reinforce)
				{
					string reinforcementReason;
					if (!OrderPartyToBattle(outgoingParty, targetParty, mapEvent, out reinforcementReason))
					{
						RemotePartySwitch.Notify("Could not issue the reinforcement order: " + reinforcementReason);
					}
					else
					{
						actionApplied = true;
						RemotePartySwitch.Notify(RemotePartySwitch.PartyName(outgoingParty) + " is moving to reinforce " + RemotePartySwitch.PartyName(targetParty) + ". The battle may end before it arrives.");
					}
				}

				if (actionApplied)
				{
					SuppressEvent(mapEvent);
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionFlow] Combined battle action failed", Unwrap(ex));
				RemotePartySwitch.Notify("The selected battle-intervention actions failed. See MultiCharacterCampaignTOR.log.");
			}
		}

		private static bool BeforeContinueBattleAsPlayer(object expectedMapEvent, ref string reason, ref bool __result)
		{
			reason = string.Empty;
			__result = false;
			try
			{
				MobileParty mainParty = MobileParty.MainParty;
				if (expectedMapEvent == null || mainParty == null || !object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(mainParty), expectedMapEvent))
				{
					reason = "The controlled party is no longer in the expected battle.";
					return false;
				}

				object attackerParty = GetSideLeaderParty(ReadMember(expectedMapEvent, "AttackerSide"));
				object defenderParty = GetSideLeaderParty(ReadMember(expectedMapEvent, "DefenderSide"));
				if (attackerParty == null || defenderParty == null)
				{
					reason = "Bannerlord did not expose both battle-side leader parties.";
					return false;
				}

				Type encounterManagerType = RequireType("TaleWorlds.CampaignSystem.EncounterManager, TaleWorlds.CampaignSystem");
				MethodInfo restart = encounterManagerType.GetMethods(StaticFlags).Single((MethodInfo method) => method.Name == "RestartPlayerEncounter" && method.GetParameters().Length == 2);
				restart.Invoke(null, new object[2] { attackerParty, defenderParty });

				Type playerEncounterType = RequireType("TaleWorlds.CampaignSystem.Encounters.PlayerEncounter, TaleWorlds.CampaignSystem");
				if (!ReadStaticBool(playerEncounterType, "IsActive"))
				{
					reason = "Bannerlord did not activate the player encounter.";
					return false;
				}
				object encounteredBattle = ReadStaticMember(playerEncounterType, "EncounteredBattle") ?? ReadStaticMember(playerEncounterType, "Battle");
				if (encounteredBattle != null && !object.ReferenceEquals(encounteredBattle, expectedMapEvent))
				{
					reason = "Bannerlord opened a different encounter than the selected battle.";
					return false;
				}
				if (!object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(mainParty), expectedMapEvent))
				{
					reason = "The native encounter initialization detached the controlled party from the selected battle.";
					return false;
				}

				__result = true;
				RemotePartySwitch.Info("[BattleInterventionFlow] Opened the selected map event through EncounterManager.RestartPlayerEncounter; party=" + SafeId(mainParty) + ".");
			}
			catch (Exception ex)
			{
				Exception unwrapped = Unwrap(ex);
				reason = unwrapped.Message;
				RemotePartySwitch.Error("[BattleInterventionFlow] Native encounter initialization failed", unwrapped);
			}
			return false;
		}

		private static bool CanOrderPartyToBattle(MobileParty reinforcingParty, MobileParty targetParty, object expectedMapEvent, out string reason)
		{
			reason = string.Empty;
			if (reinforcingParty == null || targetParty == null || expectedMapEvent == null)
			{
				reason = "The reinforcing party or battle is unavailable.";
				return false;
			}
			if (!reinforcingParty.IsActive)
			{
				reason = "The reinforcing party is no longer active.";
				return false;
			}
			if (!targetParty.IsActive || !object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(targetParty), expectedMapEvent))
			{
				reason = "That battle is no longer active.";
				return false;
			}
			if (object.ReferenceEquals(reinforcingParty, targetParty))
			{
				reason = "The selected party is already in that battle.";
				return false;
			}
			object reinforcingEvent = RemotePartySwitch.GetEffectiveMapEvent(reinforcingParty);
			if (reinforcingEvent != null)
			{
				reason = object.ReferenceEquals(reinforcingEvent, expectedMapEvent) ? "The reinforcing party has already joined that battle." : "The reinforcing party is already engaged in another battle.";
				return false;
			}
			if (Clan.PlayerClan == null || reinforcingParty.ActualClan != Clan.PlayerClan)
			{
				reason = "Only a player-clan party can be ordered to reinforce this battle.";
				return false;
			}
			if (Convert.ToBoolean(_partyInSiegeOrRaidMethod.Invoke(null, new object[1] { reinforcingParty })))
			{
				reason = "The reinforcing party cannot leave an active siege or raid operation.";
				return false;
			}
			if (reinforcingParty.CurrentSettlement != null)
			{
				reason = "The reinforcing party must leave its settlement first.";
				return false;
			}
			if (reinforcingParty.IsTransitionInProgress)
			{
				reason = "The reinforcing party is in a naval or port transition.";
				return false;
			}
			return true;
		}

		private static bool OrderPartyToBattle(MobileParty reinforcingParty, MobileParty targetParty, object expectedMapEvent, out string reason)
		{
			if (!CanOrderPartyToBattle(reinforcingParty, targetParty, expectedMapEvent, out reason))
			{
				return false;
			}
			try
			{
				MethodInfo engageMethod = reinforcingParty.GetType().GetMethods(InstanceFlags)
					.Where((MethodInfo method) => method.Name == "SetMoveEngageParty")
					.Where((MethodInfo method) =>
					{
						ParameterInfo[] parameters = method.GetParameters();
						return parameters.Length >= 1 && parameters.Length <= 2 && parameters[0].ParameterType.IsInstanceOfType(targetParty);
					})
					.OrderBy((MethodInfo method) => method.GetParameters().Length)
					.FirstOrDefault();
				if (engageMethod == null)
				{
					throw new MissingMethodException(reinforcingParty.GetType().FullName, "SetMoveEngageParty");
				}
				ParameterInfo[] engageParameters = engageMethod.GetParameters();
				object[] arguments;
				if (engageParameters.Length == 1)
				{
					arguments = new object[1] { targetParty };
				}
				else
				{
					Type navigationType = engageParameters[1].ParameterType;
					if (!navigationType.IsEnum)
					{
						throw new InvalidOperationException("SetMoveEngageParty has an unsupported navigation parameter.");
					}
					object defaultNavigation = Enum.Parse(navigationType, "Default", false);
					arguments = new object[2] { targetParty, defaultNavigation };
				}
				if (!object.ReferenceEquals(reinforcingParty, MobileParty.MainParty) && reinforcingParty.Ai != null)
				{
					reinforcingParty.Ai.EnableAi();
				}
				engageMethod.Invoke(reinforcingParty, arguments);
				if (reinforcingParty.Ai != null)
				{
					reinforcingParty.Ai.RethinkAtNextHourlyTick = false;
					WriteMember(reinforcingParty.Ai, "DefaultBehaviorNeedsUpdate", false);
				}
				object orderedTarget = ReadMember(reinforcingParty, "TargetParty");
				if (orderedTarget != null && !object.ReferenceEquals(orderedTarget, targetParty))
				{
					throw new InvalidOperationException("Bannerlord did not retain the requested reinforcement target.");
				}
				RemotePartySwitch.Info("[BattleInterventionFlow] Ordered party=" + SafeId(reinforcingParty) + " to reinforce target=" + SafeId(targetParty) + ".");
				return true;
			}
			catch (Exception ex)
			{
				Exception unwrapped = Unwrap(ex);
				reason = unwrapped.Message;
				RemotePartySwitch.Error("[BattleInterventionFlow] Reinforcement order failed", unwrapped);
				return false;
			}
		}

		private static void ResolveAlertMembers()
		{
			Type alertType = typeof(BattleInterventionAlert);
			Type candidateType = alertType.GetNestedType("Candidate", BindingFlags.NonPublic);
			if (candidateType == null)
			{
				throw new MissingMemberException(alertType.FullName, "Candidate");
			}
			_candidateHeroField = candidateType.GetField("Hero", InstanceFlags);
			_candidatePartyField = candidateType.GetField("Party", InstanceFlags);
			_candidateMapEventField = candidateType.GetField("MapEvent", InstanceFlags);
			_inquiryOpenField = alertType.GetField("_inquiryOpen", StaticFlags);
			_pendingEncounterEventField = alertType.GetField("_pendingEncounterEvent", StaticFlags);
			_encounterDelayTicksField = alertType.GetField("_encounterDelayTicks", StaticFlags);
			_suppressedEventsField = alertType.GetField("SuppressedEvents", StaticFlags);
			_isCandidateCurrentMethod = alertType.GetMethod("IsCandidateCurrent", StaticFlags);
			if (_candidateHeroField == null || _candidatePartyField == null || _candidateMapEventField == null || _inquiryOpenField == null || _pendingEncounterEventField == null || _encounterDelayTicksField == null || _suppressedEventsField == null || _isCandidateCurrentMethod == null)
			{
				throw new MissingMemberException(alertType.FullName, "battle intervention state");
			}
		}

		private static Hero CandidateHero(object candidate)
		{
			return candidate == null ? null : _candidateHeroField.GetValue(candidate) as Hero;
		}

		private static MobileParty CandidateParty(object candidate)
		{
			return candidate == null ? null : _candidatePartyField.GetValue(candidate) as MobileParty;
		}

		private static object CandidateMapEvent(object candidate)
		{
			return candidate == null ? null : _candidateMapEventField.GetValue(candidate);
		}

		private static bool IsCandidateCurrent(object candidate)
		{
			return candidate != null && Convert.ToBoolean(_isCandidateCurrentMethod.Invoke(null, new object[1] { candidate }));
		}

		private static void SetInquiryOpen(bool value)
		{
			if (_inquiryOpenField != null)
			{
				_inquiryOpenField.SetValue(null, value);
			}
		}

		private static void SuppressEvent(object mapEvent)
		{
			HashSet<object> suppressed = _suppressedEventsField.GetValue(null) as HashSet<object>;
			if (suppressed != null && mapEvent != null)
			{
				suppressed.Add(mapEvent);
			}
		}

		private static InquiryElement CreateInquiryElement(FlowChoice choice, string label, bool enabled, string hint)
		{
			ConstructorInfo preferred = null;
			ConstructorInfo fallback = null;
			foreach (ConstructorInfo constructor in typeof(InquiryElement).GetConstructors(BindingFlags.Instance | BindingFlags.Public))
			{
				ParameterInfo[] parameters = constructor.GetParameters();
				if (parameters.Length == 5 && parameters[0].ParameterType == typeof(object) && parameters[1].ParameterType == typeof(string) && parameters[3].ParameterType == typeof(bool) && parameters[4].ParameterType == typeof(string))
				{
					preferred = constructor;
					break;
				}
				if (parameters.Length == 3 && parameters[0].ParameterType == typeof(object) && parameters[1].ParameterType == typeof(string))
				{
					fallback = constructor;
				}
			}
			ConstructorInfo selected = preferred ?? fallback;
			if (selected == null)
			{
				throw new MissingMethodException(typeof(InquiryElement).FullName, ".ctor");
			}
			ParameterInfo[] selectedParameters = selected.GetParameters();
			object imageIdentifier = selectedParameters[2].ParameterType.IsValueType ? Activator.CreateInstance(selectedParameters[2].ParameterType) : null;
			object[] arguments = selectedParameters.Length == 5 ? new object[5] { choice, label, imageIdentifier, enabled, hint ?? string.Empty } : new object[3] { choice, label, imageIdentifier };
			return (InquiryElement)selected.Invoke(arguments);
		}

		private static object GetSideLeaderParty(object side)
		{
			return ReadMember(side, "LeaderParty");
		}

		private static object ReadStaticMember(Type type, string name)
		{
			PropertyInfo property = type.GetProperty(name, StaticFlags);
			if (property != null && property.GetIndexParameters().Length == 0)
			{
				return property.GetValue(null, null);
			}
			FieldInfo field = type.GetField(name, StaticFlags);
			return field == null ? null : field.GetValue(null);
		}

		private static bool ReadStaticBool(Type type, string name)
		{
			object value = ReadStaticMember(type, name);
			return value != null && Convert.ToBoolean(value);
		}

		private static object ReadMember(object instance, string name)
		{
			if (instance == null)
			{
				return null;
			}
			Type type = instance.GetType();
			PropertyInfo property = type.GetProperty(name, InstanceFlags);
			if (property != null && property.GetIndexParameters().Length == 0)
			{
				return property.GetValue(instance, null);
			}
			FieldInfo field = type.GetField(name, InstanceFlags);
			return field == null ? null : field.GetValue(instance);
		}

		private static void WriteMember(object instance, string name, object value)
		{
			if (instance == null)
			{
				return;
			}
			Type type = instance.GetType();
			PropertyInfo property = type.GetProperty(name, InstanceFlags);
			if (property != null && property.CanWrite)
			{
				property.SetValue(instance, value, null);
				return;
			}
			FieldInfo field = type.GetField(name, InstanceFlags);
			if (field != null)
			{
				field.SetValue(instance, value);
			}
		}

		private static string SafeId(object value)
		{
			if (value == null)
			{
				return "<null>";
			}
			object id = ReadMember(value, "StringId") ?? ReadMember(value, "Id");
			return id == null ? value.GetType().Name : id.ToString();
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			MethodInfo method = typeof(BattleInterventionFlowFix).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(BattleInterventionFlowFix).FullName, name);
			}
			return method;
		}

		private static Type RequireType(string qualifiedName)
		{
			Type type = Type.GetType(qualifiedName, throwOnError: false);
			if (type == null)
			{
				throw new TypeLoadException(qualifiedName);
			}
			return type;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix)
		{
			if (original == null)
			{
				throw new ArgumentNullException("original");
			}
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
			ParameterInfo[] parameters = patch.GetParameters();
			object[] arguments = new object[parameters.Length];
			arguments[0] = original;
			arguments[1] = prefix == null ? null : Activator.CreateInstance(harmonyMethodType, prefix);
			arguments[2] = postfix == null ? null : Activator.CreateInstance(harmonyMethodType, postfix);
			patch.Invoke(harmony, arguments);
		}

		private static Exception Unwrap(Exception exception)
		{
			while (exception is TargetInvocationException && exception.InnerException != null)
			{
				exception = exception.InnerException;
			}
			return exception;
		}
	}
}
