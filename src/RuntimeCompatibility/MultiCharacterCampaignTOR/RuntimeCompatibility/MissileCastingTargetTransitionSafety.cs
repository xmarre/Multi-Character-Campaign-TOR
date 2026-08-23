using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Guards TOR 1.16's MissileCastingBehavior.UpdateTarget(Target) against transient target teardown.
    ///
    /// The native method immediately dereferences CurrentTarget.Formation and later writes through the
    /// Target argument. During mission transitions (notably hideout -> boss mission) those references can
    /// legitimately disappear before the casting behavior itself is discarded. Returning the incoming
    /// target unchanged preserves the method's update contract without swallowing unrelated exceptions or
    /// changing stable-mission targeting behavior.
    /// </summary>
    internal static class MissileCastingTargetTransitionSafety
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;
        private static FieldInfo _currentTargetField;
        private static FieldInfo _formationField;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                Type abstractBehaviorType = RequireType(
                    "TOR_Core.BattleMechanics.AI.CastingAI.AgentCastingBehavior.AbstractAgentCastingBehavior, TOR_Core");
                Type missileBehaviorType = RequireType(
                    "TOR_Core.BattleMechanics.AI.CastingAI.AgentCastingBehavior.MissileCastingBehavior, TOR_Core");
                Type targetType = RequireType(
                    "TOR_Core.BattleMechanics.AI.CommonAIFunctions+Target, TOR_Core");

                _currentTargetField = RequireFieldInHierarchy(abstractBehaviorType, "CurrentTarget");
                _formationField = RequireFieldInHierarchy(targetType, "Formation");

                MethodInfo updateTarget = missileBehaviorType.GetMethods(InstanceFlags)
                    .SingleOrDefault(method => method.Name == "UpdateTarget" && method.GetParameters().Length == 1);
                if (updateTarget == null)
                {
                    throw new MissingMethodException(missileBehaviorType.FullName, "UpdateTarget(Target)");
                }

                ParameterInfo targetParameter = updateTarget.GetParameters()[0];
                if (!targetParameter.ParameterType.IsAssignableFrom(targetType) &&
                    !targetType.IsAssignableFrom(targetParameter.ParameterType))
                {
                    throw new InvalidOperationException(
                        "TOR MissileCastingBehavior.UpdateTarget has an unexpected target parameter type: " +
                        targetParameter.ParameterType.FullName + ".");
                }
                if (updateTarget.ReturnType != targetParameter.ParameterType)
                {
                    throw new InvalidOperationException(
                        "TOR MissileCastingBehavior.UpdateTarget no longer returns its Target parameter type.");
                }

                new Harmony("xmarre.multicharactercampaign.tor.missile-target-transition-safety").Patch(
                    updateTarget,
                    transpiler: new HarmonyMethod(typeof(MissileCastingTargetTransitionSafety), nameof(GuardTargetStateTranspiler)));

                _installed = true;
                Log("Installed TOR MissileCastingBehavior transition target guard.");
            }
            catch (Exception ex)
            {
                Log("TOR MissileCastingBehavior transition target guard installation failed safely: " + Unwrap(ex));
            }
        }

        private static IEnumerable<CodeInstruction> GuardTargetStateTranspiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            List<CodeInstruction> original = instructions.ToList();
            if (original.Count == 0 || _currentTargetField == null || _formationField == null)
            {
                return original;
            }

            Label runOriginal = generator.DefineLabel();
            Label returnIncomingTarget = generator.DefineLabel();
            original[0].labels.Add(runOriginal);

            var guarded = new List<CodeInstruction>
            {
                // if (__instance.CurrentTarget == null) return target;
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, _currentTargetField),
                new CodeInstruction(OpCodes.Brfalse, returnIncomingTarget),

                // if (__instance.CurrentTarget.Formation == null) return target;
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, _currentTargetField),
                new CodeInstruction(OpCodes.Ldfld, _formationField),
                new CodeInstruction(OpCodes.Brfalse, returnIncomingTarget),

                // if (target == null) return target;
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Brtrue, runOriginal)
            };

            var loadIncomingTarget = new CodeInstruction(OpCodes.Ldarg_1);
            loadIncomingTarget.labels.Add(returnIncomingTarget);
            guarded.Add(loadIncomingTarget);
            guarded.Add(new CodeInstruction(OpCodes.Ret));
            guarded.AddRange(original);
            return guarded;
        }

        private static FieldInfo RequireFieldInHierarchy(Type type, string name)
        {
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(name, BindingFlags.DeclaredOnly | InstanceFlags);
                if (field != null)
                {
                    return field;
                }
                current = current.BaseType;
            }

            throw new MissingFieldException(type.FullName, name);
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName, false);
            if (type == null)
            {
                throw new TypeLoadException("Required runtime type not found: " + assemblyQualifiedName);
            }
            return type;
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
                Type logType = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", false);
                MethodInfo info = logType?.GetMethod(
                    "Info",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
                info?.Invoke(null, new object[] { "[MissileTargetTransitionSafety] " + message });
            }
            catch
            {
            }
        }
    }
}
