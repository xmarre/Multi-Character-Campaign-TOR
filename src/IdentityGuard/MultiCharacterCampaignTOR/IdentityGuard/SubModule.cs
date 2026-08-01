// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using TaleWorlds.MountAndBlade;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	public sealed class SubModule : MBSubModuleBase
	{
		protected override void OnSubModuleLoad()
		{
			base.OnSubModuleLoad();
			RuntimeIdentityGuard.Install();
			SharedPartyRuntimeFixes.Install();
			BattleTakeoverSafetyGuard.Install();
			BattleInterventionFlowFix.Install();
			BattleInterventionSettings.Install();
			BattleInterventionPrediction.Install();
		}

		protected override void OnApplicationTick(float dt)
		{
			base.OnApplicationTick(dt);
			BattleInterventionAlert.Tick();
		}
	}
}
