using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Adapts TOR's existing spell AI to dedicated CareerAbility templates owned by MCC shared AI heroes.
    /// TOR normally never sends CareerAbilityEffect through WizardAI, so its generic fallback behaves like a
    /// missile and its AI cast-frame path ignores targeted ground/world positions. Keep TOR's native behaviors
    /// where they already fit and only repair the unsupported career-specific cases.
    /// </summary>
    internal static class AICareerAbilityBehaviorBridge
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;

        private static Type _abilityType;
        private static Type _abilityComponentType;
        private static Type _careerAbilityType;
        private static Type _abilityTemplateType;
        private static Type _wizardAIComponentType;
        private static Type _abstractCastingBehaviorType;
        private static Type _missileCastingBehaviorType;
        private static Type _summoningCastingBehaviorType;
        private static Type _tacticalTeleportCastingBehaviorType;
        private static Type _castingBehaviorConfigurationType;
        private static Type _targetType;
        private static Type _agentExtensionsType;
        private static Type _mccBehaviorType;
        private static Type _damselTeleportScriptType;
        private static Type _abilityScriptType;

        private static MethodInfo _agentGetComponentDefinition;
        private static MethodInfo _getHeroMethod;
        private static MethodInfo _isRegisteredSharedHeroMethod;
        private static MethodInfo _targetGetPositionMethod;
        private static MethodInfo _behaviorCalculateRotationMethod;
        private static MethodInfo _cameraFadeMethod;
        private static MethodInfo _abilityScriptCasterAgentGetter;

        private static PropertyInfo _mccBehaviorInstanceProperty;
        private static PropertyInfo _careerAbilityProperty;
        private static PropertyInfo _currentAbilityProperty;
        private static PropertyInfo _knownAbilitySystemProperty;
        private static PropertyInfo _abilityTemplateProperty;

        private static PropertyInfo _templateStringIdProperty;
        private static PropertyInfo _templateEffectTypeProperty;
        private static PropertyInfo _templateTargetTypeProperty;
        private static PropertyInfo _templateCrosshairTypeProperty;
        private static PropertyInfo _templateBaseMovementSpeedProperty;

        private static FieldInfo _behaviorAgentField;
        private static FieldInfo _behaviorAbilityIndexField;
        private static FieldInfo _behaviorCurrentTargetField;
        private static FieldInfo _wizardCurrentCastingBehaviorField;
        private static FieldInfo _supportActiveCareerScriptField;

        private static ConstructorInfo _summoningBehaviorConstructor;
        private static ConstructorInfo _tacticalTeleportBehaviorConstructor;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                ResolveRuntimeSurfaces();

                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.ai-career-behavior");

                MethodInfo prepareCastingBehaviors = FindUniqueMethod(_castingBehaviorConfigurationType, "PrepareCastingBehaviors", StaticFlags, 1);
                MethodInfo missileLineOfSight = FindUniqueMethod(_missileCastingBehaviorType, "HaveLineOfSightToTarget", InstanceFlags, 1);
                MethodInfo calculateAICastFrame = FindUniqueMethod(_abilityType, "CalculateAICastMatrixFrame", InstanceFlags, 1);

                harmony.Patch(
                    prepareCastingBehaviors,
                    postfix: new HarmonyMethod(typeof(AICareerAbilityBehaviorBridge), nameof(PrepareCastingBehaviorsPostfix)));

                harmony.Patch(
                    missileLineOfSight,
                    prefix: new HarmonyMethod(typeof(AICareerAbilityBehaviorBridge), nameof(MissileLineOfSightPrefix)));

                harmony.Patch(
                    calculateAICastFrame,
                    postfix: new HarmonyMethod(typeof(AICareerAbilityBehaviorBridge), nameof(CalculateAICastFramePostfix)));

                PatchAICameraSafety(harmony);

                _installed = true;
                Log("Installed TOR career-ability AI behavior, targeting, cast-frame, and camera-safety bridge.");
            }
            catch (Exception ex)
            {
                Log("AI career behavior bridge installation failed safely: " + Unwrap(ex));
            }
        }

        private static void ResolveRuntimeSurfaces()
        {
            _abilityType = RequireType("TOR_Core.AbilitySystem.Ability, TOR_Core");
            _abilityComponentType = RequireType("TOR_Core.AbilitySystem.AbilityComponent, TOR_Core");
            _careerAbilityType = RequireType("TOR_Core.AbilitySystem.CareerAbility, TOR_Core");
            _abilityTemplateType = RequireType("TOR_Core.AbilitySystem.AbilityTemplate, TOR_Core");
            _wizardAIComponentType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.Components.WizardAIComponent, TOR_Core");
            _abstractCastingBehaviorType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.AgentCastingBehavior.AbstractAgentCastingBehavior, TOR_Core");
            _missileCastingBehaviorType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.AgentCastingBehavior.MissileCastingBehavior, TOR_Core");
            _summoningCastingBehaviorType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.AgentCastingBehavior.SummoningCastingBehavior, TOR_Core");
            _tacticalTeleportCastingBehaviorType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.AgentCastingBehavior.TacticalTeleportCastingBehavior, TOR_Core");
            _castingBehaviorConfigurationType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.AgentCastingBehaviorConfiguration, TOR_Core");
            _targetType = RequireType("TOR_Core.BattleMechanics.AI.CastingAI.Target, TOR_Core");
            _agentExtensionsType = RequireType("TOR_Core.Extensions.AgentExtensions, TOR_Core");
            _mccBehaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");
            _damselTeleportScriptType = RequireType("TOR_Core.AbilitySystem.Scripts.DamselTeleportScript, TOR_Core");
            _abilityScriptType = RequireType("TOR_Core.AbilitySystem.Scripts.AbilityScript, TOR_Core");

            _agentGetComponentDefinition = typeof(Agent).GetMethods(InstanceFlags)
                .Single(method => method.Name == "GetComponent" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
            _getHeroMethod = FindUniqueMethod(_agentExtensionsType, "GetHero", StaticFlags, 1);
            _targetGetPositionMethod = FindUniqueMethod(_targetType, "GetPositionPrioritizeCalculated", InstanceFlags, 0);
            _behaviorCalculateRotationMethod = FindUniqueMethod(_abstractCastingBehaviorType, "CalculateSpellRotation", InstanceFlags, 2);

            _mccBehaviorInstanceProperty = RequireProperty(_mccBehaviorType, "Instance", StaticFlags);
            _isRegisteredSharedHeroMethod = FindUniqueMethod(_mccBehaviorType, "IsRegisteredSharedHero", InstanceFlags, 1);

            _careerAbilityProperty = RequireProperty(_abilityComponentType, "CareerAbility", InstanceFlags);
            _currentAbilityProperty = RequireProperty(_abilityComponentType, "CurrentAbility", InstanceFlags);
            _knownAbilitySystemProperty = RequireProperty(_abilityComponentType, "KnownAbilitySystem", InstanceFlags);
            _abilityTemplateProperty = RequireProperty(_abilityType, "Template", InstanceFlags);

            _templateStringIdProperty = RequireProperty(_abilityTemplateType, "StringID", InstanceFlags);
            _templateEffectTypeProperty = RequireProperty(_abilityTemplateType, "AbilityEffectType", InstanceFlags);
            _templateTargetTypeProperty = RequireProperty(_abilityTemplateType, "AbilityTargetType", InstanceFlags);
            _templateCrosshairTypeProperty = RequireProperty(_abilityTemplateType, "CrosshairType", InstanceFlags);
            _templateBaseMovementSpeedProperty = RequireProperty(_abilityTemplateType, "BaseMovementSpeed", InstanceFlags);

            _behaviorAgentField = RequireField(_abstractCastingBehaviorType, "Agent", InstanceFlags);
            _behaviorAbilityIndexField = RequireField(_abstractCastingBehaviorType, "AbilityIndex", InstanceFlags);
            _behaviorCurrentTargetField = RequireField(_abstractCastingBehaviorType, "CurrentTarget", InstanceFlags);
            _wizardCurrentCastingBehaviorField = RequireField(_wizardAIComponentType, "CurrentCastingBehavior", InstanceFlags);

            _summoningBehaviorConstructor = RequireBehaviorConstructor(_summoningCastingBehaviorType);
            _tacticalTeleportBehaviorConstructor = RequireBehaviorConstructor(_tacticalTeleportCastingBehaviorType);

            _abilityScriptCasterAgentGetter = RequireProperty(_abilityScriptType, "CasterAgent", InstanceFlags).GetGetMethod(true);
            _supportActiveCareerScriptField = RequireField(typeof(AICareerAbilitySupport), "_activeCareerScript", StaticFlags);
        }

        private static void PrepareCastingBehaviorsPostfix(Agent __0, object __result)
        {
            try
            {
                Agent agent = __0;
                IList behaviors = __result as IList;
                if (agent == null || behaviors == null || !TryGetRegisteredSharedAICareer(agent, out object component, out object careerAbility, out object template))
                {
                    return;
                }

                if (!IsCareerAbilityEffect(template))
                {
                    return;
                }

                IList known = _knownAbilitySystemProperty.GetValue(component, null) as IList;
                int index = IndexOfReference(known, careerAbility);
                if (index < 0 || index >= behaviors.Count)
                {
                    return;
                }

                string templateId = Convert.ToString(_templateStringIdProperty.GetValue(template, null));
                object replacement = null;

                // Fey Paths is a true repositioning skill. TOR already has a safety-aware tactical
                // teleport AI; use it instead of the CareerAbilityEffect -> missile fallback.
                if (string.Equals(templateId, "FeyPaths", StringComparison.Ordinal))
                {
                    replacement = _tacticalTeleportBehaviorConstructor.Invoke(new object[] { agent, template, index });
                }
                // Greater Harbinger is a ground summon. TOR's summoning AI intentionally summons near
                // the caster when battle pressure warrants it and respects the mission agent cap.
                else if (string.Equals(templateId, "GreaterHarbinger", StringComparison.Ordinal))
                {
                    replacement = _summoningBehaviorConstructor.Invoke(new object[] { agent, template, index });
                }

                if (replacement != null)
                {
                    behaviors[index] = replacement;
                    Log("Mapped shared AI career ability " + templateId + " to TOR behavior " + replacement.GetType().Name + ".");
                }
            }
            catch (Exception ex)
            {
                Log("Career AI behavior mapping failed safely: " + Unwrap(ex));
            }
        }

        private static bool MissileLineOfSightPrefix(object __instance, ref bool __result)
        {
            try
            {
                if (!TryGetBehaviorCareerContext(__instance, out Agent agent, out object template))
                {
                    return true;
                }

                if (!IsCareerAbilityEffect(template))
                {
                    return true;
                }

                string targetType = EnumName(_templateTargetTypeProperty.GetValue(template, null));
                string crosshairType = EnumName(_templateCrosshairTypeProperty.GetValue(template, null));

                // CareerAbilityEffect uses MaxDistance=1 by default in TOR. Self/local career skills
                // therefore fail the generic missile LOS/range test even though they do not require a
                // world-space target at all. Bypass only that inappropriate missile gate.
                if (string.Equals(targetType, "Self", StringComparison.Ordinal) ||
                    string.Equals(crosshairType, "Self", StringComparison.Ordinal))
                {
                    __result = true;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log("Career AI local-cast LOS bridge failed open: " + Unwrap(ex));
                return true;
            }
        }

        private static void CalculateAICastFramePostfix(object __instance, Agent __0, ref MatrixFrame __result)
        {
            try
            {
                Agent agent = __0;
                if (agent == null || !TryGetRegisteredSharedAICareer(agent, out object component, out object careerAbility, out object template))
                {
                    return;
                }

                object currentAbility = _currentAbilityProperty.GetValue(component, null);
                if (!ReferenceEquals(currentAbility, careerAbility) || !IsCareerAbilityEffect(template))
                {
                    return;
                }

                object wizard = GetAgentComponent(agent, _wizardAIComponentType);
                object behavior = wizard != null ? _wizardCurrentCastingBehaviorField.GetValue(wizard) : null;
                object target = behavior != null ? _behaviorCurrentTargetField.GetValue(behavior) : null;
                if (target == null)
                {
                    return;
                }

                object positionValue = _targetGetPositionMethod.Invoke(target, null);
                if (!(positionValue is Vec3 targetPosition) || targetPosition == Vec3.Invalid)
                {
                    return;
                }

                string targetType = EnumName(_templateTargetTypeProperty.GetValue(template, null));
                string crosshairType = EnumName(_templateCrosshairTypeProperty.GetValue(template, null));

                // Player CareerAbilityEffect ground casts use the crosshair position, but TOR's AI
                // CareerAbilityEffect branch always starts at the caster. Restore target parity for
                // genuinely targeted ground skills while keeping local/self ground slams on the caster.
                if (string.Equals(targetType, "GroundAtPosition", StringComparison.Ordinal) &&
                    !string.Equals(crosshairType, "Self", StringComparison.Ordinal))
                {
                    targetPosition.z = Mission.Current.Scene.GetGroundHeightAtPosition(targetPosition);
                    __result = new MatrixFrame(Mat3.Identity, targetPosition);
                    return;
                }

                // Moving CareerAbilityEffect projectiles (e.g. Blast of Agony) spawn at the caster in
                // TOR's AI branch but are not rotated toward WizardAI's selected target. Aim them using
                // the same behavior target that ordinary TOR missile AI selected.
                float movementSpeed = Convert.ToSingle(_templateBaseMovementSpeedProperty.GetValue(template, null));
                if (movementSpeed > 0f &&
                    !string.Equals(targetType, "Self", StringComparison.Ordinal) &&
                    !string.Equals(crosshairType, "Self", StringComparison.Ordinal))
                {
                    object rotationValue = _behaviorCalculateRotationMethod.Invoke(behavior, new object[] { targetPosition, __result.origin });
                    if (rotationValue is Mat3 rotation)
                    {
                        __result.rotation = rotation;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Career AI cast-frame bridge failed safely: " + Unwrap(ex));
            }
        }

        private static void PatchAICameraSafety(Harmony harmony)
        {
            MethodInfo damselOnAfterTick = FindUniqueMethod(_damselTeleportScriptType, "OnAfterTick", InstanceFlags, 1);
            _cameraFadeMethod = FindCameraFadeMethod(damselOnAfterTick);
            if (_cameraFadeMethod == null)
            {
                Log("DamselTeleportScript does not call MissionCameraFadeView.BeginFadeOutAndIn on this TOR build; camera guard not required.");
                return;
            }

            harmony.Patch(
                damselOnAfterTick,
                transpiler: new HarmonyMethod(typeof(AICareerAbilityBehaviorBridge), nameof(DamselTeleportCameraTranspiler)));
        }

        private static System.Collections.Generic.IEnumerable<CodeInstruction> DamselTeleportCameraTranspiler(System.Collections.Generic.IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = typeof(AICareerAbilityBehaviorBridge).GetMethod(nameof(BeginCameraFadeUnlessSharedAI), StaticFlags);
            foreach (CodeInstruction instruction in instructions)
            {
                if (_cameraFadeMethod != null && instruction.operand is MethodInfo called && called == _cameraFadeMethod)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
                yield return instruction;
            }
        }

        private static void BeginCameraFadeUnlessSharedAI(object cameraView, float fadeOutTime, float fadeInTime, float stayBlackTime)
        {
            if (cameraView == null || _cameraFadeMethod == null)
            {
                return;
            }

            try
            {
                object script = _supportActiveCareerScriptField.GetValue(null);
                object casterValue = script != null ? _abilityScriptCasterAgentGetter.Invoke(script, null) : null;
                if (casterValue is Agent caster && IsRegisteredSharedAI(caster))
                {
                    // The camera belongs to the actual player, not the remote AI Damsel.
                    return;
                }

                _cameraFadeMethod.Invoke(cameraView, new object[] { fadeOutTime, fadeInTime, stayBlackTime });
            }
            catch (Exception ex)
            {
                Log("Damsel camera guard failed safely: " + Unwrap(ex));
            }
        }

        private static bool TryGetBehaviorCareerContext(object behavior, out Agent agent, out object template)
        {
            agent = null;
            template = null;
            if (behavior == null || !_abstractCastingBehaviorType.IsInstanceOfType(behavior))
            {
                return false;
            }

            agent = _behaviorAgentField.GetValue(behavior) as Agent;
            if (agent == null || !IsRegisteredSharedAI(agent))
            {
                return false;
            }

            int index = Convert.ToInt32(_behaviorAbilityIndexField.GetValue(behavior));
            object component = GetAgentComponent(agent, _abilityComponentType);
            IList known = component != null ? _knownAbilitySystemProperty.GetValue(component, null) as IList : null;
            if (known == null || index < 0 || index >= known.Count)
            {
                return false;
            }

            object ability = known[index];
            if (ability == null || !_careerAbilityType.IsInstanceOfType(ability))
            {
                return false;
            }

            template = _abilityTemplateProperty.GetValue(ability, null);
            return template != null;
        }

        private static bool TryGetRegisteredSharedAICareer(Agent agent, out object component, out object careerAbility, out object template)
        {
            component = null;
            careerAbility = null;
            template = null;

            if (!IsRegisteredSharedAI(agent))
            {
                return false;
            }

            component = GetAgentComponent(agent, _abilityComponentType);
            if (component == null)
            {
                return false;
            }

            careerAbility = _careerAbilityProperty.GetValue(component, null);
            if (careerAbility == null || !_careerAbilityType.IsInstanceOfType(careerAbility))
            {
                return false;
            }

            template = _abilityTemplateProperty.GetValue(careerAbility, null);
            return template != null;
        }

        private static bool IsRegisteredSharedAI(Agent agent)
        {
            if (agent == null || !agent.IsAIControlled)
            {
                return false;
            }

            object hero = _getHeroMethod.Invoke(null, new object[] { agent });
            if (hero == null)
            {
                return false;
            }

            object behavior = _mccBehaviorInstanceProperty.GetValue(null, null);
            return behavior != null && Convert.ToBoolean(_isRegisteredSharedHeroMethod.Invoke(behavior, new[] { hero }));
        }

        private static bool IsCareerAbilityEffect(object template)
        {
            return template != null && string.Equals(
                EnumName(_templateEffectTypeProperty.GetValue(template, null)),
                "CareerAbilityEffect",
                StringComparison.Ordinal);
        }

        private static object GetAgentComponent(Agent agent, Type componentType)
        {
            return _agentGetComponentDefinition.MakeGenericMethod(componentType).Invoke(agent, null);
        }

        private static ConstructorInfo RequireBehaviorConstructor(Type behaviorType)
        {
            ConstructorInfo ctor = behaviorType.GetConstructors(InstanceFlags)
                .SingleOrDefault(candidate =>
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length == 3 &&
                           parameters[0].ParameterType == typeof(Agent) &&
                           parameters[1].ParameterType == _abilityTemplateType &&
                           parameters[2].ParameterType == typeof(int);
                });
            if (ctor == null)
            {
                throw new MissingMethodException(behaviorType.FullName, ".ctor(Agent, AbilityTemplate, int)");
            }
            return ctor;
        }

        private static MethodInfo FindCameraFadeMethod(MethodInfo method)
        {
            MethodBody body = method.GetMethodBody();
            byte[] il = body != null ? body.GetILAsByteArray() : null;
            if (il == null)
            {
                return null;
            }

            foreach (MethodBase called in EnumerateCalledMethods(method, il))
            {
                MethodInfo info = called as MethodInfo;
                if (info != null &&
                    string.Equals(info.Name, "BeginFadeOutAndIn", StringComparison.Ordinal) &&
                    info.GetParameters().Length == 3)
                {
                    return info;
                }
            }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<MethodBase> EnumerateCalledMethods(MethodInfo owner, byte[] il)
        {
            int position = 0;
            while (position < il.Length)
            {
                OpCode opcode;
                byte first = il[position++];
                if (first == 0xfe)
                {
                    if (position >= il.Length) yield break;
                    opcode = MultiByteOpCodes[il[position++]];
                }
                else
                {
                    opcode = SingleByteOpCodes[first];
                }

                if (opcode.OperandType == OperandType.InlineMethod && position + 4 <= il.Length)
                {
                    int token = BitConverter.ToInt32(il, position);
                    MethodBase resolved = null;
                    try
                    {
                        resolved = owner.Module.ResolveMethod(token);
                    }
                    catch
                    {
                    }
                    if (resolved != null)
                    {
                        yield return resolved;
                    }
                }

                position += OperandSize(opcode.OperandType, il, position);
            }
        }

        private static readonly OpCode[] SingleByteOpCodes = BuildSingleByteOpCodes();
        private static readonly OpCode[] MultiByteOpCodes = BuildMultiByteOpCodes();

        private static OpCode[] BuildSingleByteOpCodes()
        {
            OpCode[] result = new OpCode[256];
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (field.FieldType != typeof(OpCode)) continue;
                OpCode opcode = (OpCode)field.GetValue(null);
                ushort value = unchecked((ushort)opcode.Value);
                if (value < 0x100)
                {
                    result[value] = opcode;
                }
            }
            return result;
        }

        private static OpCode[] BuildMultiByteOpCodes()
        {
            OpCode[] result = new OpCode[256];
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (field.FieldType != typeof(OpCode)) continue;
                OpCode opcode = (OpCode)field.GetValue(null);
                ushort value = unchecked((ushort)opcode.Value);
                if ((value & 0xff00) == 0xfe00)
                {
                    result[value & 0xff] = opcode;
                }
            }
            return result;
        }

        private static int OperandSize(OperandType operandType, byte[] il, int position)
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
                    if (position + 4 > il.Length) return 0;
                    int count = BitConverter.ToInt32(il, position);
                    return 4 + Math.Max(0, count) * 4;
                default:
                    return 0;
            }
        }

        private static int IndexOfReference(IList list, object value)
        {
            if (list == null) return -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], value)) return i;
            }
            return -1;
        }

        private static string EnumName(object value)
        {
            return value != null ? value.ToString() : string.Empty;
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
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                current = current.BaseType;
            }
            throw new MissingFieldException(type.FullName, name);
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
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("O") + " [AI Career Behavior] " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
