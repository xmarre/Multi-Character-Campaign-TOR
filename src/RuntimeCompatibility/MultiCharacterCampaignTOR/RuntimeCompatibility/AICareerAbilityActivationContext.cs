using System;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Supplies the AI career owner/caster during the short interval in which TOR constructs a
    /// CareerAbilityScript. ScriptComponent OnInit can run before AbilityScript.SetCasterAgent(),
    /// so the normal script-local lookup is not yet available at that point.
    /// </summary>
    internal static class AICareerAbilityActivationContext
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;

        private static Type _agentType;
        private static Type _heroType;
        private static Type _careerAbilityType;
        private static Type _agentExtensionsType;
        private static Type _mccBehaviorType;

        private static MethodInfo _agentIsAIControlledGetter;
        private static MethodInfo _getHeroMethod;
        private static MethodInfo _getOriginMobilePartyMethod;
        private static MethodInfo _isRegisteredSharedHeroMethod;

        private static PropertyInfo _mccBehaviorInstanceProperty;
        private static FieldInfo _careerAbilityOwnerHeroField;
        private static FieldInfo _supportActiveCareerAbilityField;

        [ThreadStatic]
        private static object _activeActivationCaster;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                ResolveRuntimeSurfaces();

                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.ai-career-activation-context");
                MethodInfo activateAbility = FindUniqueMethod(_careerAbilityType, "ActivateAbility", InstanceFlags, 1);

                Type supportType = typeof(AICareerAbilitySupport);
                MethodInfo resolveScriptHero = RequireMethod(supportType, "ResolveCareerScriptHero", StaticFlags);
                MethodInfo resolveScriptAgent = RequireMethod(supportType, "ResolveCareerScriptAgent", StaticFlags);
                MethodInfo belongsToScriptParty = RequireMethod(supportType, "BelongsToCareerScriptParty", StaticFlags);

                harmony.Patch(
                    activateAbility,
                    prefix: new HarmonyMethod(typeof(AICareerAbilityActivationContext), nameof(ActivateAbilityPrefix)),
                    finalizer: new HarmonyMethod(typeof(AICareerAbilityActivationContext), nameof(ActivateAbilityFinalizer)));

                harmony.Patch(
                    resolveScriptHero,
                    postfix: new HarmonyMethod(typeof(AICareerAbilityActivationContext), nameof(ResolveCareerScriptHeroPostfix)));

                harmony.Patch(
                    resolveScriptAgent,
                    postfix: new HarmonyMethod(typeof(AICareerAbilityActivationContext), nameof(ResolveCareerScriptAgentPostfix)));

                harmony.Patch(
                    belongsToScriptParty,
                    prefix: new HarmonyMethod(typeof(AICareerAbilityActivationContext), nameof(BelongsToCareerScriptPartyPrefix)));

                _installed = true;
                Log("Installed pre-SetCasterAgent career-script owner context for AI shared heroes.");
            }
            catch (Exception ex)
            {
                Log("AI career activation-context installation failed safely: " + Unwrap(ex));
            }
        }

        private static void ResolveRuntimeSurfaces()
        {
            _agentType = RequireType("TaleWorlds.MountAndBlade.Agent, TaleWorlds.MountAndBlade");
            _heroType = RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
            _careerAbilityType = RequireType("TOR_Core.AbilitySystem.CareerAbility, TOR_Core");
            _agentExtensionsType = RequireType("TOR_Core.Extensions.AgentExtensions, TOR_Core");
            _mccBehaviorType = RequireType("MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR");

            _agentIsAIControlledGetter = RequireProperty(_agentType, "IsAIControlled", InstanceFlags).GetGetMethod(true);
            _getHeroMethod = FindUniqueMethod(_agentExtensionsType, "GetHero", StaticFlags, 1);
            _getOriginMobilePartyMethod = FindUniqueMethod(_agentExtensionsType, "GetOriginMobileParty", StaticFlags, 1);

            _mccBehaviorInstanceProperty = RequireProperty(_mccBehaviorType, "Instance", StaticFlags);
            _isRegisteredSharedHeroMethod = FindUniqueMethod(_mccBehaviorType, "IsRegisteredSharedHero", InstanceFlags, 1);

            _careerAbilityOwnerHeroField = RequireField(_careerAbilityType, "_ownerHero", InstanceFlags);
            _supportActiveCareerAbilityField = RequireField(typeof(AICareerAbilitySupport), "_activeCareerAbility", StaticFlags);
        }

        private static void ActivateAbilityPrefix(object __0, out object __state)
        {
            __state = _activeActivationCaster;
            object caster = __0;
            _activeActivationCaster = IsRegisteredSharedAIAgent(caster) ? caster : null;
        }

        private static Exception ActivateAbilityFinalizer(Exception __exception, object __state)
        {
            _activeActivationCaster = __state;
            return __exception;
        }

        private static void ResolveCareerScriptHeroPostfix(ref object __result)
        {
            try
            {
                object owner = GetActiveCareerAbilityOwner();
                if (owner != null && IsRegisteredSharedHero(owner))
                {
                    __result = owner;
                }
            }
            catch (Exception ex)
            {
                Log("Career-script pre-init hero context failed open: " + Unwrap(ex));
            }
        }

        private static void ResolveCareerScriptAgentPostfix(ref object __result)
        {
            object caster = _activeActivationCaster;
            if (IsRegisteredSharedAIAgent(caster))
            {
                __result = caster;
            }
        }

        private static bool BelongsToCareerScriptPartyPrefix(object candidateAgent, ref bool __result)
        {
            object caster = _activeActivationCaster;
            if (!IsRegisteredSharedAIAgent(caster))
            {
                return true;
            }

            __result = BelongsToSameOriginParty(candidateAgent, caster);
            return false;
        }

        private static object GetActiveCareerAbilityOwner()
        {
            object ability = _supportActiveCareerAbilityField.GetValue(null);
            return ability != null ? _careerAbilityOwnerHeroField.GetValue(ability) : null;
        }

        private static bool IsRegisteredSharedAIAgent(object agent)
        {
            if (agent == null || !_agentType.IsInstanceOfType(agent))
            {
                return false;
            }

            if (!Convert.ToBoolean(_agentIsAIControlledGetter.Invoke(agent, null)))
            {
                return false;
            }

            object hero = _getHeroMethod.Invoke(null, new[] { agent });
            return hero != null && IsRegisteredSharedHero(hero);
        }

        private static bool IsRegisteredSharedHero(object hero)
        {
            if (hero == null || !_heroType.IsInstanceOfType(hero))
            {
                return false;
            }

            object behavior = _mccBehaviorInstanceProperty.GetValue(null, null);
            return behavior != null && Convert.ToBoolean(_isRegisteredSharedHeroMethod.Invoke(behavior, new[] { hero }));
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
            MethodInfo[] matches = Array.FindAll(type.GetMethods(flags), method => method.Name == name && method.GetParameters().Length == parameterCount);
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
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("O") + " [AI Career Activation] " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
