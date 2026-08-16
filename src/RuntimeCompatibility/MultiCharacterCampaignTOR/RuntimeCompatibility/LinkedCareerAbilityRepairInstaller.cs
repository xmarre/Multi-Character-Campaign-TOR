using System;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Installs the recovered active-player career-ability invariant repair without using the
    /// recovered installer's assembly-qualified Harmony lookup. The repair callbacks themselves are
    /// retained unchanged; only their bootstrap is replaced with the RuntimeCompatibility project's
    /// hard linked 0Harmony reference.
    /// </summary>
    internal static class LinkedCareerAbilityRepairInstaller
    {
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                Type repairType = typeof(CareerAbilityRepair);
                InvokeStatic(repairType, "ResolveAndCacheRuntimeMembers");

                Type abilityManagerType = ReadStaticField<Type>(repairType, "_abilityManagerMissionLogicType");
                Type abilityHudType = ReadStaticField<Type>(repairType, "_abilityHudMissionViewType");
                ConstructorInfo abilityComponentCtor = ReadStaticField<ConstructorInfo>(repairType, "_abilityComponentConstructor");

                MethodInfo onAgentCreated = RequireUniqueMethod(abilityManagerType, "OnAgentCreated", 1);
                MethodInfo hudInitialize = RequireUniqueMethod(abilityHudType, "OnBehaviorInitialize", 0);
                MethodInfo checkMainAgent = RequireUniqueMethod(abilityHudType, "CheckMainAgent", 1);
                MethodInfo onControllerChanged = RequireUniqueMethod(abilityManagerType, "OnAgentControllerChanged", 2);
                MethodInfo onEndMission = RequireUniqueMethod(abilityManagerType, "OnEndMission", 0);

                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.careerabilityinvariant.v140");
                harmony.Patch(
                    onAgentCreated,
                    prefix: PatchMethod(repairType, "OnAgentCreatedPrefix"),
                    postfix: PatchMethod(repairType, "OnAgentCreatedPostfix"));
                harmony.Patch(
                    abilityComponentCtor,
                    prefix: PatchMethod(repairType, "AbilityComponentCtorPrefix"));
                harmony.Patch(
                    hudInitialize,
                    postfix: PatchMethod(repairType, "HudInitPostfix"));
                harmony.Patch(
                    checkMainAgent,
                    prefix: PatchMethod(repairType, "CheckMainAgentPrefix"));
                harmony.Patch(
                    onControllerChanged,
                    postfix: PatchMethod(repairType, "OnAgentControllerChangedPostfix"));
                harmony.Patch(
                    onEndMission,
                    postfix: PatchMethod(repairType, "OnEndMissionPostfix"));

                // Prevent any later compatibility retry from entering the recovered loader-sensitive
                // installer and attempting to apply the same patches a second time.
                FieldInfo legacyInstalled = RequireStaticField(repairType, "_installed");
                legacyInstalled.SetValue(null, true);
                _installed = true;

                Log("Installed TOR career-ability invariant repair through the linked 0Harmony reference.");
            }
            catch (Exception ex)
            {
                Log("Linked TOR career-ability invariant repair installation failed safely: " + Unwrap(ex));
            }
        }

        private static HarmonyMethod PatchMethod(Type repairType, string name)
        {
            MethodInfo method = repairType.GetMethod(name, StaticFlags);
            if (method == null)
            {
                throw new MissingMethodException(repairType.FullName, name);
            }
            return new HarmonyMethod(method);
        }

        private static MethodInfo RequireUniqueMethod(Type type, string name, int parameterCount)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            MethodInfo match = null;
            foreach (MethodInfo method in type.GetMethods(InstanceFlags | StaticFlags))
            {
                if (method.Name != name || method.GetParameters().Length != parameterCount)
                {
                    continue;
                }
                if (match != null)
                {
                    throw new AmbiguousMatchException(type.FullName + "." + name);
                }
                match = method;
            }

            if (match == null)
            {
                throw new MissingMethodException(type.FullName, name + "/" + parameterCount);
            }
            return match;
        }

        private static void InvokeStatic(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, StaticFlags, null, Type.EmptyTypes, null);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            method.Invoke(null, null);
        }

        private static T ReadStaticField<T>(Type type, string name) where T : class
        {
            object value = RequireStaticField(type, name).GetValue(null);
            T typed = value as T;
            if (typed == null)
            {
                throw new InvalidOperationException(type.FullName + "." + name + " was null or had the wrong type.");
            }
            return typed;
        }

        private static FieldInfo RequireStaticField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, StaticFlags);
            if (field == null)
            {
                throw new MissingFieldException(type.FullName, name);
            }
            return field;
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
                Type logType = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", throwOnError: false);
                MethodInfo info = logType?.GetMethod("Info", StaticFlags, null, new[] { typeof(string) }, null);
                info?.Invoke(null, new object[] { "[CareerAbilityFix134] " + message });
            }
            catch
            {
            }
        }
    }
}
