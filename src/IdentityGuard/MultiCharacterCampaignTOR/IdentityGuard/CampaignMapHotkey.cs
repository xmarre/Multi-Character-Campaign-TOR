// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.InputSystem;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class CampaignMapHotkey
	{
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool _installed;

		private static PropertyInfo _behaviorInstance;

		private static MethodInfo _canChangeIdentity;

		private static MethodInfo _uiMessage;

		private static MethodInfo _logInfo;

		private static PropertyInfo _campaignCurrent;

		private static PropertyInfo _currentMenuContext;

		private static PropertyInfo _gameCurrent;

		private static PropertyInfo _gameStateManager;

		private static PropertyInfo _activeState;

		private static bool _partyScreenSelectionActive;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				ResolveMembers();
				Type type = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(type, "xmarre.multicharactercampaign.tor.campaignmaphotkey.v140");
				MethodInfo original = RequireType("MultiCharacterCampaignTOR.SubModule, MultiCharacterCampaignTOR").GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "OnApplicationTick" && m.GetParameters().Length == 1);
				MethodInfo method = typeof(CampaignMapHotkey).GetMethod("AfterApplicationTick", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				Patch(harmony, type, harmonyMethodType, original, null, method);
				MethodInfo method2 = RequireType("MultiCharacterCampaignTOR.PartyScreenSelectionBridge, MultiCharacterCampaignTOR").GetMethod("OnApplicationTick", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method3 = typeof(CampaignMapHotkey).GetMethod("BeforePartyScreenSelectionTick", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				Patch(harmony, type, harmonyMethodType, method2, method3, null);
				ConstructorInfo constructor = RequireType("TaleWorlds.CampaignSystem.GameState.PartyState, TaleWorlds.CampaignSystem").GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				MethodInfo method4 = typeof(CampaignMapHotkey).GetMethod("AfterPartyStateCreated", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				Patch(harmony, type, harmonyMethodType, constructor, null, method4);
				_installed = true;
				Log("Installed Ctrl+R campaign-map manager shortcut.");
			}
			catch (Exception ex)
			{
				Log("Campaign-map shortcut installation failed safely: " + Unwrap(ex));
			}
		}

		private static void AfterPartyStateCreated()
		{
			_partyScreenSelectionActive = true;
		}

		private static bool BeforePartyScreenSelectionTick()
		{
			if (!_partyScreenSelectionActive)
			{
				return false;
			}
			object value = _gameCurrent.GetValue(null, null);
			object obj = ((value == null) ? null : _gameStateManager.GetValue(value, null));
			object obj2 = ((obj == null) ? null : _activeState.GetValue(obj, null));
			if (obj2 == null || obj2.GetType().FullName != "TaleWorlds.CampaignSystem.GameState.PartyState")
			{
				_partyScreenSelectionActive = false;
				return false;
			}
			return true;
		}

		private static void ResolveMembers()
		{
			Type type = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");
			_behaviorInstance = RequireProperty(type, "Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_canChangeIdentity = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "CanChangeCampaignIdentity" && m.GetParameters().Length == 2);
			_uiMessage = RequireType("MultiCharacterCampaignTOR.UI, MultiCharacterCampaignTOR").GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "Message" && m.GetParameters().Length == 1);
			_logInfo = RequireType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR").GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "Info" && m.GetParameters().Length == 1);
			Type type2 = RequireType("TaleWorlds.Core.Game, TaleWorlds.Core");
			Type type3 = RequireType("TaleWorlds.CampaignSystem.Campaign, TaleWorlds.CampaignSystem");
			_campaignCurrent = RequireProperty(type3, "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_currentMenuContext = RequireProperty(type3, "CurrentMenuContext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_gameCurrent = RequireProperty(type2, "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_gameStateManager = RequireProperty(type2, "GameStateManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_activeState = RequireProperty(_gameStateManager.PropertyType, "ActiveState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		private static void AfterApplicationTick()
		{
			try
			{
				object value = _gameCurrent.GetValue(null, null);
				object obj = ((value == null) ? null : _gameStateManager.GetValue(value, null));
				object obj2 = ((obj == null) ? null : _activeState.GetValue(obj, null));
				if (obj2 == null || obj2.GetType().FullName != "TaleWorlds.CampaignSystem.GameState.MapState" || GetBool(obj2, "AtMenu") || GetBool(obj2, "MapConversationActive") || GetBool(obj2, "IsSimulationActive"))
				{
					return;
				}
				object value2 = _campaignCurrent.GetValue(null, null);
				if (value2 == null || _currentMenuContext.GetValue(value2, null) != null || !Input.IsKeyPressed(InputKey.R) || (!Input.IsKeyDown(InputKey.LeftControl) && !Input.IsKeyDown(InputKey.RightControl)))
				{
					return;
				}
				object value3 = _behaviorInstance.GetValue(null, null);
				if (value3 != null)
				{
					object[] array = new object[2]
					{
						string.Empty,
						false
					};
					if (!Convert.ToBoolean(_canChangeIdentity.Invoke(value3, array)))
					{
						_uiMessage.Invoke(null, new object[1] { (array[0] as string) ?? "The shared-character manager is unavailable right now." });
					}
					else
					{
						GameMenu.ActivateGameMenu("multi_character_campaign_tor");
						Log("Opened shared-character manager from campaign map with Ctrl+R.");
					}
				}
			}
			catch (Exception ex)
			{
				Log("Ctrl+R campaign-map shortcut failed safely: " + Unwrap(ex));
			}
		}

		private static bool GetBool(object value, string propertyName)
		{
			PropertyInfo property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null)
			{
				return Convert.ToBoolean(property.GetValue(value, null));
			}
			return false;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix)
		{
			if (original == null)
			{
				throw new ArgumentNullException("original");
			}
			MethodInfo methodInfo = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "Patch" && m.GetParameters().Length >= 4);
			object[] array = new object[methodInfo.GetParameters().Length];
			array[0] = original;
			array[1] = ((prefix == null) ? null : Activator.CreateInstance(harmonyMethodType, prefix));
			array[2] = ((postfix == null) ? null : Activator.CreateInstance(harmonyMethodType, postfix));
			methodInfo.Invoke(harmony, array);
		}

		private static Type RequireType(string name)
		{
			Type type = Type.GetType(name, throwOnError: false);
			if (type == null)
			{
				throw new TypeLoadException(name);
			}
			return type;
		}

		private static PropertyInfo RequireProperty(Type type, string name, BindingFlags flags)
		{
			PropertyInfo property = type.GetProperty(name, flags);
			if (property == null)
			{
				throw new MissingMemberException(type.FullName, name);
			}
			return property;
		}

		private static string Unwrap(Exception ex)
		{
			while (ex is TargetInvocationException && ex.InnerException != null)
			{
				ex = ex.InnerException;
			}
			return ex.GetType().Name + ": " + ex.Message;
		}

		private static void Log(string message)
		{
			try
			{
				_logInfo?.Invoke(null, new object[1] { "[CampaignMapHotkey] " + message });
			}
			catch
			{
			}
		}
	}
}
