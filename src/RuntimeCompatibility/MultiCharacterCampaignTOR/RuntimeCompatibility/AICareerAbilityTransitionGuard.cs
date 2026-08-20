using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Hardens the AI-career bridge around controller changes and compiler-generated career-script
    /// closures. TOR's WizardAIComponent keeps ticking regardless of controller type, so an MCC
    /// hero that becomes player-controlled must explicitly suppress the stale AI component.
    /// </summary>
    internal static class AICareerAbilityTransitionGuard
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags DeclaredInstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static bool _installed;

        private static Type _agentType;
        private static Type _heroType;
        private static Type _abilityComponentType;
        private static Type _careerAbilityType;
        private static Type _wizardAIComponentType;
        private static Type _careerAbilityScriptType;
        private static Type _abilityManagerMissionLogicType;
        private static Type _agentExtensionsType;
        private static Type _mccBehaviorType;

        private static MethodInfo _agentGetComponentDefinition;
        private static MethodInfo _agentIsAIControlledGetter;
        private static MethodInfo _agentIsPlayerControlledGetter;
        private static MethodInfo _getHeroMethod;
        private static MethodInfo _isRegisteredSharedHeroMethod;
        private static MethodInfo _heroMainHeroGetter;
        private static MethodInfo _agentMainGetter;
        private static MethodInfo _belongsToMainPartyMethod;

        private static PropertyInfo _mccBehaviorInstanceProperty;
        private static PropertyInfo _knownAbilitySystemProperty;
        private static PropertyInfo _careerAbilityProperty;

        private static FieldInfo _wizardAgentField;
        private static FieldInfo _wizardAvailableCastingBehaviorsField;
        private static FieldInfo _wizardCurrentCastingBehaviorField;

        private static MethodInfo _resolveCareerScriptHeroMethod;
        private static MethodInfo _resolveCareerScriptAgentMethod;
        private static MethodInfo _belongsToCareerScriptPartyMethod;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                ResolveRuntimeSurfaces();

                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.ai-career-transitions");
                MethodInfo wizardOnTick = FindUniqueMethod(_wizardAIComponentType, "OnTick", InstanceFlags, 1);
                MethodInfo controllerChanged = FindUniqueMethod(_abilityManagerMissionLogicType, "OnAgentControllerChanged", InstanceFlags, 2);

                harmony.Patch(
                    wizardOnTick,
                    prefix: new HarmonyMethod(typeof(AICareerAbilityTransitionGuard), nameof(WizardAIOnTickPrefix)));

                harmony.Patch(
                    controllerChanged,
                    postfix: new HarmonyMethod(typeof(AICareerAbilityTransitionGuard), nameof(OnAgentControllerChangedPostfix)));

                PatchCompilerGeneratedCareerScriptMethods(harmony);

                _installed = true;
                Log("Installed AI career-ability controller-transition and nested-script guards.");
            }
            catch (Exception ex)
            {
                Log("AI career-ability transition guard installation failed safely: " + Unwrap(ex));
            }
        }

        private static void ResolveRuntimeSurfaces()
        {
            _agentType = RequireType("TaleWorlds.MountAndBlade.Agent, TaleWorlds.MountAndBlade");
            _heroType = RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
            _abilityComponentType = RequireType("TOR_Core.AbilitySystem.AbilityComponent, TOR_Core");
            _careerAbilityType = RequireType("TOR_Core.AbilitySystem.CareerAbility, TOR_Core");
            _wizardAIComponentType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.Components.WizardAIComponent, TOR_Core");
            _careerAbilityScriptType = RequireType("TOR_Core.AbilitySystem.Scripts.CareerAbilityScript, TOR_Core");
            _abilityManagerMissionLogicType = RequireType("TOR_Core.AbilitySystem.AbilityManagerMissionLogic, TOR_Core");
            _agentExtensionsType = RequireType("TOR_Core.Extensions.AgentExtensions, TOR_Core");
            _mccBehaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");

            _agentGetComponentDefinition = _agentType.GetMethods(InstanceFlags)
                .Single(method => method.Name == "GetComponent" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            _agentIsAIControlledGetter = RequireProperty(_agentType, "IsAIControlled", InstanceFlags).GetGetMethod(true);
            _agentIsPlayerControlledGetter = RequireProperty(_agentType, "IsPlayerControlled", InstanceFlags).GetGetMethod(true);
            _getHeroMethod = FindUniqueMethod(_agentExtensionsType, "GetHero", StaticFlags, 1);
            _heroMainHeroGetter = RequireProperty(_heroType, "MainHero", StaticFlags).GetGetMethod(true);
            _agentMainGetter = RequireProperty(_agentType, "Main", StaticFlags).GetGetMethod(true);
            _belongsToMainPartyMethod = FindUniqueMethod(_agentExtensionsType, "BelongsToMainParty", StaticFlags, 1);

            _mccBehaviorInstanceProperty = RequireProperty(_mccBehaviorType, "Instance", StaticFlags);
            _isRegisteredSharedHeroMethod = FindUniqueMethod(_mccBehaviorType, "IsRegisteredSharedHero", InstanceFlags, 1);

            _knownAbilitySystemProperty = RequireProperty(_abilityComponentType, "KnownAbilitySystem", InstanceFlags);
            _careerAbilityProperty = RequireProperty(_abilityComponentType, "CareerAbility", InstanceFlags);
            _wizardAgentField = RequireFieldInHierarchy(_wizardAIComponentType, "Agent");
            _wizardAvailableCastingBehaviorsField = RequireField(_wizardAIComponentType, "_availableCastingBehaviors", InstanceFlags);
            _wizardCurrentCastingBehaviorField = RequireField(_wizardAIComponentType, "CurrentCastingBehavior", InstanceFlags);

            Type supportType = typeof(AICareerAbilitySupport);
            _resolveCareerScriptHeroMethod = RequireMethod(supportType, "ResolveCareerScriptHero", StaticFlags);
            _resolveCareerScriptAgentMethod = RequireMethod(supportType, "ResolveCareerScriptAgent", StaticFlags);
            _belongsToCareerScriptPartyMethod = RequireMethod(supportType, "BelongsToCareerScriptParty", StaticFlags);
        }

        private static bool WizardAIOnTickPrefix(object __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return true;
                }

                object agent = _wizardAgentField.GetValue(__instance);
                if (!IsRegisteredSharedHeroAgent(agent))
                {
                    return true;
                }

                // TOR's WizardAIComponent itself has no controller-type guard. Keep it dormant while
                // MCC has promoted this same physical hero agent to player control.
                return Convert.ToBoolean(_agentIsAIControlledGetter.Invoke(agent, null));
            }
            catch (Exception ex)
            {
                Log("WizardAI tick guard failed open: " + Unwrap(ex));
                return true;
            }
        }

        private static void OnAgentControllerChangedPostfix(object __0)
        {
            try
            {
                object agent = __0;
                if (!IsRegisteredSharedHeroAgent(agent))
                {
                    return;
                }

                object wizard = GetAgentComponent(agent, _wizardAIComponentType);
                if (wizard != null)
                {
                    // Cached behavior objects store ability indices. Any MCC controller handoff can
                    // change the canonical ability order, so never retain a selected behavior across it.
                    _wizardAvailableCastingBehaviorsField.SetValue(wizard, null);
                    _wizardCurrentCastingBehaviorField.SetValue(wizard, null);
                }

                bool isAI = Convert.ToBoolean(_agentIsAIControlledGetter.Invoke(agent, null));
                bool isPlayer = Convert.ToBoolean(_agentIsPlayerControlledGetter.Invoke(agent, null));
                if (!isAI && isPlayer)
                {
                    NormalizeCareerAbilityFirst(agent);
                }
            }
            catch (Exception ex)
            {
                Log("AI career controller-transition normalization failed safely: " + Unwrap(ex));
            }
        }

        private static void NormalizeCareerAbilityFirst(object agent)
        {
            object component = GetAgentComponent(agent, _abilityComponentType);
            if (component == null)
            {
                return;
            }

            object careerAbility = _careerAbilityProperty.GetValue(component, null);
            IList known = _knownAbilitySystemProperty.GetValue(component, null) as IList;
            if (careerAbility == null || known == null)
            {
                return;
            }

            int index = IndexOfReference(known, careerAbility);
            if (index <= 0)
            {
                return;
            }

            known.RemoveAt(index);
            known.Insert(0, careerAbility);
            Log("Restored TOR's player ability ordering after MCC controller takeover for shared hero=" + SafeHeroId(_getHeroMethod.Invoke(null, new[] { agent })) + ".");
        }

        private static void PatchCompilerGeneratedCareerScriptMethods(Harmony harmony)
        {
            int patched = 0;
            foreach (Type type in GetLoadableTypes(_careerAbilityScriptType.Assembly))
            {
                if (!IsNestedInsideCareerScript(type))
                {
                    continue;
                }

                foreach (MethodInfo method in type.GetMethods(DeclaredInstanceFlags))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
                    {
                        continue;
                    }

                    harmony.Patch(
                        method,
                        transpiler: new HarmonyMethod(typeof(AICareerAbilityTransitionGuard), nameof(NestedCareerScriptContextTranspiler)));
                    patched++;
                }
            }

            Log("Patched " + patched + " compiler-generated career-script instance method(s) for MCC caster context.");
        }

        private static IEnumerable<CodeInstruction> NestedCareerScriptContextTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (Calls(instruction, _heroMainHeroGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = _resolveCareerScriptHeroMethod;
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Castclass, _heroType);
                    continue;
                }

                if (Calls(instruction, _agentMainGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = _resolveCareerScriptAgentMethod;
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Castclass, _agentType);
                    continue;
                }

                if (Calls(instruction, _belongsToMainPartyMethod))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = _belongsToCareerScriptPartyMethod;
                }

                yield return instruction;
            }
        }

        private static bool IsRegisteredSharedHeroAgent(object agent)
        {
            if (agent == null || !_agentType.IsInstanceOfType(agent))
            {
                return false;
            }

            object hero = _getHeroMethod.Invoke(null, new[] { agent });
            if (hero == null)
            {
                return false;
            }

            object behavior = _mccBehaviorInstanceProperty.GetValue(null, null);
            return behavior != null && Convert.ToBoolean(_isRegisteredSharedHeroMethod.Invoke(behavior, new[] { hero }));
        }

        private static object GetAgentComponent(object agent, Type componentType)
        {
            return _agentGetComponentDefinition.MakeGenericMethod(componentType).Invoke(agent, null);
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

        private static bool Calls(CodeInstruction instruction, MethodInfo target)
        {
            return instruction != null && target != null && instruction.operand is MethodInfo method && method == target;
        }

        private static int IndexOfReference(IList list, object value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], value))
                {
                    return i;
                }
            }
            return -1;
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

        private static FieldInfo RequireFieldInHierarchy(Type type, string name)
        {
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(name, DeclaredInstanceFlags);
                if (field != null)
                {
                    return field;
                }
                current = current.BaseType;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags)
        {
            MethodInfo method = type.GetMethod(name, flags);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
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
                if (hero == null)
                {
                    return "<null>";
                }
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
                string directory = System.IO.Path.Combine(documents, "Mount and Blade II Bannerlord", "Configs", "ModLogs");
                System.IO.Directory.CreateDirectory(directory);
                string path = System.IO.Path.Combine(directory, "MultiCharacterCampaignTOR.log");
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("O") + " [AI Career Transition] " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
