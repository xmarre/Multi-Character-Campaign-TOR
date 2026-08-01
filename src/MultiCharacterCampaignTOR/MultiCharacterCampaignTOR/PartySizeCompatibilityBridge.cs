// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace MultiCharacterCampaignTOR
{
	internal static class PartySizeCompatibilityBridge
	{
		private static string _lastApplication = string.Empty;

		public static void ApplyAdministrativeMainPartyBonuses(Hero partyLeader, Clan clan, ref ExplainedNumber number)
		{
			try
			{
				if (partyLeader == null || clan == null || !object.ReferenceEquals(clan, Clan.PlayerClan) || !object.ReferenceEquals(partyLeader, Hero.MainHero) || MobileParty.MainParty == null || !object.ReferenceEquals(MobileParty.MainParty.LeaderHero, partyLeader) || !object.ReferenceEquals(partyLeader.PartyBelongedTo, MobileParty.MainParty))
				{
					return;
				}
				MultiCharacterCampaignBehavior instance = MultiCharacterCampaignBehavior.Instance;
				if (instance != null && instance.IsRegisteredSharedHero(partyLeader) && Reflection.GetMember(clan, "Leader") is Hero hero && !object.ReferenceEquals(hero, partyLeader))
				{
					float resultNumber = number.ResultNumber;
					float clanTierLeadershipDelta = GetClanTierLeadershipDelta(partyLeader, hero, clan);
					bool flag = IsFactionLeader(hero);
					bool flag2 = IsFactionLeader(partyLeader);
					bool flag3 = IsKingdomFaction(Reflection.GetMember(hero, "MapFaction"));
					bool flag4 = flag3 && GetInt(Reflection.GetMember(clan, "Tier")) >= 5 && HasActivePolicy(hero, "NobleRetinues");
					bool flag5 = flag3 && flag && !flag2 && HasActivePolicy(hero, "RoyalGuard");
					if (clanTierLeadershipDelta > 0.001f)
					{
						number.Add(clanTierLeadershipDelta, new TextObject("{=torscc_clan_leader_tier}Shared campaign clan-leader tier"));
					}
					if (flag && !flag2)
					{
						number.Add(20f, new TextObject("{=torscc_faction_leader}Shared campaign faction leadership"));
					}
					if (flag4)
					{
						number.Add(40f, new TextObject("{=torscc_noble_retinues}Noble Retinues (shared campaign leader)"));
					}
					if (flag5)
					{
						number.Add(60f, new TextObject("{=torscc_royal_guard}Royal Guard (shared campaign leader)"));
					}
					float resultNumber2 = number.ResultNumber;
					string text = Reflection.IdOf(partyLeader) + "|" + Reflection.IdOf(hero) + "|" + clanTierLeadershipDelta.ToString("0.###") + "|" + flag + "|" + flag4 + "|" + flag5 + "|" + resultNumber.ToString("0.###") + "|" + resultNumber2.ToString("0.###");
					if (!string.Equals(text, _lastApplication, StringComparison.Ordinal))
					{
						_lastApplication = text;
						Log.Info("Applied main-party administrative limit correction. ActiveHero=" + Reflection.IdOf(partyLeader) + "; AdministrativeClanLeader=" + Reflection.IdOf(hero) + "; clanTierDelta=" + clanTierLeadershipDelta.ToString("0.###") + "; factionLeaderBonus=" + ((flag && !flag2) ? 20 : 0) + "; nobleRetinuesBonus=" + (flag4 ? 40 : 0) + "; royalGuardBonus=" + (flag5 ? 60 : 0) + "; before=" + resultNumber.ToString("0.###") + "; after=" + resultNumber2.ToString("0.###") + ".");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Main-party administrative limit correction failed safely", ex);
			}
		}

		private static float GetClanTierLeadershipDelta(Hero activeHero, Hero administrativeLeader, Clan clan)
		{
			try
			{
				object member = Reflection.GetMember(Campaign.Current, "Models");
				object member2 = Reflection.GetMember(member, "PartySizeLimitModel");
				if (member2 != null)
				{
					MethodInfo methodInfo = member2.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "GetClanTierPartySizeEffectForHero" && m.GetParameters().Length == 1);
					if (methodInfo != null)
					{
						int num = Convert.ToInt32(methodInfo.Invoke(member2, new object[1] { activeHero }));
						int num2 = Convert.ToInt32(methodInfo.Invoke(member2, new object[1] { administrativeLeader }));
						if (num2 > num)
						{
							return num2 - num;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Could not derive clan-tier party-size leadership delta from the active model", ex);
			}
			int num3 = GetInt(Reflection.GetMember(clan, "Tier"));
			return (num3 <= 0) ? 0f : ((float)num3 * 10f);
		}

		private static bool IsFactionLeader(Hero hero)
		{
			if (hero == null)
			{
				return false;
			}
			object member = Reflection.GetMember(hero, "MapFaction");
			return member != null && object.ReferenceEquals(Reflection.GetMember(member, "Leader"), hero);
		}

		private static bool IsKingdomFaction(object faction)
		{
			if (faction == null)
			{
				return false;
			}
			object member = Reflection.GetMember(faction, "IsKingdomFaction");
			try
			{
				return member != null && Convert.ToBoolean(member);
			}
			catch
			{
				return false;
			}
		}

		private static bool HasActivePolicy(Hero administrativeLeader, string policyName)
		{
			object member = Reflection.GetMember(administrativeLeader, "MapFaction");
			if (!IsKingdomFaction(member))
			{
				return false;
			}
			object staticMember = Reflection.GetStaticMember("TaleWorlds.CampaignSystem.DefaultPolicies, TaleWorlds.CampaignSystem", policyName);
			if (staticMember == null)
			{
				return false;
			}
			object member2 = Reflection.GetMember(member, "ActivePolicies");
			foreach (object item in Reflection.Enumerate(member2))
			{
				if (object.ReferenceEquals(item, staticMember) || object.Equals(item, staticMember))
				{
					return true;
				}
			}
			return false;
		}

		private static int GetInt(object value)
		{
			try
			{
				return (value != null) ? Convert.ToInt32(value) : 0;
			}
			catch
			{
				return 0;
			}
		}
	}
}
