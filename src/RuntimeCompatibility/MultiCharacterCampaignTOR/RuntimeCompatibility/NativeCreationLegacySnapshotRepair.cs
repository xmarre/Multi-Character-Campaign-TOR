using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Repairs a decompiler-corrupted helper in the packaged NativeCreation.Legacy assembly.
    ///
    /// The recovered CaptureCampaignState path snapshots clan members by calling the private
    /// FindMethod(Dictionary&lt;string, object&gt;, string, object, string) helper. The recovered helper
    /// incorrectly casts the member-name string (for example "Renown" or "Name") to FieldInfo while
    /// deciding whether a TextObject needs CopyTextObject(). That InvalidCastException occurs before
    /// NativeCreation subscribes its initialization listener or pushes CharacterCreationState.
    ///
    /// Replace only that corrupted helper body with the intended member-snapshot operation. This
    /// keeps the rest of the recovered creation flow intact and avoids another downstream workaround.
    /// </summary>
    internal static class NativeCreationLegacySnapshotRepair
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

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

                Assembly legacyAssembly = LoadSiblingAssembly(bin, "MultiCharacterCampaignTOR.NativeCreation.Legacy.dll");
                Type legacyType = legacyAssembly.GetType("MultiCharacterCampaignTOR.NativeCreation.NativeCharacterCreation", true, false);
                MethodInfo corruptedSnapshotHelper = legacyType.GetMethod(
                    "FindMethod",
                    StaticFlags,
                    null,
                    new[] { typeof(object), typeof(string), typeof(object), typeof(string) },
                    null);

                if (corruptedSnapshotHelper == null)
                {
                    throw new MissingMethodException(legacyType.FullName, "FindMethod(object,string,object,string)");
                }

                new Harmony("xmarre.multicharactercampaign.tor.nativecreation-snapshot.v136").Patch(
                    corruptedSnapshotHelper,
                    prefix: new HarmonyMethod(typeof(NativeCreationLegacySnapshotRepair), nameof(BeforeCorruptedSnapshotHelper)));

                _installed = true;
                Log("Installed NativeCreation legacy clan-snapshot repair.");
            }
            catch (Exception ex)
            {
                Log("NativeCreation legacy clan-snapshot repair installation failed safely: " + Unwrap(ex));
            }
        }

        private static bool BeforeCorruptedSnapshotHelper(object __0, string __1, object __2, string __3)
        {
            IDictionary snapshot = __0 as IDictionary;
            if (snapshot == null)
            {
                throw new InvalidOperationException("NativeCreation clan snapshot target is not an IDictionary.");
            }

            object value = GetMember(__2, __3);
            if (value != null &&
                (string.Equals(__3, "Name", StringComparison.Ordinal) ||
                 string.Equals(__3, "InformalName", StringComparison.Ordinal)))
            {
                MethodInfo copy = FindZeroArgumentMethod(value.GetType(), "CopyTextObject");
                if (copy == null)
                {
                    throw new MissingMethodException(value.GetType().FullName, "CopyTextObject()");
                }

                value = copy.Invoke(value, null);
            }

            snapshot[__1] = value;
            return false;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Type current = instance.GetType();
            while (current != null)
            {
                PropertyInfo property = current.GetProperty(name, BindingFlags.DeclaredOnly | InstanceFlags);
                if (property != null)
                {
                    return property.GetValue(instance, null);
                }

                FieldInfo field = current.GetField(name, BindingFlags.DeclaredOnly | InstanceFlags);
                if (field != null)
                {
                    return field.GetValue(instance);
                }

                current = current.BaseType;
            }

            return null;
        }

        private static MethodInfo FindZeroArgumentMethod(Type type, string name)
        {
            Type current = type;
            while (current != null)
            {
                MethodInfo method = current.GetMethods(BindingFlags.DeclaredOnly | InstanceFlags)
                    .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == 0);
                if (method != null)
                {
                    return method;
                }

                current = current.BaseType;
            }

            return null;
        }

        private static Assembly LoadSiblingAssembly(string bin, string fileName)
        {
            string simpleName = Path.GetFileNameWithoutExtension(fileName);
            Assembly existing = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            string path = Path.Combine(bin, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required MCC NativeCreation legacy assembly is missing.", path);
            }

            return Assembly.LoadFrom(path);
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
                info?.Invoke(null, new object[] { "[NativeCreationSnapshot136] " + message });
            }
            catch
            {
            }
        }
    }
}
