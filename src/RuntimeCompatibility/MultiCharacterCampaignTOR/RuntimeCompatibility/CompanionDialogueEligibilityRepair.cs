using System;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
    /// <summary>
    /// MCC's menu-based companion activation and its dialogue shortcut historically used slightly
    /// different notions of "in the player party". Bannerlord's own companion dialogue uses
    /// HeroHelper.IsCompanionInPlayerParty, so use the same native predicate for unregistered
    /// companions and retain MCC's registered-hero path for already shared characters.
    /// </summary>
    internal static class CompanionDialogueEligibilityRepair
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _installed;
        private static MethodInfo _isRegisteredSharedHeroMethod;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            try
            {
                Type behaviorType = Type.GetType(
                    "MultiCharacterCampaignTOR.MultiCharacterCampaignBehavior, MultiCharacterCampaignTOR",
                    throwOnError: true);
                MethodInfo condition = behaviorType.GetMethod("ConversationSwitchCondition", InstanceFlags);
                _isRegisteredSharedHeroMethod = behaviorType.GetMethod("IsRegisteredSharedHero", InstanceFlags);
                if (condition == null || _isRegisteredSharedHeroMethod == null)
                {
                    throw new MissingMethodException(behaviorType.FullName, "ConversationSwitchCondition/IsRegisteredSharedHero");
                }

                new Harmony("xmarre.multicharactercampaign.tor.companion-dialogue-eligibility").Patch(
                    condition,
                    prefix: new HarmonyMethod(typeof(CompanionDialogueEligibilityRepair), nameof(BeforeConversationSwitchCondition)));

                _installed = true;
                Log("Installed native player-party eligibility repair for companion activation dialogue.");
            }
            catch (Exception ex)
            {
                Log("Companion dialogue eligibility repair installation failed safely: " + Unwrap(ex));
            }
        }

        private static bool BeforeConversationSwitchCondition(object __instance, ref bool __result)
        {
            try
            {
                Hero hero = Hero.OneToOneConversationHero;
                if (hero == null || hero == Hero.MainHero || !hero.IsAlive || !hero.IsActive || hero.IsPrisoner)
                {
                    __result = false;
                    return false;
                }

                bool registered = __instance != null &&
                    Convert.ToBoolean(_isRegisteredSharedHeroMethod.Invoke(__instance, new object[] { hero }));

                if (registered)
                {
                    // Registered shared heroes remain switchable through dialogue only while physically
                    // present in the current MainParty, matching MCC's existing switching invariant.
                    __result = hero.PartyBelongedTo == MobileParty.MainParty;
                    return false;
                }

                // Use Bannerlord 1.3.15's own companion-dialogue eligibility predicate instead of
                // reconstructing it from CompanionOf/PartyBelongedTo fields that TOR or other mods may
                // represent differently.
                __result = HeroHelper.IsCompanionInPlayerParty(hero);
                return false;
            }
            catch (Exception ex)
            {
                __result = false;
                Log("Companion dialogue eligibility evaluation failed closed: " + Unwrap(ex));
                return false;
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
                Type logType = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", throwOnError: false);
                MethodInfo info = logType?.GetMethod("Info", StaticFlags, null, new[] { typeof(string) }, null);
                info?.Invoke(null, new object[] { "[CompanionDialogueFix] " + message });
            }
            catch
            {
            }
        }
    }
}
