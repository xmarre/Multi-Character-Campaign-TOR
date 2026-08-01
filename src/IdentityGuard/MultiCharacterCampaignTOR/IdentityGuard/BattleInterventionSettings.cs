using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleInterventionSettings
	{
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private const string SaveKey = "tor_shared_campaign_alert_predicted_losses_only";
		private const string MenuId = "multi_character_campaign_tor";

		private static bool _installed;
		private static Campaign _campaign;
		private static bool _predictedLossesOnly;

		internal static bool PredictedLossesOnly
		{
			get
			{
				EnsureCampaignState();
				return _predictedLossesOnly;
			}
		}

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				Type behaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");
				MethodInfo syncData = behaviorType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "SyncData" && method.GetParameters().Length == 1);
				MethodInfo registerMenus = behaviorType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "RegisterMenus" && method.GetParameters().Length == 1);

				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.battleinterventionsettings.v120");
				Patch(harmony, harmonyType, harmonyMethodType, syncData, null, GetPatchMethod("AfterSyncData"));
				Patch(harmony, harmonyType, harmonyMethodType, registerMenus, null, GetPatchMethod("AfterRegisterMenus"));

				_installed = true;
				RemotePartySwitch.Info("[BattleInterventionSettings v1.2.0] Installed persistent always-alert/predicted-loss-only policy controls.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionSettings] Installation failed", Unwrap(ex));
			}
		}

		private static void AfterSyncData(IDataStore dataStore)
		{
			try
			{
				EnsureCampaignState();
				dataStore.SyncData(SaveKey, ref _predictedLossesOnly);
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionSettings] Could not synchronize the alert policy", Unwrap(ex));
			}
		}

		private static void AfterRegisterMenus(CampaignGameStarter starter)
		{
			try
			{
				starter.AddGameMenuOption(MenuId, "mcc_tor_alerts_loss_only", "Battle alerts: notify only for predicted losses", ShowEnableLossOnly, EnableLossOnly, false, 7);
				starter.AddGameMenuOption(MenuId, "mcc_tor_alerts_always", "Battle alerts: notify for every eligible battle", ShowEnableAlways, EnableAlways, false, 7);
				RemotePartySwitch.Info("[BattleInterventionSettings] Registered battle-alert policy menu options.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionSettings] Could not register battle-alert policy menu options", Unwrap(ex));
			}
		}

		private static bool ShowEnableLossOnly(MenuCallbackArgs args)
		{
			EnsureCampaignState();
			args.IsEnabled = true;
			args.optionLeaveType = GameMenuOption.LeaveType.Manage;
			return !_predictedLossesOnly;
		}

		private static bool ShowEnableAlways(MenuCallbackArgs args)
		{
			EnsureCampaignState();
			args.IsEnabled = true;
			args.optionLeaveType = GameMenuOption.LeaveType.Manage;
			return _predictedLossesOnly;
		}

		private static void EnableLossOnly(MenuCallbackArgs args)
		{
			EnsureCampaignState();
			_predictedLossesOnly = true;
			RemotePartySwitch.Info("[BattleInterventionSettings] Alert policy changed to predicted losses only.");
			RemotePartySwitch.Notify("Shared-character battle alerts will now appear only when Bannerlord's current side-strength estimate predicts a loss. Troop details remain available in every displayed alert.");
			GameMenu.SwitchToMenu(MenuId);
		}

		private static void EnableAlways(MenuCallbackArgs args)
		{
			EnsureCampaignState();
			_predictedLossesOnly = false;
			RemotePartySwitch.Info("[BattleInterventionSettings] Alert policy changed to every eligible battle.");
			RemotePartySwitch.Notify("Shared-character battle alerts will now appear for every eligible battle.");
			GameMenu.SwitchToMenu(MenuId);
		}

		private static void EnsureCampaignState()
		{
			Campaign current = Campaign.Current;
			if (!object.ReferenceEquals(_campaign, current))
			{
				_campaign = current;
				_predictedLossesOnly = false;
			}
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			MethodInfo method = typeof(BattleInterventionSettings).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(BattleInterventionSettings).FullName, name);
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
