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
			RegisteredCareerAbilityPrerequisite.Install();
			RegisteredCareerAbilityIdentityRepair.Install();
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

			// Recovered campaign/runtime repairs need the game-start runtime boundary. The active-player
			// career repair uses a strongly linked Harmony bootstrap here; the old recovered Install()
			// method is deliberately never called because it still contains a loader-sensitive
			// assembly-qualified 0Harmony lookup.
			LinkedCareerAbilityRepairInstaller.Install();
			RuntimeRepair.Install();
			NativeCreationCompatibility.Install();
			NativeCreationLegacySnapshotRepair.Install();

			CareerButtonRefreshRepair.Install();
			RegisteredCareerAbilityPrerequisite.Install();
			RegisteredCareerAbilityIdentityRepair.Install();
			AICareerAbilitySupport.Install();
			AICareerAbilityActivationContext.Install();
			AICareerAbilityTransitionGuard.Install();
			HarbingerAIControllerSafety.Install();
			CompanionDialogueEligibilityRepair.Install();
		}
	}
}
