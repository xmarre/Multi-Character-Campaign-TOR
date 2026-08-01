// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace MultiCharacterCampaignTOR.SettlementPresence
{
	internal static class SettlementPresenceRepair
	{
		private const string HarmonyId = "MultiCharacterCampaignTOR.SettlementPresence.v141";

		private static bool _installed;

		private static MethodInfo _isRegisteredSharedHero;

		private static PropertyInfo _behaviorInstance;

		internal static void Install()
		{
			if (!_installed)
			{
				Type type = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
				Type type2 = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", throwOnError: false);
				if (type == null || type2 == null)
				{
					throw new InvalidOperationException("Harmony 0Harmony could not be resolved.");
				}
				MethodInfo method = typeof(DefaultHeroAgentLocationModel).GetMethod("GetLocationForHero", BindingFlags.Instance | BindingFlags.Public, null, new Type[3]
				{
					typeof(Hero),
					typeof(Settlement),
					typeof(HeroAgentLocationModel.HeroLocationDetail).MakeByRefType()
				}, null);
				if (method == null)
				{
					throw new MissingMethodException(typeof(DefaultHeroAgentLocationModel).FullName, "GetLocationForHero(Hero, Settlement, out HeroLocationDetail)");
				}
				MethodInfo method2 = typeof(SettlementPresenceRepair).GetMethod("AfterGetLocationForHero", BindingFlags.Static | BindingFlags.NonPublic);
				if (method2 == null)
				{
					throw new MissingMethodException(typeof(SettlementPresenceRepair).FullName, "AfterGetLocationForHero");
				}
				object harmony = Activator.CreateInstance(type, "MultiCharacterCampaignTOR.SettlementPresence.v141");
				object postfix = CreateHarmonyMethod(type2, method2);
				InvokePatch(type, harmony, method, postfix);
				ResolveRegistrationBridge();
				_installed = true;
				LogInfo("Settlement-presence normalization installed for registered inactive non-companion heroes.");
			}
		}

		private static object CreateHarmonyMethod(Type harmonyMethodType, MethodInfo patchMethod)
		{
			ConstructorInfo constructor = harmonyMethodType.GetConstructor(new Type[1] { typeof(MethodInfo) });
			if (constructor != null)
			{
				return constructor.Invoke(new object[1] { patchMethod });
			}
			object obj = Activator.CreateInstance(harmonyMethodType);
			FieldInfo field = harmonyMethodType.GetField("method", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				field.SetValue(obj, patchMethod);
				return obj;
			}
			PropertyInfo property = harmonyMethodType.GetProperty("method", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanWrite)
			{
				property.SetValue(obj, patchMethod, null);
				return obj;
			}
			throw new MissingMemberException(harmonyMethodType.FullName, "MethodInfo constructor/method member");
		}

		private static void InvokePatch(Type harmonyType, object harmony, MethodBase original, object postfix)
		{
			MethodInfo methodInfo = (from m in (from m in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
					where m.Name == "Patch"
					select m).Where(delegate(MethodInfo m)
				{
					ParameterInfo[] parameters2 = m.GetParameters();
					return parameters2.Length >= 3 && typeof(MethodBase).IsAssignableFrom(parameters2[0].ParameterType);
				})
				orderby m.GetParameters().Length descending
				select m).FirstOrDefault();
			if (methodInfo == null)
			{
				throw new MissingMethodException(harmonyType.FullName, "Patch");
			}
			ParameterInfo[] parameters = methodInfo.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = original;
			if (array.Length > 2)
			{
				array[2] = postfix;
			}
			methodInfo.Invoke(harmony, array);
		}

		private static void ResolveRegistrationBridge()
		{
			Type type = Type.GetType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR", throwOnError: false);
			if (!(type == null))
			{
				_behaviorInstance = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				_isRegisteredSharedHero = type.GetMethod("IsRegisteredSharedHero", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Hero) }, null);
			}
		}

		private static bool IsRegisteredSharedHero(Hero hero)
		{
			if (_behaviorInstance == null || _isRegisteredSharedHero == null)
			{
				ResolveRegistrationBridge();
			}
			if (_behaviorInstance == null || _isRegisteredSharedHero == null)
			{
				return false;
			}
			object value = _behaviorInstance.GetValue(null, null);
			if (value == null)
			{
				return false;
			}
			object obj = _isRegisteredSharedHero.Invoke(value, new object[1] { hero });
			return obj is bool && (bool)obj;
		}

		private static void AfterGetLocationForHero(Hero __0, Settlement __1, ref HeroAgentLocationModel.HeroLocationDetail __2, ref Location __result)
		{
			try
			{
				if (__0 != null && __1 != null && !object.ReferenceEquals(__0, Hero.MainHero) && __0.IsActive && __0.IsAlive && !__0.IsPrisoner && !__0.IsPlayerCompanion && object.ReferenceEquals(__0.Clan, Clan.PlayerClan) && object.ReferenceEquals(__0.CurrentSettlement, __1) && __1.IsTown && __0.PartyBelongedTo == null && !__0.IsPartyLeader && IsRegisteredSharedHero(__0) && (__result == null || __2 == HeroAgentLocationModel.HeroLocationDetail.PlayerClanMember))
				{
					Location location = ((__1.LocationComplex != null) ? __1.LocationComplex.GetLocationWithId("tavern") : null);
					if (location != null && (!object.ReferenceEquals(__result, location) || __2 != HeroAgentLocationModel.HeroLocationDetail.PlayerClanMember))
					{
						__2 = HeroAgentLocationModel.HeroLocationDetail.PlayerClanMember;
						__result = location;
						LogInfo("Normalized tavern presence for registered inactive hero " + SafeHeroName(__0) + " in " + __1.StringId + ".");
					}
				}
			}
			catch (Exception ex)
			{
				LogWarning("Settlement-presence repair skipped after exception: " + Unwrap(ex));
			}
		}

		private static string SafeHeroName(Hero hero)
		{
			try
			{
				return (!(hero.Name == null)) ? hero.Name.ToString() : hero.StringId;
			}
			catch
			{
				return (hero != null) ? hero.StringId : "<null>";
			}
		}

		private static string Unwrap(Exception ex)
		{
			Exception ex2 = ex;
			while (ex2 is TargetInvocationException && ex2.InnerException != null)
			{
				ex2 = ex2.InnerException;
			}
			return ex2.GetType().Name + ": " + ex2.Message;
		}

		private static void LogInfo(string message)
		{
			Log("Info", message);
		}

		private static void LogWarning(string message)
		{
			Log("Warning", message);
		}

		private static void Log(string methodName, string message)
		{
			try
			{
				Type type = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", throwOnError: false);
				MethodInfo methodInfo = ((!(type == null)) ? type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null);
				if (methodInfo != null)
				{
					methodInfo.Invoke(null, new object[1] { "[SettlementPresence] " + message });
				}
			}
			catch
			{
			}
		}
	}
}
