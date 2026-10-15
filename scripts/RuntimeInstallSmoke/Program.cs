using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

internal static class Program
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: RuntimeInstallSmoke <repo-root> <bannerlord-bin> <harmony-dll>");
            return 2;
        }

        string repoRoot = Path.GetFullPath(args[0]);
        string bannerlordBin = Path.GetFullPath(args[1]);
        string harmonyDll = Path.GetFullPath(args[2]);
        string coreDir = Path.Combine(repoRoot, "src", "MultiCharacterCampaignTOR", "bin", "Release");
        string identityDir = Path.Combine(repoRoot, "src", "IdentityGuard", "bin", "Release");
        string runtimeDir = Path.Combine(repoRoot, "src", "RuntimeCompatibility", "bin", "Release");
        string coreDll = Path.Combine(coreDir, "MultiCharacterCampaignTOR.dll");
        string identityDll = Path.Combine(identityDir, "MultiCharacterCampaignTOR.IdentityGuard.v140.dll");
        string runtimeDll = Path.Combine(runtimeDir, "MultiCharacterCampaignTOR.RuntimeCompatibility.v140.dll");

        foreach (string required in new[] { bannerlordBin, harmonyDll, coreDll, identityDll, runtimeDll })
        {
            if (!File.Exists(required) && !Directory.Exists(required))
            {
                Console.Error.WriteLine("Missing smoke-test input: " + required);
                return 3;
            }
        }

        var searchDirectories = new[] { identityDir, coreDir, runtimeDir, bannerlordBin, Path.GetDirectoryName(harmonyDll) };
        var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs eventArgs)
        {
            string simpleName = new AssemblyName(eventArgs.Name).Name;
            if (!resolving.Add(simpleName))
            {
                return null;
            }
            try
            {
                if (string.Equals(simpleName, "0Harmony", StringComparison.OrdinalIgnoreCase))
                {
                    return Assembly.LoadFrom(harmonyDll);
                }
                string fileName = simpleName + ".dll";
                foreach (string directory in searchDirectories)
                {
                    if (string.IsNullOrEmpty(directory))
                    {
                        continue;
                    }
                    string candidate = Path.Combine(directory, fileName);
                    if (File.Exists(candidate))
                    {
                        return Assembly.LoadFrom(candidate);
                    }
                }
                return null;
            }
            finally
            {
                resolving.Remove(simpleName);
            }
        };

        try
        {
            Assembly.LoadFrom(harmonyDll);
            Assembly.LoadFrom(coreDll);
            Assembly identity = Assembly.LoadFrom(identityDll);
            Assembly runtime = Assembly.LoadFrom(runtimeDll);

            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleInterventionSettings");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleInterventionPrediction");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleInterventionPredictionPriorityBridge");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleAlertUiHotfix");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.ManagerReturnHotfix");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleInterventionThresholdPolicy");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleReinforcementOrderGuard");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.SettlementCharacterSwitchMenuFix");
            VerifyInheritedAgentFieldResolver(runtime);
            VerifyMindControlCasterGuard(runtime);

            Console.WriteLine("Runtime patch installation smoke test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Unwrap(ex));
            return 1;
        }
    }

    private static void InstallAndVerify(Assembly assembly, string typeName)
    {
        Type type = assembly.GetType(typeName, true);
        MethodInfo install = type.GetMethod("Install", StaticFlags);
        FieldInfo installed = type.GetField("_installed", StaticFlags);
        if (install == null || installed == null)
        {
            throw new MissingMemberException(typeName, "Install/_installed");
        }

        install.Invoke(null, null);
        if (!Convert.ToBoolean(installed.GetValue(null)))
        {
            throw new InvalidOperationException(typeName + " did not commit its installation state.");
        }
        Console.WriteLine("Installed " + typeName);
    }

    private static void VerifyInheritedAgentFieldResolver(Assembly runtime)
    {
        const string typeName = "MultiCharacterCampaignTOR.RuntimeCompatibility.AICareerAbilityTransitionGuard";
        Type type = runtime.GetType(typeName, true);
        MethodInfo resolver = type.GetMethod("RequireFieldInHierarchy", StaticFlags);
        if (resolver == null)
        {
            throw new MissingMethodException(typeName, "RequireFieldInHierarchy");
        }

        FieldInfo field = resolver.Invoke(null, new object[] { typeof(SyntheticWizardAIComponent), "Agent" }) as FieldInfo;
        if (field == null)
        {
            throw new InvalidOperationException("Inherited Agent field resolver returned null.");
        }
        if (field.DeclaringType != typeof(SyntheticAgentComponent))
        {
            throw new InvalidOperationException("Inherited Agent field resolver returned the wrong declaring type: " + field.DeclaringType);
        }

        object marker = new object();
        var component = new SyntheticWizardAIComponent { Agent = marker };
        if (!ReferenceEquals(field.GetValue(component), marker))
        {
            throw new InvalidOperationException("Resolved inherited Agent field did not read the derived component instance correctly.");
        }

        Console.WriteLine("Verified inherited Agent field resolver through the built RuntimeCompatibility assembly.");
    }

    private static void VerifyMindControlCasterGuard(Assembly runtime)
    {
        const string typeName = "MultiCharacterCampaignTOR.RuntimeCompatibility.AICareerAbilitySupport";
        Type type = runtime.GetType(typeName, true);
        MethodInfo validator = type.GetMethod("HasUsableMindControlCaster", StaticFlags);
        MethodInfo prefix = type.GetMethod("MindControlOnAfterTickPrefix", StaticFlags);
        if (validator == null || prefix == null || prefix.ReturnType != typeof(bool))
        {
            throw new MissingMethodException(typeName, "Mind Control lifecycle validator/prefix");
        }

        object caster = new object();
        object team = new object();
        object active = new object();
        object inactive = new object();
        AssertValidation(false, null, active, active, team, "missing caster");
        AssertValidation(false, caster, inactive, active, team, "inactive caster");
        AssertValidation(false, caster, active, active, null, "missing team");
        AssertValidation(false, caster, active, null, team, "missing active state");
        AssertValidation(true, caster, active, active, team, "live caster with team");
        // Exercise the actual Harmony prefix with stand-in runtime objects. This catches
        // missing field bindings, an incorrect skip decision, and failure to update _caster.
        var snapshots = new Dictionary<FieldInfo, object>();
        void Set(string name, object value)
        {
            FieldInfo field = type.GetField(name, StaticFlags);
            if (field == null) throw new MissingFieldException(typeName, name);
            snapshots.Add(field, field.GetValue(null));
            field.SetValue(null, value);
        }

        try
        {
            Set("_abilityScriptAbilityGetter", typeof(SyntheticMindControlScript).GetProperty("Ability").GetGetMethod());
            Set("_abilityScriptCasterAgentGetter", typeof(SyntheticMindControlScript).GetProperty("CasterAgent").GetGetMethod());
            Set("_careerAbilityType", typeof(SyntheticCareerAbility));
            Set("_careerAbilityOwnerHeroField", typeof(SyntheticCareerAbility).GetField("_ownerHero"));
            Set("_mccBehaviorInstanceProperty", typeof(SyntheticSharedBehavior).GetProperty("Instance"));
            Set("_isRegisteredSharedHeroMethod", typeof(SyntheticSharedBehavior).GetMethod("IsRegisteredSharedHero"));
            Set("_mindControlAgentStateGetter", typeof(SyntheticCaster).GetProperty("State").GetGetMethod());
            Set("_mindControlAgentTeamGetter", typeof(SyntheticCaster).GetProperty("Team").GetGetMethod());
            Set("_mindControlActiveState", "Active");
            Set("_mindControlCachedCasterField", typeof(SyntheticMindControlScript).GetField("_caster"));
            Set("_mindControlStopMethod", typeof(SyntheticMindControlScript).GetMethod("Stop"));

            var owner = new object();
            var foreignOwner = new object();
            SyntheticSharedBehavior.Instance.RegisteredOwner = owner;
            var live = new SyntheticCaster { State = "Active", Team = team };
            var dead = new SyntheticCaster { State = "Killed", Team = team };

            AssertPrefix(new SyntheticMindControlScript { Ability = new SyntheticCareerAbility { _ownerHero = foreignOwner }, CasterAgent = dead }, true, false, "unrelated TOR hero");
            AssertPrefix(new SyntheticMindControlScript { Ability = new SyntheticCareerAbility { _ownerHero = owner }, CasterAgent = dead }, false, true, "removed MCC caster");
            AssertPrefix(new SyntheticMindControlScript { Ability = new SyntheticCareerAbility { _ownerHero = owner } }, false, true, "missing MCC caster");
            AssertPrefix(new SyntheticMindControlScript { Ability = new SyntheticCareerAbility { _ownerHero = owner }, CasterAgent = new SyntheticCaster { State = "Active" } }, false, true, "teamless MCC caster");

            var stale = new SyntheticMindControlScript { Ability = new SyntheticCareerAbility { _ownerHero = owner }, CasterAgent = live, _caster = dead };
            AssertPrefix(stale, true, false, "live MCC caster");
            if (!ReferenceEquals(stale._caster, live)) throw new InvalidOperationException("Mind Control caster cache was not reconciled.");
        }
        finally
        {
            foreach (KeyValuePair<FieldInfo, object> snapshot in snapshots)
                snapshot.Key.SetValue(null, snapshot.Value);
        }

        Console.WriteLine("Verified MCC Mind Control lifecycle prefix through the built RuntimeCompatibility assembly.");

        void AssertPrefix(SyntheticMindControlScript script, bool shouldContinue, bool shouldStop, string scenario)
        {
            bool result = (bool)prefix.Invoke(null, new object[] { script });
            if (result != shouldContinue || script.Stopped != shouldStop)
                throw new InvalidOperationException("Mind Control Harmony prefix failed: " + scenario);
        }

        void AssertValidation(bool expected, object agent, object state, object expectedActiveState, object actualTeam, string scenario)
        {
            bool actual = (bool)validator.Invoke(null, new[] { agent, state, expectedActiveState, actualTeam });
            if (actual != expected)
            {
                throw new InvalidOperationException("Mind Control lifecycle decision failed: " + scenario);
            }
        }
    }

    private sealed class SyntheticSharedBehavior
    {
        public static SyntheticSharedBehavior Instance { get; } = new SyntheticSharedBehavior();
        public object RegisteredOwner;
        public bool IsRegisteredSharedHero(object hero) => ReferenceEquals(hero, RegisteredOwner);
    }

    private sealed class SyntheticCareerAbility
    {
        public object _ownerHero;
    }

    private sealed class SyntheticCaster
    {
        public string State { get; set; }
        public object Team { get; set; }
    }

    private sealed class SyntheticMindControlScript
    {
        public SyntheticCareerAbility Ability { get; set; }
        public SyntheticCaster CasterAgent { get; set; }
        public SyntheticCaster _caster;
        public bool Stopped;
        public void Stop() => Stopped = true;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null)
        {
            exception = exception.InnerException;
        }
        return exception;
    }

    private class SyntheticAgentComponent
    {
        public object Agent;
    }

    private sealed class SyntheticWizardAIComponent : SyntheticAgentComponent
    {
    }
}
