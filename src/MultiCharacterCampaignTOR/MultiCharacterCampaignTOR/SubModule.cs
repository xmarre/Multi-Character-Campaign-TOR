// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TORSharedCharacterCampaign;

namespace MultiCharacterCampaignTOR
{
	public sealed class SubModule : MBSubModuleBase
	{
		protected override void OnSubModuleLoad()
		{
			base.OnSubModuleLoad();
			Log.Initialize();
			UI.LogRuntimeBindings();
			HarmonyBridge.TryInstall();
		}

		protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
		{
			base.OnGameStart(game, gameStarterObject);
			if (gameStarterObject is CampaignGameStarter campaignGameStarter)
			{
				MultiCharacterCampaignBehavior campaignBehavior = new SharedCampaignBehavior();
				campaignGameStarter.AddBehavior(campaignBehavior);
				Log.Info("Campaign behavior added.");
			}
		}

		protected override void OnApplicationTick(float dt)
		{
			base.OnApplicationTick(dt);
			PartyScreenSelectionBridge.OnApplicationTick();
		}
	}
}
