using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// Commits CharacterCreationContent.SelectedCulture to MCC's temporary player hero as soon as the
    /// culture is selected.
    ///
    /// Bannerlord normally performs Hero.MainHero.Culture = SelectedCulture in
    /// CharacterCreationManager.ApplyFinalEffects(). MCC suppresses that campaign-start finalization because
    /// it also mutates persistent clan/map state. TOR's earlier OnCultureSelected callback derives race from
    /// CharacterObject.PlayerCharacter.Culture, so the MCC candidate must expose the selected culture before
    /// TOR performs its native race/body update.
    /// </summary>
    internal static class NativeCreationMixedRaceCompatibility
    {
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;
        private static FieldInfo _legacyInProgressField;
        private static FieldInfo _legacyCandidateHeroField;

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
                _legacyInProgressField = RequireField(legacyType, "_inProgress");
                _legacyCandidateHeroField = RequireField(legacyType, "_candidateHero");

                MethodInfo setSelectedCulture = typeof(CharacterCreationContent).GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .SingleOrDefault(method => method.Name == "SetSelectedCulture");
                if (setSelectedCulture == null)
                {
                    throw new MissingMethodException(typeof(CharacterCreationContent).FullName, "SetSelectedCulture");
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

        private static void AfterSelectedCultureChanged(CharacterCreationContent __instance)
        {
            try
            {
                if (_legacyInProgressField == null ||
                    !Convert.ToBoolean(_legacyInProgressField.GetValue(null)) ||
                    __instance == null)
                {
                    return;
                }

                Hero candidate = _legacyCandidateHeroField.GetValue(null) as Hero;
                CultureObject selectedCulture = __instance.SelectedCulture;
                if (candidate == null || selectedCulture == null)
                {
                    return;
                }

                if (!ReferenceEquals(Hero.MainHero, candidate))
                {
                    // NativeCreation switches the candidate to the real player identity before opening the
                    // creation state. Never mutate an inactive/shared hero if that invariant has been lost.
                    Log("Skipped selected-culture commit because the NativeCreation candidate is not Hero.MainHero.");
                    return;
                }

                CultureObject previousCulture = candidate.Culture;
                if (ReferenceEquals(previousCulture, selectedCulture))
                {
                    return;
                }

                candidate.Culture = selectedCulture;
                Log(
                    "Committed CharacterCreationContent.SelectedCulture to the active NativeCreation candidate before TOR race resolution. " +
                    "Hero=" + candidate.StringId + "; oldCulture=" + (previousCulture?.StringId ?? "<null>") +
                    "; newCulture=" + selectedCulture.StringId + ".");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to apply the selected character-creation culture to the active MCC candidate before TOR race resolution.",
                    Unwrap(ex));
            }
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, StaticFlags);
            if (field == null)
            {
                throw new MissingFieldException(type.FullName, name);
            }
            return field;
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
                info?.Invoke(null, new object[] { "[NativeCreationMixedRace] " + message });
            }
            catch
            {
            }
        }
    }
}
