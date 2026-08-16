using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// TOR binds its party-screen special button delegates only when PartyCharacterVMExtension
    /// instances are constructed. MCC changes Hero.MainHero without necessarily reconstructing
    /// those VMs, so the handler and icon can remain bound to the previously active career.
    /// Rebind on MCC's canonical TOR refresh hook and refresh an already-open party screen.
    /// </summary>
    internal static class CareerButtonRefreshRepair
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;
        private static Type _careerHelperType;
        private static Type _specialButtonHandlerType;
        private static Type _partyVmExtensionType;
        private static MethodInfo _getCareerButtonMethod;
        private static MethodInfo _disableMethod;
        private static PropertyInfo _handlerInstanceProperty;
        private static PropertyInfo _partyVmInstanceProperty;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                Type torBridgeType = RequireType("MultiCharacterCampaignTOR.TORBridge, MultiCharacterCampaignTOR");
                _careerHelperType = RequireType("TOR_Core.CharacterDevelopment.CareerSystem.CareerHelper, TOR_Core");
                _specialButtonHandlerType = RequireType("TOR_Core.CampaignMechanics.SpecialbuttonEventManagerHandler, TOR_Core");
                _partyVmExtensionType = RequireType("TOR_Core.Extensions.UI.PartyVMExtension, TOR_Core");

                MethodInfo refreshAfterSwitch = FindUniqueMethod(torBridgeType, "RefreshAfterSwitch", StaticFlags, 0);
                _getCareerButtonMethod = FindUniqueMethod(_careerHelperType, "GetCareerButton", StaticFlags, 0);
                _handlerInstanceProperty = RequireProperty(_specialButtonHandlerType, "Instance", StaticFlags);
                _disableMethod = FindUniqueMethod(_specialButtonHandlerType, "Disable", InstanceFlags, 0);
                _partyVmInstanceProperty = RequireProperty(_partyVmExtensionType, "ViewModelInstance", StaticFlags);

                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.careerbuttonrefresh.v133");
                harmony.Patch(
                    refreshAfterSwitch,
                    postfix: new HarmonyMethod(typeof(CareerButtonRefreshRepair), nameof(AfterTorRefresh)));

                _installed = true;
                Log("Installed active-career party-button rebind on TOR refresh.");
            }
            catch (Exception ex)
            {
                Log("Career-button refresh repair installation failed safely: " + Unwrap(ex));
            }
        }

        private static void AfterTorRefresh()
        {
            try
            {
                object handler = _handlerInstanceProperty.GetValue(null, null);
                if (handler == null)
                {
                    return;
                }

                object careerButton = _getCareerButtonMethod.Invoke(null, null);
                if (careerButton == null)
                {
                    _disableMethod.Invoke(handler, null);
                }
                else
                {
                    MethodInfo register = careerButton.GetType().GetMethods(InstanceFlags)
                        .SingleOrDefault(method => method.Name == "Register" && method.GetParameters().Length == 0);
                    if (register == null)
                    {
                        throw new MissingMethodException(careerButton.GetType().FullName, "Register");
                    }
                    register.Invoke(careerButton, null);
                }

                // TOR itself uses PartyVMExtension.ViewModelInstance.RefreshValues() after a career-button
                // action. Use the same native refresh path so an already-open party screen replaces stale
                // visibility/icon state immediately after an MCC identity switch.
                object partyVm = _partyVmInstanceProperty.GetValue(null, null);
                if (partyVm != null)
                {
                    MethodInfo refreshValues = partyVm.GetType().GetMethods(InstanceFlags)
                        .FirstOrDefault(method => method.Name == "RefreshValues" && method.GetParameters().Length == 0);
                    refreshValues?.Invoke(partyVm, null);
                }
            }
            catch (Exception ex)
            {
                Log("Career-button rebind failed safely: " + Unwrap(ex));
            }
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName, throwOnError: false);
            if (type == null)
            {
                throw new TypeLoadException(assemblyQualifiedName);
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
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("O") + " [Career Button Refresh] " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
