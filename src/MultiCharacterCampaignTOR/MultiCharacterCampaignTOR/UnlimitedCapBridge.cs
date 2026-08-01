// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace MultiCharacterCampaignTOR
{
	internal static class UnlimitedCapBridge
	{
		private static string _lastCompanionApplication = string.Empty;

		private static string _lastPartyApplication = string.Empty;

		private static bool _missingAssemblyLogged;

		public static void ApplyCompanionLimit(Clan clan, ref int result)
		{
			Apply(clan, ref result, companions: true);
		}

		public static void ApplyPartyLimit(Clan clan, int clanTierToCheck, ref int result)
		{
			Apply(clan, ref result, companions: false, clanTierToCheck);
		}

		private static void Apply(Clan clan, ref int result, bool companions, int clanTierToCheck = 0)
		{
			try
			{
				if (clan == null || !object.ReferenceEquals(clan, Clan.PlayerClan))
				{
					return;
				}
				MultiCharacterCampaignBehavior instance = MultiCharacterCampaignBehavior.Instance;
				if (instance == null || !instance.IsRegisteredSharedHero(Hero.MainHero))
				{
					return;
				}
				Hero value = Reflection.GetMember(clan, "Leader") as Hero;
				object member = Reflection.GetMember(value, "IsHumanPlayerCharacter");
				if (member != null && string.Equals(member.ToString(), "True", StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
				Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => string.Equals(a.GetName().Name, "UnlimitedCAP", StringComparison.OrdinalIgnoreCase));
				if (assembly == null)
				{
					if (!_missingAssemblyLogged)
					{
						_missingAssemblyLogged = true;
						Log.Info("UnlimitedCAP was not detected. Limit compatibility bridge remains dormant.");
					}
					return;
				}
				Type type = assembly.GetType("UnlimitedCAP.Global", throwOnError: false);
				FieldInfo fieldInfo = ((!(type == null)) ? type.GetField("Settings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null);
				object obj = ((!(fieldInfo == null)) ? fieldInfo.GetValue(null) : null);
				if (obj == null)
				{
					return;
				}
				string text = ((!companions) ? "Parties" : "Companions");
				object member2 = Reflection.GetMember(obj, "activateCheat_" + text);
				bool flag;
				try
				{
					flag = member2 != null && Convert.ToBoolean(member2);
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					return;
				}
				object member3 = Reflection.GetMember(obj, "method_" + text);
				object member4 = Reflection.GetMember(member3, "SelectedValue");
				string text2 = ((member4 != null) ? member4.ToString() : string.Empty);
				object member5 = Reflection.GetMember(obj, (!companions) ? "partiesLimit" : "companionsLimit");
				if (member5 == null || !int.TryParse(member5.ToString(), out var result2))
				{
					return;
				}
				int num = result;
				if (string.Equals(text2, "Definite", StringComparison.OrdinalIgnoreCase))
				{
					result = result2;
				}
				else
				{
					if (!string.Equals(text2, "Progressive", StringComparison.OrdinalIgnoreCase))
					{
						return;
					}
					if (companions)
					{
						result += result2;
					}
					else
					{
						result = clanTierToCheck + result2;
					}
				}
				string text3 = Reflection.IdOf(Hero.MainHero) + "|" + text2 + "|" + result2 + "|" + num + "|" + result;
				if (companions)
				{
					if (!string.Equals(text3, _lastCompanionApplication, StringComparison.Ordinal))
					{
						_lastCompanionApplication = text3;
						Log.Info("Applied UnlimitedCAP companion-limit compatibility for shared active hero=" + Reflection.IdOf(Hero.MainHero) + "; mode=" + text2 + "; configured=" + result2 + "; before=" + num + "; after=" + result + ".");
					}
				}
				else if (!string.Equals(text3, _lastPartyApplication, StringComparison.Ordinal))
				{
					_lastPartyApplication = text3;
					Log.Info("Applied UnlimitedCAP party-limit compatibility for shared active hero=" + Reflection.IdOf(Hero.MainHero) + "; mode=" + text2 + "; configured=" + result2 + "; before=" + num + "; after=" + result + ".");
				}
			}
			catch (Exception ex)
			{
				Log.Error("UnlimitedCAP " + ((!companions) ? "party" : "companion") + " limit compatibility failed safely", ex);
			}
		}
	}
}
