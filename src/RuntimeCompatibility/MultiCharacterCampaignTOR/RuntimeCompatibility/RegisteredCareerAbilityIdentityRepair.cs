using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Establishes the TOR career-ability object for every registered MCC career hero independently
    /// of the agent's transient controller state or whether the hero also knows spells/prayers.
    ///
    /// TOR's AbilityComponent constructor creates CareerAbility only for Hero.MainHero. AI spellcasters
    /// usually mask that assumption because their selected spells still populate KnownAbilitySystem and
    /// therefore create the normal casting/WizardAI path. A non-caster career such as Waywatcher can
    /// instead spawn with an AbilityComponent whose career slot and known-ability list are empty.
    ///
    /// This repair owns only stable career identity: registered hero + career => AbilityComponent +
    /// CareerAbility. Existing AICareerAbilitySupport remains responsible for WizardAI and AI-only casting
    /// behavior once Bannerlord reports the agent as AI-controlled.
    /// </summary>
    internal static class RegisteredCareerAbilityIdentityRepair
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;
        private static Type _agentType;
        private static Type _abilityType;
        private static Type _abilityComponentType;
        private static Type _careerAbilityType;
        private static Type _abilityFactoryType;
        private static MethodInfo _agentGetComponentDefinition;
        private static MethodInfo _agentAddComponentMethod;
        private static MethodInfo _getHeroMethod;
        private static MethodInfo _getCareerMethod;
        private static MethodInfo _hasAttributeMethod;
        private static MethodInfo _addAttributeMethod;
        private static MethodInfo _abilityFactoryCreateNewMethod;
        private static MethodInfo _abilityComponentOnCastStartMethod;
        private static MethodInfo _abilityComponentOnCastCompleteMethod;
        private static MethodInfo _isCastingMissionMethod;
        private static MethodInfo _isRegisteredSharedHeroMethod;
        private static PropertyInfo _behaviorInstanceProperty;
        private static PropertyInfo _knownAbilitySystemProperty;
        private static PropertyInfo _careerAbilityProperty;
        private static PropertyInfo _currentAbilityProperty;
        private static PropertyInfo _careerTemplateIdProperty;
        private static ConstructorInfo _abilityComponentConstructor;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                ResolveRuntimeSurfaces();

                Type managerType = RequireType("TOR_Core.AbilitySystem.AbilityManagerMissionLogic, TOR_Core");
                MethodInfo onAgentCreated = FindUniqueMethod(managerType, "OnAgentCreated", InstanceFlags, 1);
                MethodInfo onControllerChanged = FindUniqueMethod(managerType, "OnAgentControllerChanged", InstanceFlags, 2);

                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.registered-career-identity.v136");
                harmony.Patch(
                    onAgentCreated,
                    postfix: new HarmonyMethod(typeof(RegisteredCareerAbilityIdentityRepair), nameof(AfterAgentCreated)));
                harmony.Patch(
                    onControllerChanged,
                    postfix: new HarmonyMethod(typeof(RegisteredCareerAbilityIdentityRepair), nameof(AfterControllerChanged)));

                _installed = true;
                Log("Installed controller-independent registered-career identity repair.");
            }
            catch (Exception ex)
            {
                Log("Registered-career identity repair installation failed safely: " + Unwrap(ex));
            }
        }

        private static void ResolveRuntimeSurfaces()
        {
            _agentType = RequireType("TaleWorlds.MountAndBlade.Agent, TaleWorlds.MountAndBlade");
            _abilityType = RequireType("TOR_Core.AbilitySystem.Ability, TOR_Core");
            _abilityComponentType = RequireType("TOR_Core.AbilitySystem.AbilityComponent, TOR_Core");
            _careerAbilityType = RequireType("TOR_Core.AbilitySystem.CareerAbility, TOR_Core");
            _abilityFactoryType = RequireType("TOR_Core.AbilitySystem.AbilityFactory, TOR_Core");
            Type managerType = RequireType("TOR_Core.AbilitySystem.AbilityManagerMissionLogic, TOR_Core");
            Type agentExtensionsType = RequireType("TOR_Core.Extensions.AgentExtensions, TOR_Core");
            Type heroExtensionsType = RequireType("TOR_Core.Extensions.HeroExtensions, TOR_Core");
            Type behaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");

            _agentGetComponentDefinition = _agentType.GetMethods(InstanceFlags)
                .Single(method => method.Name == "GetComponent" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            _agentAddComponentMethod = FindUniqueMethod(_agentType, "AddComponent", InstanceFlags, 1);
            _getHeroMethod = FindUniqueMethod(agentExtensionsType, "GetHero", StaticFlags, 1);
            _getCareerMethod = FindUniqueMethod(heroExtensionsType, "GetCareer", StaticFlags, 1);
            _hasAttributeMethod = FindUniqueMethod(heroExtensionsType, "HasAttribute", StaticFlags, 2);
            _addAttributeMethod = FindUniqueMethod(heroExtensionsType, "AddAttribute", StaticFlags, 2);
            _abilityFactoryCreateNewMethod = _abilityFactoryType.GetMethods(StaticFlags)
                .Single(method => method.Name == "CreateNew" && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType == _agentType);
            _abilityComponentOnCastStartMethod = FindUniqueMethod(_abilityComponentType, "OnCastStart", InstanceFlags, 1);
            _abilityComponentOnCastCompleteMethod = FindUniqueMethod(_abilityComponentType, "OnCastComplete", InstanceFlags, 1);
            _isCastingMissionMethod = FindUniqueMethod(managerType, "IsCastingMission", InstanceFlags, 0);
            _behaviorInstanceProperty = RequireProperty(behaviorType, "Instance", StaticFlags);
            _isRegisteredSharedHeroMethod = FindUniqueMethod(behaviorType, "IsRegisteredSharedHero", InstanceFlags, 1);
            _knownAbilitySystemProperty = RequireProperty(_abilityComponentType, "KnownAbilitySystem", InstanceFlags);
            _careerAbilityProperty = RequireProperty(_abilityComponentType, "CareerAbility", InstanceFlags);
            _currentAbilityProperty = RequireProperty(_abilityComponentType, "CurrentAbility", InstanceFlags);
            _abilityComponentConstructor = _abilityComponentType.GetConstructors(InstanceFlags)
                .Single(ctor => ctor.GetParameters().Length == 1 && ctor.GetParameters()[0].ParameterType == _agentType);
        }

        private static void AfterAgentCreated(object __instance, object __0)
        {
            EnsureRegisteredCareerIdentity(__instance, __0, "agent created");
        }

        private static void AfterControllerChanged(object __instance, object __0)
        {
            EnsureRegisteredCareerIdentity(__instance, __0, "controller changed");
        }

        private static void EnsureRegisteredCareerIdentity(object manager, object agent, string source)
        {
            try
            {
                if (!TryGetRegisteredCareer(agent, out object hero, out object career))
                {
                    return;
                }

                EnsureAbilityUser(hero);

                if (manager == null || !Convert.ToBoolean(_isCastingMissionMethod.Invoke(manager, null)))
                {
                    return;
                }

                object component = GetAgentComponent(agent, _abilityComponentType);
                bool componentCreated = false;
                if (component == null)
                {
                    component = _abilityComponentConstructor.Invoke(new[] { agent });
                    _agentAddComponentMethod.Invoke(agent, new[] { component });
                    componentCreated = true;
                }

                IList known = _knownAbilitySystemProperty.GetValue(component, null) as IList;
                object careerAbility = _careerAbilityProperty.GetValue(component, null);
                if (careerAbility == null && known != null)
                {
                    foreach (object ability in known)
                    {
                        if (ability != null && _careerAbilityType.IsInstanceOfType(ability))
                        {
                            careerAbility = ability;
                            break;
                        }
                    }
                }

                bool careerCreated = false;
                if (careerAbility == null)
                {
                    string templateId = GetCareerTemplateId(career);
                    if (string.IsNullOrWhiteSpace(templateId))
                    {
                        return;
                    }

                    careerAbility = _abilityFactoryCreateNewMethod.Invoke(null, new object[] { templateId, agent });
                    if (careerAbility == null || !_careerAbilityType.IsInstanceOfType(careerAbility))
                    {
                        return;
                    }

                    SubscribeComponentEvents(component, careerAbility);
                    careerCreated = true;
                }

                _careerAbilityProperty.SetValue(component, careerAbility, null);
                if (known != null && !ContainsReference(known, careerAbility))
                {
                    known.Add(careerAbility);
                }

                if (_currentAbilityProperty.GetValue(component, null) == null)
                {
                    _currentAbilityProperty.SetValue(component, careerAbility, null);
                }

                if (componentCreated || careerCreated)
                {
                    Log("Established registered career identity hero=" + SafeHeroId(hero) +
                        " source=" + source +
                        " componentCreated=" + componentCreated +
                        " careerCreated=" + careerCreated + ".");
                }
            }
            catch (Exception ex)
            {
                Log("Registered-career identity repair failed safely source=" + source + ": " + Unwrap(ex));
            }
        }

        private static bool TryGetRegisteredCareer(object agent, out object hero, out object career)
        {
            hero = null;
            career = null;
            if (agent == null || !_agentType.IsInstanceOfType(agent))
            {
                return false;
            }

            hero = _getHeroMethod.Invoke(null, new[] { agent });
            if (hero == null)
            {
                return false;
            }

            object behavior = _behaviorInstanceProperty.GetValue(null, null);
            if (behavior == null || !Convert.ToBoolean(_isRegisteredSharedHeroMethod.Invoke(behavior, new[] { hero })))
            {
                return false;
            }

            career = _getCareerMethod.Invoke(null, new[] { hero });
            return career != null;
        }

        private static void EnsureAbilityUser(object hero)
        {
            if (!Convert.ToBoolean(_hasAttributeMethod.Invoke(null, new object[] { hero, "AbilityUser" })))
            {
                _addAttributeMethod.Invoke(null, new object[] { hero, "AbilityUser" });
            }
        }

        private static string GetCareerTemplateId(object career)
        {
            PropertyInfo property = _careerTemplateIdProperty;
            if (property == null || property.DeclaringType == null || !property.DeclaringType.IsInstanceOfType(career))
            {
                property = career.GetType().GetProperty("AbilityTemplateID", InstanceFlags);
                if (property == null)
                {
                    throw new MissingMemberException(career.GetType().FullName, "AbilityTemplateID");
                }
                _careerTemplateIdProperty = property;
            }

            return property.GetValue(career, null) as string;
        }

        private static void SubscribeComponentEvents(object component, object careerAbility)
        {
            EventInfo onCastStart = _abilityType.GetEvent("OnCastStart", InstanceFlags);
            EventInfo onCastComplete = _abilityType.GetEvent("OnCastComplete", InstanceFlags);
            if (onCastStart == null || onCastComplete == null)
            {
                return;
            }

            Delegate start = Delegate.CreateDelegate(onCastStart.EventHandlerType, component, _abilityComponentOnCastStartMethod);
            Delegate complete = Delegate.CreateDelegate(onCastComplete.EventHandlerType, component, _abilityComponentOnCastCompleteMethod);
            onCastStart.AddEventHandler(careerAbility, start);
            onCastComplete.AddEventHandler(careerAbility, complete);
        }

        private static object GetAgentComponent(object agent, Type componentType)
        {
            return _agentGetComponentDefinition.MakeGenericMethod(componentType).Invoke(agent, null);
        }

        private static bool ContainsReference(IList list, object value)
        {
            if (list == null)
            {
                return false;
            }

            foreach (object item in list)
            {
                if (ReferenceEquals(item, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static string SafeHeroId(object hero)
        {
            try
            {
                PropertyInfo id = hero.GetType().GetProperty("StringId", InstanceFlags);
                return Convert.ToString(id?.GetValue(hero, null)) ?? "<unknown>";
            }
            catch
            {
                return "<unknown>";
            }
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName, false);
            if (type == null)
            {
                throw new TypeLoadException(assemblyQualifiedName);
            }
            return type;
        }

        private static MethodInfo FindUniqueMethod(Type type, string name, BindingFlags flags, int parameterCount)
        {
            MethodInfo[] matches = type.GetMethods(flags)
                .Where(method => method.Name == name && method.GetParameters().Length == parameterCount)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new MissingMethodException(type.FullName, name + "/" + parameterCount + " matches=" + matches.Length);
            }
            return matches[0];
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

        private static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            return ex;
        }

        private static void Log(string message)
        {
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string directory = Path.Combine(documents, "Mount and Blade II Bannerlord", "Configs", "ModLogs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    Path.Combine(directory, "MultiCharacterCampaignTOR.log"),
                    DateTime.Now.ToString("O") + " [Registered Career Identity] " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
