using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class SettlementCharacterSwitchMenuFix
	{
		private const string ManagerMenuId = "multi_character_campaign_tor";
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool _installed;
		private static bool _replaying;
		private static Campaign _campaign;
		private static MethodInfo _dispatchSelection;
		private static MethodInfo _cancelSelection;
		private static PropertyInfo _behaviorInstance;
		private static MethodInfo _isRegisteredSharedHero;

		private static int _pendingToken;
		private static List<InquiryElement> _pendingSelection;
		private static Hero _pendingTarget;
		private static Settlement _pendingSettlement;
		private static int _dispatchDelayTicks;
		private static int _returnDelayTicks;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				Type uiType = RequireType("MultiCharacterCampaignTOR.UI, MultiCharacterCampaignTOR");
				_dispatchSelection = uiType.GetMethod("DispatchSelection", StaticFlags, null, new Type[2]
				{
					typeof(int),
					typeof(List<InquiryElement>)
				}, null);
				_cancelSelection = uiType.GetMethod("CancelSelection", StaticFlags, null, new Type[2]
				{
					typeof(int),
					typeof(string)
				}, null);
				if (_dispatchSelection == null || _cancelSelection == null)
				{
					throw new MissingMethodException(uiType.FullName, "DispatchSelection/CancelSelection");
				}

				Type behaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");
				_behaviorInstance = behaviorType.GetProperty("Instance", StaticFlags);
				_isRegisteredSharedHero = behaviorType.GetMethod("IsRegisteredSharedHero", InstanceFlags, null, new Type[1] { typeof(Hero) }, null);
				if (_behaviorInstance == null || _isRegisteredSharedHero == null)
				{
					throw new MissingMemberException(behaviorType.FullName, "Instance/IsRegisteredSharedHero");
				}

				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.settlementswitchmenu.v131");
				Patch(harmony, harmonyType, harmonyMethodType, _dispatchSelection, GetPatchMethod("BeforeDispatchSelection"), 900);

				_installed = true;
				RemotePartySwitch.Info("[SettlementCharacterSwitchMenuFix v1.3.1] Installed deferred settlement character-switch dispatch and automatic return through the preserved settlement menu.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[SettlementCharacterSwitchMenuFix] Installation failed", Unwrap(ex));
			}
		}

		internal static void Tick()
		{
			if (!_installed)
			{
				return;
			}
			EnsureCampaignState();

			if (_pendingToken != 0)
			{
				if (_dispatchDelayTicks > 0)
				{
					_dispatchDelayTicks--;
					return;
				}
				ReplayPendingSelection();
				return;
			}

			if (_returnDelayTicks > 0)
			{
				_returnDelayTicks--;
				if (_returnDelayTicks == 0)
				{
					ReturnToSettlementAfterSwitch();
				}
			}
		}

		private static bool BeforeDispatchSelection(int __0, List<InquiryElement> __1)
		{
			if (_replaying)
			{
				return true;
			}
			try
			{
				EnsureCampaignState();
				if (_pendingToken != 0 || !string.Equals(GetCurrentMenuId(), ManagerMenuId, StringComparison.Ordinal))
				{
					return true;
				}
				MobileParty mainParty = MobileParty.MainParty;
				Settlement settlement = mainParty == null ? null : mainParty.CurrentSettlement;
				Hero target = GetSelectedHero(__1);
				if (settlement == null || target == null || target == Hero.MainHero || !IsRegisteredSharedHero(target))
				{
					return true;
				}

				_pendingToken = __0;
				_pendingSelection = __1 == null ? new List<InquiryElement>() : new List<InquiryElement>(__1);
				_pendingTarget = target;
				_pendingSettlement = settlement;
				_dispatchDelayTicks = 2;
				_returnDelayTicks = 0;
				RemotePartySwitch.Info("[SettlementCharacterSwitchMenuFix] Deferred registered-character switch until the selection inquiry completed teardown; target=" + SafeHeroId(target) + "; settlement=" + SafeSettlementId(settlement) + ".");
				return false;
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[SettlementCharacterSwitchMenuFix] Could not defer the settlement character-switch selection; allowing normal dispatch", Unwrap(ex));
				return true;
			}
		}

		private static void ReplayPendingSelection()
		{
			int token = _pendingToken;
			List<InquiryElement> selection = _pendingSelection;
			Hero target = _pendingTarget;
			Settlement settlement = _pendingSettlement;
			ClearPendingState();

			try
			{
				_replaying = true;
				_dispatchSelection.Invoke(null, new object[2] { token, selection });
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[SettlementCharacterSwitchMenuFix] Deferred selection dispatch failed", Unwrap(ex));
				TryCancelSelection(token, "deferred settlement switch failed");
			}
			finally
			{
				_replaying = false;
			}

			if (target != null && Hero.MainHero == target && settlement != null && MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == settlement)
			{
				_pendingTarget = target;
				_pendingSettlement = settlement;
				_returnDelayTicks = 1;
			}
		}

		private static void ReturnToSettlementAfterSwitch()
		{
			Hero target = _pendingTarget;
			Settlement settlement = _pendingSettlement;
			_pendingTarget = null;
			_pendingSettlement = null;
			try
			{
				if (target == null || Hero.MainHero != target || settlement == null || MobileParty.MainParty == null || MobileParty.MainParty.CurrentSettlement != settlement)
				{
					return;
				}
				if (!string.Equals(GetCurrentMenuId(), ManagerMenuId, StringComparison.Ordinal))
				{
					return;
				}
				GameMenu.ExitToLast();
				RemotePartySwitch.Info("[SettlementCharacterSwitchMenuFix] Completed the settlement character switch and returned through ManagerReturnHotfix; target=" + SafeHeroId(target) + "; settlement=" + SafeSettlementId(settlement) + ".");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[SettlementCharacterSwitchMenuFix] Post-switch settlement return failed", Unwrap(ex));
			}
		}

		private static Hero GetSelectedHero(List<InquiryElement> selected)
		{
			if (selected == null || selected.Count != 1 || selected[0] == null)
			{
				return null;
			}
			object option = ReadMember(selected[0], "Identifier") ?? ReadMember(selected[0], "Id");
			return ReadMember(option, "Value") as Hero;
		}

		private static bool IsRegisteredSharedHero(Hero hero)
		{
			object behavior = _behaviorInstance == null ? null : _behaviorInstance.GetValue(null, null);
			return behavior != null && hero != null && Convert.ToBoolean(_isRegisteredSharedHero.Invoke(behavior, new object[1] { hero }));
		}

		private static string GetCurrentMenuId()
		{
			Campaign campaign = Campaign.Current;
			object context = campaign == null ? null : campaign.CurrentMenuContext;
			object gameMenu = ReadMember(context, "GameMenu");
			return ReadMember(gameMenu, "StringId") as string;
		}

		private static object ReadMember(object instance, string name)
		{
			if (instance == null || string.IsNullOrEmpty(name))
			{
				return null;
			}
			Type type = instance.GetType();
			PropertyInfo property = type.GetProperties(InstanceFlags)
				.Where((PropertyInfo candidate) => candidate.Name == name && candidate.GetIndexParameters().Length == 0)
				.OrderBy((PropertyInfo candidate) => candidate.DeclaringType == type ? 0 : 1)
				.FirstOrDefault();
			if (property != null)
			{
				return property.GetValue(instance, null);
			}
			FieldInfo field = type.GetFields(InstanceFlags)
				.Where((FieldInfo candidate) => candidate.Name == name)
				.OrderBy((FieldInfo candidate) => candidate.DeclaringType == type ? 0 : 1)
				.FirstOrDefault();
			return field == null ? null : field.GetValue(instance);
		}

		private static void EnsureCampaignState()
		{
			Campaign current = Campaign.Current;
			if (object.ReferenceEquals(_campaign, current))
			{
				return;
			}
			if (_pendingToken != 0)
			{
				TryCancelSelection(_pendingToken, "campaign changed before deferred settlement switch");
			}
			_campaign = current;
			_replaying = false;
			ClearPendingState();
		}

		private static void ClearPendingState()
		{
			_pendingToken = 0;
			_pendingSelection = null;
			_pendingTarget = null;
			_pendingSettlement = null;
			_dispatchDelayTicks = 0;
			_returnDelayTicks = 0;
		}

		private static void TryCancelSelection(int token, string source)
		{
			try
			{
				if (token != 0 && _cancelSelection != null)
				{
					_cancelSelection.Invoke(null, new object[2] { token, source });
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[SettlementCharacterSwitchMenuFix] Could not remove a deferred selection callback", Unwrap(ex));
			}
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, int priority)
		{
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
			object harmonyPrefix = Activator.CreateInstance(harmonyMethodType, prefix);
			FieldInfo priorityField = harmonyMethodType.GetField("priority", BindingFlags.Instance | BindingFlags.Public);
			if (priorityField != null)
			{
				priorityField.SetValue(harmonyPrefix, priority);
			}
			object[] arguments = new object[patch.GetParameters().Length];
			arguments[0] = original;
			arguments[1] = harmonyPrefix;
			patch.Invoke(harmony, arguments);
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			MethodInfo method = typeof(SettlementCharacterSwitchMenuFix).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(SettlementCharacterSwitchMenuFix).FullName, name);
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

		private static string SafeHeroId(Hero hero)
		{
			try
			{
				return hero == null ? "<null>" : hero.StringId ?? hero.Name.ToString();
			}
			catch
			{
				return "<unknown>";
			}
		}

		private static string SafeSettlementId(Settlement settlement)
		{
			try
			{
				return settlement == null ? "<null>" : settlement.StringId ?? settlement.Name.ToString();
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
