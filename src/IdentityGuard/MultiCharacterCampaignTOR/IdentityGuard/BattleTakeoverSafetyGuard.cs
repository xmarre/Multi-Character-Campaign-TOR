using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleTakeoverSafetyGuard
	{
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private const int HarmonyPriorityLast = 0;

		private static bool _installed;
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
				MethodInfo eligibility = typeof(RemotePartySwitch).GetMethods(StaticFlags).Single((MethodInfo method) => method.Name == "CanUseRemoteParty" && method.GetParameters().Length == 5);
				_partyInSiegeOrRaid = typeof(RemotePartySwitch).GetMethod("PartyInSiegeOrRaid", StaticFlags);
				if (_partyInSiegeOrRaid == null)
				{
					throw new MissingMethodException(typeof(RemotePartySwitch).FullName, "PartyInSiegeOrRaid");
				}
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.battletakeoversafety.v111");
				PatchPostfix(harmony, harmonyType, harmonyMethodType, eligibility, typeof(BattleTakeoverSafetyGuard).GetMethod("AfterCanUseRemoteParty", StaticFlags));
				_installed = true;
				RemotePartySwitch.Info("[BattleTakeoverSafety v1.1.1] Installed fail-closed target-party revalidation for battle intervention.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleTakeoverSafety] Installation failed", Unwrap(ex));
			}
		}

		private static void AfterCanUseRemoteParty(Hero target, object permittedMapEvent, ref string reason, ref bool __result)
		{
			if (!__result || permittedMapEvent == null)
			{
				return;
			}
			try
			{
				MobileParty party = target == null ? null : target.PartyBelongedTo;
				if (party == null || !object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(party), permittedMapEvent))
				{
					Block("The selected party is no longer in the expected battle.", ref reason, ref __result);
					return;
				}
				if (Convert.ToBoolean(_partyInSiegeOrRaid.Invoke(null, new object[1] { party })))
				{
					Block("Cannot take control of that character during an active siege or raid operation.", ref reason, ref __result);
					return;
				}
				if (party.CurrentSettlement != null)
				{
					Block("Cannot take control of that character while their party is inside a settlement.", ref reason, ref __result);
					return;
				}
				if (party.IsTransitionInProgress)
				{
					Block("Cannot take control of that character during an embarkation, disembarkation, or port transition.", ref reason, ref __result);
				}
			}
			catch (Exception ex)
			{
				Block("The target party's battle state could not be validated safely.", ref reason, ref __result);
				RemotePartySwitch.Error("[BattleTakeoverSafety] Target-party revalidation failed", Unwrap(ex));
			}
		}

		private static void Block(string message, ref string reason, ref bool result)
		{
			reason = message;
			result = false;
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

		private static void PatchPostfix(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo postfix)
		{
			if (original == null || postfix == null)
			{
				throw new ArgumentNullException(original == null ? "original" : "postfix");
			}
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
			ParameterInfo[] parameters = patch.GetParameters();
			object[] arguments = new object[parameters.Length];
			object harmonyMethod = Activator.CreateInstance(harmonyMethodType, postfix);
			FieldInfo priorityField = harmonyMethodType.GetField("priority", InstanceFlags);
			if (priorityField == null)
			{
				throw new MissingFieldException(harmonyMethodType.FullName, "priority");
			}
			priorityField.SetValue(harmonyMethod, HarmonyPriorityLast);
			arguments[0] = original;
			arguments[2] = harmonyMethod;
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
