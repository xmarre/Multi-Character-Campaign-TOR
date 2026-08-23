using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Commits CharacterCreationContent.SelectedCulture to MCC's temporary player hero as soon as the
    /// culture is selected.
    ///
    /// Bannerlord normally performs Hero.MainHero.Culture = SelectedCulture in
    /// CharacterCreationManager.ApplyFinalEffects(). MCC cannot run that campaign-start finalization wholesale
    /// because it also rewrites clan/campaign state and map position. TOR's OnCultureSelected callback runs
    /// earlier and derives the player race from CharacterObject.PlayerCharacter.Culture. Without this narrow
    /// commit, an in-campaign candidate still exposes the race/culture it inherited from the previous player;
    /// same-race creation hides the mismatch while cross-race creation produces the wrong skeleton/body.
    /// </summary>
    internal static class NativeCreationMixedRaceCompatibility
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;
        private static FieldInfo _legacyInProgressField;
        private static FieldInfo _legacyCandidateHeroField;
        private static PropertyInfo _selectedCultureProperty;

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
                Type legacyType = legacyAssembly.GetType(
                    "MultiCharacterCampaignTOR.NativeCreation.NativeCharacterCreation",
                    true,
                    false);
                _legacyInProgressField = RequireField(legacyType, "_inProgress", StaticFlags);
                _legacyCandidateHeroField = RequireField(legacyType, "_candidateHero", StaticFlags);

                Type contentType = RequireType(
                    "TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationContent, TaleWorlds.CampaignSystem");
                MethodInfo setSelectedCulture = contentType.GetMethods(InstanceFlags)
                    .SingleOrDefault(method => method.Name == "SetSelectedCulture");
                if (setSelectedCulture == null)
                {
                    throw new MissingMethodException(contentType.FullName, "SetSelectedCulture");
                }

                _selectedCultureProperty = contentType.GetProperty("SelectedCulture", InstanceFlags);
                if (_selectedCultureProperty == null || !_selectedCultureProperty.CanRead)
                {
                    throw new MissingMemberException(contentType.FullName, "SelectedCulture");
                }

                new Harmony("xmarre.multicharactercampaign.tor.nativecreation-mixed-race").Patch(
                    setSelectedCulture,
                    postfix: new HarmonyMethod(typeof(NativeCreationMixedRaceCompatibility), nameof(AfterSelectedCultureChanged)));

                _installed = true;
                Log("Installed NativeCreation selected-culture commit for mixed-race characters.");
            }
            catch (Exception ex)
            {
                Log("NativeCreation mixed-race compatibility installation failed safely: " + Unwrap(ex));
            }
        }

        private static void AfterSelectedCultureChanged(object __instance)
        {
            try
            {
                if (!IsNativeCreationInProgress() || __instance == null)
                {
                    return;
                }

                object candidate = _legacyCandidateHeroField.GetValue(null);
                object selectedCulture = _selectedCultureProperty.GetValue(__instance, null);
                if (candidate == null || selectedCulture == null)
                {
                    return;
                }

                Type heroType = RequireType("TaleWorlds.CampaignSystem.Hero, TaleWorlds.CampaignSystem");
                object mainHero = GetStaticMember(heroType, "MainHero");
                if (!ReferenceEquals(mainHero, candidate))
                {
                    // NativeCreation switches the candidate to the real player identity before opening the
                    // creation state. Never mutate an inactive/shared hero if that invariant has been lost.
                    Log("Skipped selected-culture commit because the NativeCreation candidate is not Hero.MainHero.");
                    return;
                }

                object previousCulture = GetMember(candidate, "Culture");
                if (ReferenceEquals(previousCulture, selectedCulture))
                {
                    return;
                }

                SetMember(candidate, "Culture", selectedCulture);
                Log(
                    "Committed CharacterCreationContent.SelectedCulture to the active NativeCreation candidate before TOR race resolution. " +
                    "Hero=" + IdOf(candidate) + "; oldCulture=" + IdOf(previousCulture) + "; newCulture=" + IdOf(selectedCulture) + ".");
            }
            catch (Exception ex)
            {
                // This callback is part of the selected-culture transaction. Failing open would recreate a
                // knowingly inconsistent candidate and make the later TOR race/body result misleading.
                throw new InvalidOperationException(
                    "Failed to apply the selected character-creation culture to the active MCC candidate before TOR race resolution.",
                    Unwrap(ex));
            }
        }

        private static bool IsNativeCreationInProgress()
        {
            return _legacyInProgressField != null && Convert.ToBoolean(_legacyInProgressField.GetValue(null));
        }

        private static object GetStaticMember(Type type, string name)
        {
            PropertyInfo property = FindProperty(type, name, true);
            if (property != null)
            {
                return property.GetValue(null, null);
            }

            FieldInfo field = FindField(type, name, true);
            if (field != null)
            {
                return field.GetValue(null);
            }

            throw new MissingMemberException(type.FullName, name);
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            PropertyInfo property = FindProperty(instance.GetType(), name, false);
            if (property != null && property.CanRead)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = FindField(instance.GetType(), name, false);
            return field != null ? field.GetValue(instance) : null;
        }

        private static void SetMember(object instance, string name, object value)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            PropertyInfo property = FindProperty(instance.GetType(), name, false);
            MethodInfo setter = property?.GetSetMethod(true);
            if (setter != null)
            {
                setter.Invoke(instance, new[] { value });
                return;
            }

            FieldInfo field = FindField(instance.GetType(), name, false);
            if (field != null && !field.IsInitOnly)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingMemberException(instance.GetType().FullName, name);
        }

        private static PropertyInfo FindProperty(Type type, string name, bool isStatic)
        {
            BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                                 (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            Type current = type;
            while (current != null)
            {
                PropertyInfo property = current.GetProperty(name, flags);
                if (property != null)
                {
                    return property;
                }
                current = current.BaseType;
            }
            return null;
        }

        private static FieldInfo FindField(Type type, string name, bool isStatic)
        {
            BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                                 (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(name, flags);
                if (field != null)
                {
                    return field;
                }
                current = current.BaseType;
            }
            return null;
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

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName, false);
            if (type == null)
            {
                throw new TypeLoadException("Required runtime type not found: " + assemblyQualifiedName);
            }
            return type;
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

        private static string IdOf(object value)
        {
            if (value == null)
            {
                return "<null>";
            }

            object stringId = GetMember(value, "StringId") ?? GetMember(value, "StringID");
            return stringId != null ? stringId.ToString() : value.GetType().Name;
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
                info?.Invoke(null, new object[] { "[NativeCreationMixedRace] " + message });
            }
            catch
            {
            }
        }
    }
}
