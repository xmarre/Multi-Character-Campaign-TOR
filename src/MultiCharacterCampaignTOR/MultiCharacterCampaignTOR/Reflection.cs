// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace MultiCharacterCampaignTOR
{
	internal static class Reflection
	{
		public static string IdOf(object value)
		{
			if (value == null)
			{
				return string.Empty;
			}
			try
			{
				object obj = GetMember(value, "StringId") ?? GetMember(value, "Id");
				return (obj != null) ? obj.ToString() : string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}

		public static string DisplayName(object value)
		{
			if (value == null)
			{
				return string.Empty;
			}
			try
			{
				object member = GetMember(value, "Name");
				if (member != null && !string.IsNullOrWhiteSpace(member.ToString()))
				{
					return member.ToString();
				}
				string text = IdOf(value);
				return (!string.IsNullOrEmpty(text)) ? SplitPascal(text.Replace("tor_", "")) : value.ToString();
			}
			catch
			{
				return value.ToString();
			}
		}

		public static Hero FindHero(string id)
		{
			foreach (object item in EnumerateStatic(typeof(Hero), "AllAliveHeroes"))
			{
				if (item is Hero hero && IdOf(hero) == id)
				{
					return hero;
				}
			}
			return null;
		}

		public static void EnsureHeroActive(Hero hero)
		{
			if (hero == null)
			{
				throw new ArgumentNullException("hero");
			}
			if (hero.IsActive)
			{
				return;
			}
			Type type = ((object)hero).GetType();
			PropertyInfo propertyInfo = FindProperty(type, "HeroState", isStatic: false);
			MethodInfo methodInfo = ((!(propertyInfo == null)) ? propertyInfo.GetSetMethod(nonPublic: true) : null);
			if (methodInfo != null && propertyInfo.PropertyType.IsEnum)
			{
				object obj = Enum.Parse(propertyInfo.PropertyType, "Active", ignoreCase: true);
				methodInfo.Invoke(hero, new object[1] { obj });
			}
			else
			{
				PropertyInfo propertyInfo2 = FindProperty(type, "IsActive", isStatic: false);
				MethodInfo methodInfo2 = ((!(propertyInfo2 == null)) ? propertyInfo2.GetSetMethod(nonPublic: true) : null);
				if (methodInfo2 != null)
				{
					methodInfo2.Invoke(hero, new object[1] { true });
				}
			}
			if (!hero.IsActive)
			{
				throw new InvalidOperationException("Hero state could not be changed to Active for " + IdOf(hero) + ".");
			}
			Log.Info("Activated newly created/shared hero state. Hero=" + IdOf(hero) + ".");
		}

		public static bool IsHeroInMainPartyRoster(Hero hero)
		{
			if (hero == null || MobileParty.MainParty == null)
			{
				return false;
			}
			try
			{
				object member = GetMember(MobileParty.MainParty, "MemberRoster");
				if (member == null)
				{
					return hero.PartyBelongedTo == MobileParty.MainParty;
				}
				object obj = InvokeParameterless(member, "GetTroopRoster");
				if (obj == null)
				{
					return hero.PartyBelongedTo == MobileParty.MainParty;
				}
				foreach (object item in Enumerate(obj))
				{
					object member2 = GetMember(item, "Character");
					Hero hero2 = GetMember(member2, "HeroObject") as Hero;
					if (object.ReferenceEquals(hero2, hero) || (!string.IsNullOrEmpty(IdOf(hero2)) && IdOf(hero2) == IdOf(hero)))
					{
						return true;
					}
				}
				return false;
			}
			catch (Exception ex)
			{
				Log.Error("Could not inspect main-party roster for hero=" + IdOf(hero), ex);
				return hero.PartyBelongedTo == MobileParty.MainParty;
			}
		}

		public static void EnsureHeroInMainParty(Hero hero)
		{
			if (hero == null)
			{
				throw new ArgumentNullException("hero");
			}
			MobileParty mainParty = MobileParty.MainParty;
			if (mainParty == null)
			{
				throw new InvalidOperationException("The main party is unavailable.");
			}
			if (hero.PartyBelongedTo == mainParty && IsHeroInMainPartyRoster(hero))
			{
				return;
			}
			if (!IsHeroInMainPartyRoster(hero))
			{
				AddHeroToPartyAction.Apply(hero, mainParty, showNotification: false);
			}
			if (hero.PartyBelongedTo != mainParty)
			{
				PropertyInfo propertyInfo = FindProperty(((object)hero).GetType(), "PartyBelongedTo", isStatic: false);
				MethodInfo methodInfo = ((!(propertyInfo == null)) ? propertyInfo.GetSetMethod(nonPublic: true) : null);
				if (methodInfo != null)
				{
					methodInfo.Invoke(hero, new object[1] { mainParty });
				}
			}
			if (hero.PartyBelongedTo != mainParty || !IsHeroInMainPartyRoster(hero))
			{
				throw new InvalidOperationException("Hero " + IdOf(hero) + " could not be attached to the main party.");
			}
			Log.Info("Confirmed shared hero main-party membership. Hero=" + IdOf(hero) + ".");
		}

		public static List<object> GetPlayableCultures()
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			object obj = ((Hero.MainHero != null) ? GetMember(Hero.MainHero.CharacterObject, "Culture") : null);
			if (obj != null)
			{
				dictionary[IdOf(obj)] = obj;
			}
			foreach (object item in EnumerateStatic(typeof(CharacterObject), "All"))
			{
				object member = GetMember(item, "Culture");
				if (member != null)
				{
					string text = IdOf(member);
					string text2 = IdOf(item);
					if (!string.IsNullOrEmpty(text) && (text.IndexOf("tor_", StringComparison.OrdinalIgnoreCase) >= 0 || text2.IndexOf("tor_", StringComparison.OrdinalIgnoreCase) >= 0))
					{
						dictionary[text] = member;
					}
				}
			}
			return dictionary.Values.OrderBy(DisplayName).ToList();
		}

		public static CharacterObject FindHeroTemplate(object culture, bool female)
		{
			CharacterObject characterObject = null;
			int num = int.MinValue;
			object member = GetMember(culture, "BasicTroop");
			foreach (object item in EnumerateStatic(typeof(CharacterObject), "All"))
			{
				if (!(item is CharacterObject characterObject2))
				{
					continue;
				}
				object member2 = GetMember(characterObject2, "Culture");
				if (member2 != culture && IdOf(member2) != IdOf(culture))
				{
					continue;
				}
				bool flag = ToBool(GetMember(characterObject2, "IsFemale"));
				if (flag != female)
				{
					continue;
				}
				string text = IdOf(characterObject2).ToLowerInvariant();
				if (!text.Contains("child") && !text.Contains("baby") && !text.Contains("summon") && !text.Contains("temporary"))
				{
					bool flag2 = ToBool(GetMember(characterObject2, "IsHero"));
					int result = 1;
					object member3 = GetMember(characterObject2, "Level");
					if (member3 != null)
					{
						int.TryParse(member3.ToString(), out result);
					}
					int num2 = -Math.Max(0, result) * 2;
					if (result == 1)
					{
						num2 += 1000000;
					}
					if (text.Contains("player"))
					{
						num2 += 1000;
					}
					if (text.Contains("character_creation") || text.Contains("charactercreation") || text.Contains("creation_template"))
					{
						num2 += 700;
					}
					if (item == member)
					{
						num2 += 250;
					}
					if (text.Contains("wanderer"))
					{
						num2 += 100;
					}
					if (!flag2)
					{
						num2 += 50;
					}
					if (text.Contains("lord") || text.Contains("king") || text.Contains("legendary"))
					{
						num2 -= 100;
					}
					if (num2 > num)
					{
						num = num2;
						characterObject = characterObject2;
					}
				}
			}
			if (characterObject != null)
			{
				return characterObject;
			}
			return member as CharacterObject;
		}

		public static string RandomCultureName(object culture, bool female)
		{
			try
			{
				object member = GetMember(culture, (!female) ? "MaleNameList" : "FemaleNameList");
				List<object> list = Enumerate(member).ToList();
				if (list.Count > 0)
				{
					int index = Math.Abs(Environment.TickCount) % list.Count;
					return list[index].ToString();
				}
			}
			catch
			{
			}
			return (!female) ? "New Hero" : "New Heroine";
		}

		public static IEnumerable<Hero> GetPlayerClanCompanions()
		{
			List<Hero> list = new List<Hero>();
			HashSet<string> yielded = new HashSet<string>(StringComparer.Ordinal);
			Clan clan = null;
			Hero hero = null;
			try
			{
				clan = Clan.PlayerClan;
			}
			catch (Exception ex)
			{
				Log.Error("Companion discovery could not read Clan.PlayerClan", ex);
			}
			try
			{
				hero = Hero.MainHero;
			}
			catch (Exception ex2)
			{
				Log.Error("Companion discovery could not read Hero.MainHero", ex2);
			}
			if (hero != null)
			{
				try
				{
					object member = GetMember(hero, "CompanionsInParty");
					AddHeroCandidates(list, yielded, member, "Hero.MainHero.CompanionsInParty");
				}
				catch (Exception ex3)
				{
					Log.Error("Companion discovery source failed: Hero.MainHero.CompanionsInParty", ex3);
				}
			}
			if (clan != null)
			{
				try
				{
					object member2 = GetMember(clan, "Companions");
					AddHeroCandidates(list, yielded, member2, "Clan.PlayerClan.Companions");
				}
				catch (Exception ex4)
				{
					Log.Error("Companion discovery source failed: Clan.PlayerClan.Companions", ex4);
				}
			}
			try
			{
				object mainParty = MobileParty.MainParty;
				object member3 = GetMember(mainParty, "MemberRoster");
				object value = InvokeParameterless(member3, "GetTroopRoster");
				int num = 0;
				foreach (object item in Enumerate(value))
				{
					num++;
					object member4 = GetMember(item, "Character");
					if (GetMember(member4, "HeroObject") is Hero hero2)
					{
						bool flag = clan != null && hero2.CompanionOf == clan;
						bool flag2 = ToBool(GetMember(hero2, "IsPlayerCompanion"));
						if (flag || flag2)
						{
							AddHeroCandidate(list, yielded, hero2, "MobileParty.MainParty.MemberRoster");
						}
					}
				}
				Log.Info("Companion discovery source MobileParty.MainParty.MemberRoster enumerated " + num + " entries.");
			}
			catch (Exception ex5)
			{
				Log.Error("Companion discovery source failed: MobileParty.MainParty.MemberRoster", ex5);
			}
			try
			{
				foreach (object item2 in EnumerateStatic(typeof(Hero), "AllAliveHeroes"))
				{
					if (!(item2 is Hero hero3))
					{
						continue;
					}
					try
					{
						bool flag3 = clan != null && hero3.CompanionOf == clan;
						bool flag4 = ToBool(GetMember(hero3, "IsPlayerCompanion"));
						if (flag3 || flag4)
						{
							AddHeroCandidate(list, yielded, hero3, "Hero.AllAliveHeroes");
						}
					}
					catch (Exception ex6)
					{
						Log.Error("Companion discovery candidate classification failed for id=" + IdOf(hero3), ex6);
					}
				}
			}
			catch (Exception ex7)
			{
				Log.Error("Companion discovery source failed: Hero.AllAliveHeroes", ex7);
			}
			Log.Info("Companion discovery completed. Unique candidates=" + list.Count + ".");
			return list;
		}

		private static void AddHeroCandidates(List<Hero> result, HashSet<string> yielded, object values, string source)
		{
			int num = 0;
			foreach (object item in Enumerate(values))
			{
				num++;
				if (item is Hero hero)
				{
					AddHeroCandidate(result, yielded, hero, source);
				}
			}
			Log.Info("Companion discovery source " + source + " enumerated " + num + " entries.");
		}

		private static void AddHeroCandidate(List<Hero> result, HashSet<string> yielded, Hero hero, string source)
		{
			if (hero != null)
			{
				string text = IdOf(hero);
				if (string.IsNullOrEmpty(text))
				{
					text = "<runtime:" + ((object)hero).GetHashCode() + ">";
				}
				if (yielded.Add(text))
				{
					result.Add(hero);
					Log.Info("Companion discovery accepted candidate from " + source + ": id=" + text + ".");
				}
			}
		}

		public static void ApplyBackground(Hero hero, BackgroundProfile profile)
		{
			if (hero == null || profile == null)
			{
				return;
			}
			try
			{
				Type type = Type.GetType("TaleWorlds.Core.DefaultSkills, TaleWorlds.Core");
				Type type2 = Type.GetType("TaleWorlds.Core.DefaultCharacterAttributes, TaleWorlds.Core");
				object member = GetMember(hero, "HeroDeveloper");
				string[] skills = profile.Skills;
				foreach (string name in skills)
				{
					object obj = ((!(type == null)) ? type.GetProperty(name, BindingFlags.Static | BindingFlags.Public).GetValue(null, null) : null);
					if (obj == null)
					{
						continue;
					}
					MethodInfo methodInfo = ((object)hero).GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "GetSkillValue" && m.GetParameters().Length == 1);
					MethodInfo methodInfo2 = ((object)hero).GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "SetSkillValue" && m.GetParameters().Length == 2);
					int num = ((!(methodInfo == null)) ? Convert.ToInt32(methodInfo.Invoke(hero, new object[1] { obj })) : 0);
					if (methodInfo2 != null)
					{
						methodInfo2.Invoke(hero, new object[2]
						{
							obj,
							Math.Min(300, num + 20)
						});
					}
					if (member != null)
					{
						MethodInfo methodInfo3 = member.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "AddFocus" && m.GetParameters().Length == 3);
						if (methodInfo3 != null)
						{
							methodInfo3.Invoke(member, new object[3] { obj, 1, false });
						}
					}
				}
				object obj2 = ((!(type2 == null)) ? type2.GetProperty(profile.Attribute, BindingFlags.Static | BindingFlags.Public).GetValue(null, null) : null);
				if (member != null && obj2 != null)
				{
					MethodInfo methodInfo4 = member.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "AddAttribute" && m.GetParameters().Length == 3);
					if (methodInfo4 != null)
					{
						methodInfo4.Invoke(member, new object[3] { obj2, 1, false });
					}
				}
				Log.Info("Applied background " + profile.Id + " to " + IdOf(hero) + ".");
			}
			catch (Exception ex)
			{
				Log.Error("Background application failed safely", ex);
			}
		}

		public static bool IsMissionActive()
		{
			try
			{
				return Mission.Current != null;
			}
			catch
			{
				return GetStaticMember("TaleWorlds.MountAndBlade.Mission, TaleWorlds.MountAndBlade", "Current") != null;
			}
		}

		public static bool IsEncounterActive()
		{
			object staticMember = GetStaticMember("TaleWorlds.CampaignSystem.PlayerEncounter, TaleWorlds.CampaignSystem", "Current");
			if (staticMember == null)
			{
				return false;
			}
			object member = GetMember(staticMember, "BattleState");
			return member != null || ToBool(GetMember(staticMember, "IsActive"));
		}

		public static bool IsBarterOrInventoryActive()
		{
			string[] array = new string[2] { "TaleWorlds.CampaignSystem.BarterSystem.BarterManager, TaleWorlds.CampaignSystem", "TaleWorlds.CampaignSystem.Inventory.InventoryManager, TaleWorlds.CampaignSystem" };
			string[] array2 = array;
			foreach (string typeName in array2)
			{
				Type type = Type.GetType(typeName);
				if (!(type == null))
				{
					object obj = GetStaticMember(typeName, "Instance") ?? GetStaticMember(typeName, "Current");
					if (obj != null && (ToBool(GetMember(obj, "IsActive")) || ToBool(GetMember(obj, "IsBarterActive"))))
					{
						return true;
					}
				}
			}
			return false;
		}

		public static object GetMember(object value, string name)
		{
			if (value == null || string.IsNullOrEmpty(name))
			{
				return null;
			}
			Type type = value.GetType();
			PropertyInfo propertyInfo = FindProperty(type, name, isStatic: false);
			if (propertyInfo != null)
			{
				return propertyInfo.GetValue(value, null);
			}
			FieldInfo fieldInfo = FindField(type, name, isStatic: false);
			return (!(fieldInfo == null)) ? fieldInfo.GetValue(value) : null;
		}

		public static object GetStaticMember(string typeName, string name)
		{
			Type type = Type.GetType(typeName);
			if (type == null || string.IsNullOrEmpty(name))
			{
				return null;
			}
			PropertyInfo propertyInfo = FindProperty(type, name, isStatic: true);
			if (propertyInfo != null)
			{
				return propertyInfo.GetValue(null, null);
			}
			FieldInfo fieldInfo = FindField(type, name, isStatic: true);
			return (!(fieldInfo == null)) ? fieldInfo.GetValue(null) : null;
		}

		private static object InvokeParameterless(object value, string name)
		{
			if (value == null || string.IsNullOrEmpty(name))
			{
				return null;
			}
			MethodInfo methodInfo = (from m in value.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				where string.Equals(m.Name, name, StringComparison.Ordinal) && m.GetParameters().Length == 0
				orderby InheritanceDistance(value.GetType(), m.DeclaringType)
				select m).FirstOrDefault();
			return (!(methodInfo == null)) ? methodInfo.Invoke(value, null) : null;
		}

		public static IEnumerable<object> EnumerateStatic(Type type, string property)
		{
			if (type == null || string.IsNullOrEmpty(property))
			{
				yield break;
			}
			PropertyInfo member = FindProperty(type, property, isStatic: true);
			if (member == null)
			{
				yield break;
			}
			foreach (object item in Enumerate(member.GetValue(null, null)))
			{
				yield return item;
			}
		}

		private static PropertyInfo FindProperty(Type type, string name, bool isStatic)
		{
			BindingFlags bindingAttr = (BindingFlags)(0x30 | ((!isStatic) ? 4 : 8));
			return (from p in type.GetProperties(bindingAttr).Where(delegate(PropertyInfo p)
				{
					if (!string.Equals(p.Name, name, StringComparison.Ordinal))
					{
						return false;
					}
					if (p.GetIndexParameters().Length != 0)
					{
						return false;
					}
					MethodInfo getMethod = p.GetGetMethod(nonPublic: true);
					return getMethod != null && getMethod.IsStatic == isStatic;
				})
				orderby InheritanceDistance(type, p.DeclaringType), p.GetGetMethod(nonPublic: true).IsPublic descending
				select p).FirstOrDefault();
		}

		internal static FieldInfo FindField(Type type, string name, bool isStatic)
		{
			BindingFlags bindingAttr = (BindingFlags)(0x30 | ((!isStatic) ? 4 : 8));
			return (from f in type.GetFields(bindingAttr)
				where string.Equals(f.Name, name, StringComparison.Ordinal) && f.IsStatic == isStatic
				orderby InheritanceDistance(type, f.DeclaringType), f.IsPublic descending
				select f).FirstOrDefault();
		}

		private static int InheritanceDistance(Type type, Type declaringType)
		{
			int num = 0;
			Type type2 = type;
			while (type2 != null)
			{
				if (type2 == declaringType)
				{
					return num;
				}
				type2 = type2.BaseType;
				num++;
			}
			return int.MaxValue;
		}

		public static IEnumerable<object> Enumerate(object value)
		{
			if (!(value is IEnumerable enumerable))
			{
				yield break;
			}
			IEnumerator enumerator = enumerable.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					yield return enumerator.Current;
				}
			}
			finally
			{
				IDisposable disposable2;
				IDisposable disposable = (disposable2 = enumerator as IDisposable);
				if (disposable2 != null)
				{
					disposable.Dispose();
				}
			}
		}

		internal static bool ToBool(object value)
		{
			return value is bool && (bool)value;
		}

		private static string SplitPascal(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return value;
			}
			StringBuilder stringBuilder = new StringBuilder();
			char c = '\0';
			string text = value.Replace('_', ' ');
			foreach (char c2 in text)
			{
				if (char.IsUpper(c2) && c != 0 && !char.IsWhiteSpace(c) && !char.IsUpper(c))
				{
					stringBuilder.Append(' ');
				}
				stringBuilder.Append(c2);
				c = c2;
			}
			return stringBuilder.ToString().Trim();
		}

		public static void EnforceNewHeroLevelOne(Hero hero)
		{
			if (hero == null)
			{
				throw new InvalidOperationException("New shared-character creation returned no hero to normalize.");
			}
			int level = hero.Level;
			HeroDeveloper heroDeveloper = hero.HeroDeveloper;
			if (heroDeveloper == null)
			{
				throw new InvalidOperationException("New shared character has no HeroDeveloper; level-one initialization cannot be guaranteed.");
			}
			hero.Level = 1;
			heroDeveloper.SetInitialLevel(1);
			if (hero.Level != 1)
			{
				throw new InvalidOperationException("Failed to enforce level 1 for a newly created shared character.");
			}
			Log.Info("Enforced new shared-character level invariant. Hero=" + IdOf(hero) + "; inheritedLevel=" + level + "; finalLevel=" + hero.Level + ".");
		}
	}
}
