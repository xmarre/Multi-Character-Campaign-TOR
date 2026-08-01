// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace MultiCharacterCampaignTOR
{
	internal static class PartyScreenSelectionBridge
	{
		private static bool _selectionPending = true;

		private static object _currentProbeDataSource;

		private static Type _lastDumpedType;

		private static DateTime _nextProbeUtc = DateTime.MinValue;

		private static int _probeAttempts;

		private const int MaximumProbeAttemptsPerOpenScreen = 40;

		public static void RequestSelection(string reason)
		{
			_selectionPending = true;
			_currentProbeDataSource = null;
			_probeAttempts = 0;
			_nextProbeUtc = DateTime.MinValue;
			Log.Info("Party-screen active-character selection requested. Reason=" + (reason ?? string.Empty) + ".");
		}

		public static void OnApplicationTick()
		{
			if (!_selectionPending || DateTime.UtcNow < _nextProbeUtc)
			{
				return;
			}
			_nextProbeUtc = DateTime.UtcNow.AddMilliseconds(250.0);
			try
			{
				if (Campaign.Current == null || Hero.MainHero == null || Hero.MainHero.CharacterObject == null)
				{
					return;
				}
				object obj = TryGetPartyDataSource();
				if (obj == null)
				{
					return;
				}
				if (!object.ReferenceEquals(obj, _currentProbeDataSource))
				{
					_currentProbeDataSource = obj;
					_probeAttempts = 0;
					Log.Info("Party screen detected; beginning active-character presentation rebinding for hero=" + Reflection.IdOf(Hero.MainHero) + ".");
				}
				_probeAttempts++;
				object obj2 = FindActiveHeroEntry(obj, Hero.MainHero);
				if (obj2 == null)
				{
					if (_probeAttempts == 1 || _probeAttempts == 10 || _probeAttempts == 40)
					{
						Log.Warning("Party screen was open, but no PartyCharacterVM entry matched active hero=" + Reflection.IdOf(Hero.MainHero) + "; attempt=" + _probeAttempts + ".");
					}
					StopAfterMaximumAttemptsIfNeeded();
					return;
				}
				if (TrySelectEntry(obj, obj2, out var route))
				{
					_selectionPending = false;
					Log.Info("Party screen now presents active hero=" + Reflection.IdOf(Hero.MainHero) + " through " + route + "; attempt=" + _probeAttempts + ".");
					return;
				}
				DumpSelectionSurfaceOnce(obj.GetType());
				if (_probeAttempts == 1 || _probeAttempts == 10 || _probeAttempts == 40)
				{
					Log.Warning("Party screen active-character entry was found, but no safe selection member was available. Active hero=" + Reflection.IdOf(Hero.MainHero) + "; attempt=" + _probeAttempts + ".");
				}
				StopAfterMaximumAttemptsIfNeeded();
			}
			catch (Exception ex)
			{
				Log.Error("Party-screen active-character selection bridge failed safely", ex);
			}
		}

		private static void StopAfterMaximumAttemptsIfNeeded()
		{
			if (_probeAttempts >= 40)
			{
				_selectionPending = false;
				Log.Warning("Stopped party-screen active-character presentation rebinding after " + _probeAttempts + " attempts. The core player and party identity remain switched; send the log so the exact PartyVM selection surface can be targeted.");
			}
		}

		private static object TryGetPartyDataSource()
		{
			Type type = Type.GetType("TaleWorlds.ScreenSystem.ScreenManager, TaleWorlds.ScreenSystem");
			if (type == null)
			{
				return null;
			}
			PropertyInfo property = type.GetProperty("FocusedLayer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			object obj = ((!(property == null)) ? property.GetValue(null, null) : null);
			if (obj == null)
			{
				return null;
			}
			MethodInfo methodInfo = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "GetMovieIdentifier" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
			if (methodInfo == null)
			{
				return null;
			}
			object obj2 = methodInfo.Invoke(obj, new object[1] { "PartyScreen" });
			if (obj2 == null)
			{
				return null;
			}
			object member = Reflection.GetMember(obj2, "DataSource");
			if (member == null || member.GetType().FullName.IndexOf(".Party.PartyVM", StringComparison.Ordinal) < 0)
			{
				return null;
			}
			return member;
		}

		private static object FindActiveHeroEntry(object dataSource, Hero activeHero)
		{
			object member = Reflection.GetMember(dataSource, "MainPartyTroops");
			if (member == null)
			{
				return null;
			}
			foreach (object item in Reflection.Enumerate(member))
			{
				object member2 = Reflection.GetMember(item, "Troop");
				object member3 = Reflection.GetMember(member2, "Character");
				object member4 = Reflection.GetMember(member3, "HeroObject");
				if (object.ReferenceEquals(member4, activeHero) || object.ReferenceEquals(member3, activeHero.CharacterObject) || Reflection.IdOf(member4) == Reflection.IdOf(activeHero))
				{
					return item;
				}
			}
			return null;
		}

		private static bool TrySelectEntry(object dataSource, object entry, out string route)
		{
			route = string.Empty;
			Type type = dataSource.GetType();
			Type entryType = entry.GetType();
			string[] array = new string[7] { "ExecuteCharacterSelection", "ExecuteSelectCharacter", "ExecuteCharacterSelect", "SetCurrentCharacter", "SetSelectedCharacter", "OnCharacterSelected", "OnCharacterSelection" };
			string[] array2 = array;
			foreach (string name in array2)
			{
				MethodInfo methodInfo = (from m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					where m.Name == name
					select m).FirstOrDefault((MethodInfo m) => CanBuildSelectionCall(m, entryType));
				if (!(methodInfo == null))
				{
					methodInfo.Invoke(dataSource, BuildSelectionCall(methodInfo, entry));
					route = type.FullName + "." + methodInfo.Name;
					return true;
				}
			}
			MethodInfo methodInfo2 = (from m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				where m.ReturnType == typeof(void) && CanBuildSelectionCall(m, entryType)
				select m).FirstOrDefault(delegate(MethodInfo m)
			{
				string text = m.Name.ToLowerInvariant();
				return (text.StartsWith("execute") || text.StartsWith("set") || text.StartsWith("on")) && text.Contains("character") && text.Contains("select");
			});
			if (methodInfo2 != null)
			{
				methodInfo2.Invoke(dataSource, BuildSelectionCall(methodInfo2, entry));
				route = type.FullName + "." + methodInfo2.Name;
				return true;
			}
			string[] array3 = new string[4] { "CurrentCharacter", "SelectedCharacter", "CurrentSelectedCharacter", "SelectedTroop" };
			string[] array4 = array3;
			foreach (string name2 in array4)
			{
				PropertyInfo property = type.GetProperty(name2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo methodInfo3 = ((!(property == null)) ? property.GetSetMethod(nonPublic: true) : null);
				if (!(methodInfo3 == null) && property.PropertyType.IsAssignableFrom(entryType))
				{
					methodInfo3.Invoke(dataSource, new object[1] { entry });
					InvokeNoArg(dataSource, "RefreshValues");
					route = type.FullName + "." + property.Name + " setter";
					return true;
				}
			}
			string[] array5 = new string[4] { "_currentCharacter", "_selectedCharacter", "_currentSelectedCharacter", "_selectedTroop" };
			string[] array6 = array5;
			foreach (string name3 in array6)
			{
				FieldInfo field = type.GetField(name3, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (!(field == null) && field.FieldType.IsAssignableFrom(entryType))
				{
					field.SetValue(dataSource, entry);
					InvokeNoArg(dataSource, "RefreshValues");
					route = type.FullName + "." + field.Name + " field";
					return true;
				}
			}
			string[] array7 = new string[3] { "ExecuteSelection", "ExecuteSelect", "ExecuteSetAsCurrent" };
			string[] array8 = array7;
			foreach (string name4 in array8)
			{
				MethodInfo method = entryType.GetMethod(name4, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				if (!(method == null))
				{
					method.Invoke(entry, null);
					route = entryType.FullName + "." + method.Name;
					return true;
				}
			}
			return false;
		}

		private static bool CanBuildSelectionCall(MethodInfo method, Type entryType)
		{
			if (method == null || entryType == null)
			{
				return false;
			}
			ParameterInfo[] parameters = method.GetParameters();
			if (parameters.Length < 1 || parameters.Length > 3 || !parameters[0].ParameterType.IsAssignableFrom(entryType))
			{
				return false;
			}
			for (int i = 1; i < parameters.Length; i++)
			{
				Type parameterType = parameters[i].ParameterType;
				if (!parameters[i].IsOptional && !(parameterType == typeof(bool)) && !(parameterType == typeof(int)) && parameterType.IsValueType && !(Nullable.GetUnderlyingType(parameterType) != null))
				{
					return false;
				}
			}
			return true;
		}

		private static object[] BuildSelectionCall(MethodInfo method, object entry)
		{
			ParameterInfo[] parameters = method.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = entry;
			for (int i = 1; i < parameters.Length; i++)
			{
				if (parameters[i].DefaultValue != DBNull.Value && parameters[i].DefaultValue != Type.Missing)
				{
					array[i] = parameters[i].DefaultValue;
				}
				else if (parameters[i].ParameterType == typeof(bool))
				{
					array[i] = false;
				}
				else if (parameters[i].ParameterType == typeof(int))
				{
					array[i] = 0;
				}
				else
				{
					array[i] = null;
				}
			}
			return array;
		}

		private static void InvokeNoArg(object target, string name)
		{
			if (target != null)
			{
				MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				if (method != null)
				{
					method.Invoke(target, null);
				}
			}
		}

		private static void DumpSelectionSurfaceOnce(Type type)
		{
			if (type == null || type == _lastDumpedType)
			{
				return;
			}
			_lastDumpedType = type;
			try
			{
				string text = string.Join(", ", (from x in (from m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						where m.Name.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Character", StringComparison.OrdinalIgnoreCase) >= 0
						select m.Name + "(" + string.Join(",", (from p in m.GetParameters()
							select p.ParameterType.Name).ToArray()) + ")").Distinct()
					orderby x
					select x).ToArray());
				string text2 = string.Join(", ", (from x in (from m in type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						where m.Name.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("CurrentCharacter", StringComparison.OrdinalIgnoreCase) >= 0
						select string.Concat(m.MemberType, ":", m.Name)).Distinct()
					orderby x
					select x).ToArray());
				Log.Info("PartyVM selection surface for " + type.FullName + ": methods=[" + text + "]; members=[" + text2 + "].");
			}
			catch (Exception ex)
			{
				Log.Error("Could not dump PartyVM selection surface", ex);
			}
		}
	}
}
