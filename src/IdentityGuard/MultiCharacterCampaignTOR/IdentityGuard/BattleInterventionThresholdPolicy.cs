using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleInterventionThresholdPolicy
	{
		private const string ThresholdSaveKey = "tor_shared_campaign_alert_max_friendly_strength_share_percent";
		private const string InitializedSaveKey = "tor_shared_campaign_alert_threshold_initialized_v130";
		private const string MenuId = "multi_character_campaign_tor";
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool _installed;
		private static Campaign _campaign;
		private static int _maxFriendlyStrengthSharePercent = 100;
		private static bool _thresholdInitialized;
		private static FieldInfo _legacyLossOnlyField;
		private static Type _candidateType;
		private static ConstructorInfo _candidateConstructor;
		private static FieldInfo _candidateQueueField;
		private static MethodInfo _candidateParty;
		private static MethodInfo _candidateMapEvent;
		private static MethodInfo _markDequeued;
		private static MethodInfo _setInquiryOpen;
		private static MethodInfo _getMapEvent;
		private static MethodInfo _wasAlerted;
		private static MethodInfo _reserve;
		private static MethodInfo _selectTargetHero;
		private static MethodInfo _isSuppressed;

		internal static int MaxFriendlyStrengthSharePercent
		{
			get
			{
				EnsureCampaignState();
				return _maxFriendlyStrengthSharePercent;
			}
		}

		internal static bool FilteringEnabled
		{
			get { return MaxFriendlyStrengthSharePercent < 100; }
		}

		internal static string PolicyLabel
		{
			get
			{
				int threshold = MaxFriendlyStrengthSharePercent;
				if (threshold >= 100)
				{
					return "every eligible battle";
				}
				if (threshold == 50)
				{
					return "predicted defeats and approximately even battles (friendly strength share at or below 50%)";
				}
				if (threshold < 50)
				{
					return "only severe predicted defeats (friendly strength share at or below " + threshold + "%)";
				}
				return "predicted defeats and victories no safer than a " + threshold + "% friendly strength share";
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
				ResolveMembers();
				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.battlethresholds.v130");

				Type behaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");
				MethodInfo syncData = behaviorType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "SyncData" && method.GetParameters().Length == 1);
				MethodInfo registerMenus = behaviorType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "RegisterMenus" && method.GetParameters().Length == 1);
				Patch(harmony, harmonyType, harmonyMethodType, syncData, null, GetPatchMethod("AfterSyncData"), 0);
				Patch(harmony, harmonyType, harmonyMethodType, registerMenus, null, GetPatchMethod("AfterRegisterMenus"), 0);

				Type legacySettings = typeof(BattleInterventionSettings);
				Patch(harmony, harmonyType, harmonyMethodType, RequireMethod(legacySettings, "ShowEnableLossOnly", 1), GetPatchMethod("HideLegacyPolicyOption"), null, 900);
				Patch(harmony, harmonyType, harmonyMethodType, RequireMethod(legacySettings, "ShowEnableAlways", 1), GetPatchMethod("HideLegacyPolicyOption"), null, 900);

				Type predictionType = typeof(BattleInterventionPrediction);
				Patch(harmony, harmonyType, harmonyMethodType, RequireMethod(predictionType, "ShowPredictionInquiry", 1), GetPatchMethod("BeforeShowPredictionInquiry"), null, 900);

				Type dispatcherType = RequireType("TaleWorlds.CampaignSystem.CampaignEventDispatcher, TaleWorlds.CampaignSystem");
				MethodInfo partyAdded = dispatcherType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "OnPartyAddedToMapEvent" && method.GetParameters().Length == 1);
				Patch(harmony, harmonyType, harmonyMethodType, partyAdded, null, GetPatchMethod("AfterPartyAddedToMapEvent"), 0);

				ConstructorInfo inquiryConstructor = typeof(MultiSelectionInquiryData).GetConstructors(BindingFlags.Instance | BindingFlags.Public)
					.Single((ConstructorInfo constructor) => constructor.GetParameters().Length == 12);
				Patch(harmony, harmonyType, harmonyMethodType, inquiryConstructor, GetPatchMethod("BeforeBattleInquiryConstructed"), null, 900);

				_installed = true;
				RemotePartySwitch.Info("[BattleInterventionThresholdPolicy v1.3.0] Installed exact native-strength-share alert thresholds and old-policy migration.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionThresholdPolicy] Installation failed", Unwrap(ex));
			}
		}

		private static void AfterSyncData(IDataStore dataStore)
		{
			try
			{
				EnsureCampaignState();
				dataStore.SyncData(ThresholdSaveKey, ref _maxFriendlyStrengthSharePercent);
				dataStore.SyncData(InitializedSaveKey, ref _thresholdInitialized);
				if (!_thresholdInitialized)
				{
					bool legacyLossOnly = _legacyLossOnlyField != null && Convert.ToBoolean(_legacyLossOnlyField.GetValue(null));
					_maxFriendlyStrengthSharePercent = legacyLossOnly ? 50 : 100;
					_thresholdInitialized = true;
					RemotePartySwitch.Info("[BattleInterventionThresholdPolicy] Migrated the legacy binary alert policy to a " + _maxFriendlyStrengthSharePercent + "% friendly-strength-share threshold.");
				}
				_maxFriendlyStrengthSharePercent = Math.Max(0, Math.Min(100, _maxFriendlyStrengthSharePercent));
				if (_legacyLossOnlyField != null)
				{
					_legacyLossOnlyField.SetValue(null, false);
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionThresholdPolicy] Could not synchronize the alert threshold", Unwrap(ex));
			}
		}

		private static void AfterRegisterMenus(CampaignGameStarter starter)
		{
			try
			{
				starter.AddGameMenuOption(MenuId, "mcc_tor_alert_strength_threshold", "Configure battle alert threshold", ShowThresholdOption, OpenThresholdInquiry, false, 7);
				RemotePartySwitch.Info("[BattleInterventionThresholdPolicy] Registered the granular battle-alert threshold option.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionThresholdPolicy] Could not register the threshold menu option", Unwrap(ex));
			}
		}

		private static bool ShowThresholdOption(MenuCallbackArgs args)
		{
			args.IsEnabled = true;
			args.optionLeaveType = GameMenuOption.LeaveType.Manage;
			args.Tooltip = new TaleWorlds.Localization.TextObject("Current policy: " + PolicyLabel + ". Enter any whole percentage from 0 to 100. 50% means predicted defeat/even only; 55-65% includes progressively safer but still close victories; 100% alerts for every battle.");
			return true;
		}

		private static bool HideLegacyPolicyOption(ref bool __result)
		{
			__result = false;
			return false;
		}

		private static void OpenThresholdInquiry(MenuCallbackArgs args)
		{
			TextInquiryData data = new TextInquiryData(
				"Battle alert threshold",
				"Enter the maximum friendly share of total native battle strength that should trigger an alert.\n\nExamples:\n50 = predicted defeat or approximately even\n55 = razor-close victories and worse\n60 = difficult/heavy-casualty-risk victories and worse\n67 = enemy has at least half your side's strength\n75 = broad intervention threshold\n100 = every eligible battle\n\nThis is a strength forecast, not an exact casualty prediction.",
				true,
				true,
				"Apply",
				"Cancel",
				ApplyThreshold,
				null,
				false,
				ValidateThreshold,
				string.Empty,
				MaxFriendlyStrengthSharePercent.ToString());
			InformationManager.ShowTextInquiry(data, true, true);
		}

		private static Tuple<bool, string> ValidateThreshold(string value)
		{
			int parsed;
			if (!int.TryParse((value ?? string.Empty).Trim(), out parsed) || parsed < 0 || parsed > 100)
			{
				return new Tuple<bool, string>(false, "Enter a whole percentage from 0 to 100.");
			}
			return new Tuple<bool, string>(true, string.Empty);
		}

		private static void ApplyThreshold(string value)
		{
			int parsed;
			if (!int.TryParse((value ?? string.Empty).Trim(), out parsed))
			{
				return;
			}
			_maxFriendlyStrengthSharePercent = Math.Max(0, Math.Min(100, parsed));
			_thresholdInitialized = true;
			if (_legacyLossOnlyField != null)
			{
				_legacyLossOnlyField.SetValue(null, false);
			}
			RemotePartySwitch.Info("[BattleInterventionThresholdPolicy] Alert threshold changed to " + _maxFriendlyStrengthSharePercent + "% friendly strength share.");
			RemotePartySwitch.Notify("Shared-character battle alert policy: " + PolicyLabel + ".");
			GameMenu.SwitchToMenu(MenuId);
		}

		private static bool BeforeShowPredictionInquiry(object __0)
		{
			try
			{
				EnsureCampaignState();
				if (!FilteringEnabled)
				{
					return true;
				}
				MobileParty party = _candidateParty.Invoke(null, new object[1] { __0 }) as MobileParty;
				MapEvent mapEvent = _candidateMapEvent.Invoke(null, new object[1] { __0 }) as MapEvent;
				float friendly;
				float enemy;
				if (party == null || mapEvent == null || !TryGetStrengthShare(mapEvent, party, out friendly, out enemy))
				{
					return true;
				}
				float share = GetFriendlySharePercent(friendly, enemy);
				if (share <= MaxFriendlyStrengthSharePercent + 0.001f)
				{
					return true;
				}
				_markDequeued.Invoke(null, new object[2] { mapEvent, party });
				_setInquiryOpen.Invoke(null, new object[1] { false });
				RemotePartySwitch.Info("[BattleInterventionThresholdPolicy] Suppressed battle alert above threshold; party=" + SafeId(party) + "; friendlyShare=" + share.ToString("0.0") + "%; threshold=" + MaxFriendlyStrengthSharePercent + "%.");
				return false;
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionThresholdPolicy] Threshold prefilter failed; allowing the alert as a safety fallback", Unwrap(ex));
				return true;
			}
		}

		private static void BeforeBattleInquiryConstructed(string __0, ref string __1)
		{
			if (string.IsNullOrEmpty(__0) || !__0.StartsWith("Shared character in battle", StringComparison.Ordinal) || string.IsNullOrEmpty(__1))
			{
				return;
			}
			string replacement = "Alert policy: " + PolicyLabel + ".";
			__1 = __1.Replace("Alert policy: predicted losses only.", replacement)
				.Replace("Alert policy: every eligible battle.", replacement);
		}

		private static void AfterPartyAddedToMapEvent(PartyBase __0)
		{
			if (!FilteringEnabled)
			{
				return;
			}
			try
			{
				EnsureCampaignState();
				MapEvent mapEvent = _getMapEvent.Invoke(null, new object[1] { __0 }) as MapEvent;
				if (mapEvent == null || Convert.ToBoolean(_isSuppressed.Invoke(null, new object[1] { mapEvent })))
				{
					return;
				}
				IList<Hero> registered = RemotePartySwitch.GetRegisteredSharedHeroes();
				HashSet<MobileParty> examined = new HashSet<MobileParty>();
				for (int i = 0; i < registered.Count; i++)
				{
					Hero hero = registered[i];
					MobileParty party = hero == null ? null : hero.PartyBelongedTo;
					if (hero == null || hero == Hero.MainHero || party == null || party == MobileParty.MainParty || !party.IsActive || !examined.Add(party))
					{
						continue;
					}
					if (!object.ReferenceEquals(RemotePartySwitch.GetEffectiveMapEvent(party), mapEvent) || !RemotePartySwitch.IsDefenderInMapEvent(party, mapEvent))
					{
						continue;
					}
					float friendly;
					float enemy;
					if (!TryGetStrengthShare(mapEvent, party, out friendly, out enemy) || GetFriendlySharePercent(friendly, enemy) > MaxFriendlyStrengthSharePercent + 0.001f)
					{
						continue;
					}
					if (Convert.ToBoolean(_wasAlerted.Invoke(null, new object[2] { mapEvent, party })) || !Convert.ToBoolean(_reserve.Invoke(null, new object[2] { mapEvent, party })))
					{
						continue;
					}
					Hero target = _selectTargetHero.Invoke(null, new object[2] { party, registered }) as Hero;
					if (target == null)
					{
						_markDequeued.Invoke(null, new object[2] { mapEvent, party });
						continue;
					}
					object candidate = _candidateConstructor.Invoke(new object[3] { target, party, mapEvent });
					object queue = _candidateQueueField.GetValue(null);
					queue.GetType().GetMethod("Enqueue", InstanceFlags, null, new Type[1] { _candidateType }, null).Invoke(queue, new object[1] { candidate });
					RemotePartySwitch.Info("[BattleInterventionThresholdPolicy] A joined party crossed the configured threshold; queued alert for party=" + SafeId(party) + ".");
				}
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionThresholdPolicy] Could not reevaluate the configured threshold after a party joined", Unwrap(ex));
			}
		}

		private static bool TryGetStrengthShare(MapEvent mapEvent, MobileParty party, out float friendly, out float enemy)
		{
			friendly = 0f;
			enemy = 0f;
			if (mapEvent == null || party == null || party.Party == null)
			{
				return false;
			}
			BattleSideEnum side = party.Party.Side;
			if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender && party.Party.MapEventSide != null)
			{
				side = party.Party.MapEventSide.MissionSide;
			}
			if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender)
			{
				return false;
			}
			mapEvent.GetStrengthsRelativeToParty(side, out friendly, out enemy);
			return !float.IsNaN(friendly) && !float.IsInfinity(friendly) && !float.IsNaN(enemy) && !float.IsInfinity(enemy) && friendly + enemy > 0.001f;
		}

		private static float GetFriendlySharePercent(float friendly, float enemy)
		{
			float total = Math.Max(0f, friendly) + Math.Max(0f, enemy);
			return total > 0.001f ? Math.Max(0f, friendly) / total * 100f : 50f;
		}

		private static void EnsureCampaignState()
		{
			Campaign current = Campaign.Current;
			if (object.ReferenceEquals(_campaign, current))
			{
				return;
			}
			_campaign = current;
			_maxFriendlyStrengthSharePercent = 100;
			_thresholdInitialized = false;
		}

		private static void ResolveMembers()
		{
			_legacyLossOnlyField = typeof(BattleInterventionSettings).GetField("_predictedLossesOnly", StaticFlags);
			if (_legacyLossOnlyField == null)
			{
				throw new MissingFieldException(typeof(BattleInterventionSettings).FullName, "_predictedLossesOnly");
			}
			Type predictionType = typeof(BattleInterventionPrediction);
			_candidateParty = RequireMethod(predictionType, "CandidateParty", 1);
			_candidateMapEvent = RequireMethod(predictionType, "CandidateMapEvent", 1);
			_markDequeued = RequireMethod(predictionType, "MarkDequeued", 2);
			_setInquiryOpen = RequireMethod(predictionType, "SetInquiryOpen", 1);
			_getMapEvent = RequireMethod(predictionType, "GetMapEvent", 1);
			_wasAlerted = RequireMethod(predictionType, "WasAlerted", 2);
			_reserve = RequireMethod(predictionType, "Reserve", 2);
			_selectTargetHero = RequireMethod(predictionType, "SelectTargetHero", 2);
			_isSuppressed = RequireMethod(predictionType, "IsSuppressed", 1);

			Type alertType = typeof(BattleInterventionAlert);
			_candidateType = alertType.GetNestedType("Candidate", BindingFlags.NonPublic);
			if (_candidateType == null)
			{
				throw new MissingMemberException(alertType.FullName, "Candidate");
			}
			_candidateConstructor = _candidateType.GetConstructors(InstanceFlags).Single((ConstructorInfo constructor) => constructor.GetParameters().Length == 3);
			_candidateQueueField = alertType.GetField("Candidates", StaticFlags);
			if (_candidateQueueField == null)
			{
				throw new MissingFieldException(alertType.FullName, "Candidates");
			}
		}

		private static MethodInfo RequireMethod(Type type, string name, int parameterCount)
		{
			MethodInfo method = type.GetMethods(InstanceFlags | StaticFlags).SingleOrDefault((MethodInfo candidate) => candidate.Name == name && candidate.GetParameters().Length == parameterCount);
			if (method == null)
			{
				throw new MissingMethodException(type.FullName, name + "/" + parameterCount);
			}
			return method;
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			MethodInfo method = typeof(BattleInterventionThresholdPolicy).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(BattleInterventionThresholdPolicy).FullName, name);
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

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix, int priority)
		{
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
			object harmonyPrefix = prefix == null ? null : Activator.CreateInstance(harmonyMethodType, prefix);
			object harmonyPostfix = postfix == null ? null : Activator.CreateInstance(harmonyMethodType, postfix);
			FieldInfo priorityField = harmonyMethodType.GetField("priority", BindingFlags.Instance | BindingFlags.Public);
			if (priorityField != null)
			{
				if (harmonyPrefix != null)
				{
					priorityField.SetValue(harmonyPrefix, priority);
				}
				if (harmonyPostfix != null)
				{
					priorityField.SetValue(harmonyPostfix, priority);
				}
			}
			object[] arguments = new object[patch.GetParameters().Length];
			arguments[0] = original;
			arguments[1] = harmonyPrefix;
			arguments[2] = harmonyPostfix;
			patch.Invoke(harmony, arguments);
		}

		private static string SafeId(MobileParty party)
		{
			try
			{
				return party == null ? "<null>" : party.StringId ?? party.Name.ToString();
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
