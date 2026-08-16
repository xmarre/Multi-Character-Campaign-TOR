using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// TOR's Greater Harbinger career script is designed for the player: when the summon appears it
    /// unconditionally transfers AgentControllerType.Player to the champion. Registered MCC heroes can
    /// now cast that career while AI-controlled, so that native player-only controller swap must be
    /// suppressed for the AI path or a remote Necromancer steals the actual player's camera/control.
    /// </summary>
    internal static class HarbingerAIControllerSafety
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;
        private static PropertyInfo _casterAgentProperty;
        private static FieldInfo _championField;
        private static FieldInfo _championIsActiveField;
        private static MethodInfo _isRegisteredSharedHeroAgentMethod;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                Type scriptType = RequireType("TOR_Core.AbilitySystem.Scripts.SummonChampionScript, TOR_Core");
                _casterAgentProperty = FindProperty(scriptType, "CasterAgent");
                _championField = RequireField(scriptType, "_champion");
                _championIsActiveField = RequireField(scriptType, "_championIsActive");
                _isRegisteredSharedHeroAgentMethod = typeof(AICareerAbilityTransitionGuard).GetMethod(
                    "IsRegisteredSharedHeroAgent",
                    StaticFlags);

                if (_casterAgentProperty == null || _isRegisteredSharedHeroAgentMethod == null)
                {
                    throw new MissingMemberException("Required MCC/TOR Harbinger ownership surfaces are unavailable.");
                }

                MethodInfo shiftToChampion = RequireMethod(scriptType, "ShiftControllerToChampion");
                MethodInfo shiftToCaster = RequireMethod(scriptType, "ShiftControllerToCaster");
                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.ai-harbinger-controller-safety");

                harmony.Patch(
                    shiftToChampion,
                    prefix: new HarmonyMethod(typeof(HarbingerAIControllerSafety), nameof(BeforeShiftControllerToChampion)));
                harmony.Patch(
                    shiftToCaster,
                    prefix: new HarmonyMethod(typeof(HarbingerAIControllerSafety), nameof(BeforeShiftControllerToCaster)));

                _installed = true;
                Log("Installed Greater Harbinger AI controller/camera takeover guard.");
            }
            catch (Exception ex)
            {
                Log("Greater Harbinger AI controller guard installation failed safely: " + Unwrap(ex));
            }
        }

        private static bool BeforeShiftControllerToChampion(object __instance)
        {
            return NormalizeSharedAIHarbinger(__instance, wieldChampion: true);
        }

        private static bool BeforeShiftControllerToCaster(object __instance)
        {
            return NormalizeSharedAIHarbinger(__instance, wieldChampion: false);
        }

        /// <summary>
        /// Return true for TOR's normal player path. Return false only for a registered MCC hero that is
        /// currently AI-controlled, after normalizing both summon and caster to AI ownership.
        /// </summary>
        private static bool NormalizeSharedAIHarbinger(object script, bool wieldChampion)
        {
            Agent caster = null;
            try
            {
                if (script == null)
                {
                    return true;
                }

                caster = _casterAgentProperty.GetValue(script, null) as Agent;
                if (caster == null || !caster.IsAIControlled || !IsRegisteredSharedHeroAgent(caster))
                {
                    return true;
                }

                Agent champion = _championField.GetValue(script) as Agent;

                // This career script's controller transfer is purely a player mechanic. For a remote MCC
                // AI hero both agents remain AI-controlled; the actual player's Agent.Main is untouched.
                if (caster.IsActive())
                {
                    caster.Controller = AgentControllerType.AI;
                }
                if (champion != null && champion.IsActive())
                {
                    champion.Controller = AgentControllerType.AI;
                    if (wieldChampion)
                    {
                        champion.WieldInitialWeapons();
                    }
                }

                _championIsActiveField.SetValue(script, false);
                return false;
            }
            catch (Exception ex)
            {
                // Once we know this is the registered-AI path, fail closed: running TOR's original method
                // is worse than losing this controller transition because it explicitly steals Player control.
                if (caster != null && IsRegisteredSharedHeroAgentSafe(caster))
                {
                    Log("Greater Harbinger AI normalization failed closed: " + Unwrap(ex));
                    return false;
                }

                Log("Greater Harbinger controller guard failed open before shared-AI ownership was established: " + Unwrap(ex));
                return true;
            }
        }

        private static bool IsRegisteredSharedHeroAgent(Agent agent)
        {
            return agent != null && Convert.ToBoolean(_isRegisteredSharedHeroAgentMethod.Invoke(null, new object[] { agent }));
        }

        private static bool IsRegisteredSharedHeroAgentSafe(Agent agent)
        {
            try
            {
                return IsRegisteredSharedHeroAgent(agent);
            }
            catch
            {
                return false;
            }
        }

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name, throwOnError: false);
            if (type == null)
            {
                throw new TypeLoadException(name);
            }
            return type;
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, InstanceFlags, null, Type.EmptyTypes, null);
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

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }
            }
            return null;
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
                info?.Invoke(null, new object[] { "[HarbingerAISafety] " + message });
            }
            catch
            {
            }
        }
    }
}
