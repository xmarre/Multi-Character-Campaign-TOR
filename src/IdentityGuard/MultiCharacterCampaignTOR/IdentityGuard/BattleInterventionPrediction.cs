using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleInterventionPrediction
	{
		private sealed class ReferenceComparer : IEqualityComparer<object>
		{
			internal static readonly ReferenceComparer Instance = new ReferenceComparer();

			public new bool Equals(object x, object y)
			{
				return object.ReferenceEquals(x, y);
			}

			public int GetHashCode(object obj)
			{
				return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
			}
		}

		private sealed class UnitAggregate
		{
			internal readonly string Name;
			internal readonly int Tier;
			internal int Total;
			internal int Wounded;

			internal UnitAggregate(string name, int tier)
			{
				Name = name;
				Tier = tier;
			}
		}

		private sealed class SideSummary
		{
			internal int PartyCount;
			internal int Healthy;
			internal int Wounded;
			internal float Strength;
			internal readonly Dictionary<string, int> FormationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
			internal readonly Dictionary<string, UnitAggregate> Units = new Dictionary<string, UnitAggregate>(StringComparer.Ordinal);

			internal string CompactText(string heading)
			{
				return heading + ": " + Healthy + " ready, " + Wounded + " wounded, native strength " + Strength.ToString("0.0") + "\n" +
					"Composition: " + FormatComposition() + "\n" +
					"Main troops: " + FormatUnits(7);
			}

			internal string DetailedText(string heading)
			{
				return heading + " — " + PartyCount + " involved " + (PartyCount == 1 ? "party" : "parties") + ", " + Healthy + " ready, " + Wounded + " wounded, native strength " + Strength.ToString("0.0") + "\n" +
					"Formation composition: " + FormatComposition() + "\n" +
					"Troops: " + FormatUnits(16);
			}

			private string FormatComposition()
			{
				if (FormationCounts.Count == 0)
				{
					return "unavailable";
				}
				string[] preferred = new string[4] { "Infantry", "Ranged", "Cavalry", "HorseArcher" };
				List<string> result = new List<string>();
				for (int i = 0; i < preferred.Length; i++)
				{
					int count;
					if (FormationCounts.TryGetValue(preferred[i], out count) && count > 0)
					{
						result.Add(FormatFormationName(preferred[i]) + " " + count);
					}
				}
				foreach (KeyValuePair<string, int> entry in FormationCounts.OrderBy((KeyValuePair<string, int> item) => item.Key, StringComparer.Ordinal))
				{
					if (entry.Value > 0 && Array.IndexOf(preferred, entry.Key) < 0)
					{
						result.Add(FormatFormationName(entry.Key) + " " + entry.Value);
					}
				}
				return result.Count == 0 ? "unavailable" : string.Join(" | ", result.ToArray());
			}

			private string FormatUnits(int maximum)
			{
				List<UnitAggregate> ordered = Units.Values
					.Where((UnitAggregate unit) => unit.Total > 0)
					.OrderByDescending((UnitAggregate unit) => unit.Total)
					.ThenByDescending((UnitAggregate unit) => unit.Tier)
					.ThenBy((UnitAggregate unit) => unit.Name, StringComparer.Ordinal)
					.ToList();
				if (ordered.Count == 0)
				{
					return "unavailable";
				}
				List<string> result = new List<string>();
				int visible = Math.Min(maximum, ordered.Count);
				for (int i = 0; i < visible; i++)
				{
					UnitAggregate unit = ordered[i];
					string wounded = unit.Wounded > 0 ? ", " + unit.Wounded + " wounded" : string.Empty;
					result.Add(unit.Name + " x" + unit.Total + wounded);
				}
				if (ordered.Count > visible)
				{
					result.Add("+" + (ordered.Count - visible) + " more troop types");
				}
				return string.Join("; ", result.ToArray());
			}
		}

		private sealed class Snapshot
		{
			internal bool CanPredict;
			internal bool PredictedLoss;
			internal float FriendlyStrength;
			internal float EnemyStrength;
			internal SideSummary Friendly;
			internal SideSummary Enemy;

			internal string OutcomeLabel
			{
				get
				{
					if (!CanPredict)
					{
						return "prediction unavailable";
					}
					if (PredictedLoss)
					{
						return "likely defeat";
					}
					if (Math.Abs(FriendlyStrength - EnemyStrength) <= Math.Max(1f, Math.Max(FriendlyStrength, EnemyStrength) * 0.02f))
					{
						return "approximately even";
					}
					return "likely victory";
				}
			}

			internal string Summary
			{
				get
				{
					string estimate;
					if (!CanPredict)
					{
						estimate = "Predicted outcome: unavailable. The alert is shown as a safety fallback.";
					}
					else
					{
						float total = FriendlyStrength + EnemyStrength;
						float share = total > 0.001f ? FriendlyStrength / total * 100f : 50f;
						estimate = "Predicted outcome: " + OutcomeLabel + " — your side has " + share.ToString("0") + "% of the current native battle strength.";
					}
					return estimate + "\n\n" + Friendly.CompactText("Your side") + "\n\n" + Enemy.CompactText("Opposing side") + "\n\nHover either action for the longer troop breakdown.";
				}
			}

			internal string Tooltip
			{
				get
				{
					return Friendly.DetailedText("YOUR SIDE") + "\n\n" + Enemy.DetailedText("OPPOSING SIDE");
				}
			}
		}

		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static readonly Dictionary<object, HashSet<MobileParty>> AlertedParties = new Dictionary<object, HashSet<MobileParty>>(ReferenceComparer.Instance);
		private static readonly Dictionary<object, HashSet<MobileParty>> ReservedParties = new Dictionary<object, HashSet<MobileParty>>(ReferenceComparer.Instance);

		private static bool _installed;
		private static Campaign _campaign;
		private static Type _candidateType;
		private static ConstructorInfo _candidateConstructor;
		private static FieldInfo _candidateHeroField;
		private static FieldInfo _candidatePartyField;
		private static FieldInfo _candidateMapEventField;
		private static FieldInfo _candidateQueueField;
		private static FieldInfo _suppressedEventsField;
		private static FieldInfo _inquiryOpenField;
		private static ConstructorInfo _flowChoiceConstructor;
		private static MethodInfo _legacyHandleSelection;
		private static MethodInfo _legacyCanOrderParty;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				ResolveMembers();
				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.battleprediction.v120");

				MethodInfo legacyInquiry = typeof(BattleInterventionFlowFix).GetMethod("BeforeShowInquiry", StaticFlags);
				Patch(harmony, harmonyType, harmonyMethodType, legacyInquiry, GetPatchMethod("BeforeLegacyInquiry"), null);

				Type dispatcherType = RequireType("TaleWorlds.CampaignSystem.CampaignEventDispatcher, TaleWorlds.CampaignSystem");
				MethodInfo partyAdded = dispatcherType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "OnPartyAddedToMapEvent" && method.GetParameters().Length == 1);
				MethodInfo eventEnded = dispatcherType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "OnMapEventEnded" && method.GetParameters().Length == 1);
				Patch(harmony, harmonyType, harmonyMethodType, partyAdded, null, GetPatchMethod("AfterPartyAddedToMapEvent"));
				Patch(harmony, harmonyType, harmonyMethodType, eventEnded, null, GetPatchMethod("AfterMapEventEnded"));

				_installed = true;
				RemotePartySwitch.Info("[BattleInterventionPrediction v1.2.0] Installed native side-strength filtering, reinforcement-triggered reevaluation, and alert troop composition details.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionPrediction] Installation failed", Unwrap(ex));
			}
		}

		private static bool BeforeLegacyInquiry(object __0, ref bool __result)
		{
			__result = false;
			ShowPredictionInquiry(__0);
			return false;
		}

		private static void ShowPredictionInquiry(object candidate)
		{
			SetInquiryOpen(false);
			try
			{
				EnsureCampaignState();
				Hero hero = CandidateHero(candidate);
				MobileParty party = CandidateParty(candidate);
				MapEvent mapEvent = CandidateMapEvent(candidate);
				if (hero == null || party == null || mapEvent == null)
				{
					return;
				}

				MarkDequeued(mapEvent, party);
				if (WasAlerted(mapEvent, party))
				{
					RemotePartySwitch.Info("[BattleInterventionPrediction] Suppressed a duplicate alert for party=" + SafeId(party) + ".");
					return;
				}

				Snapshot snapshot = BuildSnapshot(mapEvent, party);
				if (BattleInterventionSettings.PredictedLossesOnly && snapshot.CanPredict && !snapshot.PredictedLoss)
				{
					RemotePartySwitch.Info("[BattleInterventionPrediction] Predicted-loss-only mode suppressed a favorable/even battle alert; party=" + SafeId(party) + "; friendlyStrength=" + snapshot.FriendlyStrength.ToString("0.0") + "; opposingStrength=" + snapshot.EnemyStrength.ToString("0.0") + ".");
					return;
				}

				MarkAlerted(mapEvent, party);
				string controlReason;
				bool canControl = RemotePartySwitch.CanTakeControlForBattle(hero, mapEvent, out controlReason);
				string reinforceReason;
				bool canReinforce = CanOrderPartyToBattle(MobileParty.MainParty, party, mapEvent, out reinforceReason);
				string detailHint = snapshot.Tooltip;
				List<InquiryElement> elements = new List<InquiryElement>
				{
					CreateInquiryElement(CreateFlowChoice(1, candidate), "Take control of " + RemotePartySwitch.HeroName(hero) + " and continue the battle", canControl, (canControl ? "Switch control to this character and immediately open Bannerlord's native encounter window." : controlReason) + "\n\n" + detailHint),
					CreateInquiryElement(CreateFlowChoice(2, candidate), "Send the current party to reinforce " + RemotePartySwitch.PartyName(party), canReinforce, (canReinforce ? "Order the current party toward this fight. This can be selected together with taking control." : reinforceReason) + "\n\n" + detailHint)
				};

				string policy = BattleInterventionSettings.PredictedLossesOnly ? "Alert policy: predicted losses only." : "Alert policy: every eligible battle.";
				string description = RemotePartySwitch.HeroName(hero) + " is involved in an active battle with " + RemotePartySwitch.OpposingLeaderPartyName(mapEvent) + ". Select either action or both actions together.\n" + policy + "\n\n" + snapshot.Summary;
				MultiSelectionInquiryData data = new MultiSelectionInquiryData("Shared character in battle — " + snapshot.OutcomeLabel, description, elements, true, 1, 2, "Apply selected actions", "Dismiss", delegate(List<InquiryElement> selected)
				{
					SetInquiryOpen(false);
					InvokeLegacySelection(candidate, selected);
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
				RemotePartySwitch.Error("[BattleInterventionPrediction] Could not open the strength-aware battle inquiry", Unwrap(ex));
				RemotePartySwitch.Notify("A shared-character battle was detected, but its strength-aware intervention window could not be opened. See MultiCharacterCampaignTOR.log.");
			}
		}

		private static void AfterPartyAddedToMapEvent(PartyBase __0)
		{
			if (!BattleInterventionSettings.PredictedLossesOnly)
			{
				return;
			}
			try
			{
				EnsureCampaignState();
				MapEvent mapEvent = GetMapEvent(__0);
				if (mapEvent == null || IsSuppressed(mapEvent))
				{
					return;
				}
				IList<Hero> registered = RemotePartySwitch.GetRegisteredSharedHeroes();
				HashSet<MobileParty> examined = new HashSet<MobileParty>();
				for (int i = 0; i < registered.Count; i++)
				{
					Hero hero = registered[i];
					MobileParty party = hero == null ? null : hero.PartyBelongedTo;
					if (hero == null || hero == Hero.MainHero || party == null || party == MobileParty.MainParty || !party.IsActive || !examined.Add(party))
					{
						continue;
					}
					if (!object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(party), mapEvent) || !RemotePartySwitch.IsDefenderInMapEvent(party, mapEvent))
					{
						continue;
					}
					Snapshot snapshot = BuildSnapshot(mapEvent, party);
					if (!snapshot.CanPredict || !snapshot.PredictedLoss || WasAlerted(mapEvent, party) || !Reserve(mapEvent, party))
					{
						continue;
					}
					Hero target = SelectTargetHero(party, registered);
					if (target == null)
					{
						MarkDequeued(mapEvent, party);
						continue;
					}
					object candidate = _candidateConstructor.Invoke(new object[3] { target, party, mapEvent });
					object queue = _candidateQueueField.GetValue(null);
					queue.GetType().GetMethod("Enqueue", InstanceFlags, null, new Type[1] { _candidateType }, null).Invoke(queue, new object[1] { candidate });
					RemotePartySwitch.Info("[BattleInterventionPrediction] An added battle party changed the native forecast to a loss; queued alert for party=" + SafeId(party) + "; friendlyStrength=" + snapshot.FriendlyStrength.ToString("0.0") + "; opposingStrength=" + snapshot.EnemyStrength.ToString("0.0") + ".");
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionPrediction] Could not reevaluate a battle after a party joined", Unwrap(ex));
			}
		}

		private static void AfterMapEventEnded(object __0)
		{
			if (__0 == null)
			{
				return;
			}
			AlertedParties.Remove(__0);
			ReservedParties.Remove(__0);
		}

		private static Snapshot BuildSnapshot(MapEvent mapEvent, MobileParty party)
		{
			Snapshot snapshot = new Snapshot();
			BattleSideEnum side;
			if (!TryGetPartySide(party, out side))
			{
				snapshot.Friendly = new SideSummary();
				snapshot.Enemy = new SideSummary();
				return snapshot;
			}
			float friendlyStrength = 0f;
			float enemyStrength = 0f;
			try
			{
				mapEvent.GetStrengthsRelativeToParty(side, out friendlyStrength, out enemyStrength);
				snapshot.CanPredict = !float.IsNaN(friendlyStrength) && !float.IsInfinity(friendlyStrength) && !float.IsNaN(enemyStrength) && !float.IsInfinity(enemyStrength) && friendlyStrength + enemyStrength > 0.001f;
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionPrediction] Native side-strength query failed", Unwrap(ex));
			}
			snapshot.FriendlyStrength = Math.Max(0f, friendlyStrength);
			snapshot.EnemyStrength = Math.Max(0f, enemyStrength);
			snapshot.PredictedLoss = snapshot.CanPredict && snapshot.FriendlyStrength < snapshot.EnemyStrength;
			snapshot.Friendly = BuildSideSummary(mapEvent, side, snapshot.FriendlyStrength);
			snapshot.Enemy = BuildSideSummary(mapEvent, mapEvent.GetOtherSide(side), snapshot.EnemyStrength);
			return snapshot;
		}

		private static SideSummary BuildSideSummary(MapEvent mapEvent, BattleSideEnum side, float strength)
		{
			SideSummary summary = new SideSummary();
			summary.Strength = strength;
			try
			{
				foreach (MapEventParty eventParty in mapEvent.PartiesOnSide(side))
				{
					PartyBase party = eventParty == null ? null : eventParty.Party;
					TroopRoster roster = party == null ? null : party.MemberRoster;
					if (roster == null)
					{
						continue;
					}
					summary.PartyCount++;
					for (int index = 0; index < roster.Count; index++)
					{
						CharacterObject character = roster.GetCharacterAtIndex(index);
						int total = Math.Max(0, roster.GetElementNumber(index));
						int wounded = Math.Max(0, Math.Min(total, roster.GetElementWoundedNumber(index)));
						int healthy = total - wounded;
						if (character == null || total <= 0)
						{
							continue;
						}
						summary.Healthy += healthy;
						summary.Wounded += wounded;
						string formation = character.GetFormationClass().ToString();
						int formationCount;
						summary.FormationCounts.TryGetValue(formation, out formationCount);
						summary.FormationCounts[formation] = formationCount + total;

						string key = string.IsNullOrEmpty(character.StringId) ? character.Name.ToString() : character.StringId;
						UnitAggregate unit;
						if (!summary.Units.TryGetValue(key, out unit))
						{
							string name = character.Name == null ? key : character.Name.ToString();
							unit = new UnitAggregate(string.IsNullOrEmpty(name) ? key : name, character.Tier);
							summary.Units.Add(key, unit);
						}
						unit.Total += total;
						unit.Wounded += wounded;
					}
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionPrediction] Could not build a battle-side troop summary", Unwrap(ex));
			}
			return summary;
		}

		private static bool TryGetPartySide(MobileParty party, out BattleSideEnum side)
		{
			side = BattleSideEnum.None;
			if (party == null || party.Party == null)
			{
				return false;
			}
			side = party.Party.Side;
			if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender && party.Party.MapEventSide != null)
			{
				side = party.Party.MapEventSide.MissionSide;
			}
			return side == BattleSideEnum.Attacker || side == BattleSideEnum.Defender;
		}

		private static bool CanOrderPartyToBattle(MobileParty reinforcingParty, MobileParty targetParty, object mapEvent, out string reason)
		{
			object[] arguments = new object[4] { reinforcingParty, targetParty, mapEvent, null };
			bool result = Convert.ToBoolean(_legacyCanOrderParty.Invoke(null, arguments));
			reason = arguments[3] as string ?? string.Empty;
			return result;
		}

		private static void InvokeLegacySelection(object candidate, List<InquiryElement> selected)
		{
			try
			{
				_legacyHandleSelection.Invoke(null, new object[2] { candidate, selected });
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionPrediction] The validated v1.1.2 action handler failed", Unwrap(ex));
				RemotePartySwitch.Notify("The selected battle-intervention actions failed. See MultiCharacterCampaignTOR.log.");
			}
		}

		private static object CreateFlowChoice(int action, object candidate)
		{
			return _flowChoiceConstructor.Invoke(new object[2] { action, candidate });
		}

		private static InquiryElement CreateInquiryElement(object choice, string label, bool enabled, string hint)
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

		private static Hero SelectTargetHero(MobileParty party, IList<Hero> registered)
		{
			Hero leader = party.LeaderHero;
			if (leader != null && registered.Contains(leader))
			{
				return leader;
			}
			for (int i = 0; i < registered.Count; i++)
			{
				Hero hero = registered[i];
				if (hero != null && object.ReferenceEquals(hero.PartyBelongedTo, party))
				{
					return hero;
				}
			}
			return null;
		}

		private static MapEvent GetMapEvent(PartyBase party)
		{
			MobileParty mobile = party == null ? null : party.MobileParty;
			MapEvent mapEvent = mobile == null ? null : RemotePartySwitch.GetEffectiveMapEvent(mobile) as MapEvent;
			if (mapEvent != null)
			{
				return mapEvent;
			}
			object side = party == null ? null : party.MapEventSide;
			return ReadMember(side, "MapEvent") as MapEvent ?? ReadMember(side, "_mapEvent") as MapEvent;
		}

		private static bool WasAlerted(object mapEvent, MobileParty party)
		{
			HashSet<MobileParty> parties;
			return mapEvent != null && party != null && AlertedParties.TryGetValue(mapEvent, out parties) && parties.Contains(party);
		}

		private static void MarkAlerted(object mapEvent, MobileParty party)
		{
			HashSet<MobileParty> parties;
			if (!AlertedParties.TryGetValue(mapEvent, out parties))
			{
				parties = new HashSet<MobileParty>();
				AlertedParties.Add(mapEvent, parties);
			}
			parties.Add(party);
		}

		private static bool Reserve(object mapEvent, MobileParty party)
		{
			HashSet<MobileParty> parties;
			if (!ReservedParties.TryGetValue(mapEvent, out parties))
			{
				parties = new HashSet<MobileParty>();
				ReservedParties.Add(mapEvent, parties);
			}
			return parties.Add(party);
		}

		private static void MarkDequeued(object mapEvent, MobileParty party)
		{
			HashSet<MobileParty> parties;
			if (mapEvent != null && party != null && ReservedParties.TryGetValue(mapEvent, out parties))
			{
				parties.Remove(party);
				if (parties.Count == 0)
				{
					ReservedParties.Remove(mapEvent);
				}
			}
		}

		private static bool IsSuppressed(object mapEvent)
		{
			IEnumerable suppressed = _suppressedEventsField.GetValue(null) as IEnumerable;
			if (suppressed == null)
			{
				return false;
			}
			foreach (object item in suppressed)
			{
				if (object.ReferenceEquals(item, mapEvent))
				{
					return true;
				}
			}
			return false;
		}

		private static void EnsureCampaignState()
		{
			Campaign current = Campaign.Current;
			if (!object.ReferenceEquals(_campaign, current))
			{
				_campaign = current;
				AlertedParties.Clear();
				ReservedParties.Clear();
			}
		}

		private static void ResolveMembers()
		{
			Type alertType = typeof(BattleInterventionAlert);
			_candidateType = alertType.GetNestedType("Candidate", BindingFlags.NonPublic);
			if (_candidateType == null)
			{
				throw new MissingMemberException(alertType.FullName, "Candidate");
			}
			_candidateConstructor = _candidateType.GetConstructors(InstanceFlags).Single((ConstructorInfo constructor) => constructor.GetParameters().Length == 3);
			_candidateHeroField = _candidateType.GetField("Hero", InstanceFlags);
			_candidatePartyField = _candidateType.GetField("Party", InstanceFlags);
			_candidateMapEventField = _candidateType.GetField("MapEvent", InstanceFlags);
			_candidateQueueField = alertType.GetField("Candidates", StaticFlags);
			_suppressedEventsField = alertType.GetField("SuppressedEvents", StaticFlags);
			_inquiryOpenField = alertType.GetField("_inquiryOpen", StaticFlags);

			Type flowType = typeof(BattleInterventionFlowFix);
			Type choiceType = flowType.GetNestedType("FlowChoice", BindingFlags.NonPublic);
			_flowChoiceConstructor = choiceType == null ? null : choiceType.GetConstructors(InstanceFlags).Single((ConstructorInfo constructor) => constructor.GetParameters().Length == 2);
			_legacyHandleSelection = flowType.GetMethod("HandleSelection", StaticFlags);
			_legacyCanOrderParty = flowType.GetMethod("CanOrderPartyToBattle", StaticFlags);

			if (_candidateConstructor == null || _candidateHeroField == null || _candidatePartyField == null || _candidateMapEventField == null || _candidateQueueField == null || _suppressedEventsField == null || _inquiryOpenField == null || _flowChoiceConstructor == null || _legacyHandleSelection == null || _legacyCanOrderParty == null)
			{
				throw new MissingMemberException("The v1.1.2 battle-intervention UI/action surfaces are unavailable.");
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

		private static MapEvent CandidateMapEvent(object candidate)
		{
			return candidate == null ? null : _candidateMapEventField.GetValue(candidate) as MapEvent;
		}

		private static void SetInquiryOpen(bool value)
		{
			_inquiryOpenField.SetValue(null, value);
		}

		private static string FormatFormationName(string name)
		{
			if (string.Equals(name, "HorseArcher", StringComparison.Ordinal))
			{
				return "Horse archers";
			}
			return name;
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
			MethodInfo method = typeof(BattleInterventionPrediction).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(BattleInterventionPrediction).FullName, name);
			}
			return method;
		}

		private static Type RequireType(string qualifiedName)
		{
			Type type = Type.GetType(qualifiedName, false);
			if (type == null)
			{
				throw new TypeLoadException(qualifiedName);
			}
			return type;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix)
		{
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
			object[] arguments = new object[patch.GetParameters().Length];
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
