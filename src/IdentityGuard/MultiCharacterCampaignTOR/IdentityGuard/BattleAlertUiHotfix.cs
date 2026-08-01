using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleAlertUiHotfix
	{
		private sealed class TooltipContext
		{
			internal readonly MapEvent MapEvent;
			internal readonly string Fallback;

			internal TooltipContext(MapEvent mapEvent, string fallback)
			{
				MapEvent = mapEvent;
				Fallback = fallback;
			}
		}

		private const string TooltipMarkerPrefix = "__MCC_NATIVE_MAP_EVENT_TOOLTIP__";
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static readonly Dictionary<string, TooltipContext> TooltipContexts = new Dictionary<string, TooltipContext>(StringComparer.Ordinal);

		private static bool _installed;
		private static Campaign _campaign;
		private static FieldInfo _hintTextField;
		private static string _activeTooltipMarker;
		private static MethodInfo _showPredictionInquiry;
		private static MethodInfo _setInquiryOpen;
		private static MethodInfo _ensureCampaignState;
		private static MethodInfo _candidateHero;
		private static MethodInfo _candidateParty;
		private static MethodInfo _candidateMapEvent;
		private static MethodInfo _markDequeued;
		private static MethodInfo _wasAlerted;
		private static MethodInfo _buildSnapshot;
		private static MethodInfo _markAlerted;
		private static MethodInfo _canOrderPartyToBattle;
		private static MethodInfo _createFlowChoice;
		private static MethodInfo _createInquiryElement;
		private static MethodInfo _invokeLegacySelection;
		private static FieldInfo _snapshotCanPredict;
		private static FieldInfo _snapshotPredictedLoss;
		private static FieldInfo _snapshotFriendlyStrength;
		private static FieldInfo _snapshotEnemyStrength;
		private static PropertyInfo _snapshotOutcomeLabel;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				ResolvePredictionMembers();
				Type hintViewModelType = RequireType("TaleWorlds.Core.ViewModelCollection.Information.HintViewModel, TaleWorlds.Core.ViewModelCollection");
				_hintTextField = hintViewModelType.GetField("HintText", InstanceFlags);
				if (_hintTextField == null)
				{
					throw new MissingFieldException(hintViewModelType.FullName, "HintText");
				}
				MethodInfo beginHint = hintViewModelType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "ExecuteBeginHint" && method.GetParameters().Length == 0);
				MethodInfo endHint = hintViewModelType.GetMethods(InstanceFlags).Single((MethodInfo method) => method.Name == "ExecuteEndHint" && method.GetParameters().Length == 0);

				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.battlealertui.v121");
				Patch(harmony, harmonyType, harmonyMethodType, _showPredictionInquiry, GetPatchMethod("BeforeShowPredictionInquiry"), 800);
				Patch(harmony, harmonyType, harmonyMethodType, beginHint, GetPatchMethod("BeforeBeginHint"), 800);
				Patch(harmony, harmonyType, harmonyMethodType, endHint, GetPatchMethod("BeforeEndHint"), 800);

				_installed = true;
				RemotePartySwitch.Info("[BattleAlertUiHotfix v1.2.1] Installed compact battle inquiry and Bannerlord-native map-event troop tooltip bridge.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleAlertUiHotfix] Installation failed", Unwrap(ex));
			}
		}

		private static bool BeforeShowPredictionInquiry(object __0)
		{
			ShowFixedInquiry(__0);
			return false;
		}

		private static bool BeforeBeginHint(object __instance)
		{
			string marker = GetHintMarker(__instance);
			TooltipContext context;
			if (marker == null || !TooltipContexts.TryGetValue(marker, out context))
			{
				return true;
			}
			try
			{
				TaleWorlds.Library.InformationManager.ShowTooltip(typeof(MapEvent), new object[1] { context.MapEvent });
				_activeTooltipMarker = marker;
			}
			catch (Exception ex)
			{
				_activeTooltipMarker = null;
				RemotePartySwitch.Error("[BattleAlertUiHotfix] Native map-event tooltip could not be opened", Unwrap(ex));
				MBInformationManager.ShowHint(context.Fallback);
			}
			return false;
		}

		private static bool BeforeEndHint(object __instance)
		{
			string marker = GetHintMarker(__instance);
			if (marker == null || (!TooltipContexts.ContainsKey(marker) && !string.Equals(_activeTooltipMarker, marker, StringComparison.Ordinal)))
			{
				return true;
			}
			HideNativeTooltip();
			return false;
		}

		private static void ShowFixedInquiry(object candidate)
		{
			SetInquiryOpen(false);
			ClearTooltipContexts();
			try
			{
				EnsureLocalCampaignState();
				_ensureCampaignState.Invoke(null, null);
				Hero hero = _candidateHero.Invoke(null, new object[1] { candidate }) as Hero;
				MobileParty party = _candidateParty.Invoke(null, new object[1] { candidate }) as MobileParty;
				MapEvent mapEvent = _candidateMapEvent.Invoke(null, new object[1] { candidate }) as MapEvent;
				if (hero == null || party == null || mapEvent == null)
				{
					return;
				}

				_markDequeued.Invoke(null, new object[2] { mapEvent, party });
				if (Convert.ToBoolean(_wasAlerted.Invoke(null, new object[2] { mapEvent, party })))
				{
					RemotePartySwitch.Info("[BattleAlertUiHotfix] Suppressed a duplicate alert for party=" + SafeId(party) + ".");
					return;
				}

				object snapshot = _buildSnapshot.Invoke(null, new object[2] { mapEvent, party });
				bool canPredict = Convert.ToBoolean(_snapshotCanPredict.GetValue(snapshot));
				bool predictedLoss = Convert.ToBoolean(_snapshotPredictedLoss.GetValue(snapshot));
				float friendlyStrength = Convert.ToSingle(_snapshotFriendlyStrength.GetValue(snapshot));
				float enemyStrength = Convert.ToSingle(_snapshotEnemyStrength.GetValue(snapshot));
				string outcomeLabel = Convert.ToString(_snapshotOutcomeLabel.GetValue(snapshot, null));

				if (BattleInterventionSettings.PredictedLossesOnly && canPredict && !predictedLoss)
				{
					RemotePartySwitch.Info("[BattleAlertUiHotfix] Predicted-loss-only mode suppressed a favorable/even alert; party=" + SafeId(party) + "; friendlyStrength=" + friendlyStrength.ToString("0.0") + "; opposingStrength=" + enemyStrength.ToString("0.0") + ".");
					return;
				}

				_markAlerted.Invoke(null, new object[2] { mapEvent, party });
				string controlReason;
				bool canControl = RemotePartySwitch.CanTakeControlForBattle(hero, mapEvent, out controlReason);
				string reinforceReason;
				bool canReinforce = InvokeCanOrderPartyToBattle(MobileParty.MainParty, party, mapEvent, out reinforceReason);

				string fallbackTooltip = BuildFallbackTooltip(outcomeLabel, canPredict, friendlyStrength, enemyStrength);
				string tooltipMarker = TooltipMarkerPrefix + Guid.NewGuid().ToString("N");
				TooltipContexts[tooltipMarker] = new TooltipContext(mapEvent, fallbackTooltip);

				string nativeHint = tooltipMarker;
				List<InquiryElement> elements = new List<InquiryElement>
				{
					CreateActionElement(1, candidate, "Take control of " + RemotePartySwitch.HeroName(hero) + " and continue the battle", canControl, canControl ? nativeHint : controlReason),
					CreateActionElement(2, candidate, "Send the current party to reinforce " + RemotePartySwitch.PartyName(party), canReinforce, canReinforce ? nativeHint : reinforceReason)
				};

				string predictionLine = canPredict
					? "Prediction: " + outcomeLabel + " — native strength " + friendlyStrength.ToString("0.0") + " vs " + enemyStrength.ToString("0.0") + "."
					: "Prediction: unavailable; this alert is shown as a safety fallback.";
				string policyLine = BattleInterventionSettings.PredictedLossesOnly
					? "Alert policy: predicted losses only."
					: "Alert policy: every eligible battle.";
				string description = RemotePartySwitch.PartyName(party) + " is involved in an active battle.\n" +
					predictionLine + "\n" +
					"Select takeover, reinforcement, or both. Hover either available action for Bannerlord's native battle troop tooltip.\n" +
					policyLine;

				MultiSelectionInquiryData data = new MultiSelectionInquiryData(
					"Shared character in battle — " + outcomeLabel,
					description,
					elements,
					true,
					1,
					2,
					"Apply selected actions",
					"Dismiss",
					delegate(List<InquiryElement> selected)
					{
						CloseInquiryTooltip(tooltipMarker);
						SetInquiryOpen(false);
						_invokeLegacySelection.Invoke(null, new object[2] { candidate, selected });
					},
					delegate(List<InquiryElement> selected)
					{
						CloseInquiryTooltip(tooltipMarker);
						SetInquiryOpen(false);
					},
					string.Empty,
					false);

				SetInquiryOpen(true);
				MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
			}
			catch (Exception ex)
			{
				SetInquiryOpen(false);
				ClearTooltipContexts();
				RemotePartySwitch.Error("[BattleAlertUiHotfix] Could not open the corrected battle inquiry", Unwrap(ex));
				RemotePartySwitch.Notify("A shared-character battle was detected, but its corrected intervention window could not be opened. See MultiCharacterCampaignTOR.log.");
			}
		}

		private static InquiryElement CreateActionElement(int action, object candidate, string label, bool enabled, string hint)
		{
			object choice = _createFlowChoice.Invoke(null, new object[2] { action, candidate });
			return (InquiryElement)_createInquiryElement.Invoke(null, new object[4] { choice, label, enabled, hint ?? string.Empty });
		}

		private static bool InvokeCanOrderPartyToBattle(MobileParty reinforcingParty, MobileParty targetParty, MapEvent mapEvent, out string reason)
		{
			object[] arguments = new object[4] { reinforcingParty, targetParty, mapEvent, null };
			bool result = Convert.ToBoolean(_canOrderPartyToBattle.Invoke(null, arguments));
			reason = arguments[3] as string ?? string.Empty;
			return result;
		}

		private static string BuildFallbackTooltip(string outcomeLabel, bool canPredict, float friendlyStrength, float enemyStrength)
		{
			return canPredict
				? "Battle forecast: " + outcomeLabel + ". Native side strength " + friendlyStrength.ToString("0.0") + " vs " + enemyStrength.ToString("0.0") + "."
				: "Battle forecast unavailable.";
		}

		private static string GetHintMarker(object instance)
		{
			if (instance == null || _hintTextField == null)
			{
				return null;
			}
			object textObject = _hintTextField.GetValue(instance);
			string text = textObject == null ? null : textObject.ToString();
			return !string.IsNullOrEmpty(text) && text.StartsWith(TooltipMarkerPrefix, StringComparison.Ordinal) ? text : null;
		}

		private static void CloseInquiryTooltip(string marker)
		{
			if (string.Equals(_activeTooltipMarker, marker, StringComparison.Ordinal))
			{
				HideNativeTooltip();
			}
			TooltipContexts.Remove(marker);
		}

		private static void HideNativeTooltip()
		{
			try
			{
				TaleWorlds.Library.InformationManager.HideTooltip();
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleAlertUiHotfix] Native tooltip cleanup failed", Unwrap(ex));
			}
			_activeTooltipMarker = null;
		}

		private static void ClearTooltipContexts()
		{
			if (_activeTooltipMarker != null)
			{
				HideNativeTooltip();
			}
			TooltipContexts.Clear();
		}

		private static void EnsureLocalCampaignState()
		{
			Campaign current = Campaign.Current;
			if (object.ReferenceEquals(_campaign, current))
			{
				return;
			}
			_campaign = current;
			ClearTooltipContexts();
		}

		private static void SetInquiryOpen(bool value)
		{
			_setInquiryOpen.Invoke(null, new object[1] { value });
		}

		private static void ResolvePredictionMembers()
		{
			Type type = typeof(BattleInterventionPrediction);
			_showPredictionInquiry = RequireMethod(type, "ShowPredictionInquiry", 1);
			_setInquiryOpen = RequireMethod(type, "SetInquiryOpen", 1);
			_ensureCampaignState = RequireMethod(type, "EnsureCampaignState", 0);
			_candidateHero = RequireMethod(type, "CandidateHero", 1);
			_candidateParty = RequireMethod(type, "CandidateParty", 1);
			_candidateMapEvent = RequireMethod(type, "CandidateMapEvent", 1);
			_markDequeued = RequireMethod(type, "MarkDequeued", 2);
			_wasAlerted = RequireMethod(type, "WasAlerted", 2);
			_buildSnapshot = RequireMethod(type, "BuildSnapshot", 2);
			_markAlerted = RequireMethod(type, "MarkAlerted", 2);
			_canOrderPartyToBattle = RequireMethod(type, "CanOrderPartyToBattle", 4);
			_createFlowChoice = RequireMethod(type, "CreateFlowChoice", 2);
			_createInquiryElement = RequireMethod(type, "CreateInquiryElement", 4);
			_invokeLegacySelection = RequireMethod(type, "InvokeLegacySelection", 2);

			Type snapshotType = _buildSnapshot.ReturnType;
			_snapshotCanPredict = RequireField(snapshotType, "CanPredict");
			_snapshotPredictedLoss = RequireField(snapshotType, "PredictedLoss");
			_snapshotFriendlyStrength = RequireField(snapshotType, "FriendlyStrength");
			_snapshotEnemyStrength = RequireField(snapshotType, "EnemyStrength");
			_snapshotOutcomeLabel = snapshotType.GetProperty("OutcomeLabel", InstanceFlags);
			if (_snapshotOutcomeLabel == null)
			{
				throw new MissingMemberException(snapshotType.FullName, "OutcomeLabel");
			}
		}

		private static MethodInfo RequireMethod(Type type, string name, int parameterCount)
		{
			MethodInfo method = type.GetMethods(StaticFlags).SingleOrDefault((MethodInfo candidate) => candidate.Name == name && candidate.GetParameters().Length == parameterCount);
			if (method == null)
			{
				throw new MissingMethodException(type.FullName, name);
			}
			return method;
		}

		private static FieldInfo RequireField(Type type, string name)
		{
			FieldInfo field = type.GetField(name, InstanceFlags);
			if (field == null)
			{
				throw new MissingFieldException(type.FullName, name);
			}
			return field;
		}

		private static string SafeId(MobileParty party)
		{
			return party == null || party.StringId == null ? "<null>" : party.StringId;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodInfo original, MethodInfo prefix, int priority)
		{
			object harmonyPrefix = Activator.CreateInstance(harmonyMethodType, prefix);
			FieldInfo priorityField = harmonyMethodType.GetField("priority", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (priorityField != null)
			{
				priorityField.SetValue(harmonyPrefix, priority);
			}
			MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
			object[] arguments = new object[patch.GetParameters().Length];
			arguments[0] = original;
			arguments[1] = harmonyPrefix;
			patch.Invoke(harmony, arguments);
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			MethodInfo method = typeof(BattleAlertUiHotfix).GetMethod(name, StaticFlags);
			if (method == null)
			{
				throw new MissingMethodException(typeof(BattleAlertUiHotfix).FullName, name);
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
