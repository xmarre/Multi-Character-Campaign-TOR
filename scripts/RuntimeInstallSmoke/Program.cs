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
        string coreDll = Path.Combine(coreDir, "MultiCharacterCampaignTOR.dll");
        string identityDll = Path.Combine(identityDir, "MultiCharacterCampaignTOR.IdentityGuard.v140.dll");

        foreach (string required in new[] { bannerlordBin, harmonyDll, coreDll, identityDll })
        {
            if (!File.Exists(required) && !Directory.Exists(required))
            {
                Console.Error.WriteLine("Missing smoke-test input: " + required);
                return 3;
            }
        }

        var searchDirectories = new[] { identityDir, coreDir, bannerlordBin, Path.GetDirectoryName(harmonyDll) };
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

            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleInterventionSettings");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleInterventionPrediction");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleInterventionPredictionPriorityBridge");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleAlertUiHotfix");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.ManagerReturnHotfix");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleInterventionThresholdPolicy");
            InstallAndVerify(identity, "MultiCharacterCampaignTOR.IdentityGuard.BattleReinforcementOrderGuard");

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

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null)
        {
            exception = exception.InnerException;
        }
        return exception;
    }
}
