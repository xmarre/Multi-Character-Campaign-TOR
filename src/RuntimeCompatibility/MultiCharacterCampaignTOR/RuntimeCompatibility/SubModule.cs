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
			CareerAbilityRepair.Install();
			AICareerAbilitySupport.Install();
			AICareerAbilityActivationContext.Install();
			AICareerAbilityTransitionGuard.Install();
			AICareerAbilityBehaviorBridge.Install();
			RuntimeRepair.Install();
		}

		protected override void OnGameStart(Game game, IGameStarter gameStarter)
		{
			base.OnGameStart(game, gameStarter);
			CareerAbilityRepair.Install();
			AICareerAbilitySupport.Install();
			AICareerAbilityActivationContext.Install();
			AICareerAbilityTransitionGuard.Install();
			AICareerAbilityBehaviorBridge.Install();
			RuntimeRepair.Install();
		}
	}
}
