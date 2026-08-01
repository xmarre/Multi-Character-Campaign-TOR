// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MultiCharacterCampaignTOR.NativeCreation
{
	public static class NativeCharacterCreation
	{
		private sealed class FieldValue
		{
			internal readonly FieldInfo Field;

			internal readonly object Value;

			internal FieldValue(FieldInfo field, object value)
			{
				Field = field;
				Value = value;
			}
		}

		private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static readonly object EventOwner = new object();

		private static readonly Dictionary<string, object> ClanSnapshot = new Dictionary<string, object>();

		private static readonly Dictionary<string, object> ClanFieldSnapshot = new Dictionary<string, object>();

		private static readonly List<FieldValue> CampaignOptionSnapshot = new List<FieldValue>();

		private static bool _inProgress;

		private static object _behavior;

		private static object _oldHero;

		private static object _candidateHero;

		private static object _playerClan;

		private static object _mainParty;

		private static object _partyPosition;

		private static object _campaignOptions;

		private static object _inventorySnapshot;

		private static object _completionEvent;

		private static object _initializationEvent;

		private static string _bannerCode;

		private static int _sharedGold;

		public static void Start(object behavior)
		{
			if (behavior == null)
			{
				throw new ArgumentNullException("behavior");
			}
			if (_inProgress)
			{
				Message("Character creation is already in progress.");
				return;
			}
			object[] array = new object[2] { null, false };
			if (!(bool)InvokeMethod(behavior, "CanChangeCampaignIdentity", array, 2))
			{
				string text = array[0] as string;
				Message((!string.IsNullOrEmpty(text)) ? text : "Character creation is currently unavailable.");
				return;
			}
			try
			{
				_behavior = behavior;
				_oldHero = StaticMember(RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem"), "MainHero");
				_playerClan = StaticMember(RequireType("TaleWorlds.CampaignSystem.Clan, TaleWorlds.CampaignSystem"), "PlayerClan");
				_mainParty = StaticMember(RequireType("TaleWorlds.CampaignSystem.Party.MobileParty, TaleWorlds.CampaignSystem"), "MainParty");
				if (_oldHero == null || _playerClan == null || _mainParty == null)
				{
					throw new InvalidOperationException("The active hero, player clan, or main party is unavailable.");
				}
				CaptureCampaignState();
				SubscribeCompletionEvent();
				_candidateHero = CreateCleanCandidateSeed();
				PrepareAndActivateCandidate();
				_inProgress = true;
				LogInfo("Native TOR character creation starting. Candidate=" + IdOf(_candidateHero) + "; oldHero=" + IdOf(_oldHero) + ".");
				LaunchTorCharacterCreation();
			}
			catch (Exception exception)
			{
				Exception ex = Unwrap(exception);
				LogError("Native TOR character creation failed to start", ex);
				RollBackStartFailure();
				Message("Character creation failed to start: " + ex.Message + ". See MultiCharacterCampaignTOR.log.");
			}
		}

		private static object CreateCleanCandidateSeed()
		{
			Type type = RequireType("TaleWorlds.CampaignSystem.CharacterObject, TaleWorlds.CampaignSystem");
			Type type2 = RequireType("TaleWorlds.CampaignSystem.HeroCreator, TaleWorlds.CampaignSystem");
			object obj = StaticMember(type, "PlayerCharacter");
			if (obj == null)
			{
				throw new InvalidOperationException("CharacterObject.PlayerCharacter is unavailable.");
			}
			object member = GetMember(_playerClan, "HomeSettlement");
			object obj2 = InvokeStatic(type2, "CreateSpecialHero", new object[5] { obj, member, _playerClan, null, 25 }, 5);
			if (obj2 == null)
			{
				throw new InvalidOperationException("HeroCreator returned null while allocating the new player hero.");
			}
			SetMember(obj2, "CompanionOf", null);
			SetMember(obj2, "Clan", _playerClan);
			return obj2;
		}

		private static void PrepareAndActivateCandidate()
		{
			Type type = RequireType("MultiCharacterCampaignTOR.Reflection, MultiCharacterCampaignTOR");
			InvokeStatic(type, "EnsureHeroActive", new object[1] { _candidateHero }, 1);
			Type type2 = RequireType("TaleWorlds.CampaignSystem.Actions.AddHeroToPartyAction, TaleWorlds.CampaignSystem");
			InvokeStatic(type2, "Apply", new object[3] { _candidateHero, _mainParty, false }, 3);
			InvokeStatic(type, "EnsureHeroInMainParty", new object[1] { _candidateHero }, 1);
			InvokeMethod(_behavior, "RegisterHero", new object[2] { _candidateHero, false }, 2);
			InvokeMethod(_behavior, "SynchronizeGold", new object[1] { _sharedGold }, 1);
			if (!(bool)InvokeMethod(_behavior, "TrySwitch", new object[3] { _candidateHero, false, false }, 3))
			{
				throw new InvalidOperationException("The new hero was allocated, but the active-character switch failed.");
			}
		}

		private static void LaunchTorCharacterCreation()
		{
			Type type = RequireType("TaleWorlds.CampaignSystem.CampaignEvents, TaleWorlds.CampaignSystem");
			_initializationEvent = StaticMember(type, "OnCharacterCreationInitializedEvent");
			if (_initializationEvent == null)
			{
				throw new InvalidOperationException("The character-creation initialization event is unavailable.");
			}
			InvokeMethod(_initializationEvent, "ClearListeners", new object[1] { EventOwner }, 1);
			InvokeMethod(_initializationEvent, "AddNonSerializedListener", new object[2]
			{
				EventOwner,
				new Action<object>(OnNativeCharacterCreationInitialized)
			}, 2);
			MoveOwnerListenerToTail(_initializationEvent, EventOwner);
			try
			{
				Type type2 = RequireType("TaleWorlds.Core.Game, TaleWorlds.Core");
				object obj = StaticMember(type2, "Current");
				if (obj == null)
				{
					throw new InvalidOperationException("Game.Current is unavailable.");
				}
				object member = GetMember(obj, "GameStateManager");
				if (member == null)
				{
					throw new InvalidOperationException("Game.Current.GameStateManager is unavailable.");
				}
				Type type3 = RequireType("TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState, TaleWorlds.CampaignSystem");
				MethodInfo methodInfo = FindGenericCreateStateMethod(member.GetType());
				if (methodInfo == null)
				{
					throw new MissingMethodException(member.GetType().FullName, "CreateState<T>()");
				}
				object obj2 = methodInfo.MakeGenericMethod(type3).Invoke(member, null);
				if (obj2 == null)
				{
					throw new InvalidOperationException("GameStateManager.CreateState<CharacterCreationState>() returned null.");
				}
				InvokeMethod(member, "PushState", new object[2] { obj2, 0 }, 2);
				LogInfo("Pushed native CharacterCreationState above the live campaign map with TOR's own content handler.");
			}
			finally
			{
				if (_initializationEvent != null)
				{
					InvokeMethod(_initializationEvent, "ClearListeners", new object[1] { EventOwner }, 1);
				}
			}
		}

		private static void OnNativeCharacterCreationInitialized(object manager)
		{
			if (manager == null)
			{
				throw new InvalidOperationException("CharacterCreationManager initialization callback received null.");
			}
			Type type = RequireType("TOR_Core.CampaignMechanics.CharacterCreation.TORCharacterCreationContentHandler, TOR_Core");
			object characterCreationHandler = GetCharacterCreationHandler(manager, 0);
			if (characterCreationHandler != null)
			{
				if (!type.IsInstanceOfType(characterCreationHandler))
				{
					throw new InvalidOperationException("Character-creation handler priority 0 is already occupied by " + characterCreationHandler.GetType().FullName + ".");
				}
				LogInfo("TORCharacterCreationContentHandler was already registered by TOR; duplicate registration was skipped.");
				return;
			}
			object obj = Activator.CreateInstance(type);
			if (obj == null)
			{
				throw new InvalidOperationException("TORCharacterCreationContentHandler could not be created.");
			}
			InvokeMethod(manager, "RegisterCharacterCreationContentHandler", new object[2] { obj, 0 }, 2);
			LogInfo("Registered TORCharacterCreationContentHandler directly for in-campaign native character creation.");
		}

		private static object GetCharacterCreationHandler(object manager, int priority)
		{
			FieldInfo fieldInfo = FindField(manager.GetType(), "_handlers");
			object obj = ((!(fieldInfo == null)) ? fieldInfo.GetValue(manager) : null);
			if (obj == null)
			{
				return null;
			}
			object obj2 = InvokeMethod(obj, "ContainsKey", new object[1] { priority }, 1);
			if (!(obj2 is bool) || !(bool)obj2)
			{
				return null;
			}
			PropertyInfo propertyInfo = FindProperty(obj.GetType(), "Item", isStatic: false);
			return (!(propertyInfo == null)) ? propertyInfo.GetValue(obj, new object[1] { priority }) : null;
		}

		private static void SubscribeCompletionEvent()
		{
			Type type = RequireType("TaleWorlds.CampaignSystem.CampaignEvents, TaleWorlds.CampaignSystem");
			_completionEvent = StaticMember(type, "OnCharacterCreationIsOverEvent");
			if (_completionEvent == null)
			{
				throw new InvalidOperationException("The character-creation completion event is unavailable.");
			}
			InvokeMethod(_completionEvent, "ClearListeners", new object[1] { EventOwner }, 1);
			InvokeMethod(_completionEvent, "AddNonSerializedListener", new object[2]
			{
				EventOwner,
				new Action(OnCharacterCreationCompleted)
			}, 2);
			MoveOwnerListenerToTail(_completionEvent, EventOwner);
		}

		private static void OnCharacterCreationCompleted()
		{
			if (!_inProgress)
			{
				return;
			}
			try
			{
				ClearCompletionListener();
				RestoreCampaignState();
				Type type = RequireType("MultiCharacterCampaignTOR.Reflection, MultiCharacterCampaignTOR");
				InvokeStatic(type, "EnsureHeroActive", new object[1] { _candidateHero }, 1);
				InvokeStatic(type, "EnsureHeroInMainParty", new object[1] { _candidateHero }, 1);
				InvokeMethod(_behavior, "RegisterHero", new object[2] { _candidateHero, false }, 2);
				InvokeMethod(_behavior, "SynchronizeGold", new object[1] { _sharedGold }, 1);
				Type type2 = Type.GetType("MultiCharacterCampaignTOR.PartyScreenSelectionBridge, MultiCharacterCampaignTOR", throwOnError: false);
				if (type2 != null)
				{
					InvokeStatic(type2, "RequestSelection", new object[1] { "native TOR character creation completed" }, 1);
				}
				object member = GetMember(_candidateHero, "Level");
				object member2 = GetMember(_candidateHero, "Name");
				LogInfo("Native TOR character creation completed. Hero=" + IdOf(_candidateHero) + "; name=" + ((member2 != null) ? member2.ToString() : "<null>") + "; level=" + ((member != null) ? member.ToString() : "<unknown>") + "CopyTextObject");
			}
			catch (Exception exception)
			{
				LogError("Native TOR character creation completion cleanup failed", Unwrap(exception));
				Message("The character was created, but campaign-state restoration reported an error. See MultiCharacterCampaignTOR.log.");
			}
			finally
			{
				ResetSession();
			}
		}

		private static void CaptureCampaignState()
		{
			ClanSnapshot.Clear();
			string[] array = new string[7] { "Renown", "Culture", "Name", "InformalName", "Influence", "Color", "Color2" };
			string[] array2 = array;
			foreach (string text in array2)
			{
				FindMethod(ClanSnapshot, text, _playerClan, text);
			}
			ClanFieldSnapshot.Clear();
			string[] array3 = new string[8] { "_home", "_midSettlement", "<InitialHomeSettlement>k__BackingField", "_tier", "_unused", "<BannerBackgroundColorPrimary>k__BackingField", "<BannerBackgroundColorSecondary>k__BackingField", "<BannerIconColor>k__BackingField" };
			string[] array4 = array3;
			foreach (string text2 in array4)
			{
				FieldInfo fieldInfo = FindField(_playerClan.GetType(), text2);
				if (fieldInfo != null)
				{
					ClanFieldSnapshot[text2] = fieldInfo.GetValue(_playerClan);
				}
			}
			object member = GetMember(_playerClan, "Banner");
			_bannerCode = ((member != null) ? (InvokeMethod(member, "Serialize", new object[0], 0) as string) : null);
			_partyPosition = GetMember(_mainParty, "Position");
			CaptureCampaignOptions();
			FieldInfo fieldInfo2 = FindField(_behavior.GetType(), "_sharedGold");
			_sharedGold = ((!(fieldInfo2 == null)) ? Convert.ToInt32(fieldInfo2.GetValue(_behavior)) : Convert.ToInt32(GetMember(_oldHero, "Gold")));
			Type type = Type.GetType("MultiCharacterCampaignTOR.PartyInventorySnapshot, MultiCharacterCampaignTOR", throwOnError: false);
			_inventorySnapshot = ((!(type == null)) ? InvokeStatic(type, "Capture", new object[1] { _mainParty }, 1) : null);
		}

		private static void RestoreCampaignState()
		{
			if (_playerClan != null)
			{
				foreach (KeyValuePair<string, object> item in ClanSnapshot)
				{
					SetMember(_playerClan, item.Key, item.Value);
				}
				foreach (KeyValuePair<string, object> item2 in ClanFieldSnapshot)
				{
					FieldInfo fieldInfo = FindField(_playerClan.GetType(), item2.Key);
					if (fieldInfo != null)
					{
						fieldInfo.SetValue(_playerClan, item2.Value);
					}
				}
				if (!string.IsNullOrEmpty(_bannerCode))
				{
					object member = GetMember(_playerClan, "Banner");
					Type type = ((member != null) ? member.GetType() : Type.GetType("TaleWorlds.Core.Banner, TaleWorlds.Core", throwOnError: true));
					object obj = Activator.CreateInstance(type);
					InvokeMethod(obj, "Deserialize", new object[1] { _bannerCode }, 1);
					SetMember(_playerClan, "Banner", obj);
				}
			}
			if (_mainParty != null && _partyPosition != null)
			{
				SetMember(_mainParty, "Position", _partyPosition);
			}
			if (_inventorySnapshot != null && _mainParty != null)
			{
				InvokeMethod(_inventorySnapshot, "RestoreIfChanged", new object[1] { _mainParty }, 1);
			}
			RestoreCampaignOptions();
			RecenterMapCamera();
		}

		private static void RollBackStartFailure()
		{
			try
			{
				ClearCompletionListener();
			}
			catch
			{
			}
			try
			{
				if (_initializationEvent != null)
				{
					InvokeMethod(_initializationEvent, "ClearListeners", new object[1] { EventOwner }, 1);
				}
			}
			catch
			{
			}
			try
			{
				RestoreCampaignState();
			}
			catch (Exception exception)
			{
				LogError("Rollback state restoration failed", Unwrap(exception));
			}
			try
			{
				if (_behavior != null && _oldHero != null && !object.ReferenceEquals(StaticMember(RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem"), "MainHero"), _oldHero))
				{
					InvokeMethod(_behavior, "TrySwitch", new object[3] { _oldHero, true, false }, 3);
				}
			}
			catch (Exception exception2)
			{
				LogError("Rollback active-character restoration failed", Unwrap(exception2));
			}
			try
			{
				DiscardFailedCandidate();
			}
			catch (Exception exception3)
			{
				LogError("Rollback candidate cleanup failed", Unwrap(exception3));
			}
			ResetSession();
		}

		private static void DiscardFailedCandidate()
		{
			if (_candidateHero != null && !object.ReferenceEquals(_candidateHero, _oldHero))
			{
				Type type = RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
				object objA = StaticMember(type, "MainHero");
				if (!object.ReferenceEquals(objA, _oldHero))
				{
					LogInfo("Rollback candidate cleanup was skipped because the original hero was not restored.");
					return;
				}
				string text = IdOf(_candidateHero);
				RemoveBehaviorId("_sharedHeroIds", text);
				RemoveBehaviorId("_originalCompanionIds", text);
				RemoveBehaviorId("_careerPromptHandledHeroIds", text);
				RemoveBehaviorId("_careerRepairHandledHeroIds", text);
				ClearBehaviorIdIfEqual("_pendingConversationSwitchId", text);
				ClearBehaviorIdIfEqual("_pendingCareerPromptHeroId", text);
				ClearBehaviorIdIfEqual("_loadedActiveHeroId", text);
				Type type2 = RequireType("TaleWorlds.CampaignSystem.Actions.DisableHeroAction, TaleWorlds.CampaignSystem");
				InvokeStatic(type2, "Apply", new object[1] { _candidateHero }, 1);
				SetMember(_candidateHero, "CompanionOf", null);
				SetMember(_candidateHero, "Clan", null);
				LogInfo("Discarded unfinished native character-creation candidate after startup failure. Hero=" + text + ".");
			}
		}

		private static void RemoveBehaviorId(string fieldName, string id)
		{
			if (_behavior == null || string.IsNullOrEmpty(id))
			{
				return;
			}
			FieldInfo fieldInfo = FindField(_behavior.GetType(), fieldName);
			object obj = ((!(fieldInfo == null)) ? fieldInfo.GetValue(_behavior) : null);
			if (obj is IList list)
			{
				while (list.Contains(id))
				{
					list.Remove(id);
				}
			}
		}

		private static void ClearBehaviorIdIfEqual(string fieldName, string id)
		{
			if (_behavior != null && !string.IsNullOrEmpty(id))
			{
				FieldInfo fieldInfo = FindField(_behavior.GetType(), fieldName);
				if (fieldInfo != null && string.Equals(fieldInfo.GetValue(_behavior) as string, id, StringComparison.Ordinal))
				{
					fieldInfo.SetValue(_behavior, null);
				}
			}
		}

		private static MethodInfo FindGenericCreateStateMethod(Type type)
		{
			Type type2 = type;
			while (type2 != null)
			{
				MethodInfo[] methods = type2.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (MethodInfo methodInfo in methods)
				{
					if (methodInfo.Name == "CreateState" && methodInfo.IsGenericMethodDefinition && methodInfo.GetGenericArguments().Length == 1 && methodInfo.GetParameters().Length == 0)
					{
						return methodInfo;
					}
				}
				type2 = type2.BaseType;
			}
			return null;
		}

		private static void MoveOwnerListenerToTail(object eventObject, object owner)
		{
			if (eventObject == null || owner == null)
			{
				return;
			}
			FieldInfo fieldInfo = FindField(eventObject.GetType(), "_nonSerializedListenerList");
			if (fieldInfo == null)
			{
				return;
			}
			object value = fieldInfo.GetValue(eventObject);
			if (value == null)
			{
				return;
			}
			PropertyInfo propertyInfo = FindProperty(value.GetType(), "Owner", isStatic: false);
			FieldInfo fieldInfo2 = FindField(value.GetType(), "Next");
			if (propertyInfo == null || fieldInfo2 == null || !object.ReferenceEquals(propertyInfo.GetValue(value, null), owner))
			{
				return;
			}
			object value2 = fieldInfo2.GetValue(value);
			if (value2 != null)
			{
				fieldInfo.SetValue(eventObject, value2);
				object obj = value2;
				while (fieldInfo2.GetValue(obj) != null)
				{
					obj = fieldInfo2.GetValue(obj);
				}
				fieldInfo2.SetValue(obj, value);
				fieldInfo2.SetValue(value, null);
			}
		}

		private static void ClearCompletionListener()
		{
			if (_completionEvent != null)
			{
				InvokeMethod(_completionEvent, "ClearListeners", new object[1] { EventOwner }, 1);
			}
		}

		private static void ResetSession()
		{
			_inProgress = false;
			_behavior = null;
			_oldHero = null;
			_candidateHero = null;
			_playerClan = null;
			_mainParty = null;
			_partyPosition = null;
			_campaignOptions = null;
			_inventorySnapshot = null;
			_completionEvent = null;
			_initializationEvent = null;
			_bannerCode = null;
			ClanSnapshot.Clear();
			ClanFieldSnapshot.Clear();
			CampaignOptionSnapshot.Clear();
		}

		private static void CaptureCampaignOptions()
		{
			CampaignOptionSnapshot.Clear();
			_campaignOptions = null;
			Type type = RequireType("TaleWorlds.CampaignSystem.Campaign, TaleWorlds.CampaignSystem");
			object obj = StaticMember(type, "Current");
			if (obj == null)
			{
				return;
			}
			_campaignOptions = GetMember(obj, "Options");
			if (_campaignOptions == null)
			{
				return;
			}
			Type type2 = _campaignOptions.GetType();
			while (type2 != null)
			{
				FieldInfo[] fields = type2.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (!fieldInfo.IsStatic && !fieldInfo.IsLiteral)
					{
						CampaignOptionSnapshot.Add(new FieldValue(fieldInfo, fieldInfo.GetValue(_campaignOptions)));
					}
				}
				type2 = type2.BaseType;
			}
			LogInfo("Captured campaign options before native character creation. FieldCount=" + CampaignOptionSnapshot.Count + ".");
		}

		private static void RestoreCampaignOptions()
		{
			if (_campaignOptions == null)
			{
				return;
			}
			foreach (FieldValue item in CampaignOptionSnapshot)
			{
				try
				{
					item.Field.SetValue(_campaignOptions, item.Value);
				}
				catch (Exception exception)
				{
					LogError("Failed to restore campaign option field " + item.Field.Name, Unwrap(exception));
					throw;
				}
			}
		}

		private static void RecenterMapCamera()
		{
			try
			{
				Type type = Type.GetType("TaleWorlds.Core.GameStateManager, TaleWorlds.Core", throwOnError: false);
				object obj = ((!(type == null)) ? StaticMember(type, "Current") : null);
				object obj2 = ((obj != null) ? GetMember(obj, "ActiveState") : null);
				if (obj2 != null && !(obj2.GetType().FullName != "TaleWorlds.CampaignSystem.GameState.MapState"))
				{
					object member = GetMember(obj2, "Handler");
					if (member != null)
					{
						InvokeMethod(member, "ResetCamera", new object[2] { true, true }, 2);
						InvokeMethod(member, "TeleportCameraToMainParty", new object[0], 0);
					}
				}
			}
			catch (Exception exception)
			{
				LogError("Map camera recenter after character creation failed safely", Unwrap(exception));
			}
		}

		private static Type RequireType(string assemblyQualifiedName)
		{
			Type type = Type.GetType(assemblyQualifiedName, throwOnError: false);
			if (type == null)
			{
				throw new TypeLoadException("Could not resolve " + assemblyQualifiedName + ".");
			}
			return type;
		}

		private static object StaticMember(Type type, string name)
		{
			PropertyInfo propertyInfo = FindProperty(type, name, isStatic: true);
			if (propertyInfo != null)
			{
				return propertyInfo.GetValue(null, null);
			}
			FieldInfo fieldInfo = FindField(type, name, isStatic: true);
			if (fieldInfo != null)
			{
				return fieldInfo.GetValue(null);
			}
			return null;
		}

		private static object GetMember(object target, string name)
		{
			if (target == null)
			{
				return null;
			}
			Type type = target.GetType();
			PropertyInfo propertyInfo = FindProperty(type, name, isStatic: false);
			if (propertyInfo != null)
			{
				return propertyInfo.GetValue(target, null);
			}
			FieldInfo fieldInfo = FindField(type, name);
			return (!(fieldInfo == null)) ? fieldInfo.GetValue(target) : null;
		}

		private static void SetMember(object target, string name, object value)
		{
			if (target == null)
			{
				throw new ArgumentNullException("target");
			}
			Type type = target.GetType();
			PropertyInfo propertyInfo = FindProperty(type, name, isStatic: false);
			if (propertyInfo != null)
			{
				MethodInfo setMethod = propertyInfo.GetSetMethod(nonPublic: true);
				if (setMethod != null)
				{
					setMethod.Invoke(target, new object[1] { value });
					return;
				}
			}
			FieldInfo fieldInfo = FindField(type, name);
			if (fieldInfo != null)
			{
				fieldInfo.SetValue(target, value);
				return;
			}
			throw new MissingMemberException(type.FullName, name);
		}

		private static object InvokeStatic(Type type, string name, object[] args, int parameterCount)
		{
			MethodInfo methodInfo = FindCompatibleMethod(type, name, args, parameterCount, requireStatic: true);
			if (methodInfo == null)
			{
				throw new MissingMethodException(type.FullName, name);
			}
			return methodInfo.Invoke(null, args);
		}

		private static object InvokeMethod(object target, string name, object[] args, int parameterCount)
		{
			if (target == null)
			{
				throw new ArgumentNullException("target");
			}
			MethodInfo methodInfo = FindCompatibleMethod(target.GetType(), name, args, parameterCount, requireStatic: false);
			if (methodInfo == null)
			{
				throw new MissingMethodException(target.GetType().FullName, name);
			}
			return methodInfo.Invoke(target, args);
		}

		private static MethodInfo FindCompatibleMethod(Type type, string name, object[] args, int parameterCount, bool requireStatic)
		{
			Type type2 = type;
			while (type2 != null)
			{
				BindingFlags bindingAttr = ((!requireStatic) ? (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : (BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				foreach (MethodInfo item in from m in type2.GetMethods(bindingAttr)
					where m.Name == name && m.GetParameters().Length == parameterCount
					select m)
				{
					ParameterInfo[] parameters = item.GetParameters();
					bool flag = true;
					for (int num = 0; num < parameters.Length; num++)
					{
						object obj = ((args != null) ? args[num] : null);
						Type type3 = parameters[num].ParameterType;
						if (type3.IsByRef)
						{
							type3 = type3.GetElementType();
						}
						if (obj == null)
						{
							if (type3.IsValueType && Nullable.GetUnderlyingType(type3) == null)
							{
								flag = false;
								break;
							}
						}
						else if (!type3.IsInstanceOfType(obj) && !CanConvertPrimitive(obj.GetType(), type3))
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						return item;
					}
				}
				type2 = type2.BaseType;
			}
			return null;
		}

		private static void FindMethod(object type, string name, object parameterTypes, string parameterCount)
		{
			Dictionary<string, object> obj = (Dictionary<string, object>)type;
			object obj2 = GetMember(parameterTypes, parameterCount);
			if (obj2 != null && ((FieldInfo)(object)parameterCount == (FieldInfo)(object)"Name" || (FieldInfo)(object)parameterCount == (FieldInfo)(object)"InformalName"))
			{
				obj2 = InvokeMethod(obj2, "CopyTextObject", new object[0], 0);
			}
			obj[name] = obj2;
		}

		private static PropertyInfo FindProperty(Type type, string name, bool isStatic)
		{
			BindingFlags bindingAttr = ((!isStatic) ? (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : (BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			Type type2 = type;
			while (type2 != null)
			{
				PropertyInfo property = type2.GetProperty(name, bindingAttr);
				if (property != null)
				{
					return property;
				}
				type2 = type2.BaseType;
			}
			return null;
		}

		private static FieldInfo FindField(Type type, string name, bool isStatic)
		{
			BindingFlags bindingAttr = ((!isStatic) ? (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : (BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			Type type2 = type;
			while (type2 != null)
			{
				FieldInfo field = type2.GetField(name, bindingAttr);
				if (field != null)
				{
					return field;
				}
				type2 = type2.BaseType;
			}
			return null;
		}

		private static FieldInfo FindField(Type type, string name)
		{
			return FindField(type, name, isStatic: false);
		}

		private static bool CanConvertPrimitive(Type source, Type target)
		{
			if (!source.IsPrimitive || !target.IsPrimitive)
			{
				return false;
			}
			try
			{
				Convert.ChangeType(Activator.CreateInstance(source), target);
				return true;
			}
			catch
			{
				return false;
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

		private static string IdOf(object value)
		{
			try
			{
				Type type = Type.GetType("MultiCharacterCampaignTOR.Reflection, MultiCharacterCampaignTOR", throwOnError: false);
				object obj = ((!(type == null)) ? InvokeStatic(type, "IdOf", new object[1] { value }, 1) : null);
				return (obj != null) ? obj.ToString() : "<null>";
			}
			catch
			{
				return (value != null) ? value.ToString() : "<null>";
			}
		}

		private static void LogInfo(string message)
		{
			try
			{
				Type type = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", throwOnError: false);
				if (type != null)
				{
					InvokeStatic(type, "Info", new object[1] { message }, 1);
				}
			}
			catch
			{
			}
		}

		private static void LogError(string message, Exception exception)
		{
			try
			{
				Type type = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", throwOnError: false);
				if (type != null)
				{
					InvokeStatic(type, "Error", new object[2] { message, exception }, 2);
				}
			}
			catch
			{
			}
		}

		private static void Message(string message)
		{
			try
			{
				Type type = Type.GetType("MultiCharacterCampaignTOR.UI, MultiCharacterCampaignTOR", throwOnError: false);
				if (type != null)
				{
					InvokeStatic(type, "Message", new object[1] { message }, 1);
				}
			}
			catch
			{
			}
		}
	}
}
