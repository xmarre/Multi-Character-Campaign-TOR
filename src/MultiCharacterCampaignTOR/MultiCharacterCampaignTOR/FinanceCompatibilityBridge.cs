// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace MultiCharacterCampaignTOR
{
	internal static class FinanceCompatibilityBridge
	{
		private static DateTime _lastMainPartySuppressionLog = DateTime.MinValue;

		public static bool ShouldSkipMainPartyIncome(MobileParty party, Clan clan)
		{
			try
			{
				if (party == null || clan == null)
				{
					return false;
				}
				if (!object.ReferenceEquals(party, MobileParty.MainParty) || !object.ReferenceEquals(clan, Clan.PlayerClan))
				{
					return false;
				}
				MultiCharacterCampaignBehavior instance = MultiCharacterCampaignBehavior.Instance;
				if (instance == null || !instance.IsRegisteredSharedHero(Hero.MainHero))
				{
					return false;
				}
				if ((DateTime.UtcNow - _lastMainPartySuppressionLog).TotalSeconds >= 5.0)
				{
					_lastMainPartySuppressionLog = DateTime.UtcNow;
					Log.Info("Excluded MobileParty.MainParty from secondary caravan/party income. ActiveHero=" + Reflection.IdOf(Hero.MainHero) + "; MainPartyLeader=" + Reflection.IdOf(party.LeaderHero) + "; ClanLeader=" + Reflection.IdOf(Reflection.GetMember(clan, "Leader")) + ".");
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("Main-party finance compatibility check failed safely", ex);
				return false;
			}
		}

		public static void LogFinanceInputs(Clan clan, ref ExplainedNumber goldChange, bool SetActiveLeader)
		{
			MultiCharacterCampaignBehavior instance = MultiCharacterCampaignBehavior.Instance;
			if (instance == null || !instance.IsRegisteredSharedHero(Hero.MainHero) || !object.ReferenceEquals(Clan.PlayerClan, clan) || Reflection.ToBool(Reflection.GetMember(clan, "IsUnderMercenaryService")))
			{
				return;
			}
			object member = Reflection.GetMember(clan, "Kingdom");
			if (member != null && Convert.ToInt32(Reflection.GetMember(clan, "Gold")) > 100000 && !object.ReferenceEquals(Reflection.GetMember(clan, "Leader"), Hero.MainHero))
			{
				int num = (int)(((float)Convert.ToInt32(Reflection.GetMember(clan, "Gold")) - 100000f) * 0.01f);
				goldChange.Add(num, (TextObject)Reflection.FindField(Type.GetType("TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel, TaleWorlds.CampaignSystem"), "_kingdomBudgetText", true).GetValue(null));
				if (SetActiveLeader)
				{
					Reflection.FindField(member.GetType(), "_leader", false)?.SetValue(member, Convert.ToInt32(Reflection.GetMember(member, "KingdomBudgetWallet")) - num);
				}
			}
		}
	}
}
