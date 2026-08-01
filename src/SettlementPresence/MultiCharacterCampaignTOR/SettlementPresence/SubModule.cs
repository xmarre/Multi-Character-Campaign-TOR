// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace MultiCharacterCampaignTOR.SettlementPresence
{
	public sealed class SubModule : MBSubModuleBase
	{
		protected override void OnSubModuleLoad()
		{
			base.OnSubModuleLoad();
			try
			{
				SettlementPresenceRepair.Install();
			}
			catch (Exception ex)
			{
				try
				{
					Type type = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", throwOnError: false);
					MethodInfo methodInfo = ((!(type == null)) ? type.GetMethod("Warning", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null);
					if (methodInfo != null)
					{
						methodInfo.Invoke(null, new object[1] { "[SettlementPresence] Install failed: " + ex.GetType().Name + ": " + ex.Message });
					}
				}
				catch
				{
				}
			}
		}
	}
}
