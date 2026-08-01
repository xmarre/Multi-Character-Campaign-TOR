// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MultiCharacterCampaignTOR.WaywatcherFix;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace MultiCharacterCampaignTOR
{
	internal static class HarmonyBridge
	{
		private static bool _installed;

		public static void TryInstall()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				Type type = Type.GetType("HarmonyLib.Harmony, 0Harmony");
				Type type2 = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony");
				if (type == null || type2 == null)
				{
					Log.Info("Harmony was not available yet; succession and campaign-shared quest patches will retry at campaign launch.");
					return;
				}
				object harmony = Activator.CreateInstance(type, "xmarre.multicharactercampaign.tor");
				MethodInfo method = typeof(Campaign).GetMethod("OnGameOver", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method2 = typeof(HarmonyBridge).GetMethod("BeforeCampaignGameOver", BindingFlags.Static | BindingFlags.Public);
				PatchPrefix(harmony, type, type2, method, method2);
				Log.Info("Installed Campaign.OnGameOver succession guard.");
				Type type3 = Type.GetType("TaleWorlds.CampaignSystem.QuestManager, TaleWorlds.CampaignSystem");
				MethodInfo methodInfo = ((!(type3 == null)) ? type3.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "OnPlayerCharacterChanged" && m.GetParameters().Length == 4) : null);
				MethodInfo method3 = typeof(HarmonyBridge).GetMethod("BeforeQuestManagerPlayerCharacterChanged", BindingFlags.Static | BindingFlags.Public);
				if (methodInfo != null)
				{
					PatchPrefix(harmony, type, type2, methodInfo, method3);
					Log.Info("Installed QuestManager.OnPlayerCharacterChanged guard so shared-character switches do not cancel campaign quests.");
				}
				else
				{
					Log.Warning("QuestManager.OnPlayerCharacterChanged was not found; quest-preservation patch was not installed.");
				}
				Type type4 = Type.GetType("TOR_Core.CharacterDevelopment.CareerSystem.CareerScreenVM, TOR_Core");
				MethodInfo methodInfo2 = ((!(type4 == null)) ? type4.GetMethod("OpenBattlePrayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) : null);
				MethodInfo method4 = typeof(HarmonyBridge).GetMethod("BeforeOpenBattlePrayers", BindingFlags.Static | BindingFlags.Public);
				if (methodInfo2 != null)
				{
					PatchPrefix(harmony, type, type2, methodInfo2, method4);
					Log.Info("Installed CareerScreenVM.OpenBattlePrayers compatibility guard.");
				}
				else
				{
					Log.Warning("CareerScreenVM.OpenBattlePrayers was not found; prayer compatibility guard was not installed.");
				}
				Type type5 = Type.GetType("TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel, TaleWorlds.CampaignSystem");
				MethodInfo methodInfo3 = ((!(type5 == null)) ? type5.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "AddIncomeFromParty" && m.GetParameters().Length == 4 && m.ReturnType == typeof(int)) : null);
				MethodInfo method5 = typeof(HarmonyBridge).GetMethod("BeforeDefaultClanFinanceAddIncomeFromParty", BindingFlags.Static | BindingFlags.Public);
				if (methodInfo3 != null)
				{
					PatchPrefix(harmony, type, type2, methodInfo3, method5);
					Log.Info("Installed player-main-party finance exclusion patch.");
				}
				else
				{
					Log.Warning("DefaultClanFinanceModel.AddIncomeFromParty was not found; main-party finance exclusion was not installed.");
				}
				Type type6 = Type.GetType("TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel, TaleWorlds.CampaignSystem");
				MethodInfo methodInfo4 = ((!(type6 == null)) ? type6.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "CalculateBaseMemberSize" && m.GetParameters().Length == 4 && m.ReturnType == typeof(void)) : null);
				MethodInfo method6 = typeof(HarmonyBridge).GetMethod("AfterDefaultPartySizeCalculateBaseMemberSize", BindingFlags.Static | BindingFlags.Public);
				if (methodInfo4 != null)
				{
					PatchPostfix(harmony, type, type2, methodInfo4, method6);
					Log.Info("Installed shared-character main-party administrative size-limit correction.");
				}
				else
				{
					Log.Warning("DefaultPartySizeLimitModel.CalculateBaseMemberSize was not found; administrative party-size correction was not installed.");
				}
				Type type7 = Type.GetType("TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel, TaleWorlds.CampaignSystem");
				MethodInfo methodInfo5 = ((!(type7 == null)) ? type7.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "GetCompanionLimit" && m.GetParameters().Length == 1 && m.ReturnType == typeof(int)) : null);
				MethodInfo method7 = typeof(HarmonyBridge).GetMethod("AfterDefaultClanTierGetCompanionLimit", BindingFlags.Static | BindingFlags.Public);
				if (methodInfo5 != null)
				{
					PatchPostfix(harmony, type, type2, methodInfo5, method7);
					Log.Info("Installed UnlimitedCAP shared-character companion-limit bridge.");
				}
				else
				{
					Log.Warning("DefaultClanTierModel.GetCompanionLimit was not found; UnlimitedCAP companion-limit bridge was not installed.");
				}
				MethodInfo methodInfo6 = ((!(type7 == null)) ? type7.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "GetPartyLimitForTier" && m.GetParameters().Length == 2 && m.ReturnType == typeof(int)) : null);
				MethodInfo method8 = typeof(HarmonyBridge).GetMethod("AfterDefaultClanTierGetPartyLimitForTier", BindingFlags.Static | BindingFlags.Public);
				if (methodInfo6 != null)
				{
					PatchPostfix(harmony, type, type2, methodInfo6, method8);
					Log.Info("Installed UnlimitedCAP shared-character party-limit bridge.");
				}
				else
				{
					Log.Warning("DefaultClanTierModel.GetPartyLimitForTier was not found; UnlimitedCAP party-limit bridge was not installed.");
				}
				Type type8 = Type.GetType("TaleWorlds.CampaignSystem.CampaignBehaviors.LordDefectionCampaignBehavior, TaleWorlds.CampaignSystem");
				if ((object)type8 != null)
				{
					MethodInfo method9 = type8.GetMethod("GetPersuasionTasksForDefection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					MethodInfo method10 = typeof(HarmonyBridge).GetMethod("TranspileDefectionTrustRelation", BindingFlags.Static | BindingFlags.Public);
					if ((object)method9 != null)
					{
						PatchTranspiler(harmony, type, type2, method9, method10);
						Log.Info("Installed shared-character best-relation bridge for lord-recruitment trust.");
						goto IL_0486;
					}
				}
				Log.Warning("LordDefectionCampaignBehavior.GetPersuasionTasksForDefection was not found; shared-character recruitment relation bridge was not installed.");
				goto IL_0486;
				IL_0486:
				_installed = true;
			}
			catch (Exception ex)
			{
				Log.Error("Could not install shared-campaign Harmony patches", ex);
			}
		}

		internal static void PatchPrefix(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefixMethod)
		{
			if (original == null)
			{
				throw new ArgumentNullException("original");
			}
			if (prefixMethod == null)
			{
				throw new ArgumentNullException("prefixMethod");
			}
			object obj = Activator.CreateInstance(harmonyMethodType, prefixMethod);
			MethodInfo methodInfo = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "Patch" && m.GetParameters().Length >= 2);
			ParameterInfo[] parameters = methodInfo.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = original;
			array[1] = obj;
			for (int num = 2; num < array.Length; num++)
			{
				array[num] = null;
			}
			methodInfo.Invoke(harmony, array);
		}

		private static void PatchPostfix(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo postfixMethod)
		{
			if (original == null)
			{
				throw new ArgumentNullException("original");
			}
			if (postfixMethod == null)
			{
				throw new ArgumentNullException("postfixMethod");
			}
			object obj = Activator.CreateInstance(harmonyMethodType, postfixMethod);
			MethodInfo methodInfo = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "Patch" && m.GetParameters().Length >= 3);
			ParameterInfo[] parameters = methodInfo.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = original;
			array[1] = null;
			array[2] = obj;
			for (int num = 3; num < array.Length; num++)
			{
				array[num] = null;
			}
			methodInfo.Invoke(harmony, array);
		}

		public static bool BeforeDefaultClanFinanceAddIncomeFromParty(MobileParty party, Clan clan, ref int __result)
		{
			if (!FinanceCompatibilityBridge.ShouldSkipMainPartyIncome(party, clan))
			{
				return true;
			}
			__result = 0;
			return false;
		}

		public static void AfterDefaultPartySizeCalculateBaseMemberSize(Hero __0, Clan __2, ref ExplainedNumber __3)
		{
			PartySizeCompatibilityBridge.ApplyAdministrativeMainPartyBonuses(__0, __2, ref __3);
		}

		public static void AfterDefaultClanTierGetCompanionLimit(Clan clan, ref int __result)
		{
			UnlimitedCapBridge.ApplyCompanionLimit(clan, ref __result);
		}

		public static void AfterDefaultClanTierGetPartyLimitForTier(Clan clan, int clanTierToCheck, ref int __result)
		{
			UnlimitedCapBridge.ApplyPartyLimit(clan, clanTierToCheck, ref __result);
		}

		public static bool BeforeQuestManagerPlayerCharacterChanged()
		{
			try
			{
				MultiCharacterCampaignBehavior instance = MultiCharacterCampaignBehavior.Instance;
				if (instance != null && instance.IsIdentitySwitchInProgress)
				{
					Log.Info("Skipped QuestManager player-character-change cancellation during a shared-character switch; campaign quests remain active and shared.");
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Quest-preservation prefix failed", ex);
			}
			return true;
		}

		public static bool BeforeOpenBattlePrayers()
		{
			try
			{
				if (TORBridge.CanOpenBattlePrayers(Hero.MainHero, out var reason))
				{
					return true;
				}
				Log.Warning("Blocked incompatible TOR battle-prayer screen for hero=" + Reflection.IdOf(Hero.MainHero) + ". Reason=" + reason);
				UI.Message("Prayers cannot be opened for this character: " + reason + " Use Multi-Character Campaign - TOR > Review active companion career to repair the assignment.");
				return false;
			}
			catch (Exception ex)
			{
				Log.Error("Battle-prayer guard failed safely", ex);
				UI.Message("Prayers could not be opened safely. See MultiCharacterCampaignTOR.log.");
				return false;
			}
		}

		public static bool BeforeCampaignGameOver()
		{
			try
			{
				MultiCharacterCampaignBehavior instance = MultiCharacterCampaignBehavior.Instance;
				if (instance != null && instance.ShouldSuppressGameOver())
				{
					Log.Info("Suppressed native game over after successful shared-character succession.");
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Game-over prefix failed", ex);
			}
			return true;
		}

		public static IEnumerable<CodeInstruction> TranspileDefectionTrustRelation(IEnumerable<CodeInstruction> instructions)
		{
			return RuntimeRepair.TranspileDefectionTrustRelation(instructions);
		}

		private static void PatchTranspiler(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo postfixMethod)
		{
			if (original == null)
			{
				throw new ArgumentNullException("original");
			}
			if (postfixMethod == null)
			{
				throw new ArgumentNullException("postfixMethod");
			}
			object obj = Activator.CreateInstance(harmonyMethodType, postfixMethod);
			MethodInfo methodInfo = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "Patch" && m.GetParameters().Length >= 3);
			ParameterInfo[] parameters = methodInfo.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = original;
			array[1] = null;
			array[3] = obj;
			for (int num = 4; num < array.Length; num++)
			{
				array[num] = null;
			}
			methodInfo.Invoke(harmony, array);
		}
	}
}
