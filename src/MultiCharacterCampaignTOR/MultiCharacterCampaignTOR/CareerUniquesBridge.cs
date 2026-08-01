// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MultiCharacterCampaignTOR
{
	internal static class CareerUniquesBridge
	{
		private static bool _statusLogged;

		private static DateTime _lastRefresh = DateTime.MinValue;

		public static void LogStatus()
		{
			if (!_statusLogged)
			{
				Type type = Type.GetType("HarmonyLib.Harmony, 0Harmony");
				Type type2 = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(type, "xmarre.multicharactercampaign.tor");
				MethodInfo method = Type.GetType("TOR_Core.AbilitySystem.AbilityManagerMissionLogic, TOR_Core").GetMethod("OnBehaviorInitialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method2 = typeof(TORBridge).GetMethod("RefreshAfterSwitch", BindingFlags.Static | BindingFlags.Public);
				HarmonyBridge.PatchPrefix(harmony, type, type2, method, method2);
				_statusLogged = true;
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
