// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using MultiCharacterCampaignTOR.WaywatcherFix;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace MultiCharacterCampaignTOR.RuntimeCompatibility
{
	public sealed class SubModule : MBSubModuleBase
	{
		protected override void OnSubModuleLoad()
		{
			base.OnSubModuleLoad();
			HarmonyAssemblyResolver.Install();
			CareerButtonRefreshRepair.Install();
			AICareerAbilitySupport.Install();
			AICareerAbilityActivationContext.Install();
			AICareerAbilityTransitionGuard.Install();
			HarbingerAIControllerSafety.Install();
			CompanionDialogueEligibilityRepair.Install();
		}

		protected override void OnGameStart(Game game, IGameStarter gameStarter)
		{
			base.OnGameStart(game, gameStarter);
			HarmonyAssemblyResolver.Install();

			// The affected Harmony 2.4 loader can reject assembly-qualified runtime lookup during
			// OnSubModuleLoad even though the same lookup is valid once GameStart is reached. These two
			// reconstructed legacy repairs need campaign/runtime types and therefore belong here, not in
			// the early module-loader phase.
			CareerAbilityRepair.Install();
			RuntimeRepair.Install();

			CareerButtonRefreshRepair.Install();
			AICareerAbilitySupport.Install();
			AICareerAbilityActivationContext.Install();
			AICareerAbilityTransitionGuard.Install();
			HarbingerAIControllerSafety.Install();
			CompanionDialogueEligibilityRepair.Install();
		}
	}
}
