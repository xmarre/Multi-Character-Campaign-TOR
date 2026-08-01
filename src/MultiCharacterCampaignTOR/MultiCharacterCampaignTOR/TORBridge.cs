// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MultiCharacterCampaignTOR.WaywatcherFix;
using TaleWorlds.CampaignSystem;

namespace MultiCharacterCampaignTOR
{
	internal static class TORBridge
	{
		private sealed class CareerIdComparer : IEqualityComparer<object>
		{
			bool IEqualityComparer<object>.Equals(object x, object y)
			{
				return SameCareer(x, y);
			}

			int IEqualityComparer<object>.GetHashCode(object obj)
			{
				string text = Reflection.IdOf(obj);
				return (!string.IsNullOrEmpty(text)) ? StringComparer.Ordinal.GetHashCode(text) : 0;
			}
		}

		private static Type ExtensionsType => Type.GetType("TOR_Core.Extensions.HeroExtensions, TOR_Core");

		private static Type CareersType => Type.GetType("TOR_Core.CharacterDevelopment.TORCareers, TOR_Core");

		public static List<object> GetAllCareers()
		{
			List<object> list = new List<object>();
			try
			{
				Type careersType = CareersType;
				if (careersType == null)
				{
					return list;
				}
				PropertyInfo property = careersType.GetProperty("All", BindingFlags.Static | BindingFlags.Public);
				object value = ((!(property == null)) ? property.GetValue(null, null) : null);
				foreach (object item in Reflection.Enumerate(value))
				{
					if (item != null && !list.Contains(item))
					{
						list.Add(item);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Could not enumerate all TOR careers", ex);
			}
			return list.OrderBy(Reflection.DisplayName).ToList();
		}

		public static List<object> GetEligibleCareers(Hero hero)
		{
			List<object> cultureCareerPool = GetCultureCareerPool(hero, includeSetupCareers: true);
			LogCareerCandidates("new-character", hero, cultureCareerPool);
			return cultureCareerPool;
		}

		public static List<object> GetCompatibleCareers(Hero hero)
		{
			if (hero == null)
			{
				return new List<object>();
			}
			List<object> list = new List<object>();
			string role = GetHeroRoleText(hero);
			string cultureId = GetCultureId(hero);
			if (ContainsRole(role, "waywatcher"))
			{
				AddStaticCareer(list, "Waywatcher");
			}
			if (ContainsRole(role, "glade captain"))
			{
				AddStaticCareer(list, "Warden");
			}
			if (ContainsRole(role, "bright wizard") || ContainsRole(role, "gold wizard") || ContainsRole(role, "magister"))
			{
				AddStaticCareer(list, "ImperialMagister");
			}
			if (ContainsRole(role, "guardian mage") || ContainsRole(role, "grey lord"))
			{
				if (string.Equals(cultureId, "eonir", StringComparison.OrdinalIgnoreCase))
				{
					AddStaticCareer(list, "GreyLord");
				}
				else if (string.Equals(cultureId, "battania", StringComparison.OrdinalIgnoreCase))
				{
					AddStaticCareer(list, "Spellsinger");
				}
				else
				{
					AddStaticCareer(list, "ImperialMagister");
				}
			}
			if (ContainsRole(role, "spellsinger"))
			{
				AddStaticCareer(list, "Spellsinger");
			}
			if (ContainsRole(role, "witch hunter"))
			{
				AddStaticCareer(list, "WitchHunter");
			}
			if (ContainsRole(role, "warrior priest") || ContainsRole(role, "priest of sigmar"))
			{
				AddStaticCareer(list, "WarriorPriest");
			}
			if (ContainsRole(role, "ulric") && ContainsRole(role, "priest"))
			{
				AddStaticCareer(list, "WarriorPriestUlric");
			}
			if (ContainsRole(role, "grail damsel") || ContainsRole(role, "damsel"))
			{
				AddStaticCareer(list, "GrailDamsel");
			}
			if (ContainsRole(role, "grail knight"))
			{
				AddStaticCareer(list, "GrailKnight");
			}
			if (ContainsRole(role, "black grail"))
			{
				AddStaticCareer(list, "BlackGrailKnight");
			}
			if (ContainsRole(role, "blood knight"))
			{
				AddStaticCareer(list, "BloodKnight");
			}
			if (ContainsRole(role, "necrarch"))
			{
				AddStaticCareer(list, "Necrarch");
			}
			if (ContainsRole(role, "necromancer"))
			{
				AddStaticCareer(list, "Necromancer");
			}
			if (ContainsRole(role, "vampire"))
			{
				AddStaticCareer(list, "MinorVampire");
			}
			if (ContainsRole(role, "rune") && (ContainsRole(role, "smith") || ContainsRole(role, "lord")))
			{
				AddStaticCareer(list, "Runelord");
			}
			if (ContainsRole(role, "ironbreaker") || ContainsRole(role, "shield breaker"))
			{
				AddStaticCareer(list, "Ironbreaker");
			}
			if (ContainsRole(role, "slayer"))
			{
				AddStaticCareer(list, "Slayer");
			}
			if (ContainsRole(role, "orc shaman") || ContainsRole(role, "shaman"))
			{
				AddStaticCareer(list, "OrcShaman");
			}
			if (ContainsRole(role, "orc boss") || ContainsRole(role, "warboss"))
			{
				AddStaticCareer(list, "OrcBoss");
			}
			if (ContainsRole(role, "hunter") && (string.Equals(cultureId, "battania", StringComparison.OrdinalIgnoreCase) || string.Equals(cultureId, "eonir", StringComparison.OrdinalIgnoreCase)))
			{
				AddStaticCareer(list, "Waywatcher");
				if (string.Equals(cultureId, "battania", StringComparison.OrdinalIgnoreCase))
				{
					AddStaticCareer(list, "Warden");
				}
			}
			if (HasAttribute(hero, "PriestSigmar"))
			{
				AddStaticCareer(list, "WarriorPriest");
			}
			if (HasAttribute(hero, "PriestUlric"))
			{
				AddStaticCareer(list, "WarriorPriestUlric");
			}
			if (HasAttribute(hero, "PriestLady"))
			{
				AddStaticCareer(list, "GrailDamsel");
			}
			if (HasAttribute(hero, "RuneCraft"))
			{
				AddStaticCareer(list, "Runelord");
			}
			if (HasAttribute(hero, "Necromancer"))
			{
				AddStaticCareer(list, "Necromancer");
			}
			if (HasAttribute(hero, "Vampire"))
			{
				if (string.Equals(cultureId, "blooddragons", StringComparison.OrdinalIgnoreCase))
				{
					AddStaticCareer(list, "BloodKnight");
				}
				else
				{
					AddStaticCareer(list, "MinorVampire");
				}
			}
			if (IsSpellCaster(hero))
			{
				if (string.Equals(cultureId, "empire", StringComparison.OrdinalIgnoreCase))
				{
					AddStaticCareer(list, "ImperialMagister");
				}
				else if (string.Equals(cultureId, "battania", StringComparison.OrdinalIgnoreCase))
				{
					AddStaticCareer(list, "Spellsinger");
				}
				else if (string.Equals(cultureId, "eonir", StringComparison.OrdinalIgnoreCase))
				{
					AddStaticCareer(list, "GreyLord");
				}
				else if (string.Equals(cultureId, "aserai", StringComparison.OrdinalIgnoreCase) || string.Equals(cultureId, "greenskin_bandit", StringComparison.OrdinalIgnoreCase) || string.Equals(cultureId, "looters", StringComparison.OrdinalIgnoreCase))
				{
					AddStaticCareer(list, "OrcShaman");
				}
			}
			List<object> list2 = list.Where((object c) => IsSafeForExistingCompanion(hero, c, role)).Distinct(new CareerIdComparer()).ToList();
			if (list2.Count > 0)
			{
				LogCareerCandidates("companion-role", hero, list2);
				return list2.OrderBy(Reflection.DisplayName).ToList();
			}
			List<object> list3 = (from c in GetCultureCareerPool(hero, includeSetupCareers: false)
				where IsSafeForExistingCompanion(hero, c, role)
				select c).Distinct(new CareerIdComparer()).OrderBy(Reflection.DisplayName).ToList();
			LogCareerCandidates("companion-culture-fallback", hero, list3);
			return list3;
		}

		private static List<object> GetCultureCareerPool(Hero hero, bool includeSetupCareers)
		{
			List<object> list = new List<object>();
			string cultureId = GetCultureId(hero);
			if (string.Equals(cultureId, "empire", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "Mercenary");
				AddStaticCareer(list, "KnightOldWorld");
				AddStaticCareer(list, "WitchHunter");
				if (includeSetupCareers || IsSpellCaster(hero))
				{
					AddStaticCareer(list, "ImperialMagister");
				}
				if (includeSetupCareers || HasAttribute(hero, "PriestSigmar"))
				{
					AddStaticCareer(list, "WarriorPriest");
				}
				if (includeSetupCareers || HasAttribute(hero, "PriestUlric"))
				{
					AddStaticCareer(list, "WarriorPriestUlric");
				}
			}
			else if (string.Equals(cultureId, "battania", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "Warden");
				AddStaticCareer(list, "Waywatcher");
				if (includeSetupCareers || IsSpellCaster(hero))
				{
					AddStaticCareer(list, "Spellsinger");
				}
			}
			else if (string.Equals(cultureId, "eonir", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "Mercenary");
				AddStaticCareer(list, "Waywatcher");
				if (includeSetupCareers || IsSpellCaster(hero))
				{
					AddStaticCareer(list, "GreyLord");
				}
			}
			else if (string.Equals(cultureId, "vlandia", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "Mercenary");
				AddStaticCareer(list, "GrailKnight");
				if (includeSetupCareers || IsSpellCaster(hero) || HasAttribute(hero, "PriestLady"))
				{
					AddStaticCareer(list, "GrailDamsel");
				}
			}
			else if (string.Equals(cultureId, "sturgia", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "Mercenary");
				AddStaticCareer(list, "Ironbreaker");
				AddStaticCareer(list, "Slayer");
				if (includeSetupCareers || HasAttribute(hero, "RuneCraft"))
				{
					AddStaticCareer(list, "Runelord");
				}
			}
			else if (string.Equals(cultureId, "aserai", StringComparison.OrdinalIgnoreCase) || string.Equals(cultureId, "greenskin_bandit", StringComparison.OrdinalIgnoreCase) || string.Equals(cultureId, "looters", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "OrcBoss");
				if (includeSetupCareers || IsSpellCaster(hero))
				{
					AddStaticCareer(list, "OrcShaman");
				}
			}
			else if (string.Equals(cultureId, "blooddragons", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "BloodKnight");
			}
			else if (string.Equals(cultureId, "khuzait", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "Mercenary");
				if (includeSetupCareers || HasAttribute(hero, "Vampire"))
				{
					AddStaticCareer(list, "MinorVampire");
				}
				if (includeSetupCareers || HasAttribute(hero, "Necromancer"))
				{
					AddStaticCareer(list, "Necromancer");
				}
			}
			else if (string.Equals(cultureId, "mousillon", StringComparison.OrdinalIgnoreCase))
			{
				AddStaticCareer(list, "Mercenary");
				AddStaticCareer(list, "BlackGrailKnight");
				if (includeSetupCareers || HasAttribute(hero, "Vampire"))
				{
					AddStaticCareer(list, "MinorVampire");
				}
				if (includeSetupCareers || HasAttribute(hero, "Necromancer"))
				{
					AddStaticCareer(list, "Necromancer");
				}
			}
			else
			{
				AddStaticCareer(list, "Mercenary");
			}
			return list.Distinct(new CareerIdComparer()).OrderBy(Reflection.DisplayName).ToList();
		}

		private static void AddStaticCareer(List<object> output, string propertyName)
		{
			if (output != null && !string.IsNullOrEmpty(propertyName))
			{
				object career = GetStaticCareer(propertyName);
				if (career == null)
				{
					Log.Warning("TOR career property could not be resolved: " + propertyName + ".");
				}
				else if (!output.Any((object c) => SameCareer(c, career)))
				{
					output.Add(career);
				}
			}
		}

		private static string GetHeroRoleText(Hero hero)
		{
			if (hero == null)
			{
				return string.Empty;
			}
			List<string> list = new List<string>();
			list.Add(Reflection.DisplayName(hero));
			object characterObject = hero.CharacterObject;
			list.Add(Reflection.DisplayName(characterObject));
			list.Add(Reflection.IdOf(characterObject));
			object member = Reflection.GetMember(hero, "Template");
			list.Add(Reflection.DisplayName(member));
			list.Add(Reflection.IdOf(member));
			return string.Join(" ", list.Where((string v) => !string.IsNullOrEmpty(v)).ToArray()).ToLowerInvariant();
		}

		private static bool ContainsRole(string role, string token)
		{
			return !string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(token) && role.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsSafeForExistingCompanion(Hero hero, object career, string role)
		{
			if (hero == null || career == null)
			{
				return false;
			}
			string a = Reflection.IdOf(career);
			string cultureId = GetCultureId(hero);
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("ImperialMagister")), StringComparison.Ordinal))
			{
				return IsSpellCaster(hero) || ContainsRole(role, "wizard") || ContainsRole(role, "magister");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("Spellsinger")), StringComparison.Ordinal))
			{
				return IsSpellCaster(hero) || ContainsRole(role, "spellsinger") || ContainsRole(role, "mage");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("GreyLord")), StringComparison.Ordinal))
			{
				return IsSpellCaster(hero) || ContainsRole(role, "grey lord") || ContainsRole(role, "guardian mage");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("OrcShaman")), StringComparison.Ordinal))
			{
				return IsSpellCaster(hero) || ContainsRole(role, "shaman");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("WarriorPriest")), StringComparison.Ordinal))
			{
				return HasAttribute(hero, "PriestSigmar") || ContainsRole(role, "warrior priest") || ContainsRole(role, "priest of sigmar");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("WarriorPriestUlric")), StringComparison.Ordinal))
			{
				return HasAttribute(hero, "PriestUlric") || (ContainsRole(role, "ulric") && ContainsRole(role, "priest"));
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("GrailDamsel")), StringComparison.Ordinal))
			{
				return HasAttribute(hero, "PriestLady") || IsSpellCaster(hero) || ContainsRole(role, "damsel");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("Runelord")), StringComparison.Ordinal))
			{
				return HasAttribute(hero, "RuneCraft") || ContainsRole(role, "rune");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("Necromancer")), StringComparison.Ordinal))
			{
				return HasAttribute(hero, "Necromancer") || ContainsRole(role, "necromancer");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("Necrarch")), StringComparison.Ordinal))
			{
				return ContainsRole(role, "necrarch");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("MinorVampire")), StringComparison.Ordinal))
			{
				return HasAttribute(hero, "Vampire") || ContainsRole(role, "vampire");
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("BloodKnight")), StringComparison.Ordinal))
			{
				return HasAttribute(hero, "Vampire") || ContainsRole(role, "blood knight") || string.Equals(cultureId, "blooddragons", StringComparison.OrdinalIgnoreCase);
			}
			if (string.Equals(a, Reflection.IdOf(GetStaticCareer("BlackGrailKnight")), StringComparison.Ordinal))
			{
				return ContainsRole(role, "black grail") || (string.Equals(cultureId, "mousillon", StringComparison.OrdinalIgnoreCase) && ContainsRole(role, "knight"));
			}
			return true;
		}

		private static void LogCareerCandidates(string source, Hero hero, IEnumerable<object> careers)
		{
			List<object> list = ((careers != null) ? careers.Where((object c) => c != null).ToList() : new List<object>());
			Log.Info("TOR career candidates source=" + source + "; hero=" + Reflection.IdOf(hero) + "; role=" + GetHeroRoleText(hero) + "; archetype=" + DescribeArchetype(hero) + "; count=" + list.Count + "; careers=" + string.Join(",", list.Select(Reflection.IdOf).ToArray()) + ".");
		}

		public static bool IsCareerCompatibleWithExistingArchetype(Hero hero, object career)
		{
			if (hero == null || career == null)
			{
				return false;
			}
			return GetCompatibleCareers(hero).Any((object c) => SameCareer(c, career));
		}

		public static string DescribeArchetype(Hero hero)
		{
			if (hero == null)
			{
				return "unavailable";
			}
			List<string> list = new List<string>();
			string cultureId = GetCultureId(hero);
			if (!string.IsNullOrEmpty(cultureId))
			{
				list.Add("culture=" + cultureId);
			}
			if (IsSpellCaster(hero))
			{
				list.Add("spellcaster");
			}
			string[] array = new string[7] { "PriestSigmar", "PriestUlric", "PriestLady", "Priest", "RuneCraft", "Necromancer", "Vampire" };
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (HasAttribute(hero, text))
				{
					list.Add(text);
				}
			}
			return (list.Count != 0) ? string.Join(", ", list.ToArray()) : "ordinary companion";
		}

		public static string GetCareerId(Hero hero)
		{
			if (hero == null)
			{
				return string.Empty;
			}
			try
			{
				object extendedInfo = GetExtendedInfo(hero);
				string text = Convert.ToString(Reflection.GetMember(extendedInfo, "CareerID"));
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text;
				}
				object career = GetCareer(hero);
				string text2 = Reflection.IdOf(career);
				if (!string.IsNullOrEmpty(text2))
				{
					return text2;
				}
				Type extensionsType = ExtensionsType;
				MethodInfo methodInfo = ((!(extensionsType == null)) ? extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "HasAnyCareer" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(Hero))) : null);
				return (!(methodInfo != null) || !Convert.ToBoolean(methodInfo.Invoke(null, new object[1] { hero }))) ? string.Empty : "<assigned TOR career>";
			}
			catch (Exception ex)
			{
				Log.Error("Could not read TOR career identifier for hero=" + Reflection.IdOf(hero), ex);
				return string.Empty;
			}
		}

		public static object GetCareer(Hero hero)
		{
			if (hero == null)
			{
				return null;
			}
			try
			{
				Type extensionsType = ExtensionsType;
				if (extensionsType != null)
				{
					MethodInfo methodInfo = extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "GetCareer" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(Hero)));
					if (methodInfo != null)
					{
						object obj = methodInfo.Invoke(null, new object[1] { hero });
						if (obj != null)
						{
							return obj;
						}
					}
				}
				object extendedInfo = GetExtendedInfo(hero);
				string storedCareerId = Convert.ToString(Reflection.GetMember(extendedInfo, "CareerID"));
				if (string.IsNullOrEmpty(storedCareerId))
				{
					return null;
				}
				return GetAllCareers().FirstOrDefault((object c) => string.Equals(Reflection.IdOf(c), storedCareerId, StringComparison.Ordinal));
			}
			catch (Exception ex)
			{
				Log.Error("Could not read TOR career for hero=" + Reflection.IdOf(hero), ex);
				return null;
			}
		}

		public static void AddCareer(Hero hero, object career)
		{
			if (hero == null || career == null)
			{
				return;
			}
			if (Hero.MainHero != hero)
			{
				throw new InvalidOperationException("TOR career assignment requires the selected hero to be active.");
			}
			if (!IsCareerCompatibleWithExistingArchetype(hero, career))
			{
				throw new InvalidOperationException("The selected career is incompatible with this hero's existing TOR archetype: " + DescribeArchetype(hero));
			}
			EnsureExtendedInfo(hero);
			object extendedInfo = GetExtendedInfo(hero);
			if (extendedInfo == null)
			{
				throw new InvalidOperationException("TOR HeroExtendedInfo is unavailable for " + Reflection.IdOf(hero) + ".");
			}
			object member = Reflection.GetMember(career, "RootNode");
			string value = Reflection.IdOf(member);
			if (string.IsNullOrEmpty(value))
			{
				throw new InvalidOperationException("TOR career root node is unavailable for " + Reflection.IdOf(career) + ".");
			}
			object member2 = Reflection.GetMember(extendedInfo, "CareerChoices");
			if (!(member2 is IList list))
			{
				throw new InvalidOperationException("TOR CareerChoices collection is unavailable for " + Reflection.IdOf(hero) + ".");
			}
			string stringMember = GetStringMember(extendedInfo, "CareerID");
			List<object> list2 = new List<object>();
			foreach (object item in list)
			{
				list2.Add(item);
			}
			try
			{
				SetStringMember(extendedInfo, "CareerID", Reflection.IdOf(career));
				list.Clear();
				list.Add(value);
			}
			catch
			{
				try
				{
					SetStringMember(extendedInfo, "CareerID", stringMember);
					list.Clear();
					foreach (object item2 in list2)
					{
						list.Add(item2);
					}
				}
				catch (Exception ex)
				{
					Log.Error("Existing-companion career-state rollback failed for hero=" + Reflection.IdOf(hero), ex);
				}
				throw;
			}
			RemoveAttribute(hero, "CareerTier1");
			RemoveAttribute(hero, "CareerTier2");
			RemoveAttribute(hero, "CareerTier3");
			RefreshAfterSwitch();
			Log.Info("Assigned existing companion career without rerunning InitialCareerSetup. Hero=" + Reflection.IdOf(hero) + "; career=" + Reflection.IdOf(career) + "; preserved existing spells, lores, attributes, skills, and resources. " + GetCareerProgressSummary(hero));
		}

		public static void AddCareerForNewHero(Hero hero, object career)
		{
			if (hero != null && career != null)
			{
				if (Hero.MainHero != hero)
				{
					throw new InvalidOperationException("TOR career setup requires the selected hero to be active.");
				}
				EnsureExtendedInfo(hero);
				Type extensionsType = ExtensionsType;
				if (extensionsType == null)
				{
					throw new TypeLoadException("TOR_Core.Extensions.HeroExtensions");
				}
				MethodInfo methodInfo = extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "AddCareer" && m.GetParameters().Length == 2);
				if (methodInfo == null)
				{
					throw new MissingMethodException("HeroExtensions.AddCareer");
				}
				methodInfo.Invoke(null, new object[2] { hero, career });
				Log.Info("Initialized new-hero TOR career through HeroExtensions.AddCareer. Hero=" + Reflection.IdOf(hero) + "; career=" + Reflection.IdOf(career) + ". " + GetCareerProgressSummary(hero));
			}
		}

		private static string GetStringMember(object instance, string name)
		{
			if (instance == null)
			{
				return string.Empty;
			}
			Type type = instance.GetType();
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null && field.FieldType == typeof(string))
			{
				return Convert.ToString(field.GetValue(instance));
			}
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo methodInfo = ((!(property == null)) ? property.GetGetMethod(nonPublic: true) : null);
			if (methodInfo != null && property.PropertyType == typeof(string))
			{
				return Convert.ToString(methodInfo.Invoke(instance, null));
			}
			throw new MissingMemberException(type.FullName, name);
		}

		private static void SetStringMember(object instance, string name, string value)
		{
			if (instance == null)
			{
				throw new ArgumentNullException("instance");
			}
			Type type = instance.GetType();
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null && field.FieldType == typeof(string))
			{
				field.SetValue(instance, value ?? string.Empty);
				return;
			}
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo methodInfo = ((!(property == null)) ? property.GetSetMethod(nonPublic: true) : null);
			if (methodInfo != null && property.PropertyType == typeof(string))
			{
				methodInfo.Invoke(instance, new object[1] { value ?? string.Empty });
				return;
			}
			throw new MissingMemberException(type.FullName, name);
		}

		public static void ClearCareer(Hero hero)
		{
			if (hero == null)
			{
				return;
			}
			if (Hero.MainHero != hero)
			{
				throw new InvalidOperationException("TOR career repair requires the selected hero to be active.");
			}
			object extendedInfo = GetExtendedInfo(hero);
			if (extendedInfo == null)
			{
				return;
			}
			object member = Reflection.GetMember(extendedInfo, "CareerChoices");
			if (!(member is IList list))
			{
				throw new InvalidOperationException("TOR CareerChoices collection is unavailable for " + Reflection.IdOf(hero) + ".");
			}
			string stringMember = GetStringMember(extendedInfo, "CareerID");
			List<object> list2 = new List<object>();
			foreach (object item in list)
			{
				list2.Add(item);
			}
			try
			{
				SetStringMember(extendedInfo, "CareerID", string.Empty);
				list.Clear();
			}
			catch
			{
				try
				{
					SetStringMember(extendedInfo, "CareerID", stringMember);
					list.Clear();
					foreach (object item2 in list2)
					{
						list.Add(item2);
					}
				}
				catch (Exception ex)
				{
					Log.Error("Career-clear rollback failed for hero=" + Reflection.IdOf(hero), ex);
				}
				throw;
			}
			RemoveAttribute(hero, "CareerTier1");
			RemoveAttribute(hero, "CareerTier2");
			RemoveAttribute(hero, "CareerTier3");
			Log.Info("Cleared TOR CareerID and CareerChoices for hero=" + Reflection.IdOf(hero) + ". Existing non-career extended info was retained.");
		}

		public static int GetCareerChoiceCount(Hero hero)
		{
			try
			{
				object extendedInfo = GetExtendedInfo(hero);
				object member = Reflection.GetMember(extendedInfo, "CareerChoices");
				return Reflection.Enumerate(member).Count();
			}
			catch (Exception ex)
			{
				Log.Error("Could not count TOR career choices for hero=" + Reflection.IdOf(hero), ex);
				return 0;
			}
		}

		public static string GetCareerProgressSummary(Hero hero)
		{
			if (hero == null)
			{
				return "Career progression unavailable.";
			}
			try
			{
				int careerChoiceCount = GetCareerChoiceCount(hero);
				int num = Math.Max(0, careerChoiceCount - 1);
				int maximumCareerPoints = GetMaximumCareerPoints();
				int num2 = Math.Min(hero.Level, maximumCareerPoints);
				int num3 = Math.Max(0, num2 - num);
				return "Career progression for " + Reflection.IdOf(hero) + ": heroLevel=" + hero.Level + ", maximumCareerPoints=" + maximumCareerPoints + ", selectedChoicesIncludingRoot=" + careerChoiceCount + ", spentPoints=" + num + ", freePoints=" + num3 + ".";
			}
			catch (Exception ex)
			{
				Log.Error("Could not summarize TOR career progression for hero=" + Reflection.IdOf(hero), ex);
				return "Career progression unavailable.";
			}
		}

		public static bool CanOpenBattlePrayers(Hero hero, out string reason)
		{
			reason = string.Empty;
			try
			{
				object career = GetCareer(hero);
				if (career == null)
				{
					reason = "The active character has no TOR career.";
					return false;
				}
				Type type = Type.GetType("TOR_Core.CharacterDevelopment.CareerSystem.CareerHelper, TOR_Core");
				if (type == null)
				{
					reason = "TOR CareerHelper is unavailable.";
					return false;
				}
				MethodInfo method = type.GetMethod("IsPriestCareer", BindingFlags.Static | BindingFlags.Public);
				if (method == null || !Convert.ToBoolean(method.Invoke(null, new object[1] { career })))
				{
					reason = "The active character's career is not a priest career.";
					return false;
				}
				MethodInfo method2 = type.GetMethod("GetPriestPrayerList", BindingFlags.Static | BindingFlags.Public);
				object value = ((!(method2 == null)) ? method2.Invoke(null, new object[1] { hero }) : null);
				if (!Reflection.Enumerate(value).Any())
				{
					reason = "The priest career is missing its required TOR priest attribute and prayer list. The career assignment is incompatible with this companion's existing archetype.";
					return false;
				}
				MethodInfo method3 = type.GetMethod("GetGodCareerIsDevotedTo", BindingFlags.Static | BindingFlags.Public);
				string text = Convert.ToString((!(method3 == null)) ? method3.Invoke(null, new object[1] { career }) : null);
				if (string.IsNullOrEmpty(text) || text == "-")
				{
					reason = "TOR could not resolve the religion attached to this priest career.";
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("Battle-prayer compatibility validation failed", ex);
				reason = "TOR prayer state validation failed. See MultiCharacterCampaignTOR.log.";
				return false;
			}
		}

		private static int GetMaximumCareerPoints()
		{
			try
			{
				Type type = Type.GetType("TOR_Core.Utilities.TORConfig, TOR_Core");
				PropertyInfo propertyInfo = ((!(type == null)) ? type.GetProperty("MaximumNumberOfCareerPerkPoints", BindingFlags.Static | BindingFlags.Public) : null);
				return (!(propertyInfo == null)) ? Convert.ToInt32(propertyInfo.GetValue(null, null)) : 10;
			}
			catch
			{
				return 10;
			}
		}

		private static void AddIfEligible(List<object> output, List<object> eligible, object career)
		{
			if (career != null)
			{
				object match = eligible.FirstOrDefault((object c) => SameCareer(c, career));
				if (match != null && !output.Any((object c) => SameCareer(c, match)))
				{
					output.Add(match);
				}
			}
		}

		private static bool SameCareer(object left, object right)
		{
			if (object.ReferenceEquals(left, right))
			{
				return true;
			}
			if (left == null || right == null)
			{
				return false;
			}
			string text = Reflection.IdOf(left);
			string b = Reflection.IdOf(right);
			return !string.IsNullOrEmpty(text) && string.Equals(text, b, StringComparison.Ordinal);
		}

		private static object GetStaticCareer(string propertyName)
		{
			Type careersType = CareersType;
			PropertyInfo propertyInfo = ((!(careersType == null)) ? careersType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public) : null);
			return (!(propertyInfo == null)) ? propertyInfo.GetValue(null, null) : null;
		}

		private static bool RequiresPreExistingSpecialistState(object career)
		{
			string[] array = new string[13]
			{
				"WarriorPriest", "WarriorPriestUlric", "GrailDamsel", "ImperialMagister", "Spellsinger", "GreyLord", "Runelord", "OrcShaman", "Necromancer", "Necrarch",
				"MinorVampire", "BloodKnight", "BlackGrailKnight"
			};
			string[] array2 = array;
			foreach (string propertyName in array2)
			{
				if (SameCareer(career, GetStaticCareer(propertyName)))
				{
					return true;
				}
			}
			return false;
		}

		private static string GetCultureId(Hero hero)
		{
			object value = hero?.CharacterObject;
			object member = Reflection.GetMember(value, "Culture");
			return Reflection.IdOf(member);
		}

		private static bool IsSpellCaster(Hero hero)
		{
			try
			{
				Type extensionsType = ExtensionsType;
				MethodInfo methodInfo = ((!(extensionsType == null)) ? extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "IsSpellCaster" && m.GetParameters().Length == 1) : null);
				return methodInfo != null && Convert.ToBoolean(methodInfo.Invoke(null, new object[1] { hero }));
			}
			catch (Exception ex)
			{
				Log.Error("Could not determine TOR spellcaster state for hero=" + Reflection.IdOf(hero), ex);
				return false;
			}
		}

		private static bool HasAttribute(Hero hero, string attribute)
		{
			try
			{
				Type extensionsType = ExtensionsType;
				MethodInfo methodInfo = ((!(extensionsType == null)) ? extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "HasAttribute" && m.GetParameters().Length == 2) : null);
				return methodInfo != null && Convert.ToBoolean(methodInfo.Invoke(null, new object[2] { hero, attribute }));
			}
			catch (Exception ex)
			{
				Log.Error("Could not determine TOR attribute " + attribute + " for hero=" + Reflection.IdOf(hero), ex);
				return false;
			}
		}

		private static void RemoveAttribute(Hero hero, string attribute)
		{
			try
			{
				Type extensionsType = ExtensionsType;
				MethodInfo methodInfo = ((!(extensionsType == null)) ? extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "RemoveAttribute" && m.GetParameters().Length == 2) : null);
				if (methodInfo != null)
				{
					methodInfo.Invoke(null, new object[2] { hero, attribute });
				}
			}
			catch (Exception ex)
			{
				Log.Error("Could not remove TOR attribute " + attribute + " from hero=" + Reflection.IdOf(hero), ex);
			}
		}

		private static object GetExtendedInfo(Hero hero)
		{
			if (hero == null)
			{
				return null;
			}
			Type type = Type.GetType("TOR_Core.Extensions.ExtendedInfoSystem.ExtendedInfoManager, TOR_Core");
			if (type == null)
			{
				return null;
			}
			object value = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
			if (value == null)
			{
				return null;
			}
			MethodInfo method = type.GetMethod("GetHeroInfoFor", BindingFlags.Instance | BindingFlags.Public);
			return (!(method == null)) ? method.Invoke(value, new object[1] { Reflection.IdOf(hero) }) : null;
		}

		private static void EnsureExtendedInfo(Hero hero)
		{
			Type type = Type.GetType("TOR_Core.Extensions.ExtendedInfoSystem.ExtendedInfoManager, TOR_Core");
			if (type == null)
			{
				throw new TypeLoadException("TOR ExtendedInfoManager");
			}
			object value = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
			if (value == null)
			{
				throw new InvalidOperationException("TOR ExtendedInfoManager.Instance is null.");
			}
			MethodInfo method = type.GetMethod("GetHeroInfoFor", BindingFlags.Instance | BindingFlags.Public);
			object obj = ((!(method == null)) ? method.Invoke(value, new object[1] { Reflection.IdOf(hero) }) : null);
			if (obj == null)
			{
				MethodInfo method2 = type.GetMethod("OnHeroCreated", BindingFlags.Instance | BindingFlags.NonPublic);
				if (method2 == null)
				{
					throw new MissingMethodException("ExtendedInfoManager.OnHeroCreated");
				}
				method2.Invoke(value, new object[2] { hero, false });
				obj = ((!(method == null)) ? method.Invoke(value, new object[1] { Reflection.IdOf(hero) }) : null);
				if (obj == null)
				{
					throw new InvalidOperationException("TOR did not create HeroExtendedInfo for the new character.");
				}
				Log.Info("Created missing TOR HeroExtendedInfo for " + Reflection.IdOf(hero) + ".");
			}
		}

		public static void RefreshAfterSwitch()
		{
			RuntimeRepair.RefreshAfterSwitch();
		}
	}
}
