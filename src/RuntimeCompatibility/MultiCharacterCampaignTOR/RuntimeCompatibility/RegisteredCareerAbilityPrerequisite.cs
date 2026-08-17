using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// TOR decides whether to construct AbilityComponent at the start of AbilityManagerMissionLogic.OnAgentCreated
    /// by checking the hero's persistent AbilityUser attribute. A registered MCC hero can reach that callback before
    /// Bannerlord has finalized the spawned agent's AI controller, so the older AI-only prerequisite hook could miss
    /// the first battle. Using the career ability once as the player happened to persist AbilityUser and masked the
    /// problem on later spawns.
    ///
    /// Establish the prerequisite from stable ownership instead: registered shared hero + TOR career. The existing
    /// AI-career support still owns CareerAbility/WizardAI creation and controller transitions; this patch only makes
    /// TOR's native component-construction prerequisite deterministic before the native OnAgentCreated body runs.
    /// </summary>
    internal static class RegisteredCareerAbilityPrerequisite
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;
        private static Type _agentType;
        private static MethodInfo _getHeroMethod;
        private static MethodInfo _getCareerMethod;
        private static MethodInfo _hasAttributeMethod;
        private static MethodInfo _addAttributeMethod;
        private static PropertyInfo _behaviorInstanceProperty;
        private static MethodInfo _isRegisteredSharedHeroMethod;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                _agentType = RequireType("TaleWorlds.MountAndBlade.Agent, TaleWorlds.MountAndBlade");
                Type abilityManagerType = RequireType("TOR_Core.AbilitySystem.AbilityManagerMissionLogic, TOR_Core");
                Type agentExtensionsType = RequireType("TOR_Core.Extensions.AgentExtensions, TOR_Core");
                Type heroExtensionsType = RequireType("TOR_Core.Extensions.HeroExtensions, TOR_Core");
                Type behaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");

                _getHeroMethod = FindUniqueMethod(agentExtensionsType, "GetHero", StaticFlags, 1);
                _getCareerMethod = FindUniqueMethod(heroExtensionsType, "GetCareer", StaticFlags, 1);
                _hasAttributeMethod = FindUniqueMethod(heroExtensionsType, "HasAttribute", StaticFlags, 2);
                _addAttributeMethod = FindUniqueMethod(heroExtensionsType, "AddAttribute", StaticFlags, 2);
                _behaviorInstanceProperty = RequireProperty(behaviorType, "Instance", StaticFlags);
                _isRegisteredSharedHeroMethod = FindUniqueMethod(behaviorType, "IsRegisteredSharedHero", InstanceFlags, 1);

                MethodInfo onAgentCreated = FindUniqueMethod(abilityManagerType, "OnAgentCreated", InstanceFlags, 1);
                new Harmony("xmarre.multicharactercampaign.tor.registered-career-prerequisite.v135").Patch(
                    onAgentCreated,
                    prefix: new HarmonyMethod(typeof(RegisteredCareerAbilityPrerequisite), nameof(BeforeOnAgentCreated)));

                _installed = true;
                Log("Installed registered-career AbilityUser prerequisite before TOR OnAgentCreated.");
            }
            catch (Exception ex)
            {
                Log("Registered-career AbilityUser prerequisite installation failed safely: " + Unwrap(ex));
            }
        }

        private static void BeforeOnAgentCreated(object __0)
        {
            try
            {
                object agent = __0;
                if (agent == null || !_agentType.IsInstanceOfType(agent))
                {
                    return;
                }

                object hero = _getHeroMethod.Invoke(null, new[] { agent });
                if (hero == null)
                {
                    return;
                }

                object behavior = _behaviorInstanceProperty.GetValue(null, null);
                if (behavior == null || !Convert.ToBoolean(_isRegisteredSharedHeroMethod.Invoke(behavior, new[] { hero })))
                {
                    return;
                }

                object career = _getCareerMethod.Invoke(null, new[] { hero });
                if (career == null)
                {
                    return;
                }

                if (!Convert.ToBoolean(_hasAttributeMethod.Invoke(null, new object[] { hero, "AbilityUser" })))
                {
                    _addAttributeMethod.Invoke(null, new object[] { hero, "AbilityUser" });
                    Log("Established AbilityUser before native agent initialization for registered career hero=" + SafeHeroId(hero) + ".");
                }
            }
            catch (Exception ex)
            {
                Log("Registered-career pre-spawn prerequisite failed safely: " + Unwrap(ex));
            }
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
                Type logType = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", false);
                MethodInfo info = logType?.GetMethod("Info", StaticFlags, null, new[] { typeof(string) }, null);
                info?.Invoke(null, new object[] { "[AI Career Prerequisite] " + message });
            }
            catch
            {
            }
        }
    }
}
