using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Extends TOR's mission ability pipeline so registered MCC heroes retain their dedicated
    /// career ability while AI-controlled. TOR normally creates and casts CareerAbility only
    /// for Hero.MainHero; this compatibility layer scopes the missing behavior to MCC's own
    /// registered shared heroes and leaves all other TOR agents on the native path.
    /// </summary>
    public static class AICareerAbilitySupport
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags DeclaredInstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags DeclaredStaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static bool _installed;

        private static Type _agentType;
        private static Type _heroType;
        private static Type _mobilePartyType;
        private static Type _abilityType;
        private static Type _abilityComponentType;
        private static Type _careerAbilityType;
        private static Type _abilityFactoryType;
        private static Type _abilityManagerMissionLogicType;
        private static Type _wizardAIComponentType;
        private static Type _careerAbilityScriptType;
        private static Type _abilityScriptType;
        private static Type _agentExtensionsType;
        private static Type _heroExtensionsType;
        private static Type _careerHelperType;
        private static Type _careerAbilityChargeSupplierType;
        private static Type _torDamageHelperType;
        private static Type _attackCollisionDataType;
        private static Type _chargeTypeType;
        private static Type _mccBehaviorType;

        private static MethodInfo _agentGetComponentDefinition;
        private static MethodInfo _agentAddComponentMethod;
        private static MethodInfo _agentIsAIControlledGetter;
        private static MethodInfo _agentIsPlayerControlledGetter;
        private static MethodInfo _agentIsMainAgentGetter;
        private static MethodInfo _agentMainGetter;
        private static MethodInfo _mobilePartyMainPartyGetter;
        private static MethodInfo _getHeroMethod;
        private static MethodInfo _getOriginMobilePartyMethod;
        private static MethodInfo _belongsToMainPartyMethod;
        private static MethodInfo _hasAttributeMethod;
        private static MethodInfo _addAttributeMethod;
        private static MethodInfo _getCareerMethod;
        private static MethodInfo _abilityFactoryCreateNewMethod;
        private static MethodInfo _abilityComponentOnCastStartMethod;
        private static MethodInfo _abilityComponentOnCastCompleteMethod;
        private static MethodInfo _abilityIsSingleTargetGetter;
        private static MethodInfo _isCastingMissionMethod;
        private static MethodInfo _calculateChargeForCareerMethod;
        private static MethodInfo _isRegisteredSharedHeroMethod;
        private static MethodInfo _heroMainHeroGetter;
        private static MethodInfo _abilityScriptCasterAgentGetter;
        private static MethodInfo _abilityScriptAbilityGetter;
        private static MethodInfo _careerAbilityAddChargeMethod;

        private static PropertyInfo _mccBehaviorInstanceProperty;
        private static PropertyInfo _knownAbilitySystemProperty;
        private static PropertyInfo _careerAbilityProperty;
        private static PropertyInfo _careerAbilityTemplateProperty;
        private static PropertyInfo _careerAbilityTemplateIdProperty;

        private static FieldInfo _careerAbilityOwnerHeroField;
        private static FieldInfo _wizardAvailableCastingBehaviorsField;

        private static ConstructorInfo _abilityComponentConstructor;
        private static ConstructorInfo _wizardAIComponentConstructor;

        [ThreadStatic]
        private static object _activeCareerAbility;

        [ThreadStatic]
        private static object _activeCareerAbilityCaster;

        [ThreadStatic]
        private static object _activeCareerScript;

        [ThreadStatic]
        private static object _activeChargeHero;

        [ThreadStatic]
        private static object _activeChargeAgent;

        public static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                ResolveRuntimeSurfaces();

                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.ai-career-abilities");

                MethodInfo onAgentCreated = FindUniqueMethod(_abilityManagerMissionLogicType, "OnAgentCreated", InstanceFlags, 1);
                MethodInfo onAgentControllerChanged = FindUniqueMethod(_abilityManagerMissionLogicType, "OnAgentControllerChanged", InstanceFlags, 2);
                MethodInfo onAgentHit = FindUniqueMethod(_abilityManagerMissionLogicType, "OnAgentHit", InstanceFlags, 5);
                MethodInfo onAgentRemoved = FindUniqueMethod(_abilityManagerMissionLogicType, "OnAgentRemoved", InstanceFlags, 4);
                MethodInfo onEndMission = FindUniqueMethod(_abilityManagerMissionLogicType, "OnEndMission", InstanceFlags, 0);

                harmony.Patch(
                    onAgentCreated,
                    prefix: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(OnAgentCreatedPrefix)),
                    postfix: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(OnAgentCreatedPostfix)));

                harmony.Patch(
                    onAgentControllerChanged,
                    postfix: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(OnAgentControllerChangedPostfix)));

                harmony.Patch(
                    onAgentHit,
                    postfix: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(OnAgentHitPostfix)));

                harmony.Patch(
                    onAgentRemoved,
                    postfix: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(OnAgentRemovedPostfix)));

                harmony.Patch(
                    onEndMission,
                    postfix: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(OnEndMissionPostfix)));

                PatchCareerAbilityCore(harmony);
                PatchCareerAbilityScripts(harmony);
                PatchCareerChargeContext(harmony);

                _installed = true;
                Log("Installed event-driven AI career-ability support for registered shared heroes.");
            }
            catch (Exception ex)
            {
                Log("AI career-ability support installation failed safely: " + Unwrap(ex));
            }
        }

        private static void ResolveRuntimeSurfaces()
        {
            _agentType = RequireType("TaleWorlds.MountAndBlade.Agent, TaleWorlds.MountAndBlade");
            _heroType = RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
            _mobilePartyType = RequireType("TaleWorlds.CampaignSystem.Party.MobileParty, TaleWorlds.CampaignSystem");
            _abilityType = RequireType("TOR_Core.AbilitySystem.Ability, TOR_Core");
            _abilityComponentType = RequireType("TOR_Core.AbilitySystem.AbilityComponent, TOR_Core");
            _careerAbilityType = RequireType("TOR_Core.AbilitySystem.CareerAbility, TOR_Core");
            _abilityFactoryType = RequireType("TOR_Core.AbilitySystem.AbilityFactory, TOR_Core");
            _abilityManagerMissionLogicType = RequireType("TOR_Core.AbilitySystem.AbilityManagerMissionLogic, TOR_Core");
            _wizardAIComponentType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.Components.WizardAIComponent, TOR_Core");
            _careerAbilityScriptType = RequireType("TOR_Core.AbilitySystem.Scripts.CareerAbilityScript, TOR_Core");
            _abilityScriptType = RequireType("TOR_Core.AbilitySystem.Scripts.AbilityScript, TOR_Core");
            _agentExtensionsType = RequireType("TOR_Core.Extensions.AgentExtensions, TOR_Core");
            _heroExtensionsType = RequireType("TOR_Core.Extensions.HeroExtensions, TOR_Core");
            _careerHelperType = RequireType("TOR_Core.CharacterDevelopment.CareerSystem.CareerHelper, TOR_Core");
            _careerAbilityChargeSupplierType = RequireType("TOR_Core.CharacterDevelopment.CareerAbilityChargeSupplier, TOR_Core");
            _torDamageHelperType = RequireType("TOR_Core.BattleMechanics.DamageSystem.TORDamageHelper, TOR_Core");
            _attackCollisionDataType = RequireType("TaleWorlds.MountAndBlade.AttackCollisionData, TaleWorlds.MountAndBlade");
            _chargeTypeType = RequireType("TOR_Core.AbilitySystem.ChargeType, TOR_Core");
            _mccBehaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");

            _agentGetComponentDefinition = _agentType.GetMethods(InstanceFlags)
                .Single(method => method.Name == "GetComponent" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            _agentAddComponentMethod = FindUniqueMethod(_agentType, "AddComponent", InstanceFlags, 1);
            _agentIsAIControlledGetter = RequireProperty(_agentType, "IsAIControlled", InstanceFlags).GetGetMethod(true);
            _agentIsPlayerControlledGetter = RequireProperty(_agentType, "IsPlayerControlled", InstanceFlags).GetGetMethod(true);
            _agentIsMainAgentGetter = RequireProperty(_agentType, "IsMainAgent", InstanceFlags).GetGetMethod(true);
            _agentMainGetter = RequireProperty(_agentType, "Main", StaticFlags).GetGetMethod(true);
            _mobilePartyMainPartyGetter = RequireProperty(_mobilePartyType, "MainParty", StaticFlags).GetGetMethod(true);

            _getHeroMethod = FindUniqueMethod(_agentExtensionsType, "GetHero", StaticFlags, 1);
            _getOriginMobilePartyMethod = FindUniqueMethod(_agentExtensionsType, "GetOriginMobileParty", StaticFlags, 1);
            _belongsToMainPartyMethod = FindUniqueMethod(_agentExtensionsType, "BelongsToMainParty", StaticFlags, 1);
            _hasAttributeMethod = FindUniqueMethod(_heroExtensionsType, "HasAttribute", StaticFlags, 2);
            _addAttributeMethod = FindUniqueMethod(_heroExtensionsType, "AddAttribute", StaticFlags, 2);
            _getCareerMethod = FindUniqueMethod(_heroExtensionsType, "GetCareer", StaticFlags, 1);

            _abilityFactoryCreateNewMethod = _abilityFactoryType.GetMethods(StaticFlags)
                .Single(method => method.Name == "CreateNew" && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType == _agentType);
            _abilityComponentOnCastStartMethod = FindUniqueMethod(_abilityComponentType, "OnCastStart", InstanceFlags, 1);
            _abilityComponentOnCastCompleteMethod = FindUniqueMethod(_abilityComponentType, "OnCastComplete", InstanceFlags, 1);
            _abilityIsSingleTargetGetter = RequireProperty(_abilityType, "IsSingleTarget", InstanceFlags).GetGetMethod(true);
            _isCastingMissionMethod = FindUniqueMethod(_abilityManagerMissionLogicType, "IsCastingMission", InstanceFlags, 0);
            _calculateChargeForCareerMethod = FindUniqueMethod(_careerHelperType, "CalculateChargeForCareer", StaticFlags, 6);
            _careerAbilityAddChargeMethod = FindUniqueMethod(_careerAbilityType, "AddCharge", InstanceFlags, 1);

            _heroMainHeroGetter = RequireProperty(_heroType, "MainHero", StaticFlags).GetGetMethod(true);
            _abilityScriptCasterAgentGetter = RequireProperty(_abilityScriptType, "CasterAgent", InstanceFlags).GetGetMethod(true);
            _abilityScriptAbilityGetter = RequireProperty(_abilityScriptType, "Ability", InstanceFlags).GetGetMethod(true);

            _mccBehaviorInstanceProperty = RequireProperty(_mccBehaviorType, "Instance", StaticFlags);
            _isRegisteredSharedHeroMethod = FindUniqueMethod(_mccBehaviorType, "IsRegisteredSharedHero", InstanceFlags, 1);

            _knownAbilitySystemProperty = RequireProperty(_abilityComponentType, "KnownAbilitySystem", InstanceFlags);
            _careerAbilityProperty = RequireProperty(_abilityComponentType, "CareerAbility", InstanceFlags);
            _careerAbilityOwnerHeroField = RequireField(_careerAbilityType, "_ownerHero", InstanceFlags);
            _wizardAvailableCastingBehaviorsField = RequireField(_wizardAIComponentType, "_availableCastingBehaviors", InstanceFlags);

            _abilityComponentConstructor = _abilityComponentType.GetConstructors(InstanceFlags)
                .Single(ctor => ctor.GetParameters().Length == 1 && ctor.GetParameters()[0].ParameterType == _agentType);
            _wizardAIComponentConstructor = _wizardAIComponentType.GetConstructors(InstanceFlags)
                .Single(ctor => ctor.GetParameters().Length == 1 && ctor.GetParameters()[0].ParameterType == _agentType);
        }

        private static void PatchCareerAbilityCore(Harmony harmony)
        {
            ConstructorInfo careerCtor = _careerAbilityType.GetConstructors(InstanceFlags)
                .Single(ctor => ctor.GetParameters().Length >= 2 && ctor.GetParameters().Any(parameter => parameter.ParameterType == _agentType));
            MethodInfo canCast = FindUniqueMethod(_careerAbilityType, "CanCast", InstanceFlags, 2);
            MethodInfo activate = FindUniqueMethod(_careerAbilityType, "ActivateAbility", InstanceFlags, 1);

            HarmonyMethod prefix = new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerAbilityContextPrefix));
            HarmonyMethod finalizer = new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerAbilityContextFinalizer));

            harmony.Patch(
                careerCtor,
                prefix: prefix,
                transpiler: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerAbilityOwnerTranspiler)),
                finalizer: finalizer);

            harmony.Patch(
                canCast,
                prefix: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerAbilityCanCastContextPrefix)),
                transpiler: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerAbilityCanCastTranspiler)),
                finalizer: finalizer);

            harmony.Patch(
                activate,
                prefix: prefix,
                transpiler: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerAbilityOwnerTranspiler)),
                finalizer: finalizer);
        }

        private static void PatchCareerAbilityScripts(Harmony harmony)
        {
            Assembly torAssembly = _careerAbilityScriptType.Assembly;
            int contextMethods = 0;
            int rewrittenMethods = 0;

            foreach (Type type in GetLoadableTypes(torAssembly))
            {
                bool isCareerScript = _careerAbilityScriptType.IsAssignableFrom(type) && type != _careerAbilityScriptType;
                bool isNestedCareerScriptType = IsNestedInsideCareerScript(type);
                if (!isCareerScript && !isNestedCareerScriptType)
                {
                    continue;
                }

                if (isCareerScript)
                {
                    foreach (MethodInfo method in type.GetMethods(DeclaredInstanceFlags))
                    {
                        if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
                        {
                            continue;
                        }

                        HarmonyMethod transpiler = CallsCareerGlobalSurface(method)
                            ? new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerScriptContextTranspiler))
                            : null;

                        harmony.Patch(
                            method,
                            prefix: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerScriptContextPrefix)),
                            transpiler: transpiler,
                            finalizer: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerScriptContextFinalizer)));

                        contextMethods++;
                        if (transpiler != null)
                        {
                            rewrittenMethods++;
                        }
                    }
                }

                foreach (MethodInfo method in type.GetMethods(DeclaredStaticFlags))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null || !CallsCareerGlobalSurface(method))
                    {
                        continue;
                    }

                    harmony.Patch(
                        method,
                        transpiler: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerScriptContextTranspiler)));
                    rewrittenMethods++;
                }
            }

            Log("AI career script context installed: wrapped=" + contextMethods + ", rewritten=" + rewrittenMethods + ".");
        }

        private static void PatchCareerChargeContext(Harmony harmony)
        {
            harmony.Patch(
                _calculateChargeForCareerMethod,
                transpiler: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerChargeContextTranspiler)));

            int supplierMethods = 0;
            foreach (MethodInfo method in _careerAbilityChargeSupplierType.GetMethods(DeclaredStaticFlags))
            {
                if (method.ContainsGenericParameters || method.GetMethodBody() == null || !CallsChargeGlobalSurface(method))
                {
                    continue;
                }

                harmony.Patch(
                    method,
                    transpiler: new HarmonyMethod(typeof(AICareerAbilitySupport), nameof(CareerChargeContextTranspiler)));
                supplierMethods++;
            }

            Log("AI career charge owner context installed for " + supplierMethods + " TOR charge supplier method(s).");
        }

        private static void OnAgentCreatedPrefix(object __0)
        {
            try
            {
                if (!IsRegisteredSharedAICareerAgent(__0, out object hero, out _))
                {
                    return;
                }

                EnsureAbilityUser(hero);
            }
            catch (Exception ex)
            {
                Log("AI career pre-spawn preparation failed safely: " + Unwrap(ex));
            }
        }

        private static void OnAgentCreatedPostfix(object __instance, object __0)
        {
            try
            {
                EnsureAISharedCareerAbility(__instance, __0, "agent created");
            }
            catch (Exception ex)
            {
                Log("AI career post-spawn repair failed safely: " + Unwrap(ex));
            }
        }

        private static void OnAgentControllerChangedPostfix(object __instance, object __0)
        {
            try
            {
                EnsureAISharedCareerAbility(__instance, __0, "controller changed");
            }
            catch (Exception ex)
            {
                Log("AI career controller-change repair failed safely: " + Unwrap(ex));
            }
        }

        private static void OnAgentHitPostfix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 5)
                {
                    return;
                }

                object affected = __args[0];
                object affector = __args[1];
                object blow = __args[3];
                object collisionData = __args[4];
                if (blow == null)
                {
                    return;
                }

                int amount = ReadIntMember(blow, "InflictedDamage");
                if (amount <= 0)
                {
                    return;
                }

                object mask = DetermineAttackMask(blow);
                if (mask == null)
                {
                    return;
                }

                ApplyAIChargeForOwner(affector, "DamageDone", amount, affector, affected, mask, collisionData);
                ApplyAIChargeForOwner(affected, "DamageTaken", amount, affector, affected, mask, collisionData);
            }
            catch (Exception ex)
            {
                Log("AI career hit-charge integration failed safely: " + Unwrap(ex));
            }
        }

        private static void OnAgentRemovedPostfix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 4)
                {
                    return;
                }

                object affected = __args[0];
                object affector = __args[1];
                object killingBlow = __args[3];
                if (affector == null || killingBlow == null)
                {
                    return;
                }

                object mask = DetermineAttackMask(killingBlow);
                if (mask == null)
                {
                    return;
                }

                object emptyCollisionData = Activator.CreateInstance(_attackCollisionDataType);
                ApplyAIChargeForOwner(affector, "NumberOfKills", 1, affector, affected, mask, emptyCollisionData);
            }
            catch (Exception ex)
            {
                Log("AI career kill-charge integration failed safely: " + Unwrap(ex));
            }
        }

        private static void OnEndMissionPostfix()
        {
            _activeCareerAbility = null;
            _activeCareerAbilityCaster = null;
            _activeCareerScript = null;
            _activeChargeHero = null;
            _activeChargeAgent = null;
        }

        private static void EnsureAISharedCareerAbility(object abilityManager, object agent, string source)
        {
            if (!IsRegisteredSharedAICareerAgent(agent, out object hero, out object career))
            {
                return;
            }

            EnsureAbilityUser(hero);

            bool isCastingMission = abilityManager != null && Convert.ToBoolean(_isCastingMissionMethod.Invoke(abilityManager, null));
            if (!isCastingMission)
            {
                return;
            }

            object component = GetAgentComponent(agent, _abilityComponentType);
            if (component == null)
            {
                component = _abilityComponentConstructor.Invoke(new[] { agent });
                _agentAddComponentMethod.Invoke(agent, new[] { component });
            }

            object careerAbility = EnsureCareerAbility(component, career, agent);
            if (careerAbility == null)
            {
                return;
            }

            object wizard = GetAgentComponent(agent, _wizardAIComponentType);
            if (wizard == null)
            {
                wizard = _wizardAIComponentConstructor.Invoke(new[] { agent });
                _agentAddComponentMethod.Invoke(agent, new[] { wizard });
                Log("Enabled TOR WizardAI career casting for registered shared hero=" + SafeHeroId(hero) + " source=" + source + ".");
            }
            else
            {
                // WizardAI lazily caches behavior objects by ability index. Career abilities are appended
                // for AI heroes so existing indices stay stable, then the cache is invalidated so the new
                // ability receives a behavior on the next TOR occasional-AI evaluation.
                _wizardAvailableCastingBehaviorsField.SetValue(wizard, null);
            }
        }

        private static object EnsureCareerAbility(object component, object career, object agent)
        {
            object current = _careerAbilityProperty.GetValue(component, null);
            IList known = _knownAbilitySystemProperty.GetValue(component, null) as IList;

            if (current == null && known != null)
            {
                foreach (object ability in known)
                {
                    if (ability != null && _careerAbilityType.IsInstanceOfType(ability))
                    {
                        current = ability;
                        break;
                    }
                }
            }

            if (current == null)
            {
                PropertyInfo templateIdProperty = _careerAbilityTemplateIdProperty;
                if (templateIdProperty == null || templateIdProperty.DeclaringType == null || !templateIdProperty.DeclaringType.IsInstanceOfType(career))
                {
                    templateIdProperty = career.GetType().GetProperty("AbilityTemplateID", InstanceFlags);
                    if (templateIdProperty == null)
                    {
                        throw new MissingMemberException(career.GetType().FullName, "AbilityTemplateID");
                    }
                    _careerAbilityTemplateIdProperty = templateIdProperty;
                }

                string templateId = templateIdProperty.GetValue(career, null) as string;
                if (string.IsNullOrWhiteSpace(templateId))
                {
                    return null;
                }

                current = _abilityFactoryCreateNewMethod.Invoke(null, new object[] { templateId, agent });
                if (current == null || !_careerAbilityType.IsInstanceOfType(current))
                {
                    return null;
                }

                EventInfo onCastStart = _abilityType.GetEvent("OnCastStart", InstanceFlags);
                EventInfo onCastComplete = _abilityType.GetEvent("OnCastComplete", InstanceFlags);
                if (onCastStart != null && onCastComplete != null)
                {
                    Delegate start = Delegate.CreateDelegate(onCastStart.EventHandlerType, component, _abilityComponentOnCastStartMethod);
                    Delegate complete = Delegate.CreateDelegate(onCastComplete.EventHandlerType, component, _abilityComponentOnCastCompleteMethod);
                    onCastStart.AddEventHandler(current, start);
                    onCastComplete.AddEventHandler(current, complete);
                }
            }

            _careerAbilityProperty.SetValue(component, current, null);

            if (known != null && !ContainsReference(known, current))
            {
                // Append instead of inserting at zero: an already-created WizardAI behavior retains its
                // existing selected-spell indices until its lazy cache is rebuilt.
                known.Add(current);
            }

            return current;
        }

        private static bool IsRegisteredSharedAICareerAgent(object agent, out object hero, out object career)
        {
            hero = null;
            career = null;
            if (agent == null || !_agentType.IsInstanceOfType(agent))
            {
                return false;
            }

            if (!Convert.ToBoolean(_agentIsAIControlledGetter.Invoke(agent, null)))
            {
                return false;
            }

            hero = _getHeroMethod.Invoke(null, new[] { agent });
            if (hero == null || !IsRegisteredSharedHero(hero))
            {
                return false;
            }

            career = _getCareerMethod.Invoke(null, new[] { hero });
            return career != null;
        }

        private static bool IsRegisteredSharedHero(object hero)
        {
            if (hero == null)
            {
                return false;
            }

            object behavior = _mccBehaviorInstanceProperty.GetValue(null, null);
            if (behavior == null)
            {
                return false;
            }

            return Convert.ToBoolean(_isRegisteredSharedHeroMethod.Invoke(behavior, new[] { hero }));
        }

        private static void EnsureAbilityUser(object hero)
        {
            if (hero == null)
            {
                return;
            }

            bool hasAbilityUser = Convert.ToBoolean(_hasAttributeMethod.Invoke(null, new object[] { hero, "AbilityUser" }));
            if (!hasAbilityUser)
            {
                _addAttributeMethod.Invoke(null, new object[] { hero, "AbilityUser" });
            }
        }

        private static object GetAgentComponent(object agent, Type componentType)
        {
            MethodInfo getComponent = _agentGetComponentDefinition.MakeGenericMethod(componentType);
            return getComponent.Invoke(agent, null);
        }

        private static void CareerAbilityContextPrefix(object __instance, out object __state)
        {
            __state = _activeCareerAbility;
            _activeCareerAbility = __instance;
        }

        private static void CareerAbilityCanCastContextPrefix(object __instance, object[] __args, out object[] __state)
        {
            __state = new[] { _activeCareerAbility, _activeCareerAbilityCaster };
            _activeCareerAbility = __instance;
            _activeCareerAbilityCaster = __args != null && __args.Length > 0 ? __args[0] : null;
        }

        private static Exception CareerAbilityContextFinalizer(Exception __exception, object __state)
        {
            object[] state = __state as object[];
            if (state != null)
            {
                _activeCareerAbility = state.Length > 0 ? state[0] : null;
                _activeCareerAbilityCaster = state.Length > 1 ? state[1] : null;
            }
            else
            {
                _activeCareerAbility = __state;
                _activeCareerAbilityCaster = null;
            }
            return __exception;
        }

        private static IEnumerable<CodeInstruction> CareerAbilityOwnerTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = typeof(AICareerAbilitySupport).GetMethod(nameof(ResolveCareerAbilityHero), StaticFlags);
            return ReplaceStaticGetter(instructions, _heroMainHeroGetter, replacement, _heroType);
        }

        private static IEnumerable<CodeInstruction> CareerAbilityCanCastTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo heroReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(ResolveCareerAbilityHero), StaticFlags);
            MethodInfo playerReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(IsCareerAbilityCasterAllowed), StaticFlags);
            MethodInfo singleTargetReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(IsCareerAbilitySingleTargetForCurrentCaster), StaticFlags);

            foreach (CodeInstruction instruction in ReplaceStaticGetter(instructions, _heroMainHeroGetter, heroReplacement, _heroType))
            {
                if (Calls(instruction, _agentIsPlayerControlledGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = playerReplacement;
                }
                else if (Calls(instruction, _abilityIsSingleTargetGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = singleTargetReplacement;
                }

                yield return instruction;
            }
        }

        private static object ResolveCareerAbilityHero()
        {
            object nativeHero = _heroMainHeroGetter.Invoke(null, null);
            object ability = _activeCareerAbility;
            if (ability == null || _careerAbilityOwnerHeroField == null)
            {
                return nativeHero;
            }

            object owner = _careerAbilityOwnerHeroField.GetValue(ability);
            return owner != null && IsRegisteredSharedHero(owner) ? owner : nativeHero;
        }

        private static bool IsCareerAbilityCasterAllowed(object agent)
        {
            if (agent == null)
            {
                return false;
            }

            bool nativePlayer = Convert.ToBoolean(_agentIsPlayerControlledGetter.Invoke(agent, null));
            if (nativePlayer)
            {
                return true;
            }

            return IsRegisteredSharedAICareerAgent(agent, out _, out _);
        }

        private static bool IsCareerAbilitySingleTargetForCurrentCaster(object ability)
        {
            object caster = _activeCareerAbilityCaster;
            if (caster != null && IsRegisteredSharedAICareerAgent(caster, out _, out _))
            {
                // TOR's CareerAbility target-lock check is a player crosshair requirement. WizardAI
                // resolves targets through its casting behavior and has no player crosshair to lock.
                return false;
            }

            return Convert.ToBoolean(_abilityIsSingleTargetGetter.Invoke(ability, null));
        }

        private static void CareerScriptContextPrefix(object __instance, out object __state)
        {
            __state = _activeCareerScript;
            _activeCareerScript = __instance;
        }

        private static Exception CareerScriptContextFinalizer(Exception __exception, object __state)
        {
            _activeCareerScript = __state;
            return __exception;
        }

        private static IEnumerable<CodeInstruction> CareerScriptContextTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo heroReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(ResolveCareerScriptHero), StaticFlags);
            MethodInfo agentReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(ResolveCareerScriptAgent), StaticFlags);
            MethodInfo partyReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(BelongsToCareerScriptParty), StaticFlags);

            IEnumerable<CodeInstruction> stage = ReplaceStaticGetter(instructions, _heroMainHeroGetter, heroReplacement, _heroType);
            stage = ReplaceStaticGetter(stage, _agentMainGetter, agentReplacement, _agentType);

            foreach (CodeInstruction instruction in stage)
            {
                if (Calls(instruction, _belongsToMainPartyMethod))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = partyReplacement;
                }
                yield return instruction;
            }
        }

        private static object ResolveCareerScriptHero()
        {
            object hero = TryGetCareerScriptOwnerHero(_activeCareerScript);
            return hero != null && IsRegisteredSharedHero(hero) ? hero : _heroMainHeroGetter.Invoke(null, null);
        }

        private static object ResolveCareerScriptAgent()
        {
            object agent = TryGetCareerScriptCasterAgent(_activeCareerScript);
            if (agent != null && IsRegisteredSharedAICareerAgent(agent, out _, out _))
            {
                return agent;
            }
            return _agentMainGetter.Invoke(null, null);
        }

        private static bool BelongsToCareerScriptParty(object candidateAgent)
        {
            object caster = TryGetCareerScriptCasterAgent(_activeCareerScript);
            if (caster != null && IsRegisteredSharedAICareerAgent(caster, out _, out _))
            {
                return BelongsToSameOriginParty(candidateAgent, caster);
            }
            return Convert.ToBoolean(_belongsToMainPartyMethod.Invoke(null, new[] { candidateAgent }));
        }

        private static object TryGetCareerScriptCasterAgent(object script)
        {
            if (script == null || !_abilityScriptType.IsInstanceOfType(script))
            {
                return null;
            }
            return _abilityScriptCasterAgentGetter.Invoke(script, null);
        }

        private static object TryGetCareerScriptOwnerHero(object script)
        {
            object caster = TryGetCareerScriptCasterAgent(script);
            if (caster != null)
            {
                object hero = _getHeroMethod.Invoke(null, new[] { caster });
                if (hero != null)
                {
                    return hero;
                }
            }

            if (script != null && _abilityScriptType.IsInstanceOfType(script))
            {
                object ability = _abilityScriptAbilityGetter.Invoke(script, null);
                if (ability != null && _careerAbilityType.IsInstanceOfType(ability))
                {
                    return _careerAbilityOwnerHeroField.GetValue(ability);
                }
            }
            return null;
        }

        private static IEnumerable<CodeInstruction> CareerChargeContextTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo heroReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(ResolveChargeHero), StaticFlags);
            MethodInfo agentReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(ResolveChargeMainAgent), StaticFlags);
            MethodInfo partyReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(ResolveChargeMainParty), StaticFlags);
            MethodInfo belongsReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(BelongsToChargeOwnerParty), StaticFlags);
            MethodInfo isMainReplacement = typeof(AICareerAbilitySupport).GetMethod(nameof(IsChargeOwnerAgent), StaticFlags);

            IEnumerable<CodeInstruction> stage = ReplaceStaticGetter(instructions, _heroMainHeroGetter, heroReplacement, _heroType);
            stage = ReplaceStaticGetter(stage, _agentMainGetter, agentReplacement, _agentType);
            stage = ReplaceStaticGetter(stage, _mobilePartyMainPartyGetter, partyReplacement, _mobilePartyType);

            foreach (CodeInstruction instruction in stage)
            {
                if (Calls(instruction, _belongsToMainPartyMethod))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = belongsReplacement;
                }
                else if (Calls(instruction, _agentIsMainAgentGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = isMainReplacement;
                }
                yield return instruction;
            }
        }

        private static object ResolveChargeHero()
        {
            return _activeChargeHero ?? _heroMainHeroGetter.Invoke(null, null);
        }

        private static object ResolveChargeMainAgent()
        {
            return _activeChargeAgent ?? _agentMainGetter.Invoke(null, null);
        }

        private static object ResolveChargeMainParty()
        {
            if (_activeChargeAgent != null)
            {
                object party = _getOriginMobilePartyMethod.Invoke(null, new[] { _activeChargeAgent });
                if (party != null)
                {
                    return party;
                }
            }
            return _mobilePartyMainPartyGetter.Invoke(null, null);
        }

        private static bool BelongsToChargeOwnerParty(object candidateAgent)
        {
            if (_activeChargeAgent != null)
            {
                return BelongsToSameOriginParty(candidateAgent, _activeChargeAgent);
            }
            return Convert.ToBoolean(_belongsToMainPartyMethod.Invoke(null, new[] { candidateAgent }));
        }

        private static bool IsChargeOwnerAgent(object candidateAgent)
        {
            if (_activeChargeAgent != null)
            {
                return ReferenceEquals(candidateAgent, _activeChargeAgent);
            }
            return Convert.ToBoolean(_agentIsMainAgentGetter.Invoke(candidateAgent, null));
        }

        private static void ApplyAIChargeForOwner(object ownerAgent, string chargeTypeName, int amount, object affector, object affected, object mask, object collisionData)
        {
            if (amount <= 0 || !IsRegisteredSharedAICareerAgent(ownerAgent, out object hero, out _))
            {
                return;
            }

            object component = GetAgentComponent(ownerAgent, _abilityComponentType);
            object careerAbility = component != null ? _careerAbilityProperty.GetValue(component, null) : null;
            if (careerAbility == null)
            {
                return;
            }

            object oldHero = _activeChargeHero;
            object oldAgent = _activeChargeAgent;
            try
            {
                _activeChargeHero = hero;
                _activeChargeAgent = ownerAgent;

                object chargeType = Enum.Parse(_chargeTypeType, chargeTypeName, ignoreCase: false);
                object collision = collisionData ?? Activator.CreateInstance(_attackCollisionDataType);
                object valueObject = _calculateChargeForCareerMethod.Invoke(null, new[] { chargeType, (object)amount, affector, affected, mask, collision });
                float value = valueObject == null ? 0f : Convert.ToSingle(valueObject);
                if (value > 0f)
                {
                    _careerAbilityAddChargeMethod.Invoke(careerAbility, new object[] { value });
                }
            }
            finally
            {
                _activeChargeHero = oldHero;
                _activeChargeAgent = oldAgent;
            }
        }

        private static object DetermineAttackMask(object blow)
        {
            Type blowType = blow.GetType();
            MethodInfo method = _torDamageHelperType.GetMethods(StaticFlags)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != "DetermineMask")
                    {
                        return false;
                    }
                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == blowType;
                });

            return method != null ? method.Invoke(null, new[] { blow }) : null;
        }

        private static bool BelongsToSameOriginParty(object leftAgent, object rightAgent)
        {
            if (leftAgent == null || rightAgent == null)
            {
                return false;
            }

            object leftParty = _getOriginMobilePartyMethod.Invoke(null, new[] { leftAgent });
            object rightParty = _getOriginMobilePartyMethod.Invoke(null, new[] { rightAgent });
            return leftParty != null && rightParty != null && ReferenceEquals(leftParty, rightParty);
        }

        private static bool CallsCareerGlobalSurface(MethodInfo method)
        {
            return MethodCalls(method, _heroMainHeroGetter) ||
                   MethodCalls(method, _agentMainGetter) ||
                   MethodCalls(method, _belongsToMainPartyMethod);
        }

        private static bool CallsChargeGlobalSurface(MethodInfo method)
        {
            return MethodCalls(method, _heroMainHeroGetter) ||
                   MethodCalls(method, _agentMainGetter) ||
                   MethodCalls(method, _mobilePartyMainPartyGetter) ||
                   MethodCalls(method, _belongsToMainPartyMethod) ||
                   MethodCalls(method, _agentIsMainAgentGetter);
        }

        private static bool IsNestedInsideCareerScript(Type type)
        {
            Type declaring = type.DeclaringType;
            while (declaring != null)
            {
                if (_careerAbilityScriptType.IsAssignableFrom(declaring))
                {
                    return true;
                }
                declaring = declaring.DeclaringType;
            }
            return false;
        }

        private static IEnumerable<CodeInstruction> ReplaceStaticGetter(IEnumerable<CodeInstruction> instructions, MethodInfo originalGetter, MethodInfo replacement, Type resultType)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (!Calls(instruction, originalGetter))
                {
                    yield return instruction;
                    continue;
                }

                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Castclass, resultType);
            }
        }

        private static bool Calls(CodeInstruction instruction, MethodInfo target)
        {
            return target != null && instruction != null && instruction.operand is MethodInfo method && method == target;
        }

        private static bool MethodCalls(MethodInfo method, MethodInfo target)
        {
            if (method == null || target == null)
            {
                return false;
            }

            MethodBody body;
            try
            {
                body = method.GetMethodBody();
            }
            catch
            {
                return false;
            }
            if (body == null)
            {
                return false;
            }

            byte[] il = body.GetILAsByteArray();
            if (il == null || il.Length == 0)
            {
                return false;
            }

            Dictionary<short, OpCode> opcodes = OpCodeMap.Value;
            int position = 0;
            while (position < il.Length)
            {
                short key = il[position++];
                if (key == 0xfe)
                {
                    if (position >= il.Length)
                    {
                        break;
                    }
                    key = (short)(0xfe00 | il[position++]);
                }

                if (!opcodes.TryGetValue(key, out OpCode opcode))
                {
                    return false;
                }

                if (opcode.OperandType == OperandType.InlineMethod)
                {
                    if (position + 4 > il.Length)
                    {
                        return false;
                    }
                    int token = BitConverter.ToInt32(il, position);
                    try
                    {
                        MethodBase resolved = method.Module.ResolveMethod(
                            token,
                            method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null,
                            method.IsGenericMethod ? method.GetGenericArguments() : null);
                        if (resolved == target)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }

                position += OperandSize(opcode.OperandType, il, position);
            }

            return false;
        }

        private static int OperandSize(OperandType operandType, byte[] il, int operandPosition)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    if (operandPosition + 4 > il.Length)
                    {
                        return 0;
                    }
                    int count = BitConverter.ToInt32(il, operandPosition);
                    return 4 + Math.Max(0, count) * 4;
                default:
                    return 0;
            }
        }

        private static readonly Lazy<Dictionary<short, OpCode>> OpCodeMap = new Lazy<Dictionary<short, OpCode>>(() =>
        {
            Dictionary<short, OpCode> result = new Dictionary<short, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (field.FieldType == typeof(OpCode))
                {
                    OpCode opcode = (OpCode)field.GetValue(null);
                    result[opcode.Value] = opcode;
                }
            }
            return result;
        });

        private static int ReadIntMember(object value, string name)
        {
            if (value == null)
            {
                return 0;
            }
            Type type = value.GetType();
            PropertyInfo property = type.GetProperty(name, InstanceFlags);
            if (property != null)
            {
                return Convert.ToInt32(property.GetValue(value, null));
            }
            FieldInfo field = type.GetField(name, InstanceFlags);
            return field != null ? Convert.ToInt32(field.GetValue(value)) : 0;
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

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName, throwOnError: false);
            if (type == null)
            {
                throw new TypeLoadException("Missing runtime type " + assemblyQualifiedName);
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

        private static FieldInfo RequireField(Type type, string name, BindingFlags flags)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field == null)
            {
                throw new MissingFieldException(type.FullName, name);
            }
            return field;
        }

        private static MethodInfo FindUniqueMethod(Type type, string name, BindingFlags flags, int parameterCount)
        {
            MethodInfo[] matches = type.GetMethods(flags)
                .Where(method => method.Name == name && method.GetParameters().Length == parameterCount)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new MissingMethodException(type.FullName, name + "/" + parameterCount + " (matches=" + matches.Length + ")");
            }
            return matches[0];
        }

        private static string SafeHeroId(object hero)
        {
            try
            {
                PropertyInfo stringId = hero.GetType().GetProperty("StringId", InstanceFlags);
                object value = stringId != null ? stringId.GetValue(hero, null) : null;
                return value != null ? value.ToString() : hero.ToString();
            }
            catch
            {
                return "<unknown>";
            }
        }

        private static string Unwrap(Exception ex)
        {
            Exception current = ex;
            while (current is TargetInvocationException && current.InnerException != null)
            {
                current = current.InnerException;
            }
            return current.ToString();
        }

        private static void Log(string message)
        {
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string directory = Path.Combine(documents, "Mount and Blade II Bannerlord", "Configs", "ModLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "MultiCharacterCampaignTOR.log");
                File.AppendAllText(path, DateTime.Now.ToString("O") + " [AI Career Ability] " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
