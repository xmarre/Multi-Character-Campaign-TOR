// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.WaywatcherFix
{
	public static class RuntimeRepair
	{
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool _installed;

		private static bool _loggedIdentityRepair;

		private static bool _loggedCharacterStateRepair;

		private static bool _loggedInactiveClassificationRepair;

		public static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				Type type = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
				Type type2 = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", throwOnError: false);
				Type type3 = Type.GetType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR", throwOnError: false);
				if (type == null || type2 == null || type3 == null)
				{
					return;
				}
				object harmony = Activator.CreateInstance(type, "xmarre.multicharactercampaign.tor.runtimefix.v132");
				MethodInfo method = typeof(RuntimeRepair).GetMethod("AfterInitializeState", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method2 = typeof(RuntimeRepair).GetMethod("AfterIdentityRebind", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo original = type3.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "InitializeState" && m.GetParameters().Length == 1);
				MethodInfo original2 = type3.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "RebindMainPartyIdentity" && m.GetParameters().Length == 1);
				PatchPostfix(harmony, type, type2, original, method);
				PatchPostfix(harmony, type, type2, original2, method2);
				Type type4 = Type.GetType("TaleWorlds.CampaignSystem.GameState.CharacterDeveloperState, TaleWorlds.CampaignSystem", throwOnError: false);
				if (type4 != null)
				{
					MethodInfo method3 = typeof(RuntimeRepair).GetMethod("AfterCharacterDeveloperStateCreated", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					ConstructorInfo[] constructors = type4.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (ConstructorInfo constructorInfo in constructors)
					{
						ParameterInfo[] parameters = constructorInfo.GetParameters();
						if (parameters.Length == 0 || (parameters.Length == 1 && parameters[0].ParameterType.FullName == "TaleWorlds.CampaignSystem.Hero"))
						{
							PatchPostfix(harmony, type, type2, constructorInfo, method3);
						}
					}
				}
				_installed = true;
				Log("Installed campaign identity finalization, inactive-companion rollback repair, and Character-screen initial-selection repair.");
			}
			catch (Exception ex)
			{
				Log("Failed to install campaign player-identity repair: " + Unwrap(ex));
			}
		}

		public static IEnumerable<CodeInstruction> TranspileDefectionTrustRelation(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null)
			{
				yield break;
			}
			MethodInfo replacement = null;
			try
			{
				Type type = Type.GetType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR", throwOnError: false);
				if (type != null)
				{
					replacement = type.GetMethod("GetBestRegisteredRelationForDefection", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				}
			}
			catch (Exception ex)
			{
				Log("Could not resolve lord-recruitment relation replacement; leaving native relation calls unchanged: " + Unwrap(ex));
			}
			bool warnedMissingReplacement = false;
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction != null)
				{
					MethodInfo methodInfo = instruction.operand as MethodInfo;
					if (methodInfo != null && methodInfo.Name == "GetUnmodifiedClanLeaderRelationshipWithPlayer")
					{
						if (replacement != null)
						{
							instruction.opcode = OpCodes.Call;
							instruction.operand = replacement;
						}
						else if (!warnedMissingReplacement)
						{
							warnedMissingReplacement = true;
							Log("Lord-recruitment relation replacement method was unavailable; preserved the original relation call safely.");
						}
					}
				}
				yield return instruction;
			}
		}

		public static void RefreshAfterSwitch()
		{
			try
			{
				EnsurePlayerTroopMatchesMainHero();
				Type type = Type.GetType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem", throwOnError: false);
				Type type2 = Type.GetType("TOR_Core.Extensions.HeroExtensions, TOR_Core", throwOnError: false);
				if (type == null || type2 == null)
				{
					return;
				}
				object propertyValue = GetPropertyValue(type, "MainHero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null);
				if (propertyValue == null || FindUniqueMethod(type2, "GetCareer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 1).Invoke(null, new object[1] { propertyValue }) == null)
				{
					return;
				}
				if (!Convert.ToBoolean(FindUniqueMethod(type2, "HasAttribute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 2).Invoke(null, new object[2] { propertyValue, "AbilityUser" })))
				{
					FindUniqueMethod(type2, "AddAttribute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 2).Invoke(null, new object[2] { propertyValue, "AbilityUser" });
				}
				Type type3 = Type.GetType("TOR_Core.CharacterDevelopment.CareerSystem.CareerHelper, TOR_Core", throwOnError: false);
				if (type3 != null)
				{
					MethodInfo methodInfo = type3.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "RefreshCareerChoicesCache" && m.GetParameters().Length == 0);
					if (methodInfo != null)
					{
						methodInfo.Invoke(null, null);
					}
				}
			}
			catch (Exception ex)
			{
				Log("TOR cache refresh after player switch failed safely: " + Unwrap(ex));
			}
		}

		private static void AfterInitializeState(object __instance)
		{
			try
			{
				EnsurePlayerTroopMatchesMainHero();
				RepairInactiveOriginalCompanions(__instance);
			}
			catch (Exception ex)
			{
				Log("Campaign initialization repair failed safely: " + Unwrap(ex));
			}
		}

		private static void AfterIdentityRebind()
		{
			try
			{
				EnsurePlayerTroopMatchesMainHero();
			}
			catch (Exception ex)
			{
				Log("Campaign player-identity repair failed safely: " + Unwrap(ex));
			}
		}

		private static void AfterCharacterDeveloperStateCreated(object __instance)
		{
			try
			{
				if (__instance == null)
				{
					return;
				}
				Type type = Type.GetType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR", throwOnError: false);
				Type type2 = Type.GetType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem", throwOnError: false);
				if (type == null || type2 == null)
				{
					return;
				}
				PropertyInfo property = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				object obj = ((!(property == null)) ? property.GetValue(null, null) : null);
				object propertyValue = GetPropertyValue(type2, "MainHero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null);
				if (obj == null || propertyValue == null)
				{
					return;
				}
				MethodInfo methodInfo = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "IsRegisteredSharedHero" && m.GetParameters().Length == 1);
				if (methodInfo == null || !Convert.ToBoolean(methodInfo.Invoke(obj, new object[1] { propertyValue })))
				{
					return;
				}
				PropertyInfo property2 = __instance.GetType().GetProperty("InitialSelectedHero", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property2 == null || !property2.CanRead || !property2.CanWrite)
				{
					return;
				}
				object value = property2.GetValue(__instance, null);
				if (value == propertyValue)
				{
					return;
				}
				FieldInfo field = type.GetField("_founderHeroId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				string text = ((!(field == null)) ? (field.GetValue(obj) as string) : null);
				if (value != null)
				{
					string heroId = GetHeroId(value);
					if (string.IsNullOrEmpty(text) || !string.Equals(heroId, text, StringComparison.Ordinal))
					{
						return;
					}
				}
				property2.SetValue(__instance, propertyValue, null);
				if (!_loggedCharacterStateRepair)
				{
					_loggedCharacterStateRepair = true;
					Log("CharacterDeveloperState initial selection rebound from the stale founder/default to the active registered shared hero.");
				}
			}
			catch (Exception ex)
			{
				Log("Character-screen initial-selection repair failed safely: " + Unwrap(ex));
			}
		}

		private static void RepairInactiveOriginalCompanions(object behavior)
		{
			if (behavior == null)
			{
				return;
			}
			Type type = Type.GetType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR", throwOnError: false) ?? behavior.GetType();
			FieldInfo field = type.GetField("_originalCompanionIds", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (!(((!(field == null)) ? field.GetValue(behavior) : null) is IEnumerable enumerable))
			{
				return;
			}
			Type type2 = Type.GetType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem", throwOnError: false);
			if (type2 == null)
			{
				return;
			}
			object propertyValue = GetPropertyValue(type2, "MainHero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null);
			MethodInfo methodInfo = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "ResolveHero" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
			MethodInfo methodInfo2 = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "RestoreInactiveCompanion" && m.GetParameters().Length == 1);
			if (methodInfo == null || methodInfo2 == null)
			{
				return;
			}
			int num = 0;
			foreach (object item in enumerable)
			{
				string text = item as string;
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}
				object obj = methodInfo.Invoke(null, new object[1] { text });
				if (obj != null && obj != propertyValue)
				{
					PropertyInfo property = obj.GetType().GetProperty("Clan", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					PropertyInfo property2 = obj.GetType().GetProperty("CompanionOf", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					object obj2 = ((!(property == null)) ? property.GetValue(obj, null) : null);
					object obj3 = ((!(property2 == null)) ? property2.GetValue(obj, null) : null);
					methodInfo2.Invoke(behavior, new object[1] { obj });
					object obj4 = ((!(property == null)) ? property.GetValue(obj, null) : null);
					object obj5 = ((!(property2 == null)) ? property2.GetValue(obj, null) : null);
					if (obj2 != obj4 || obj3 != obj5)
					{
						num++;
					}
				}
			}
			if (num > 0 && !_loggedInactiveClassificationRepair)
			{
				_loggedInactiveClassificationRepair = true;
				Log("Restored inactive companion classification for " + num + " registered converted companion(s) left dirty by an interrupted earlier switch.");
			}
		}

		private static string GetHeroId(object hero)
		{
			if (hero == null)
			{
				return string.Empty;
			}
			PropertyInfo property = hero.GetType().GetProperty("StringId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null)
			{
				string text = property.GetValue(hero, null) as string;
				if (!string.IsNullOrEmpty(text))
				{
					return text;
				}
			}
			PropertyInfo property2 = hero.GetType().GetProperty("CharacterObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object obj = ((!(property2 == null)) ? property2.GetValue(hero, null) : null);
			if (obj != null)
			{
				PropertyInfo property3 = obj.GetType().GetProperty("StringId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property3 != null)
				{
					return (property3.GetValue(obj, null) as string) ?? string.Empty;
				}
			}
			return string.Empty;
		}

		private static void EnsurePlayerTroopMatchesMainHero()
		{
			Type type = Type.GetType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem", throwOnError: false);
			Type type2 = Type.GetType("TaleWorlds.Core.Game, TaleWorlds.Core", throwOnError: false);
			if (type == null || type2 == null)
			{
				return;
			}
			object propertyValue = GetPropertyValue(type, "MainHero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null);
			object propertyValue2 = GetPropertyValue(type2, "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null);
			if (propertyValue == null || propertyValue2 == null)
			{
				return;
			}
			object propertyValue3 = GetPropertyValue(type, "CharacterObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, propertyValue);
			if (propertyValue3 == null)
			{
				return;
			}
			PropertyInfo property = type2.GetProperty("PlayerTroop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property == null || !property.CanRead || !property.CanWrite)
			{
				throw new MissingMemberException(type2.FullName, "PlayerTroop");
			}
			if (property.GetValue(propertyValue2, null) != propertyValue3)
			{
				property.SetValue(propertyValue2, propertyValue3, null);
				if (property.GetValue(propertyValue2, null) != propertyValue3)
				{
					throw new InvalidOperationException("Game.PlayerTroop did not remain bound to Hero.MainHero.CharacterObject.");
				}
				if (GetPropertyValue(type, "MainHero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null) != propertyValue)
				{
					throw new InvalidOperationException("Game.PlayerTroop repair changed Hero.MainHero unexpectedly.");
				}
				if (!_loggedIdentityRepair)
				{
					_loggedIdentityRepair = true;
					Log("Rebound Game.PlayerTroop to the active shared Hero.MainHero for character-screen and progression UI consistency.");
				}
			}
		}

		private static object GetPropertyValue(Type type, string name, BindingFlags flags, object target)
		{
			PropertyInfo property = type.GetProperty(name, flags);
			if (property == null)
			{
				throw new MissingMemberException(type.FullName, name);
			}
			return property.GetValue(target, null);
		}

		private static MethodInfo FindUniqueMethod(Type type, string name, BindingFlags flags, int parameterCount)
		{
			MethodInfo[] array = (from m in type.GetMethods(flags)
				where m.Name == name && m.GetParameters().Length == parameterCount
				select m).ToArray();
			if (array.Length != 1)
			{
				throw new MissingMethodException(type.FullName, name + " with " + parameterCount + " parameter(s); matches=" + array.Length);
			}
			return array[0];
		}

		private static void PatchPostfix(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo postfix)
		{
			object obj = Activator.CreateInstance(harmonyMethodType, postfix);
			MethodInfo methodInfo = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "Patch" && m.GetParameters().Length >= 4);
			object[] array = new object[methodInfo.GetParameters().Length];
			array[0] = original;
			array[2] = obj;
			methodInfo.Invoke(harmony, array);
		}

		private static string Unwrap(Exception ex)
		{
			while (ex is TargetInvocationException && ex.InnerException != null)
			{
				ex = ex.InnerException;
			}
			return ex.ToString();
		}

		private static void Log(string message)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord", "ModLogs");
				Directory.CreateDirectory(text);
				File.AppendAllText(Path.Combine(text, "MultiCharacterCampaignTOR.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [RuntimeFix132] " + message + Environment.NewLine);
			}
			catch
			{
			}
		}
	}
}
