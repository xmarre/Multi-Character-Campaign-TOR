using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class SharedPartyRuntimeFixes
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

		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private const int RemotePartyReserve = 10000;

		private static readonly Dictionary<object, HashSet<MobileParty>> QueuedJoinedParties = new Dictionary<object, HashSet<MobileParty>>(ReferenceComparer.Instance);
		private static readonly HashSet<MobileParty> LoggedFinanceParties = new HashSet<MobileParty>();

		private static bool _installed;
		private static Campaign _campaign;
		private static Type _candidateType;
		private static ConstructorInfo _candidateConstructor;
		private static FieldInfo _candidateQueueField;
		private static FieldInfo _processedEventsField;
		private static MethodInfo _partyInSiegeOrRaid;

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
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.sharedpartyfixes.v111");

				Type behaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");
				MethodInfo synchronizeGold = behaviorType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "SynchronizeGold" && method.GetParameters().Length == 1);
				Patch(harmony, harmonyType, harmonyMethodType, synchronizeGold, GetPatchMethod("BeforeSynchronizeGold"), GetPatchMethod("AfterSynchronizeGold"));

				Type financeType = RequireType("TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel, TaleWorlds.CampaignSystem");
				MethodInfo addIncomeFromParty = financeType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "AddIncomeFromParty" && method.GetParameters().Length == 4 && method.ReturnType == typeof(int));
				Patch(harmony, harmonyType, harmonyMethodType, addIncomeFromParty, GetPatchMethod("BeforeAddIncomeFromParty"), null);

				MethodInfo participantMethod = typeof(RemotePartySwitch).GetMethod("IsDefenderInMapEvent", StaticFlags);
				Patch(harmony, harmonyType, harmonyMethodType, participantMethod, GetPatchMethod("BeforeIsDefenderInMapEvent"), null);

				MethodInfo remoteEligibility = typeof(RemotePartySwitch).GetMethods(StaticFlags).Single((MethodInfo method) => method.Name == "CanUseRemoteParty" && method.GetParameters().Length == 5);
				Patch(harmony, harmonyType, harmonyMethodType, remoteEligibility, null, GetPatchMethod("AfterCanUseRemoteParty"));
				_partyInSiegeOrRaid = typeof(RemotePartySwitch).GetMethod("PartyInSiegeOrRaid", StaticFlags);

				Type dispatcherType = RequireType("TaleWorlds.CampaignSystem.CampaignEventDispatcher, TaleWorlds.CampaignSystem");
				MethodInfo partyAdded = dispatcherType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "OnPartyAddedToMapEvent" && method.GetParameters().Length == 1);
				Patch(harmony, harmonyType, harmonyMethodType, partyAdded, null, GetPatchMethod("AfterPartyAddedToMapEvent"));
				MethodInfo mapEventEnded = dispatcherType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "OnMapEventEnded" && method.GetParameters().Length == 1);
				Patch(harmony, harmonyType, harmonyMethodType, mapEventEnded, null, GetPatchMethod("AfterMapEventEnded"));

				ResolveBattleAlertQueue();
				_installed = true;
				RemotePartySwitch.Info("[SharedPartyRuntimeFixes v1.1.1] Installed independent remote-party treasuries, mirrored-wallet finance guard, all-side battle eligibility, and joined-battle alerts.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[SharedPartyRuntimeFixes] Installation failed", Unwrap(ex));
			}
		}

		private static void BeforeSynchronizeGold(int amount, ref Dictionary<Hero, int> __state)
		{
			__state = null;
			if (!EnsureCampaignState())
			{
				return;
			}
			IList<Hero> registered = RemotePartySwitch.GetRegisteredSharedHeroes();
			Dictionary<Hero, int> remoteBalances = null;
			for (int i = 0; i < registered.Count; i++)
			{
				Hero hero = registered[i];
				MobileParty party = hero == null ? null : hero.PartyBelongedTo;
				if (hero == null || !hero.IsAlive || hero == Hero.MainHero || party == null || party == MobileParty.MainParty)
				{
					continue;
				}
				int balance = hero.Gold;
				int restoredBalance = balance == amount ? Math.Min(balance, RemotePartyReserve) : balance;
				if (remoteBalances == null)
				{
					remoteBalances = new Dictionary<Hero, int>();
				}
				remoteBalances[hero] = restoredBalance;
				if (balance != restoredBalance && LoggedFinanceParties.Add(party))
				{
					RemotePartySwitch.Info("[SharedPartyFinance] Removed mirrored shared wallet from remote party=" + SafeId(party) + "; hero=" + SafeId(hero) + "; mirrored=" + balance + "; independentReserve=" + restoredBalance + ".");
				}
			}
			__state = remoteBalances;
		}

		private static void AfterSynchronizeGold(Dictionary<Hero, int> __state)
		{
			if (__state == null)
			{
				return;
			}
			foreach (KeyValuePair<Hero, int> entry in __state)
			{
				Hero hero = entry.Key;
				MobileParty party = hero == null ? null : hero.PartyBelongedTo;
				if (hero != null && hero.IsAlive && hero != Hero.MainHero && party != null && party != MobileParty.MainParty)
				{
					hero.Gold = Math.Max(0, entry.Value);
				}
			}
		}

		private static bool BeforeAddIncomeFromParty(MobileParty party, Clan clan, ref int __result)
		{
			if (party == null || clan == null || !object.ReferenceEquals(clan, Clan.PlayerClan) || party == MobileParty.MainParty)
			{
				return true;
			}
			Hero leader = party.LeaderHero;
			Hero active = Hero.MainHero;
			if (leader == null || active == null || leader.Gold <= RemotePartyReserve || leader.Gold != active.Gold || !IsRegisteredSharedHero(leader))
			{
				return true;
			}
			__result = 0;
			if (LoggedFinanceParties.Add(party))
			{
				RemotePartySwitch.Info("[SharedPartyFinance] Blocked vanilla party-income collection from a still-mirrored shared wallet; party=" + SafeId(party) + "; hero=" + SafeId(leader) + "; balance=" + leader.Gold + ".");
			}
			return false;
		}

		private static bool BeforeIsDefenderInMapEvent(MobileParty party, object mapEvent, ref bool __result)
		{
			__result = IsPartyParticipant(party, mapEvent);
			return false;
		}

		private static void AfterCanUseRemoteParty(Hero target, object permittedMapEvent, ref string reason, ref bool __result)
		{
			if (__result || permittedMapEvent == null || target == null || !string.Equals(reason, "That party is attached to another party outside a valid army.", StringComparison.Ordinal))
			{
				return;
			}
			MobileParty targetParty = target.PartyBelongedTo;
			MobileParty attachedTo = targetParty == null ? null : targetParty.AttachedTo;
			if (targetParty == null || attachedTo == null || targetParty.Army != null || RemotePartySwitch.GetEffectiveMapEvent(targetParty) != permittedMapEvent || RemotePartySwitch.GetEffectiveMapEvent(attachedTo) != permittedMapEvent || !IsPartyParticipant(targetParty, permittedMapEvent))
			{
				return;
			}
			MobileParty mainParty = MobileParty.MainParty;
			Hero mainHero = Hero.MainHero;
			if (mainParty == null || mainHero == null || mainParty.LeaderHero != mainHero || RemotePartySwitch.GetEffectiveMapEvent(mainParty) != null || PartyInSiegeOrRaid(mainParty) || mainParty.CurrentSettlement != null || mainParty.IsTransitionInProgress)
			{
				return;
			}
			reason = string.Empty;
			__result = true;
			RemotePartySwitch.Info("[BattleIntervention] Permitted takeover of a shared party attached to an AI battle side; party=" + SafeId(targetParty) + "; attachedTo=" + SafeId(attachedTo) + ".");
		}

		private static void AfterPartyAddedToMapEvent(PartyBase __0)
		{
			try
			{
				if (__0 == null || !EnsureCampaignState())
				{
					return;
				}
				MobileParty party = __0.MobileParty;
				if (party == null || party == MobileParty.MainParty || !party.IsActive)
				{
					return;
				}
				object mapEvent = RemotePartySwitch.GetEffectiveMapEvent(party);
				if (mapEvent == null || !IsPartyParticipant(party, mapEvent) || !IsMapEventAlreadyProcessed(mapEvent))
				{
					return;
				}
				Hero target = SelectRegisteredHero(party);
				if (target == null)
				{
					return;
				}
				HashSet<MobileParty> parties;
				if (!QueuedJoinedParties.TryGetValue(mapEvent, out parties))
				{
					parties = new HashSet<MobileParty>();
					QueuedJoinedParties.Add(mapEvent, parties);
				}
				if (!parties.Add(party))
				{
					return;
				}
				object candidate = _candidateConstructor.Invoke(new object[3] { target, party, mapEvent });
				object queue = _candidateQueueField.GetValue(null);
				queue.GetType().GetMethod("Enqueue", InstanceFlags, null, new Type[1] { _candidateType }, null).Invoke(queue, new object[1] { candidate });
				RemotePartySwitch.Info("[BattleIntervention] Queued joined-battle alert; hero=" + RemotePartySwitch.HeroName(target) + "; party=" + RemotePartySwitch.PartyName(party) + ".");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleIntervention] Could not queue a party-added battle alert", Unwrap(ex));
			}
		}

		private static void AfterMapEventEnded(object __0)
		{
			if (__0 != null)
			{
				QueuedJoinedParties.Remove(__0);
			}
		}

		private static void ResolveBattleAlertQueue()
		{
			Type alertType = typeof(BattleInterventionAlert);
			_candidateType = alertType.GetNestedType("Candidate", BindingFlags.NonPublic);
			if (_candidateType == null)
			{
				throw new MissingMemberException(alertType.FullName, "Candidate");
			}
			_candidateConstructor = _candidateType.GetConstructors(InstanceFlags).Single((ConstructorInfo constructor) => constructor.GetParameters().Length == 3);
			_candidateQueueField = alertType.GetField("Candidates", StaticFlags);
			_processedEventsField = alertType.GetField("ProcessedEvents", StaticFlags);
			if (_candidateQueueField == null || _processedEventsField == null)
			{
				throw new MissingFieldException(alertType.FullName, "battle alert queues");
			}
		}

		private static bool IsMapEventAlreadyProcessed(object mapEvent)
		{
			IEnumerable processed = _processedEventsField.GetValue(null) as IEnumerable;
			if (processed == null)
			{
				return false;
			}
			foreach (object item in processed)
			{
				if (object.ReferenceEquals(item, mapEvent))
				{
					return true;
				}
			}
			return false;
		}

		private static Hero SelectRegisteredHero(MobileParty party)
		{
			IList<Hero> registered = RemotePartySwitch.GetRegisteredSharedHeroes();
			Hero leader = party.LeaderHero;
			if (leader != null && registered.Contains(leader) && leader != Hero.MainHero)
			{
				return leader;
			}
			for (int i = 0; i < registered.Count; i++)
			{
				Hero hero = registered[i];
				if (hero != null && hero != Hero.MainHero && hero.PartyBelongedTo == party)
				{
					return hero;
				}
			}
			return null;
		}

		private static bool IsRegisteredSharedHero(Hero hero)
		{
			IList<Hero> registered = RemotePartySwitch.GetRegisteredSharedHeroes();
			for (int i = 0; i < registered.Count; i++)
			{
				if (object.ReferenceEquals(registered[i], hero))
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsPartyParticipant(MobileParty party, object mapEvent)
		{
			if (party == null || mapEvent == null || !object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(party), mapEvent))
			{
				return false;
			}
			object partyBase = ReadMember(party, "Party");
			object partySide = ReadMember(partyBase, "MapEventSide");
			object attackerSide = ReadMember(mapEvent, "AttackerSide");
			object defenderSide = ReadMember(mapEvent, "DefenderSide");
			if (partySide != null && (object.ReferenceEquals(partySide, attackerSide) || object.ReferenceEquals(partySide, defenderSide)))
			{
				return true;
			}
			return SideContainsParty(attackerSide, party, partyBase) || SideContainsParty(defenderSide, party, partyBase);
		}

		private static bool SideContainsParty(object side, MobileParty party, object partyBase)
		{
			IEnumerable parties = ReadMember(side, "Parties") as IEnumerable;
			if (parties == null)
			{
				return false;
			}
			foreach (object entry in parties)
			{
				object entryPartyBase = ReadMember(entry, "Party") ?? ReadMember(entry, "PartyBase");
				object entryMobileParty = ReadMember(entry, "MobileParty") ?? ReadMember(entryPartyBase, "MobileParty");
				if (object.ReferenceEquals(entry, partyBase) || object.ReferenceEquals(entryPartyBase, partyBase) || object.ReferenceEquals(entryMobileParty, party))
				{
					return true;
				}
			}
			return false;
		}

		private static bool PartyInSiegeOrRaid(MobileParty party)
		{
			if (_partyInSiegeOrRaid == null)
			{
				return false;
			}
			return Convert.ToBoolean(_partyInSiegeOrRaid.Invoke(null, new object[1] { party }));
		}

		private static bool EnsureCampaignState()
		{
			Campaign current = Campaign.Current;
			if (!object.ReferenceEquals(_campaign, current))
			{
				QueuedJoinedParties.Clear();
				LoggedFinanceParties.Clear();
				_campaign = current;
			}
			return current != null;
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
			MethodInfo method = typeof(SharedPartyRuntimeFixes).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(SharedPartyRuntimeFixes).FullName, name);
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
