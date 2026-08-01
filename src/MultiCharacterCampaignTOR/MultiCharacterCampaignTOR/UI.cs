// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace MultiCharacterCampaignTOR
{
	internal static class UI
	{
		private static readonly Dictionary<int, Action<SelectionOption>> SelectionCallbacks = new Dictionary<int, Action<SelectionOption>>();

		private static readonly Dictionary<int, Action<string>> TextCallbacks = new Dictionary<int, Action<string>>();

		private static int _nextToken = 1;

		public static void LogRuntimeBindings()
		{
			try
			{
				Log.Info("UI binding probe started.");
				Log.Info("InquiryElement runtime type=" + DescribeType(typeof(InquiryElement)));
				ConstructorInfo[] constructors = typeof(InquiryElement).GetConstructors();
				foreach (ConstructorInfo method in constructors)
				{
					Log.Info("InquiryElement constructor: " + DescribeMethod(method));
				}
				Log.Info("MultiSelectionInquiryData runtime type=" + DescribeType(typeof(MultiSelectionInquiryData)));
				ConstructorInfo[] constructors2 = typeof(MultiSelectionInquiryData).GetConstructors();
				foreach (ConstructorInfo method2 in constructors2)
				{
					Log.Info("MultiSelectionInquiryData constructor: " + DescribeMethod(method2));
				}
				foreach (MethodInfo item in from m in typeof(MBInformationManager).GetMethods(BindingFlags.Static | BindingFlags.Public)
					where m.Name == "ShowMultiSelectionInquiry"
					select m)
				{
					Log.Info("Multi-selection show method: " + DescribeMethod(item));
				}
				Log.Info("TextInquiryData runtime type=" + DescribeType(typeof(TextInquiryData)));
				ConstructorInfo[] constructors3 = typeof(TextInquiryData).GetConstructors();
				foreach (ConstructorInfo method3 in constructors3)
				{
					Log.Info("TextInquiryData constructor: " + DescribeMethod(method3));
				}
				foreach (MethodInfo item2 in from m in typeof(InformationManager).GetMethods(BindingFlags.Static | BindingFlags.Public)
					where m.Name == "ShowTextInquiry" || m.Name == "DisplayMessage"
					select m)
				{
					Log.Info("Library information method: " + DescribeMethod(item2));
				}
				Log.Info("UI binding probe completed.");
			}
			catch (Exception ex)
			{
				Log.Error("UI binding probe failed", ex);
			}
		}

		public static void Message(string text)
		{
			try
			{
				Log.Info("Information message requested: " + SingleLine(text));
				InformationManager.DisplayMessage(new InformationMessage(text ?? string.Empty));
				Log.Info("Information message dispatched.");
			}
			catch (Exception ex)
			{
				Log.Error("Could not display information message", ex);
			}
		}

		public static void SelectOne(string title, string description, IList<SelectionOption> options, Action<SelectionOption> callback)
		{
			//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f0: Expected O, but got Unknown
			int token = 0;
			try
			{
				if (options == null)
				{
					throw new ArgumentNullException("options");
				}
				if (callback == null)
				{
					throw new ArgumentNullException("callback");
				}
				if (options.Count == 0)
				{
					throw new InvalidOperationException("The selection window was requested with no options.");
				}
				token = _nextToken++;
				SelectionCallbacks[token] = callback;
				Log.Info("SelectOne BEGIN token=" + token + "; title=" + SingleLine(title) + "; optionCount=" + options.Count + ".");
				List<InquiryElement> list = new List<InquiryElement>(options.Count);
				for (int i = 0; i < options.Count; i++)
				{
					SelectionOption selectionOption = options[i];
					if (selectionOption != null)
					{
						Log.Info("SelectOne option token=" + token + "; index=" + i + "; enabled=" + selectionOption.Enabled + "; label=" + SingleLine(selectionOption.Label) + "; hint=" + SingleLine(selectionOption.Hint) + ".");
						list.Add(CreateInquiryElement(selectionOption));
					}
				}
				if (list.Count == 0)
				{
					throw new InvalidOperationException("No valid inquiry elements could be created.");
				}
				MultiSelectionInquiryData val = new MultiSelectionInquiryData(title ?? string.Empty, description ?? string.Empty, list, true, 1, 1, "Select", "Cancel", (Action<List<InquiryElement>>)delegate(List<InquiryElement> selected)
				{
					DispatchSelection(token, selected);
				}, (Action<List<InquiryElement>>)delegate
				{
					CancelSelection(token, "negative action");
				}, string.Empty, list.Count > 10);
				Log.Info("SelectOne invoking MBInformationManager.ShowMultiSelectionInquiry token=" + token + "; pauseGameActiveState=true; prioritize=false.");
				MBInformationManager.ShowMultiSelectionInquiry(val, true, false);
				Log.Info("SelectOne invocation returned token=" + token + ". Callback remains registered=" + SelectionCallbacks.ContainsKey(token) + ".");
			}
			catch (Exception ex)
			{
				if (token != 0)
				{
					SelectionCallbacks.Remove(token);
				}
				Log.Error("Selection inquiry failed token=" + token + "; title=" + SingleLine(title), ex);
				Message("Multi-Character Campaign could not open the selection window. See MultiCharacterCampaignTOR.log at: " + Log.FilePath);
			}
		}

		private static InquiryElement CreateInquiryElement(SelectionOption option)
		{
			if (option == null)
			{
				throw new ArgumentNullException("option");
			}
			Type typeFromHandle = typeof(InquiryElement);
			ConstructorInfo constructorInfo = null;
			ConstructorInfo constructorInfo2 = null;
			ConstructorInfo[] constructors = typeFromHandle.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
			foreach (ConstructorInfo constructorInfo3 in constructors)
			{
				ParameterInfo[] parameters = constructorInfo3.GetParameters();
				if (parameters.Length == 5 && parameters[0].ParameterType == typeof(object) && parameters[1].ParameterType == typeof(string) && parameters[3].ParameterType == typeof(bool) && parameters[4].ParameterType == typeof(string))
				{
					constructorInfo = constructorInfo3;
					break;
				}
				if (parameters.Length == 3 && parameters[0].ParameterType == typeof(object) && parameters[1].ParameterType == typeof(string))
				{
					constructorInfo2 = constructorInfo3;
				}
			}
			constructorInfo = constructorInfo ?? constructorInfo2;
			if (constructorInfo == null)
			{
				throw new MissingMethodException(typeFromHandle.FullName, ".ctor(object, string, <runtime image identifier>[, bool, string])");
			}
			ParameterInfo[] parameters2 = constructorInfo.GetParameters();
			object obj = ((!parameters2[2].ParameterType.IsValueType) ? null : Activator.CreateInstance(parameters2[2].ParameterType));
			object[] parameters3 = ((parameters2.Length == 5) ? new object[5]
			{
				option,
				option.Label ?? string.Empty,
				obj,
				option.Enabled,
				option.Hint ?? string.Empty
			} : new object[3]
			{
				option,
				option.Label ?? string.Empty,
				obj
			});
			Log.Info("Creating InquiryElement through runtime constructor: " + DescribeMethod(constructorInfo) + ".");
			object obj2 = constructorInfo.Invoke(parameters3);
			InquiryElement val = (InquiryElement)((obj2 is InquiryElement) ? obj2 : null);
			if (val == null)
			{
				throw new InvalidCastException("Runtime InquiryElement constructor returned " + ((obj2 != null) ? obj2.GetType().AssemblyQualifiedName : "<null>") + ".");
			}
			return val;
		}

		public static void AskText(string title, string description, string defaultText, Action<string> callback)
		{
			int token = 0;
			try
			{
				if (callback == null)
				{
					throw new ArgumentNullException("callback");
				}
				token = _nextToken++;
				TextCallbacks[token] = callback;
				Log.Info("AskText BEGIN token=" + token + "; title=" + SingleLine(title) + "; default=" + SingleLine(defaultText) + ".");
				TextInquiryData textData = new TextInquiryData(title ?? string.Empty, description ?? string.Empty, isAffirmativeOptionShown: true, isNegativeOptionShown: true, "Continue", "Cancel", delegate(string value)
				{
					DispatchText(token, value);
				}, delegate
				{
					CancelText(token, "negative action");
				}, shouldInputBeObfuscated: false, null, string.Empty, defaultText ?? string.Empty);
				Log.Info("AskText invoking InformationManager.ShowTextInquiry token=" + token + "; pauseGameActiveState=true; prioritize=false.");
				InformationManager.ShowTextInquiry(textData, pauseGameActiveState: true);
				Log.Info("AskText invocation returned token=" + token + ". Callback remains registered=" + TextCallbacks.ContainsKey(token) + ".");
			}
			catch (Exception ex)
			{
				if (token != 0)
				{
					TextCallbacks.Remove(token);
				}
				Log.Error("Text inquiry failed token=" + token + "; title=" + SingleLine(title), ex);
				Message("Multi-Character Campaign could not open the text-entry window. See MultiCharacterCampaignTOR.log at: " + Log.FilePath);
			}
		}

		private static void DispatchSelection(int token, List<InquiryElement> selected)
		{
			if (!SelectionCallbacks.TryGetValue(token, out var value))
			{
				Log.Warning("Selection callback arrived for unknown token=" + token + ".");
				return;
			}
			SelectionCallbacks.Remove(token);
			try
			{
				int num = selected?.Count ?? 0;
				Log.Info("Selection callback BEGIN token=" + token + "; selectedCount=" + num + ".");
				if (selected == null || selected.Count == 0)
				{
					Log.Warning("Selection callback token=" + token + " contained no selected elements.");
					return;
				}
				object obj = ReadMember(selected[0], "Identifier") ?? ReadMember(selected[0], "Id");
				if (!(obj is SelectionOption selectionOption))
				{
					throw new InvalidOperationException("Selected inquiry element did not contain the expected SelectionOption identifier. IdentifierType=" + ((obj != null) ? obj.GetType().FullName : "<null>"));
				}
				Log.Info("Selection callback resolved token=" + token + "; label=" + SingleLine(selectionOption.Label) + "; valueType=" + ((selectionOption.Value != null) ? selectionOption.Value.GetType().FullName : "<null>") + ".");
				value(selectionOption);
				Log.Info("Selection callback END token=" + token + ".");
			}
			catch (Exception ex)
			{
				Log.Error("Selection callback failed token=" + token, ex);
				Message("Multi-Character Campaign selection failed. See MultiCharacterCampaignTOR.log at: " + Log.FilePath);
			}
		}

		private static void DispatchText(int token, string value)
		{
			if (!TextCallbacks.TryGetValue(token, out var value2))
			{
				Log.Warning("Text callback arrived for unknown token=" + token + ".");
				return;
			}
			TextCallbacks.Remove(token);
			try
			{
				Log.Info("Text callback BEGIN token=" + token + "; value=" + SingleLine(value) + ".");
				value2(value);
				Log.Info("Text callback END token=" + token + ".");
			}
			catch (Exception ex)
			{
				Log.Error("Text callback failed token=" + token, ex);
				Message("Multi-Character Campaign text-entry callback failed. See MultiCharacterCampaignTOR.log at: " + Log.FilePath);
			}
		}

		private static void CancelSelection(int token, string source)
		{
			bool flag = SelectionCallbacks.Remove(token);
			Log.Info("Selection cancelled token=" + token + "; source=" + source + "; callbackRemoved=" + flag + ".");
		}

		private static void CancelText(int token, string source)
		{
			bool flag = TextCallbacks.Remove(token);
			Log.Info("Text inquiry cancelled token=" + token + "; source=" + source + "; callbackRemoved=" + flag + ".");
		}

		private static object ReadMember(object instance, string name)
		{
			if (instance == null)
			{
				return null;
			}
			Type type = instance.GetType();
			PropertyInfo propertyInfo = (from p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				where p.Name == name && p.GetIndexParameters().Length == 0
				orderby (!(p.DeclaringType == type)) ? 1 : 0
				select p).FirstOrDefault();
			if (propertyInfo != null)
			{
				return propertyInfo.GetValue(instance, null);
			}
			FieldInfo fieldInfo = (from f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				where f.Name == name
				orderby (!(f.DeclaringType == type)) ? 1 : 0
				select f).FirstOrDefault();
			return (!(fieldInfo == null)) ? fieldInfo.GetValue(instance) : null;
		}

		private static string DescribeType(Type type)
		{
			if (type == null)
			{
				return "<null>";
			}
			AssemblyName name = type.Assembly.GetName();
			return type.FullName + " from " + name.Name + " " + name.Version;
		}

		private static string DescribeMethod(MethodBase method)
		{
			if (method == null)
			{
				return "<null>";
			}
			string text = string.Join(", ", (from p in method.GetParameters()
				select p.ParameterType.FullName + " " + p.Name).ToArray());
			return method.DeclaringType.FullName + "." + method.Name + "(" + text + ")";
		}

		private static string SingleLine(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			string text = value.Replace("\r", " ").Replace("\n", " ");
			return (text.Length > 300) ? (text.Substring(0, 300) + "...") : text;
		}
	}
}
