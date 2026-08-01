using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class ManagerReturnHotfix
	{
		private const string ManagerMenuId = "tor_shared_campaign_manage";
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool _installed;
		private static bool _managerSessionActive;
		private static bool _redirecting;
		private static Campaign _campaign;
		private static string _sourceMenuId;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				MethodInfo switchToMenu = typeof(GameMenu).GetMethods(StaticFlags)
					.Single((MethodInfo method) => method.Name == "SwitchToMenu" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string));
				MethodInfo exitToLast = typeof(GameMenu).GetMethods(StaticFlags)
					.Single((MethodInfo method) => method.Name == "ExitToLast" && method.GetParameters().Length == 0);

				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.managerreturn.v121");
				Patch(harmony, harmonyType, harmonyMethodType, switchToMenu, GetPatchMethod("BeforeSwitchToMenu"));
				Patch(harmony, harmonyType, harmonyMethodType, exitToLast, GetPatchMethod("BeforeExitToLast"));

				_installed = true;
				RemotePartySwitch.Info("[ManagerReturnHotfix v1.2.1] Installed source-menu preservation for the shared-character manager.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[ManagerReturnHotfix] Installation failed", Unwrap(ex));
			}
		}

		private static void BeforeSwitchToMenu(string __0)
		{
			try
			{
				EnsureCampaignState();
				if (_redirecting || !string.Equals(__0, ManagerMenuId, StringComparison.Ordinal))
				{
					return;
				}

				if (!_managerSessionActive)
				{
					string currentMenuId = GetCurrentMenuId();
					_sourceMenuId = !string.IsNullOrEmpty(currentMenuId) && !string.Equals(currentMenuId, ManagerMenuId, StringComparison.Ordinal)
						? currentMenuId
						: null;
					_managerSessionActive = true;
					RemotePartySwitch.Info("[ManagerReturnHotfix] Manager opened; sourceMenu=" + (_sourceMenuId ?? "<campaign-map>") + ".");
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[ManagerReturnHotfix] Could not capture the manager source menu", Unwrap(ex));
			}
		}

		private static bool BeforeExitToLast()
		{
			try
			{
				EnsureCampaignState();
				if (!string.Equals(GetCurrentMenuId(), ManagerMenuId, StringComparison.Ordinal))
				{
					return true;
				}

				string sourceMenuId = _sourceMenuId;
				_managerSessionActive = false;
				_sourceMenuId = null;

				if (string.IsNullOrEmpty(sourceMenuId) || Campaign.Current == null || Campaign.Current.GameMenuManager == null || Campaign.Current.GameMenuManager.GetGameMenu(sourceMenuId) == null)
				{
					RemotePartySwitch.Info("[ManagerReturnHotfix] Manager was opened from the campaign map; retaining native ExitToLast behavior.");
					return true;
				}

				_redirecting = true;
				try
				{
					GameMenu.SwitchToMenu(sourceMenuId);
				}
				finally
				{
					_redirecting = false;
				}
				RemotePartySwitch.Info("[ManagerReturnHotfix] Returned to source settlement menu=" + sourceMenuId + " instead of exiting its native settlement lifecycle.");
				return false;
			}
			catch (Exception ex)
			{
				_managerSessionActive = false;
				_sourceMenuId = null;
				_redirecting = false;
				RemotePartySwitch.Error("[ManagerReturnHotfix] Source-menu return failed; falling back to native ExitToLast", Unwrap(ex));
				return true;
			}
		}

		private static string GetCurrentMenuId()
		{
			Campaign campaign = Campaign.Current;
			object context = campaign == null ? null : campaign.CurrentMenuContext;
			if (context == null)
			{
				return null;
			}
			PropertyInfo gameMenuProperty = context.GetType().GetProperty("GameMenu", InstanceFlags);
			object gameMenu = gameMenuProperty == null ? null : gameMenuProperty.GetValue(context, null);
			PropertyInfo stringIdProperty = gameMenu == null ? null : gameMenu.GetType().GetProperty("StringId", InstanceFlags);
			return stringIdProperty == null ? null : stringIdProperty.GetValue(gameMenu, null) as string;
		}

		private static void EnsureCampaignState()
		{
			Campaign current = Campaign.Current;
			if (object.ReferenceEquals(_campaign, current))
			{
				return;
			}
			_campaign = current;
			_managerSessionActive = false;
			_redirecting = false;
			_sourceMenuId = null;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodInfo original, MethodInfo prefix)
		{
			object harmonyPrefix = Activator.CreateInstance(harmonyMethodType, prefix);
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
			object[] arguments = new object[patch.GetParameters().Length];
			arguments[0] = original;
			arguments[1] = harmonyPrefix;
			patch.Invoke(harmony, arguments);
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			MethodInfo method = typeof(ManagerReturnHotfix).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(ManagerReturnHotfix).FullName, name);
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
