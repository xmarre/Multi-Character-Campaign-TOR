using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleReinforcementOrderGuard
	{
		private sealed class ActiveOrder
		{
			internal readonly MobileParty Party;
			internal readonly MobileParty TargetParty;
			internal readonly MapEvent MapEvent;
			internal CampaignVec2 Destination;
			internal bool AiDecisionLock;
			internal float InteractionRetryDelay;

			internal ActiveOrder(MobileParty party, MobileParty targetParty, MapEvent mapEvent)
			{
				Party = party;
				TargetParty = targetParty;
				MapEvent = mapEvent;
				Destination = targetParty.Position;
			}
		}

		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private static readonly Dictionary<MobileParty, ActiveOrder> Orders = new Dictionary<MobileParty, ActiveOrder>();

		private static bool _installed;
		private static Campaign _campaign;
		private static float _tickAccumulator;
		private static MethodInfo _canOrderPartyToBattle;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				_canOrderPartyToBattle = typeof(BattleInterventionFlowFix).GetMethod("CanOrderPartyToBattle", StaticFlags);
				MethodInfo orderPartyToBattle = typeof(BattleInterventionFlowFix).GetMethod("OrderPartyToBattle", StaticFlags);
				if (_canOrderPartyToBattle == null || orderPartyToBattle == null)
				{
					throw new MissingMethodException(typeof(BattleInterventionFlowFix).FullName, "CanOrderPartyToBattle/OrderPartyToBattle");
				}

				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.reinforcementorders.v130");
				Patch(harmony, harmonyType, harmonyMethodType, orderPartyToBattle, GetPatchMethod("BeforeOrderPartyToBattle"), null, 900);

				Type dispatcherType = RequireType("TaleWorlds.CampaignSystem.CampaignEventDispatcher, TaleWorlds.CampaignSystem");
				MethodInfo partyAdded = dispatcherType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "OnPartyAddedToMapEvent" && method.GetParameters().Length == 1);
				MethodInfo eventEnded = dispatcherType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "OnMapEventEnded" && method.GetParameters().Length == 1);
				Patch(harmony, harmonyType, harmonyMethodType, partyAdded, null, GetPatchMethod("AfterPartyAddedToMapEvent"), 0);
				Patch(harmony, harmonyType, harmonyMethodType, eventEnded, null, GetPatchMethod("AfterMapEventEnded"), 0);

				_installed = true;
				RemotePartySwitch.Info("[BattleReinforcementOrderGuard v1.3.0] Installed battle-site routing, arrival interaction, and temporary AI objective locks.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleReinforcementOrderGuard] Installation failed", Unwrap(ex));
			}
		}

		internal static void Tick(float dt)
		{
			if (!_installed)
			{
				return;
			}
			EnsureCampaignState();
			if (Orders.Count == 0)
			{
				return;
			}
			_tickAccumulator += Math.Max(0f, dt);
			if (_tickAccumulator < 0.15f)
			{
				return;
			}
			float elapsed = _tickAccumulator;
			_tickAccumulator = 0f;

			ActiveOrder[] snapshot = Orders.Values.ToArray();
			for (int i = 0; i < snapshot.Length; i++)
			{
				TickOrder(snapshot[i], elapsed);
			}
		}

		private static bool BeforeOrderPartyToBattle(MobileParty reinforcingParty, MobileParty targetParty, object expectedMapEvent, ref string reason, ref bool __result)
		{
			reason = string.Empty;
			__result = false;
			try
			{
				MapEvent mapEvent = expectedMapEvent as MapEvent;
				object[] validationArguments = new object[4] { reinforcingParty, targetParty, expectedMapEvent, null };
				if (!Convert.ToBoolean(_canOrderPartyToBattle.Invoke(null, validationArguments)))
				{
					reason = validationArguments[3] as string ?? "The reinforcement order is no longer valid.";
					return false;
				}
				if (mapEvent == null)
				{
					reason = "The active battle does not expose a Bannerlord map event.";
					return false;
				}

				CancelOrder(reinforcingParty, false, false, "replaced by a new reinforcement order");
				ActiveOrder order = new ActiveOrder(reinforcingParty, targetParty, mapEvent);
				Orders[reinforcingParty] = order;
				IssueBattleSiteRoute(order, true);
				__result = true;
				RemotePartySwitch.Info("[BattleReinforcementOrderGuard] Ordered party=" + SafeId(reinforcingParty) + " toward active battle site for target=" + SafeId(targetParty) + "; playerControlled=" + object.ReferenceEquals(reinforcingParty, MobileParty.MainParty) + ".");
			}
			catch (Exception ex)
			{
				Exception unwrapped = Unwrap(ex);
				reason = unwrapped.Message;
				CancelOrder(reinforcingParty, true, false, "route setup failed");
				RemotePartySwitch.Error("[BattleReinforcementOrderGuard] Reinforcement route setup failed", unwrapped);
			}
			return false;
		}

		private static void TickOrder(ActiveOrder order, float elapsed)
		{
			if (order == null || !Orders.ContainsKey(order.Party))
			{
				return;
			}
			MobileParty party = order.Party;
			MobileParty target = order.TargetParty;
			if (party == null || !party.IsActive)
			{
				CancelOrder(party, false, false, "reinforcing party became inactive");
				return;
			}
			if (target == null || !target.IsActive || !object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(target), order.MapEvent))
			{
				CancelOrder(party, true, false, "target battle ended or changed");
				return;
			}
			object partyEvent = RemotePartySwitch.GetEffectiveMapEvent(party);
			if (partyEvent != null)
			{
				if (object.ReferenceEquals(partyEvent, order.MapEvent))
				{
					CompleteOrder(order, "reinforcing party joined the selected battle");
				}
				else
				{
					CancelOrder(party, false, false, "reinforcing party entered another battle");
				}
				return;
			}

			bool playerControlled = object.ReferenceEquals(party, MobileParty.MainParty);
			if (playerControlled)
			{
				ReleaseAiDecisionLock(order);
				if (party.DefaultBehavior != AiBehavior.GoToPoint || party.MoveTargetPoint.DistanceSquared(order.Destination) > 0.01f)
				{
					CancelOrder(party, false, false, "player replaced the reinforcement movement order");
					return;
				}
			}
			else
			{
				EnsureAiDecisionLock(order);
				CampaignVec2 currentDestination = target.Position;
				bool destinationMoved = currentDestination.DistanceSquared(order.Destination) > 0.04f;
				if (destinationMoved || party.DefaultBehavior != AiBehavior.GoToPoint || party.MoveTargetPoint.DistanceSquared(order.Destination) > 0.01f)
				{
					order.Destination = currentDestination;
					IssueBattleSiteRoute(order, false);
				}
			}

			order.InteractionRetryDelay = Math.Max(0f, order.InteractionRetryDelay - elapsed);
			float interactionDistance = 0.35f;
			try
			{
				if (Campaign.Current != null && Campaign.Current.Models != null && Campaign.Current.Models.EncounterModel != null)
				{
					interactionDistance = Math.Max(0.1f, Campaign.Current.Models.EncounterModel.NeededMaximumDistanceForEncounteringMobileParty * 1.05f);
				}
			}
			catch
			{
			}
			if (party.Position.DistanceSquared(target.Position) > interactionDistance * interactionDistance || order.InteractionRetryDelay > 0f)
			{
				return;
			}

			order.InteractionRetryDelay = 1f;
			try
			{
				target.OnPartyInteraction(party);
				if (object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(party), order.MapEvent) || (playerControlled && PlayerEncounter.IsActive))
				{
					CompleteOrder(order, "native arrival interaction started the selected battle encounter");
				}
				else
				{
					RemotePartySwitch.Info("[BattleReinforcementOrderGuard] Arrival interaction did not complete immediately; retaining guarded order for party=" + SafeId(party) + ".");
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleReinforcementOrderGuard] Native battle-site interaction failed; the order remains active for a guarded retry", Unwrap(ex));
			}
		}

		private static void IssueBattleSiteRoute(ActiveOrder order, bool initial)
		{
			MobileParty party = order.Party;
			order.Destination = order.TargetParty.Position;
			if (!object.ReferenceEquals(party, MobileParty.MainParty) && party.Ai != null)
			{
				party.Ai.EnableAi();
			}
			party.SetMoveGoToPoint(order.Destination, MobileParty.NavigationType.Default);
			if (!object.ReferenceEquals(party, MobileParty.MainParty))
			{
				EnsureAiDecisionLock(order);
			}
			if (!initial)
			{
				RemotePartySwitch.Info("[BattleReinforcementOrderGuard] Restored guarded battle-site route for party=" + SafeId(party) + ".");
			}
		}

		private static void EnsureAiDecisionLock(ActiveOrder order)
		{
			MobileParty party = order.Party;
			if (party == null || object.ReferenceEquals(party, MobileParty.MainParty) || party.Ai == null)
			{
				return;
			}
			party.Ai.EnableAi();
			party.Ai.SetDoNotMakeNewDecisions(true);
			party.Ai.RethinkAtNextHourlyTick = false;
			WriteMember(party.Ai, "DefaultBehaviorNeedsUpdate", false);
			order.AiDecisionLock = true;
		}

		private static void ReleaseAiDecisionLock(ActiveOrder order)
		{
			if (order == null || !order.AiDecisionLock)
			{
				return;
			}
			MobileParty party = order.Party;
			if (party != null && party.Ai != null)
			{
				party.Ai.SetDoNotMakeNewDecisions(false);
				party.Ai.RethinkAtNextHourlyTick = true;
				WriteMember(party.Ai, "DefaultBehaviorNeedsUpdate", true);
			}
			order.AiDecisionLock = false;
		}

		private static void CompleteOrder(ActiveOrder order, string reason)
		{
			if (order == null)
			{
				return;
			}
			ReleaseAiDecisionLock(order);
			Orders.Remove(order.Party);
			RemotePartySwitch.Info("[BattleReinforcementOrderGuard] Completed reinforcement order for party=" + SafeId(order.Party) + ": " + reason + ".");
		}

		private static void CancelOrder(MobileParty party, bool stopIfStillOwnedRoute, bool notify, string reason)
		{
			if (party == null)
			{
				return;
			}
			ActiveOrder order;
			if (!Orders.TryGetValue(party, out order))
			{
				return;
			}
			bool stillOwnedRoute = false;
			try
			{
				stillOwnedRoute = party.DefaultBehavior == AiBehavior.GoToPoint && party.MoveTargetPoint.DistanceSquared(order.Destination) <= 0.01f;
			}
			catch
			{
			}
			ReleaseAiDecisionLock(order);
			Orders.Remove(party);
			if (stopIfStillOwnedRoute && stillOwnedRoute && party.IsActive && RemotePartySwitch.GetEffectiveMapEvent(party) == null)
			{
				try
				{
					party.SetMoveModeHold();
				}
				catch
				{
				}
			}
			if (notify)
			{
				RemotePartySwitch.Notify(RemotePartySwitch.PartyName(party) + " stopped reinforcing because " + reason + ".");
			}
			RemotePartySwitch.Info("[BattleReinforcementOrderGuard] Cancelled reinforcement order for party=" + SafeId(party) + ": " + reason + ".");
		}

		private static void AfterPartyAddedToMapEvent(PartyBase __0)
		{
			MobileParty party = __0 == null ? null : __0.MobileParty;
			if (party == null)
			{
				return;
			}
			ActiveOrder order;
			if (Orders.TryGetValue(party, out order) && object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(party), order.MapEvent))
			{
				CompleteOrder(order, "Bannerlord dispatched the party-added-to-map-event callback");
			}
		}

		private static void AfterMapEventEnded(MapEvent __0)
		{
			if (__0 == null || Orders.Count == 0)
			{
				return;
			}
			ActiveOrder[] affected = Orders.Values.Where((ActiveOrder order) => object.ReferenceEquals(order.MapEvent, __0)).ToArray();
			for (int i = 0; i < affected.Length; i++)
			{
				CancelOrder(affected[i].Party, true, true, "the battle ended before the party arrived");
			}
		}

		private static void EnsureCampaignState()
		{
			Campaign current = Campaign.Current;
			if (object.ReferenceEquals(_campaign, current))
			{
				return;
			}
			ActiveOrder[] oldOrders = Orders.Values.ToArray();
			for (int i = 0; i < oldOrders.Length; i++)
			{
				ReleaseAiDecisionLock(oldOrders[i]);
			}
			Orders.Clear();
			_campaign = current;
			_tickAccumulator = 0f;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix, int priority)
		{
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
			object harmonyPrefix = prefix == null ? null : Activator.CreateInstance(harmonyMethodType, prefix);
			object harmonyPostfix = postfix == null ? null : Activator.CreateInstance(harmonyMethodType, postfix);
			FieldInfo priorityField = harmonyMethodType.GetField("priority", BindingFlags.Instance | BindingFlags.Public);
			if (priorityField != null)
			{
				if (harmonyPrefix != null)
				{
					priorityField.SetValue(harmonyPrefix, priority);
				}
				if (harmonyPostfix != null)
				{
					priorityField.SetValue(harmonyPostfix, priority);
				}
			}
			object[] arguments = new object[patch.GetParameters().Length];
			arguments[0] = original;
			arguments[1] = harmonyPrefix;
			arguments[2] = harmonyPostfix;
			patch.Invoke(harmony, arguments);
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			MethodInfo method = typeof(BattleReinforcementOrderGuard).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(BattleReinforcementOrderGuard).FullName, name);
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

		private static void WriteMember(object instance, string name, object value)
		{
			if (instance == null)
			{
				return;
			}
			PropertyInfo property = instance.GetType().GetProperty(name, InstanceFlags);
			if (property != null && property.CanWrite)
			{
				property.SetValue(instance, value, null);
				return;
			}
			FieldInfo field = instance.GetType().GetField(name, InstanceFlags) ?? instance.GetType().GetField("_" + char.ToLowerInvariant(name[0]) + name.Substring(1), InstanceFlags);
			if (field != null)
			{
				field.SetValue(instance, value);
			}
		}

		private static string SafeId(MobileParty party)
		{
			try
			{
				return party == null ? "<null>" : party.StringId ?? party.Name.ToString();
			}
			catch
			{
				return "<unknown>";
			}
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
