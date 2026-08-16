using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Bridges legacy auxiliary patches that still resolve Harmony by assembly-qualified name.
    /// Bannerlord.Harmony is a hard module dependency and this assembly links 0Harmony directly,
    /// so the linked runtime assembly is the authoritative resolution target.
    /// </summary>
    internal static class HarmonyAssemblyResolver
    {
        private static bool _installed;
        private static Assembly _harmonyAssembly;
        private static string _harmonyAssemblyName;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                _harmonyAssembly = typeof(Harmony).Assembly;
                _harmonyAssemblyName = _harmonyAssembly.GetName().Name;

                AppDomain.CurrentDomain.AssemblyResolve += ResolveHarmonyAssembly;

                // Validate the exact legacy lookup shape used by the reconstructed sidecars. This is
                // intentionally performed only after the linked-assembly resolver is active.
                if (Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false) == null ||
                    Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", throwOnError: false) == null)
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= ResolveHarmonyAssembly;
                    _harmonyAssembly = null;
                    _harmonyAssemblyName = null;
                    Log("Linked 0Harmony could not satisfy legacy assembly-qualified type resolution; auxiliary patches will retain their normal safe-failure behavior.");
                    return;
                }

                _installed = true;
                Log("Installed linked 0Harmony resolver for reconstructed auxiliary patches.");
            }
            catch (Exception ex)
            {
                try
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= ResolveHarmonyAssembly;
                }
                catch
                {
                }

                _harmonyAssembly = null;
                _harmonyAssemblyName = null;
                Log("Harmony resolver initialization failed safely: " + ex);
            }
        }

        private static Assembly ResolveHarmonyAssembly(object sender, ResolveEventArgs args)
        {
            if (_harmonyAssembly == null || string.IsNullOrEmpty(args?.Name))
            {
                return null;
            }

            string requestedName;
            try
            {
                requestedName = new AssemblyName(args.Name).Name;
            }
            catch
            {
                return null;
            }

            if (string.Equals(requestedName, _harmonyAssemblyName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedName, "0Harmony", StringComparison.OrdinalIgnoreCase))
            {
                return _harmonyAssembly;
            }

            return null;
        }

        private static void Log(string message)
        {
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string directory = Path.Combine(documents, "Mount and Blade II Bannerlord", "Configs", "ModLogs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    Path.Combine(directory, "MultiCharacterCampaignTOR.log"),
                    DateTime.Now.ToString("O") + " [Harmony Resolver] " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
