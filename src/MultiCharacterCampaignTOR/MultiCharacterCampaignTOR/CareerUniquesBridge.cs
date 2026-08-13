// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR
{
	internal static class CareerUniquesBridge
	{
		private static bool _statusLogged;

		private static DateTime _lastRefresh = DateTime.MinValue;

		public static void LogStatus()
		{
			if (_statusLogged)
			{
				return;
			}
			try
			{
				Type harmonyType = typeof(Harmony);
				Type harmonyMethodType = typeof(HarmonyMethod);
				object harmony = new Harmony("xmarre.multicharactercampaign.tor");
				Type abilityManagerType = Type.GetType("TOR_Core.AbilitySystem.AbilityManagerMissionLogic, TOR_Core");
				if (abilityManagerType == null)
				{
					Log.Warning("TOR AbilityManagerMissionLogic was not found; active-equipment refresh hook was not installed.");
					_statusLogged = true;
					return;
				}
				MethodInfo method = abilityManagerType.GetMethod("OnBehaviorInitialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method2 = typeof(TORBridge).GetMethod("RefreshAfterSwitch", BindingFlags.Static | BindingFlags.Public);
				if (method == null || method2 == null)
				{
					Log.Warning("TOR active-equipment refresh patch surface was not found; refresh hook was not installed.");
					_statusLogged = true;
					return;
				}
				HarmonyBridge.PatchPrefix(harmony, harmonyType, harmonyMethodType, method, method2);
				_statusLogged = true;
				Log.Info("Installed TOR active-equipment refresh hook.");
			}
			catch (Exception ex)
			{
				Log.Error("Could not install TOR active-equipment refresh hook; campaign startup will continue without it", ex);
			}
		}

		public static void RefreshAfterSwitch()
		{
			if ((DateTime.UtcNow - _lastRefresh).TotalSeconds < 1.0)
			{
				return;
			}
			_lastRefresh = DateTime.UtcNow;
			try
			{
				Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "TORCareerUniques");
				if (assembly == null)
				{
					return;
				}
				string[] safeNames = new string[4] { "RefreshForMainHero", "RefreshSetBonuses", "UpdateEquippedSetEffects", "ForceRefreshSetBonuses" };
				foreach (Type item in SafeGetTypes(assembly))
				{
					if (!(item == null) && (item.Name.IndexOf("SetItem", StringComparison.OrdinalIgnoreCase) >= 0 || item.Name.IndexOf("CareerUnique", StringComparison.OrdinalIgnoreCase) >= 0))
					{
						MethodInfo methodInfo = item.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => safeNames.Contains(m.Name) && m.GetParameters().Length == 0 && m.ReturnType == typeof(void));
						if (methodInfo != null)
						{
							methodInfo.Invoke(null, null);
							Log.Info("Requested Career Uniques active-equipment refresh through " + item.FullName + "." + methodInfo.Name + ".");
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Career Uniques refresh bridge failed safely", ex);
			}
		}

		private static IEnumerable<Type> SafeGetTypes(Assembly asm)
		{
			try
			{
				return asm.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				return ex.Types.Where((Type t) => t != null);
			}
		}
	}
}
