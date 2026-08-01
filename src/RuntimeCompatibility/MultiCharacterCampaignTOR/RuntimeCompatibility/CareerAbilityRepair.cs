// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
	internal static class CareerAbilityRepair
	{
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private const string AbilityUserAttribute = "AbilityUser";

		private static bool _installed;

		private static bool _loggedAbilityUserRepair;

		private static bool _loggedIdentityRepair;

		private static bool _loggedHudRebind;

		private static bool _loggedCareerInjection;

		private static bool _loggedComponentCreation;

		private static Type _agentType;

		private static Type _heroType;

		private static Type _mobilePartyType;

		private static Type _gameType;

		private static Type _agentExtensionsType;

		private static Type _heroExtensionsType;

		private static Type _abilityComponentType;

		private static Type _abilityHudMissionViewType;

		private static Type _abilityManagerMissionLogicType;

		private static Type _abilityFactoryType;

		private static Type _careerAbilityType;

		private static MethodInfo _getHeroMethod;

		private static MethodInfo _getCareerMethod;

		private static MethodInfo _hasAttributeMethod;

		private static MethodInfo _addAttributeMethod;

		private static MethodInfo _checkMainAgentMethod;

		private static MethodInfo _agentGetComponentDefinition;

		private static MethodInfo _agentIsHeroGetter;

		private static MethodInfo _agentAddComponentMethod;

		private static MethodInfo _abilityFactoryCreateNewMethod;

		private static MethodInfo _componentOnCastStartMethod;

		private static MethodInfo _componentOnCastCompleteMethod;

		private static MethodInfo _componentSelectAbilityByIndexMethod;

		private static MethodInfo _isCastingMissionMethod;

		private static ConstructorInfo _abilityComponentConstructor;

		private static PropertyInfo _agentMainProperty;

		private static PropertyInfo _heroMainHeroProperty;

		private static PropertyInfo _mainPartyProperty;

		private static PropertyInfo _leaderHeroProperty;

		private static PropertyInfo _gameCurrentProperty;

		private static PropertyInfo _playerTroopProperty;

		private static PropertyInfo _heroCharacterObjectProperty;

		private static PropertyInfo _careerAbilityProperty;

		private static PropertyInfo _knownAbilitySystemProperty;

		private static object _activeAbilityManager;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				ResolveAndCacheRuntimeMembers();
				Type type = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
				Type type2 = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", throwOnError: false);
				if (type == null || type2 == null)
				{
					throw new InvalidOperationException("Harmony 0Harmony assembly is unavailable.");
				}
				object harmony = Activator.CreateInstance(type, "xmarre.multicharactercampaign.tor.careerabilityinvariant.v140");
				MethodInfo original = FindUniqueMethod(_abilityManagerMissionLogicType, "OnAgentCreated", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 1);
				MethodInfo original2 = FindUniqueMethod(_abilityHudMissionViewType, "OnBehaviorInitialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 0);
				MethodInfo original3 = FindUniqueMethod(_abilityHudMissionViewType, "CheckMainAgent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 1);
				MethodInfo original4 = FindUniqueMethod(_abilityManagerMissionLogicType, "OnAgentControllerChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 2);
				MethodInfo original5 = FindUniqueMethod(_abilityManagerMissionLogicType, "OnEndMission", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 0);
				MethodInfo patchMethod = GetPatchMethod("OnAgentCreatedPrefix");
				MethodInfo patchMethod2 = GetPatchMethod("OnAgentCreatedPostfix");
				MethodInfo patchMethod3 = GetPatchMethod("AbilityComponentCtorPrefix");
				MethodInfo patchMethod4 = GetPatchMethod("HudInitPostfix");
				MethodInfo patchMethod5 = GetPatchMethod("CheckMainAgentPrefix");
				MethodInfo patchMethod6 = GetPatchMethod("OnAgentControllerChangedPostfix");
				MethodInfo patchMethod7 = GetPatchMethod("OnEndMissionPostfix");
				Patch(harmony, type, type2, original, patchMethod, patchMethod2);
				Patch(harmony, type, type2, _abilityComponentConstructor, patchMethod3, null);
				Patch(harmony, type, type2, original2, null, patchMethod4);
				Patch(harmony, type, type2, original3, patchMethod5, null);
				Patch(harmony, type, type2, original4, null, patchMethod6);
				Patch(harmony, type, type2, original5, null, patchMethod7);
				_installed = true;
				Log("Installed TOR career-ability invariant repair: spawn prerequisites, constructor identity, post-spawn validation, controller changes, HUD initialization, and every main-agent transition.");
			}
			catch (Exception ex)
			{
				Log("Failed to install TOR career-ability invariant repair: " + Unwrap(ex));
			}
		}

		private static void ResolveAndCacheRuntimeMembers()
		{
			_agentType = RequireType("TaleWorlds.MountAndBlade.Agent, TaleWorlds.MountAndBlade");
			_heroType = RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
			_mobilePartyType = RequireType("TaleWorlds.CampaignSystem.Party.MobileParty, TaleWorlds.CampaignSystem");
			_gameType = RequireType("TaleWorlds.Core.Game, TaleWorlds.Core");
			_agentExtensionsType = RequireType("TOR_Core.Extensions.AgentExtensions, TOR_Core");
			_heroExtensionsType = RequireType("TOR_Core.Extensions.HeroExtensions, TOR_Core");
			_abilityComponentType = RequireType("TOR_Core.AbilitySystem.AbilityComponent, TOR_Core");
			_abilityHudMissionViewType = RequireType("TOR_Core.AbilitySystem.AbilityHUDMissionView, TOR_Core");
			_abilityManagerMissionLogicType = RequireType("TOR_Core.AbilitySystem.AbilityManagerMissionLogic, TOR_Core");
			_abilityFactoryType = RequireType("TOR_Core.AbilitySystem.AbilityFactory, TOR_Core");
			_careerAbilityType = RequireType("TOR_Core.AbilitySystem.CareerAbility, TOR_Core");
			_getHeroMethod = FindUniqueMethod(_agentExtensionsType, "GetHero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 1);
			_getCareerMethod = FindUniqueMethod(_heroExtensionsType, "GetCareer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 1);
			_hasAttributeMethod = FindUniqueMethod(_heroExtensionsType, "HasAttribute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 2);
			_addAttributeMethod = FindUniqueMethod(_heroExtensionsType, "AddAttribute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 2);
			_checkMainAgentMethod = FindUniqueMethod(_abilityHudMissionViewType, "CheckMainAgent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 1);
			_agentIsHeroGetter = FindUniqueMethod(_agentType, "get_IsHero", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 0);
			_agentAddComponentMethod = FindUniqueMethod(_agentType, "AddComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 1);
			_abilityFactoryCreateNewMethod = FindUniqueMethod(_abilityFactoryType, "CreateNew", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 2);
			_componentOnCastStartMethod = FindUniqueMethod(_abilityComponentType, "OnCastStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 1);
			_componentOnCastCompleteMethod = FindUniqueMethod(_abilityComponentType, "OnCastComplete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 1);
			_isCastingMissionMethod = FindUniqueMethod(_abilityManagerMissionLogicType, "IsCastingMission", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 0);
			_componentSelectAbilityByIndexMethod = FindMethod(_abilityComponentType, "SelectAbility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, delegate(MethodInfo m)
			{
				ParameterInfo[] parameters = m.GetParameters();
				return parameters.Length == 1 && parameters[0].ParameterType == typeof(int);
			});
			_agentGetComponentDefinition = _agentType.GetMethods(BindingFlags.Instance | BindingFlags.Public).Single((MethodInfo m) => m.Name == "GetComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
			_abilityComponentConstructor = _abilityComponentType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { _agentType }, null);
			if (_abilityComponentConstructor == null)
			{
				throw new MissingMethodException(_abilityComponentType.FullName, ".ctor(Agent)");
			}
			_agentMainProperty = RequireProperty(_agentType, "Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_heroMainHeroProperty = RequireProperty(_heroType, "MainHero", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_mainPartyProperty = RequireProperty(_mobilePartyType, "MainParty", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_leaderHeroProperty = RequireProperty(_mobilePartyType, "LeaderHero", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_gameCurrentProperty = RequireProperty(_gameType, "Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			_playerTroopProperty = RequireProperty(_gameType, "PlayerTroop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_heroCharacterObjectProperty = RequireProperty(_heroType, "CharacterObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_careerAbilityProperty = RequireProperty(_abilityComponentType, "CareerAbility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_knownAbilitySystemProperty = RequireProperty(_abilityComponentType, "KnownAbilitySystem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

		private static MethodInfo FindMethod(Type type, string name, BindingFlags flags, Func<MethodInfo, bool> predicate)
		{
			MethodInfo[] array = (from m in type.GetMethods(flags)
				where m.Name == name && predicate(m)
				select m).ToArray();
			if (array.Length != 1)
			{
				throw new MissingMethodException(type.FullName, name + "; matches=" + array.Length);
			}
			return array[0];
		}

		private static MethodInfo GetPatchMethod(string name)
		{
			MethodInfo method = typeof(CareerAbilityRepair).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new MissingMethodException(typeof(CareerAbilityRepair).FullName, name);
			}
			return method;
		}

		private static void Patch(object harmony, Type harmonyType, Type harmonyMethodType, MethodBase original, MethodInfo prefix, MethodInfo postfix)
		{
			MethodInfo methodInfo = (from m in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
				where m.Name == "Patch"
				select m).First(delegate(MethodInfo m)
			{
				ParameterInfo[] parameters = m.GetParameters();
				return parameters.Length >= 3 && typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType);
			});
			object[] array = new object[methodInfo.GetParameters().Length];
			array[0] = original;
			array[1] = ((!(prefix == null)) ? Activator.CreateInstance(harmonyMethodType, prefix) : null);
			array[2] = ((!(postfix == null)) ? Activator.CreateInstance(harmonyMethodType, postfix) : null);
			methodInfo.Invoke(harmony, array);
		}

		private static void OnAgentCreatedPrefix(object __instance, object __0)
		{
			try
			{
				RememberAbilityManager(__instance);
				if (!IsHeroAgent(__0))
				{
					return;
				}
				object hero = GetHero(__0);
				if (hero != null && GetCareer(hero) != null)
				{
					object value = _heroMainHeroProperty.GetValue(null, null);
					object mainPartyLeader = GetMainPartyLeader();
					object value2 = _agentMainProperty.GetValue(null, null);
					if ((hero == value || hero == mainPartyLeader || __0 == value2) && EnsureAbilityUser(hero) && !_loggedAbilityUserRepair)
					{
						_loggedAbilityUserRepair = true;
						Log("Restored TOR AbilityUser prerequisite before OnAgentCreated for hero=" + GetStringId(hero) + ".");
					}
				}
			}
			catch (Exception ex)
			{
				Log("OnAgentCreated prerequisite repair failed: " + Unwrap(ex));
			}
		}

		private static void OnAgentCreatedPostfix(object __instance, object __0)
		{
			try
			{
				RememberAbilityManager(__instance);
				if (IsPlayerCandidateAgent(__0) && GetAbilityComponent(__0) != null)
				{
					EnsureCareerInvariant(__0, "OnAgentCreated postfix", allowCreateComponent: false);
				}
			}
			catch (Exception ex)
			{
				Log("OnAgentCreated invariant validation failed: " + Unwrap(ex));
			}
		}

		private static void AbilityComponentCtorPrefix(object __0)
		{
			try
			{
				if (!IsHeroAgent(__0))
				{
					return;
				}
				object hero = GetHero(__0);
				if (hero == null || GetCareer(hero) == null)
				{
					return;
				}
				object value = _heroMainHeroProperty.GetValue(null, null);
				if (hero == value)
				{
					return;
				}
				object mainPartyLeader = GetMainPartyLeader();
				if (hero != mainPartyLeader)
				{
					return;
				}
				object value2 = _gameCurrentProperty.GetValue(null, null);
				object value3 = _heroCharacterObjectProperty.GetValue(hero, null);
				if (value2 != null && value3 != null && _playerTroopProperty.CanWrite)
				{
					_playerTroopProperty.SetValue(value2, value3, null);
					if (_heroMainHeroProperty.GetValue(null, null) != hero)
					{
						throw new InvalidOperationException("PlayerTroop rebind did not establish constructor invariant. agentHero=" + GetStringId(hero));
					}
					EnsureAbilityUser(hero);
					if (!_loggedIdentityRepair)
					{
						_loggedIdentityRepair = true;
						Log("Rebound Game.PlayerTroop before AbilityComponent construction for active shared hero=" + GetStringId(hero) + ".");
					}
				}
			}
			catch (Exception ex)
			{
				Log("AbilityComponent constructor identity repair failed: " + Unwrap(ex));
			}
		}

		private static void HudInitPostfix(object __instance)
		{
			try
			{
				if (__instance == null)
				{
					return;
				}
				object value = _agentMainProperty.GetValue(null, null);
				if (value != null)
				{
					EnsureCareerInvariant(value, "HUD initialization", CanCreateMissingComponent(null));
					_checkMainAgentMethod.Invoke(__instance, new object[1]);
					if (!_loggedHudRebind)
					{
						_loggedHudRebind = true;
						Log("Rebound TOR career HUD after initialization with career invariant validated first.");
					}
				}
			}
			catch (Exception ex)
			{
				Log("HUD initialization repair failed: " + Unwrap(ex));
			}
		}

		private static void CheckMainAgentPrefix()
		{
			try
			{
				object value = _agentMainProperty.GetValue(null, null);
				if (value != null)
				{
					EnsureCareerInvariant(value, "main-agent transition", CanCreateMissingComponent(null));
				}
			}
			catch (Exception ex)
			{
				Log("Main-agent transition career repair failed: " + Unwrap(ex));
			}
		}

		private static void OnAgentControllerChangedPostfix(object __instance, object __0)
		{
			try
			{
				RememberAbilityManager(__instance);
				if (__0 != null)
				{
					object value = _agentMainProperty.GetValue(null, null);
					if (__0 == value)
					{
						EnsureCareerInvariant(__0, "main-agent controller change", CanCreateMissingComponent(__instance));
					}
				}
			}
			catch (Exception ex)
			{
				Log("Main-agent controller-change career repair failed: " + Unwrap(ex));
			}
		}

		private static void OnEndMissionPostfix(object __instance)
		{
			if (_activeAbilityManager == __instance)
			{
				_activeAbilityManager = null;
			}
		}

		private static void RememberAbilityManager(object manager)
		{
			if (manager != null && _abilityManagerMissionLogicType.IsInstanceOfType(manager))
			{
				_activeAbilityManager = manager;
			}
		}

		private static bool CanCreateMissingComponent(object preferredManager)
		{
			object obj = preferredManager;
			if (obj == null || !_abilityManagerMissionLogicType.IsInstanceOfType(obj))
			{
				obj = _activeAbilityManager;
			}
			if (obj == null || !_abilityManagerMissionLogicType.IsInstanceOfType(obj))
			{
				return false;
			}
			try
			{
				return Convert.ToBoolean(_isCastingMissionMethod.Invoke(obj, null));
			}
			catch
			{
				return false;
			}
		}

		private static bool IsPlayerCandidateAgent(object agent)
		{
			if (!IsHeroAgent(agent))
			{
				return false;
			}
			object hero = GetHero(agent);
			if (hero == null || GetCareer(hero) == null)
			{
				return false;
			}
			object value = _agentMainProperty.GetValue(null, null);
			if (agent == value)
			{
				return true;
			}
			object value2 = _heroMainHeroProperty.GetValue(null, null);
			if (hero == value2)
			{
				return true;
			}
			return hero == GetMainPartyLeader();
		}

		private static bool IsHeroAgent(object agent)
		{
			if (agent != null)
			{
				return Convert.ToBoolean(_agentIsHeroGetter.Invoke(agent, null));
			}
			return false;
		}

		private static object GetHero(object agent)
		{
			return _getHeroMethod.Invoke(null, new object[1] { agent });
		}

		private static object GetCareer(object hero)
		{
			if (hero == null)
			{
				return null;
			}
			return _getCareerMethod.Invoke(null, new object[1] { hero });
		}

		private static object GetMainPartyLeader()
		{
			object value = _mainPartyProperty.GetValue(null, null);
			if (value == null)
			{
				return null;
			}
			return _leaderHeroProperty.GetValue(value, null);
		}

		private static bool EnsureAbilityUser(object hero)
		{
			if (Convert.ToBoolean(_hasAttributeMethod.Invoke(null, new object[2] { hero, "AbilityUser" })))
			{
				return false;
			}
			_addAttributeMethod.Invoke(null, new object[2] { hero, "AbilityUser" });
			return true;
		}

		private static object GetAbilityComponent(object agent)
		{
			return _agentGetComponentDefinition.MakeGenericMethod(_abilityComponentType).Invoke(agent, null);
		}

		private static void EnsureCareerInvariant(object agent, string source, bool allowCreateComponent)
		{
			if (!IsHeroAgent(agent))
			{
				return;
			}
			object hero = GetHero(agent);
			object career = GetCareer(hero);
			if (hero == null || career == null)
			{
				return;
			}
			EnsureAbilityUser(hero);
			object obj = GetAbilityComponent(agent);
			bool flag = false;
			if (obj == null)
			{
				if (!allowCreateComponent)
				{
					return;
				}
				obj = _abilityComponentConstructor.Invoke(new object[1] { agent });
				if (obj == null)
				{
					throw new InvalidOperationException("AbilityComponent constructor returned null.");
				}
				flag = true;
			}
			bool flag2 = false;
			object obj2 = _careerAbilityProperty.GetValue(obj, null);
			if (obj2 == null)
			{
				obj2 = FindExistingCareerAbility(obj);
				if (obj2 != null)
				{
					_careerAbilityProperty.SetValue(obj, obj2, null);
				}
				else
				{
					obj2 = InjectCareerAbility(obj, career, agent);
					flag2 = true;
					if (!_loggedCareerInjection)
					{
						_loggedCareerInjection = true;
						Log("Injected missing TOR CareerAbility using TOR's own AbilityFactory and event wiring. hero=" + GetStringId(hero) + "; source=" + source + ".");
					}
				}
			}
			else
			{
				EnsureKnownAbilityMembership(obj, obj2);
			}
			if (obj2 == null || !_careerAbilityType.IsInstanceOfType(obj2))
			{
				throw new InvalidOperationException("CareerAbility invariant could not be established for hero=" + GetStringId(hero));
			}
			if (flag)
			{
				if (flag2)
				{
					_componentSelectAbilityByIndexMethod.Invoke(obj, new object[1] { 0 });
				}
				object abilityComponent = GetAbilityComponent(agent);
				if (abilityComponent == null)
				{
					_agentAddComponentMethod.Invoke(agent, new object[1] { obj });
				}
				else
				{
					EnsureExistingComponentInvariant(abilityComponent, career, agent);
					obj = abilityComponent;
				}
				if (!_loggedComponentCreation)
				{
					_loggedComponentCreation = true;
					Log("Created and attached a missing TOR AbilityComponent for main-agent career hero=" + GetStringId(hero) + "; source=" + source + ".");
				}
			}
			object abilityComponent2 = GetAbilityComponent(agent);
			object obj3 = ((abilityComponent2 != null) ? _careerAbilityProperty.GetValue(abilityComponent2, null) : null);
			if (abilityComponent2 != null && obj3 != null)
			{
				return;
			}
			throw new InvalidOperationException("Post-repair verification failed. hero=" + GetStringId(hero) + "; component=" + ((abilityComponent2 != null) ? "present" : "missing") + "; CareerAbility=" + ((obj3 != null) ? "present" : "missing"));
		}

		private static void EnsureExistingComponentInvariant(object component, object career, object agent)
		{
			object obj = _careerAbilityProperty.GetValue(component, null);
			if (obj == null)
			{
				obj = FindExistingCareerAbility(component);
				if (obj != null)
				{
					_careerAbilityProperty.SetValue(component, obj, null);
				}
				else
				{
					obj = InjectCareerAbility(component, career, agent);
				}
			}
			else
			{
				EnsureKnownAbilityMembership(component, obj);
			}
			if (obj == null || !_careerAbilityType.IsInstanceOfType(obj))
			{
				throw new InvalidOperationException("Existing AbilityComponent could not be repaired.");
			}
		}

		private static object FindExistingCareerAbility(object component)
		{
			if (!(_knownAbilitySystemProperty.GetValue(component, null) is IEnumerable enumerable))
			{
				return null;
			}
			foreach (object item in enumerable)
			{
				if (item != null && _careerAbilityType.IsInstanceOfType(item))
				{
					return item;
				}
			}
			return null;
		}

		private static object InjectCareerAbility(object component, object career, object agent)
		{
			PropertyInfo property = career.GetType().GetProperty("AbilityTemplateID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property == null)
			{
				throw new MissingMemberException(career.GetType().FullName, "AbilityTemplateID");
			}
			string text = Convert.ToString(property.GetValue(career, null));
			if (string.IsNullOrEmpty(text))
			{
				throw new InvalidOperationException("Career has no AbilityTemplateID.");
			}
			object obj = _abilityFactoryCreateNewMethod.Invoke(null, new object[2] { text, agent });
			if (obj == null || !_careerAbilityType.IsInstanceOfType(obj))
			{
				throw new InvalidOperationException("AbilityFactory.CreateNew did not return CareerAbility for template=" + text + ".");
			}
			EventInfo eventInfo = FindEvent(obj.GetType(), "OnCastStart");
			EventInfo eventInfo2 = FindEvent(obj.GetType(), "OnCastComplete");
			Delegate handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, component, _componentOnCastStartMethod, throwOnBindFailure: true);
			Delegate handler2 = Delegate.CreateDelegate(eventInfo2.EventHandlerType, component, _componentOnCastCompleteMethod, throwOnBindFailure: true);
			object value = _knownAbilitySystemProperty.GetValue(component, null);
			if (value == null)
			{
				throw new InvalidOperationException("AbilityComponent.KnownAbilitySystem is null.");
			}
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			bool flag4 = false;
			try
			{
				_careerAbilityProperty.SetValue(component, obj, null);
				flag = true;
				eventInfo.AddEventHandler(obj, handler);
				flag2 = true;
				eventInfo2.AddEventHandler(obj, handler2);
				flag3 = true;
				InsertKnownAbilityAtFront(value, obj);
				flag4 = true;
				return obj;
			}
			catch
			{
				if (flag4)
				{
					RemoveKnownAbility(value, obj);
				}
				if (flag3)
				{
					SafeRemoveEventHandler(eventInfo2, obj, handler2);
				}
				if (flag2)
				{
					SafeRemoveEventHandler(eventInfo, obj, handler);
				}
				if (flag)
				{
					_careerAbilityProperty.SetValue(component, null, null);
				}
				throw;
			}
		}

		private static EventInfo FindEvent(Type type, string name)
		{
			EventInfo eventInfo = type.GetEvent(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (eventInfo != null)
			{
				return eventInfo;
			}
			Type baseType = type.BaseType;
			while (baseType != null)
			{
				eventInfo = baseType.GetEvent(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (eventInfo != null)
				{
					return eventInfo;
				}
				baseType = baseType.BaseType;
			}
			throw new MissingMemberException(type.FullName, name);
		}

		private static void EnsureKnownAbilityMembership(object component, object ability)
		{
			object value = _knownAbilitySystemProperty.GetValue(component, null);
			if (value == null)
			{
				throw new InvalidOperationException("AbilityComponent.KnownAbilitySystem is null.");
			}
			if (value is IList list)
			{
				if (!list.Contains(ability))
				{
					list.Insert(0, ability);
				}
			}
			else if (!Convert.ToBoolean(FindCollectionMethod(value.GetType(), "Contains", 1, ability).Invoke(value, new object[1] { ability })))
			{
				InsertKnownAbilityAtFront(value, ability);
			}
		}

		private static void InsertKnownAbilityAtFront(object known, object ability)
		{
			if (known is IList list)
			{
				list.Insert(0, ability);
				return;
			}
			known.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single(delegate(MethodInfo m)
			{
				if (m.Name != "Insert")
				{
					return false;
				}
				ParameterInfo[] parameters = m.GetParameters();
				return parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType.IsAssignableFrom(ability.GetType());
			})
				.Invoke(known, new object[2] { 0, ability });
		}

		private static void RemoveKnownAbility(object known, object ability)
		{
			try
			{
				if (known is IList list)
				{
					list.Remove(ability);
					return;
				}
				FindCollectionMethod(known.GetType(), "Remove", 1, ability).Invoke(known, new object[1] { ability });
			}
			catch
			{
			}
		}

		private static MethodInfo FindCollectionMethod(Type type, string name, int count, object value)
		{
			return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single(delegate(MethodInfo m)
			{
				if (m.Name != name)
				{
					return false;
				}
				ParameterInfo[] parameters = m.GetParameters();
				return parameters.Length == count && parameters[0].ParameterType.IsAssignableFrom(value.GetType());
			});
		}

		private static void SafeRemoveEventHandler(EventInfo eventInfo, object target, Delegate handler)
		{
			try
			{
				eventInfo.RemoveEventHandler(target, handler);
			}
			catch
			{
			}
		}

		private static string GetStringId(object value)
		{