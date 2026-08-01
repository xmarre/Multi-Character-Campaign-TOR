// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Linq;
using System.Reflection;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	public static class RuntimeIdentityGuard
	{
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static bool _installed;

		private static bool _repairing;

		private static Type _behaviorType;

		private static Type _heroType;

		private static Type _mobilePartyType;

		private static Type _gameType;

		private static MethodInfo _isRegisteredSharedHero;

		private static PropertyInfo _behaviorInstanceProperty;

		private static FieldInfo _switchInProgressField;

		private static FieldInfo _founderHeroIdField;

		private static FieldInfo _activeHeroIdField;

		private static FieldInfo _loadedActiveHeroIdField;

		private static PropertyInfo _heroMainHeroProperty;

		private static PropertyInfo _mainPartyProperty;

		private static PropertyInfo _partyLeaderHeroProperty;

		private static PropertyInfo _heroCharacterObjectProperty;

		private static PropertyInfo _gameCurrentProperty;

		private static PropertyInfo _playerTroopProperty;

		private static MethodInfo _logInfoMethod;

		public static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				ResolveMembers();
				Type type = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
				Type type2 = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", throwOnError: false);
				if (type == null || type2 == null)
				{
					throw new InvalidOperationException("Harmony 2 runtime types are unavailable.");
				}
				object harmony = Activator.CreateInstance(type, "xmarre.multicharactercampaign.tor.identityguard.v133");
				MethodInfo method = typeof(RuntimeIdentityGuard).GetMethod("AfterPlayerTroopSet", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method2 = typeof(RuntimeIdentityGuard).GetMethod("BeforeCharacterDeveloperStateCreated", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method3 = typeof(RuntimeIdentityGuard).GetMethod("BeforeInitializeState", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo setMethod = _playerTroopProperty.GetSetMethod(nonPublic: true);
				if (setMethod == null)
				{
					throw new MissingMethodException(_gameType.FullName, "set_PlayerTroop");
				}
				Patch(harmony, type, type2, setMethod, null, method);
				MethodInfo original = _behaviorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == "InitializeState" && m.GetParameters().Length == 1);
				Patch(harmony, type, type2, original, method3, null);
				Type type3 = Type.GetType("TaleWorlds.CampaignSystem.GameState.CharacterDeveloperState, TaleWorlds.CampaignSystem", throwOnError: false);
				if (type3 != null)
				{
					ConstructorInfo[] constructors = type3.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (ConstructorInfo original2 in constructors)
					{
						Patch(harmony, type, type2, original2, method2, null);
					}
				}
				_installed = true;
				RemotePartySwitch.Install();
				BattleInterventionAlert.Install();
				CampaignMapHotkey.Install();
				Log("v1.1.0 sidecar loaded; installed identity recovery, remote-party control, defensive-battle alerts, and native encounter continuation.");
			}
			catch (Exception ex)
			{
				Log("Failed to install active-character identity guard: " + Unwrap(ex));
			}
		}

		private static void ResolveMembers()
		{
			_behaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");
			_heroType = RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
			_mobilePartyType = RequireType("TaleWorlds.CampaignSystem.Party.MobileParty, TaleWorlds.CampaignSystem");
			_gameType = RequireType("TaleWorlds.Core.Game, TaleWorlds.Core");
			_behaviorInstanceProperty = RequireProperty(_behaviorType, "Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_switchInProgressField = _behaviorType.GetField("_switchInProgress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_founderHeroIdField = _behaviorType.GetField("_founderHeroId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_activeHeroIdField = _behaviorType.GetField("_activeHeroId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_loadedActiveHeroIdField = _behaviorType.GetField("_loadedActiveHeroId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (_switchInProgressField == null || _founderHeroIdField == null || _activeHeroIdField == null || _loadedActiveHeroIdField == null)
			{
				throw new MissingFieldException(_behaviorType.FullName, "shared-character identity state fields");
			}
			_isRegisteredSharedHero = _behaviorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SingleOrDefault((MethodInfo m) => m.Name == "IsRegisteredSharedHero" && m.GetParameters().Length == 1);
			if (_isRegisteredSharedHero == null)
			{
				throw new MissingMethodException(_behaviorType.FullName, "IsRegisteredSharedHero");
			}
			_heroMainHeroProperty = RequireProperty(_heroType, "MainHero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_heroCharacterObjectProperty = RequireProperty(_heroType, "CharacterObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_mainPartyProperty = RequireProperty(_mobilePartyType, "MainParty", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_partyLeaderHeroProperty = RequireProperty(_mainPartyProperty.PropertyType, "LeaderHero", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_gameCurrentProperty = RequireProperty(_gameType, "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_playerTroopProperty = RequireProperty(_gameType, "PlayerTroop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (!_playerTroopProperty.CanRead || !_playerTroopProperty.CanWrite)
			{
				throw new MissingMemberException(_gameType.FullName, "read/write PlayerTroop");
			}
			Type type = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", throwOnError: false);
			if (type != null)
			{
				_logInfoMethod = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "Info" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
			}
		}

		private static void AfterPlayerTroopSet()
		{
			RepairFromRegisteredMainPartyLeader("Game.PlayerTroop setter");
		}

		private static void BeforeInitializeState()
		{
			RepairFromRegisteredMainPartyLeader("campaign state initialization");
		}

		private static void BeforeCharacterDeveloperStateCreated()
		{
			RepairFromRegisteredMainPartyLeader("CharacterDeveloperState construction");
		}

		private static void RepairFromRegisteredMainPartyLeader(string source)
		{
			if (_repairing)
			{
				return;
			}
			try
			{
				object value = _behaviorInstanceProperty.GetValue(null, null);
				if (value == null)
				{
					return;
				}
				object value2 = _switchInProgressField.GetValue(value);
				if (value2 is bool && (bool)value2)
				{
					return;
				}
				object value3 = _mainPartyProperty.GetValue(null, null);
				if (value3 == null)
				{
					return;
				}
				object value4 = _partyLeaderHeroProperty.GetValue(value3, null);
				if (value4 == null || !Convert.ToBoolean(_isRegisteredSharedHero.Invoke(value, new object[1] { value4 })))
				{
					return;
				}
				object value5 = _heroMainHeroProperty.GetValue(null, null);
				if (value5 == value4)
				{
					return;
				}
				string text = _founderHeroIdField.GetValue(value) as string;
				string heroId = GetHeroId(value5);
				if (value5 == null || string.IsNullOrEmpty(text) || !string.Equals(heroId, text, StringComparison.Ordinal))
				{
					return;
				}
				object value6 = _gameCurrentProperty.GetValue(null, null);
				object value7 = _heroCharacterObjectProperty.GetValue(value4, null);
				if (value6 != null && value7 != null)
				{
					_repairing = true;
					try
					{
						_playerTroopProperty.SetValue(value6, value7, null);
					}
					finally
					{
						_repairing = false;
					}
					if (_heroMainHeroProperty.GetValue(null, null) != value4)
					{
						throw new InvalidOperationException("PlayerTroop repair did not restore Hero.MainHero to the registered main-party leader.");
					}
					RepairActiveHeroIds(value, value4, text);
					Log("Repaired stale founder PlayerTroop at " + source + "; restored active shared hero=" + GetHeroId(value4) + ".");
				}
			}
			catch (Exception ex)
			{
				_repairing = false;
				Log("Active-character identity repair failed safely at " + source + ": " + Unwrap(ex));
			}
		}

		private static void RepairActiveHeroIds(object behavior, object leader, string founderHeroId)
		{
			string heroId = GetHeroId(leader);
			if (!string.IsNullOrEmpty(heroId))
			{
				if (!string.Equals(_activeHeroIdField.GetValue(behavior) as string, heroId, StringComparison.Ordinal))
				{
					_activeHeroIdField.SetValue(behavior, heroId);
				}
				string a = _loadedActiveHeroIdField.GetValue(behavior) as string;
				if (!string.IsNullOrEmpty(founderHeroId) && string.Equals(a, founderHeroId, StringComparison.Ordinal))
				{
					_loadedActiveHeroIdField.SetValue(behavior, heroId);
				}
			}
		}

		private static string GetHeroId(object hero)
		{
			if (hero == null)
			{
				return string.Empty;
			}
			Type type = hero.GetType();
			PropertyInfo property = type.GetProperty("StringId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanRead)
			{
				object value = property.GetValue(hero, null);
				if (value != null)
				{
					return value.ToString();
				}
			}
			FieldInfo field = type.GetField("StringId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object obj = ((!(field == null)) ? field.GetValue(hero) : null);
			if (obj == null)
			{
				return string.Empty;
			}
			return obj.ToString();
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix)
		{
			object obj = ((!(prefix == null)) ? Activator.CreateInstance(harmonyMethodType, prefix) : null);
			object obj2 = ((!(postfix == null)) ? Activator.CreateInstance(harmonyMethodType, postfix) : null);
			MethodInfo methodInfo = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public).First((MethodInfo m) => m.Name == "Patch" && m.GetParameters().Length >= 4);
			object[] array = new object[methodInfo.GetParameters().Length];
			array[0] = original;
			array[1] = obj;
			array[2] = obj2;
			methodInfo.Invoke(harmony, array);
		}

		private static Type RequireType(string assemblyQualifiedName)
		{
			Type type = Type.GetType(assemblyQualifiedName, throwOnError: false);
			if (type == null)
			{
				throw new TypeLoadException(assemblyQualifiedName);
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
			Exception ex2 = ex;
			while (ex2 is TargetInvocationException && ex2.InnerException != null)
			{
				ex2 = ex2.InnerException;
			}
			return ex2.GetType().Name + ": " + ex2.Message;
		}

		private static void Log(string message)
		{
			try
			{
				if (_logInfoMethod != null)
				{
					_logInfoMethod.Invoke(null, new object[1] { "[IdentityGuard] " + message });
				}
			}
			catch
			{
			}
		}
	}
}
