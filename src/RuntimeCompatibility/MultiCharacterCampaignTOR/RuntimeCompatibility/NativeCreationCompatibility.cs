using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Hardens the reconstructed NativeCreation bridge against Bannerlord 1.3.15's generic campaign-event
    /// listener signature and makes failures observable from the first wrapper entry point.
    ///
    /// The legacy bridge intentionally avoids a compile-time CampaignSystem dependency and discovers methods
    /// through FindCompatibleMethod. It constructs Action<object> for OnCharacterCreationInitializedEvent,
    /// while Bannerlord exposes MbEvent<CharacterCreationManager>.AddNonSerializedListener(object,
    /// Action<CharacterCreationManager>). The legacy compatibility matcher therefore rejects the correct method
    /// before reflection can invoke it. Adapt that one callback to the exact delegate type when it is encountered.
    /// </summary>
    internal static class NativeCreationCompatibility
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
                string bin = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(bin))
                {
                    throw new InvalidOperationException("RuntimeCompatibility assembly directory is unavailable.");
                }

                Assembly wrapperAssembly = LoadSiblingAssembly(bin, "MultiCharacterCampaignTOR.NativeCreation.dll");
                Assembly legacyAssembly = LoadSiblingAssembly(bin, "MultiCharacterCampaignTOR.NativeCreation.Legacy.dll");
                Type wrapperType = wrapperAssembly.GetType("MultiCharacterCampaignTOR.NativeCreation.NativeCharacterCreation", true, false);
                Type legacyType = legacyAssembly.GetType("MultiCharacterCampaignTOR.NativeCreation.NativeCharacterCreation", true, false);

                MethodInfo wrapperStart = wrapperType.GetMethod("Start", StaticFlags, null, new[] { typeof(object) }, null);
                MethodInfo legacyStart = legacyType.GetMethod("Start", StaticFlags, null, new[] { typeof(object) }, null);
                MethodInfo findCompatible = legacyType.GetMethod(
                    "FindCompatibleMethod",
                    StaticFlags,
                    null,
                    new[] { typeof(Type), typeof(string), typeof(object[]), typeof(int), typeof(bool) },
                    null);

                if (wrapperStart == null || legacyStart == null || findCompatible == null)
                {
                    throw new MissingMemberException("NativeCreation wrapper/legacy compatibility surfaces are incomplete.");
                }

                Harmony harmony = new Harmony("xmarre.multicharactercampaign.tor.nativecreation-generic-event.v135");
                harmony.Patch(
                    findCompatible,
                    postfix: new HarmonyMethod(typeof(NativeCreationCompatibility), nameof(AfterFindCompatibleMethod)));
                harmony.Patch(
                    wrapperStart,
                    prefix: new HarmonyMethod(typeof(NativeCreationCompatibility), nameof(BeforeWrapperStart)),
                    finalizer: new HarmonyMethod(typeof(NativeCreationCompatibility), nameof(AfterWrapperStart)));
                harmony.Patch(
                    legacyStart,
                    prefix: new HarmonyMethod(typeof(NativeCreationCompatibility), nameof(BeforeLegacyStart)),
                    finalizer: new HarmonyMethod(typeof(NativeCreationCompatibility), nameof(AfterLegacyStart)));

                _installed = true;
                Log("Installed NativeCreation generic-event compatibility and entry/failure diagnostics.");
            }
            catch (Exception ex)
            {
                Log("NativeCreation compatibility installation failed safely: " + Unwrap(ex));
            }
        }

        private static Assembly LoadSiblingAssembly(string bin, string fileName)
        {
            string path = Path.Combine(bin, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required MCC native-creation assembly is missing.", path);
            }

            string simpleName = Path.GetFileNameWithoutExtension(fileName);
            Assembly existing = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
            return existing ?? Assembly.LoadFrom(path);
        }

        private static void BeforeWrapperStart()
        {
            Log("NativeCreation wrapper Start entered.");
        }

        private static Exception AfterWrapperStart(Exception __exception)
        {
            if (__exception != null)
            {
                Log("NativeCreation wrapper Start failed: " + Unwrap(__exception));
            }
            return __exception;
        }

        private static void BeforeLegacyStart()
        {
            Log("NativeCreation legacy Start entered.");
        }

        private static Exception AfterLegacyStart(Exception __exception)
        {
            if (__exception != null)
            {
                Log("NativeCreation legacy Start escaped with an exception: " + Unwrap(__exception));
            }
            return __exception;
        }

        private static void AfterFindCompatibleMethod(
            Type __0,
            string __1,
            object[] __2,
            int __3,
            bool __4,
            ref MethodInfo __result)
        {
            try
            {
                if (__result != null || __4 || __0 == null || __2 == null || __2.Length != 2 ||
                    __3 != 2 || !string.Equals(__1, "AddNonSerializedListener", StringComparison.Ordinal) ||
                    !(__2[1] is Delegate suppliedListener))
                {
                    return;
                }

                MethodInfo candidate = __0.GetMethods(InstanceFlags)
                    .Where(method => method.Name == __1 && method.GetParameters().Length == 2)
                    .FirstOrDefault(method =>
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        return parameters[0].ParameterType.IsInstanceOfType(__2[0]) &&
                               typeof(Delegate).IsAssignableFrom(parameters[1].ParameterType);
                    });

                if (candidate == null)
                {
                    return;
                }

                Type expectedDelegateType = candidate.GetParameters()[1].ParameterType;
                MethodInfo expectedInvoke = expectedDelegateType.GetMethod("Invoke", InstanceFlags);
                ParameterInfo[] expectedParameters = expectedInvoke?.GetParameters();
                if (expectedInvoke == null || expectedInvoke.ReturnType != typeof(void) || expectedParameters == null || expectedParameters.Length != 1)
                {
                    return;
                }

                MethodInfo suppliedInvoke = suppliedListener.GetType().GetMethod("Invoke", InstanceFlags);
                ParameterInfo[] suppliedParameters = suppliedInvoke?.GetParameters();
                if (suppliedInvoke == null || suppliedInvoke.ReturnType != typeof(void) || suppliedParameters == null || suppliedParameters.Length != 1)
                {
                    return;
                }

                ParameterExpression value = Expression.Parameter(expectedParameters[0].ParameterType, "manager");
                Expression forwardedValue = Expression.Convert(value, suppliedParameters[0].ParameterType);
                InvocationExpression invoke = Expression.Invoke(Expression.Constant(suppliedListener), forwardedValue);
                Delegate adapted = Expression.Lambda(expectedDelegateType, invoke, value).Compile();

                __2[1] = adapted;
                __result = candidate;
                Log("Adapted OnCharacterCreationInitialized listener to Bannerlord's exact " + expectedDelegateType.FullName + " signature.");
            }
            catch (Exception ex)
            {
                Log("NativeCreation event-listener adaptation failed safely: " + Unwrap(ex));
            }
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
                info?.Invoke(null, new object[] { "[NativeCreationCompat] " + message });
            }
            catch
            {
            }
        }
    }
}
