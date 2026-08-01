using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleInterventionAlert
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

		private sealed class Candidate
		{
			internal readonly Hero Hero;
			internal readonly MobileParty Party;
			internal readonly object MapEvent;

			internal Candidate(Hero hero, MobileParty party, object mapEvent)
			{
				Hero = hero;
				Party = party;
				MapEvent = mapEvent;
			}
		}

		private sealed class AlertChoice
		{
			internal const int TakeControl = 1;
			internal const int Reinforce = 2;
			internal const int Dismiss = 3;

			internal readonly int Action;
			internal readonly Candidate Candidate;

			internal AlertChoice(int action, Candidate candidate)
			{
				Action = action;
				Candidate = candidate;
			}
		}

		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static readonly Queue<object> StartedEvents = new Queue<object>();
		private static readonly Queue<Candidate> Candidates = new Queue<Candidate>();
		private static readonly HashSet<object> ProcessedEvents = new HashSet<object>(ReferenceComparer.Instance);
		private static readonly HashSet<object> SuppressedEvents = new HashSet<object>(ReferenceComparer.Instance);

		private static bool _installed;
		private static bool _inquiryOpen;
		private static object _pendingEncounterEvent;
		private static int _encounterDelayTicks;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				Type dispatcherType = RequireType("TaleWorlds.CampaignSystem.CampaignEventDispatcher, TaleWorlds.CampaignSystem");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.battleintervention.v110");
				MethodInfo started = FindMethod(dispatcherType, "OnMapEventStarted", 3);
				Patch(harmony, harmonyType, harmonyMethodType, started, null, RequireMethod(typeof(BattleInterventionAlert), "AfterMapEventStarted", StaticFlags));
				MethodInfo ended = FindMethod(dispatcherType, "OnMapEventEnded", 1);
				if (ended != null)
				{
					Patch(harmony, harmonyType, harmonyMethodType, ended, null, RequireMethod(typeof(BattleInterventionAlert), "AfterMapEventEnded", StaticFlags));
				}
				_installed = true;
				RemotePartySwitch.Info("[BattleIntervention v1.1.0] Installed event-driven defensive-battle alerts and native encounter continuation.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleIntervention] Installation failed", Unwrap(ex));
			}
		}

		internal static void Tick()
		{
			if (!_installed || Campaign.Current == null)
			{
				return;
			}
			if (_pendingEncounterEvent != null)
			{
				if (_encounterDelayTicks > 0)
				{
					_encounterDelayTicks--;
					return;
				}
				object mapEvent = _pendingEncounterEvent;
				_pendingEncounterEvent = null;
				string reason;
				if (!RemotePartySwitch.ContinueBattleAsPlayer(mapEvent, out reason))
				{
					RemotePartySwitch.Notify("Control changed to the endangered character, but Bannerlord could not reopen the native battle encounter: " + reason + ". Save in a new slot and inspect MultiCharacterCampaignTOR.log.");
				}
				return;
			}
			if (_inquiryOpen)
			{
				return;
			}
			ProcessStartedEvents();
			ShowNextCandidate();
		}

		private static void AfterMapEventStarted(object __0)
		{
			if (__0 != null)
			{
				StartedEvents.Enqueue(__0);
			}
		}

		private static void AfterMapEventEnded(object __0)
		{
			if (__0 == null)
			{
				return;
			}
			ProcessedEvents.Remove(__0);
			SuppressedEvents.Remove(__0);
		}

		private static void ProcessStartedEvents()
		{
			while (StartedEvents.Count > 0)
			{
				object mapEvent = StartedEvents.Dequeue();
				if (mapEvent == null || ProcessedEvents.Contains(mapEvent))
				{
					continue;
				}
				ProcessedEvents.Add(mapEvent);
				DiscoverCandidates(mapEvent);
			}
		}

		private static void DiscoverCandidates(object mapEvent)
		{
			HashSet<MobileParty> parties = new HashSet<MobileParty>();
			IList<Hero> heroes = RemotePartySwitch.GetRegisteredSharedHeroes();
			for (int i = 0; i < heroes.Count; i++)
			{
				Hero hero = heroes[i];
				MobileParty party = hero == null ? null : hero.PartyBelongedTo;
				if (hero == null || hero == Hero.MainHero || party == null || party == MobileParty.MainParty || parties.Contains(party))
				{
					continue;
				}
				if (RemotePartySwitch.GetEffectiveMapEvent(party) != mapEvent || !RemotePartySwitch.IsDefenderInMapEvent(party, mapEvent))
				{
					continue;
				}
				Hero target = SelectTargetHero(party, heroes);
				if (target == null)
				{
					continue;
				}
				parties.Add(party);
				Candidates.Enqueue(new Candidate(target, party, mapEvent));
				RemotePartySwitch.Info("[BattleIntervention] Queued alert; hero=" + RemotePartySwitch.HeroName(target) + "; party=" + RemotePartySwitch.PartyName(party) + ".");
			}
		}

		private static Hero SelectTargetHero(MobileParty party, IList<Hero> registeredHeroes)
		{
			Hero leader = party.LeaderHero;
			if (leader != null && registeredHeroes.Contains(leader))
			{
				return leader;
			}
			for (int i = 0; i < registeredHeroes.Count; i++)
			{
				Hero hero = registeredHeroes[i];
				if (hero != null && hero.PartyBelongedTo == party)
				{
					return hero;
				}
			}
			return null;
		}

		private static void ShowNextCandidate()
		{
			while (Candidates.Count > 0)
			{
				Candidate candidate = Candidates.Dequeue();
				if (!IsCandidateCurrent(candidate) || SuppressedEvents.Contains(candidate.MapEvent))
				{
					continue;
				}
				ShowInquiry(candidate);
				return;
			}
		}

		private static bool IsCandidateCurrent(Candidate candidate)
		{
			return candidate != null && candidate.Hero != null && candidate.Hero.IsAlive && candidate.Hero.IsActive && !candidate.Hero.IsPrisoner && candidate.Party != null && candidate.Hero.PartyBelongedTo == candidate.Party && RemotePartySwitch.GetEffectiveMapEvent(candidate.Party) == candidate.MapEvent && RemotePartySwitch.IsDefenderInMapEvent(candidate.Party, candidate.MapEvent);
		}

		private static void ShowInquiry(Candidate candidate)
		{
			string controlReason;
			string reinforceReason;
			bool canControl = RemotePartySwitch.CanTakeControlForBattle(candidate.Hero, candidate.MapEvent, out controlReason);
			bool canReinforce = RemotePartySwitch.CanOrderReinforcement(candidate.Hero, candidate.MapEvent, out reinforceReason);
			List<InquiryElement> elements = new List<InquiryElement>
			{
				CreateInquiryElement(new AlertChoice(AlertChoice.TakeControl, candidate), "Take control of " + RemotePartySwitch.HeroName(candidate.Hero) + " and continue the battle", canControl, canControl ? "Switch the real player identity and MainParty to this character's existing party, then reopen Bannerlord's native encounter." : controlReason),
				CreateInquiryElement(new AlertChoice(AlertChoice.Reinforce, candidate), "Move the current party to reinforce " + RemotePartySwitch.PartyName(candidate.Party), canReinforce, canReinforce ? "Order the current MainParty toward the endangered party. Arrival is not guaranteed before the simulated battle ends." : reinforceReason),
				CreateInquiryElement(new AlertChoice(AlertChoice.Dismiss, candidate), "Dismiss this alert", true, "Take no immediate action.")
			};
			string description = RemotePartySwitch.HeroName(candidate.Hero) + " is in " + RemotePartySwitch.PartyName(candidate.Party) + ", which is being attacked by " + RemotePartySwitch.OpposingLeaderPartyName(candidate.MapEvent) + ".";
			MultiSelectionInquiryData data = new MultiSelectionInquiryData("Shared character under attack", description, elements, true, 1, 1, "Select", "Dismiss", delegate(List<InquiryElement> selected)
			{
				HandleSelection(candidate, selected);
			}, delegate(List<InquiryElement> selected)
			{
				_inquiryOpen = false;
			}, string.Empty, false);
			try
			{
				_inquiryOpen = true;
				MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
			}
			catch (Exception ex)
			{
				_inquiryOpen = false;
				RemotePartySwitch.Error("[BattleIntervention] Could not open defensive-battle inquiry", Unwrap(ex));
				RemotePartySwitch.Notify(description);
			}
		}

		private static void HandleSelection(Candidate candidate, List<InquiryElement> selected)
		{
			_inquiryOpen = false;
			try
			{
				if (selected == null || selected.Count == 0)
				{
					return;
				}
				AlertChoice choice = ReadMember(selected[0], "Identifier") as AlertChoice ?? ReadMember(selected[0], "Id") as AlertChoice;
				if (choice == null || choice.Candidate != candidate || !IsCandidateCurrent(candidate))
				{
					RemotePartySwitch.Notify("That defensive battle ended or changed before the selected action could be applied.");
					return;
				}
				if (choice.Action == AlertChoice.TakeControl)
				{
					string reason;
					if (!RemotePartySwitch.TakeControlForBattle(candidate.Hero, candidate.MapEvent, out reason))
					{
						RemotePartySwitch.Notify("Could not take control of " + RemotePartySwitch.HeroName(candidate.Hero) + ": " + reason);
						return;
					}
					SuppressedEvents.Add(candidate.MapEvent);
					_pendingEncounterEvent = candidate.MapEvent;
					_encounterDelayTicks = 2;
					return;
				}
				if (choice.Action == AlertChoice.Reinforce)
				{
					string reason;
					if (!RemotePartySwitch.OrderReinforcement(candidate.Hero, candidate.MapEvent, out reason))
					{
						RemotePartySwitch.Notify("Could not issue the reinforcement order: " + reason);
						return;
					}
					SuppressedEvents.Add(candidate.MapEvent);
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleIntervention] Alert action failed", Unwrap(ex));
				RemotePartySwitch.Notify("The selected battle-intervention action failed. See MultiCharacterCampaignTOR.log.");
			}
		}

		private static InquiryElement CreateInquiryElement(AlertChoice choice, string label, bool enabled, string hint)
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

		private static object ReadMember(object instance, string name)
		{
			if (instance == null)
			{
				return null;
			}
			PropertyInfo property = instance.GetType().GetProperty(name, InstanceFlags);
			if (property != null && property.CanRead)
			{
				return property.GetValue(instance, null);
			}
			FieldInfo field = instance.GetType().GetField(name, InstanceFlags);
			return field == null ? null : field.GetValue(instance);
		}

		private static MethodInfo FindMethod(Type type, string name, int parameterCount)
		{
			return type.GetMethods(InstanceFlags).FirstOrDefault(delegate(MethodInfo method)
			{
				return method.Name == name && method.GetParameters().Length == parameterCount;
			});
		}

		private static Type RequireType(string name)
		{
			Type type = Type.GetType(name, false);
			if (type == null)
			{
				throw new TypeLoadException(name);
			}
			return type;
		}

		private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags)
		{
			MethodInfo method = type.GetMethods(flags).FirstOrDefault(delegate(MethodInfo candidate)
			{
				return candidate.Name == name;
			});
			if (method == null)
			{
				throw new MissingMethodException(type.FullName, name);
			}
			return method;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix)
		{
			if (original == null)
			{
				throw new MissingMethodException("Campaign event dispatcher method was not found.");
			}
			object prefixMethod = prefix == null ? null : Activator.CreateInstance(harmonyMethodType, prefix);
			object postfixMethod = postfix == null ? null : Activator.CreateInstance(harmonyMethodType, postfix);
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First(delegate(MethodInfo method)
			{
				return method.Name == "Patch" && method.GetParameters().Length >= 4;
			});
			object[] arguments = new object[patch.GetParameters().Length];
			arguments[0] = original;
			arguments[1] = prefixMethod;
			arguments[2] = postfixMethod;
			patch.Invoke(harmony, arguments);
		}

		private static Exception Unwrap(Exception ex)
		{
			Exception current = ex;
			while (current is TargetInvocationException && current.InnerException != null)
			{
				current = current.InnerException;
			}
			return current;
		}
	}
}
