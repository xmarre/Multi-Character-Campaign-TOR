// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MultiCharacterCampaignTOR.NativeCreation
{
	public static class NativeCharacterCreation
	{
		private const string HarmonyId = "multicharactercampaigntor.nativecreation.incampaigncompletion";

		private static readonly object Sync = new object();

		private static bool _initialized;

		private static Type _legacyType;

		private static FieldInfo _legacyInProgressField;

		private static MethodInfo _legacyStartMethod;

		private static MethodInfo _legacyCompletionMethod;

		private static bool _stagesTrimmedForCurrentSession;

		public static void Start(object behavior)
		{
			EnsureInitialized();
			_stagesTrimmedForCurrentSession = false;
			try
			{
				_legacyStartMethod.Invoke(null, new object[1] { behavior });
			}
			catch (TargetInvocationException ex)
			{
				throw ex.InnerException ?? ex;
			}
		}

		private static void EnsureInitialized()
		{
			if (_initialized)
			{
				return;
			}
			lock (Sync)
			{
				if (!_initialized)
				{
					string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
					string text = Path.Combine(directoryName, "MultiCharacterCampaignTOR.NativeCreation.Legacy.dll");
					if (!File.Exists(text))
					{
						throw new FileNotFoundException("The native character-creation implementation is missing.", text);
					}
					Assembly assembly = Assembly.LoadFrom(text);
					_legacyType = assembly.GetType("MultiCharacterCampaignTOR.NativeCreation.NativeCharacterCreation", throwOnError: true, ignoreCase: false);
					BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
					_legacyInProgressField = _legacyType.GetField("_inProgress", bindingAttr);
					_legacyStartMethod = _legacyType.GetMethod("Start", bindingAttr, null, new Type[1] { typeof(object) }, null);
					_legacyCompletionMethod = _legacyType.GetMethod("OnCharacterCreationCompleted", bindingAttr, null, Type.EmptyTypes, null);
					if (_legacyInProgressField == null || _legacyStartMethod == null || _legacyCompletionMethod == null)
					{
						throw new MissingMemberException("The packaged native character-creation implementation does not expose the expected v1.0.15 bridge members.");
					}
					InstallHarmonyPatches();
					_initialized = true;
					LogInfo("Installed in-campaign character-creation stage and completion patches.");
				}
			}
		}

		private static void InstallHarmonyPatches()
		{
			Type type = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: true);
			Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", throwOnError: true);
			object obj = Activator.CreateInstance(type, "multicharactercampaigntor.nativecreation.incampaigncompletion");
			Type type2 = RequireType("TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager, TaleWorlds.CampaignSystem");
			Type type3 = RequireType("TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState, TaleWorlds.CampaignSystem");
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			MethodInfo method = type2.GetMethod("OnStateActivated", bindingAttr, null, Type.EmptyTypes, null);
			MethodInfo method2 = type3.GetMethod("FinalizeCharacterCreationState", bindingAttr, null, Type.EmptyTypes, null);
			if (method == null || method2 == null)
			{
				throw new MissingMethodException("Bannerlord 1.3.15 character-creation methods could not be resolved.");
			}
			MethodInfo method3 = typeof(NativeCharacterCreation).GetMethod("OnStateActivatedPrefix", BindingFlags.Static | BindingFlags.NonPublic);
			MethodInfo method4 = typeof(NativeCharacterCreation).GetMethod("FinalizeCharacterCreationStatePrefix", BindingFlags.Static | BindingFlags.NonPublic);
			object prefix = Activator.CreateInstance(harmonyMethodType, method3);
			object prefix2 = Activator.CreateInstance(harmonyMethodType, method4);
			MethodInfo methodInfo = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).First(delegate(MethodInfo methodInfo2)
			{
				if (methodInfo2.Name != "Patch")
				{
					return false;
				}
				ParameterInfo[] parameters3 = methodInfo2.GetParameters();
				return parameters3.Length >= 3 && typeof(MethodBase).IsAssignableFrom(parameters3[0].ParameterType) && parameters3[1].ParameterType == harmonyMethodType;
			});
			object[] parameters = BuildPatchArguments(methodInfo, method, prefix);
			methodInfo.Invoke(obj, parameters);
			object[] parameters2 = BuildPatchArguments(methodInfo, method2, prefix2);
			methodInfo.Invoke(obj, parameters2);
		}

		private static object[] BuildPatchArguments(MethodInfo patch, MethodBase original, object prefix)
		{
			ParameterInfo[] parameters = patch.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = original;
			array[1] = prefix;
			for (int i = 2; i < array.Length; i++)
			{
				array[i] = null;
			}
			return array;
		}

		private static void OnStateActivatedPrefix(object __instance)
		{
			if (!IsLegacyCreationInProgress() || _stagesTrimmedForCurrentSession || __instance == null)
			{
				return;
			}
			try
			{
				FieldInfo fieldInfo = FindField(__instance.GetType(), "_stages");
				IList list = ((!(fieldInfo == null)) ? (fieldInfo.GetValue(__instance) as IList) : null);
				if (list == null)
				{
					throw new MissingFieldException(__instance.GetType().FullName, "_stages");
				}
				int num = -1;
				for (int i = 0; i < list.Count; i++)
				{
					object obj = list[i];
					if (obj != null && obj.GetType().FullName == "TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationReviewStage")
					{
						num = i;
						break;
					}
				}
				string text = DescribeStages(list);
				if (num >= 0)
				{
					for (int num2 = list.Count - 1; num2 > num; num2--)
					{
						list.RemoveAt(num2);
					}
				}
				for (int num3 = num - 1; num3 >= 0; num3--)
				{
					object obj2 = list[num3];
					if (obj2 != null && obj2.GetType().FullName == "TOR_Core.CampaignMechanics.CharacterCreation.TORSpecializationStage")
					{
						break;
					}
					list.RemoveAt(num3);
				}
				FindCompatibleStaticMethod(_legacyType, "RestoreCampaignState", new object[0]).Invoke(null, null);
				_stagesTrimmedForCurrentSession = true;
				LogInfo("Trimmed campaign-start-only character-creation stages. Before=" + text + "; after=" + DescribeStages(list) + ".");
			}
			catch (Exception exception)
			{
				LogError("Failed to trim campaign-start-only character-creation stages", Unwrap(exception));
			}
		}

		private static bool FinalizeCharacterCreationStatePrefix(object __instance)
		{
			if (!IsLegacyCreationInProgress())
			{
				return true;
			}
			Exception ex = null;
			object obj = null;
			try
			{
				Type type = RequireType("TaleWorlds.Core.Game, TaleWorlds.Core");
				object staticMember = GetStaticMember(type, "Current");
				obj = GetMember(staticMember, "GameStateManager");
				if (obj == null)
				{
					throw new InvalidOperationException("Game.Current.GameStateManager is unavailable during in-campaign character-creation completion.");
				}
				InvokeCompatible(obj, "UnregisterActiveStateDisableRequest", new object[1] { __instance });
			}
			catch (Exception exception)
			{
				ex = Unwrap(exception);
				LogError("Failed to unregister the character-creation active-state disable request", ex);
			}
			try
			{
				if (obj != null)
				{
					InvokeCompatible(obj, "PopState", new object[1] { 0 });
				}
			}
			catch (Exception exception2)
			{
				Exception ex2 = Unwrap(exception2);
				if (ex == null)
				{
					ex = ex2;
				}
				LogError("Failed to pop the in-campaign CharacterCreationState", ex2);
			}
			try
			{
				MarkMainPartyVisualDirty();
			}
			catch (Exception exception3)
			{
				Exception ex3 = Unwrap(exception3);
				if (ex == null)
				{
					ex = ex3;
				}
				LogError("Failed to mark the main party visual as dirty after character creation", ex3);
			}
			try
			{
				FieldInfo fieldInfo = FindField(__instance.GetType(), "_handler");
				object obj2 = ((!(fieldInfo == null)) ? fieldInfo.GetValue(__instance) : null);
				if (obj2 != null)
				{
					InvokeCompatible(obj2, "TaleWorlds.CampaignSystem.CharacterCreationContent.ICharacterCreationStateHandler.OnCharacterCreationFinalized", new object[0]);
				}
			}
			catch (Exception exception4)
			{
				Exception ex4 = Unwrap(exception4);
				if (ex == null)
				{
					ex = ex4;
				}
				LogError("Character-creation state handler finalization failed", ex4);
			}
			try
			{
				_legacyCompletionMethod.Invoke(null, null);
			}
			catch (Exception exception5)
			{
				Exception ex5 = Unwrap(exception5);
				if (ex == null)
				{
					ex = ex5;
				}
				LogError("Native TOR character-creation completion cleanup failed", ex5);
			}
			if (ex == null)
			{
				LogInfo("Native creation cleanup complete");
			}
			else
			{
				DisplayMessage("The character was created, but completion cleanup reported an error. See MultiCharacterCampaignTOR.log.");
			}
			_stagesTrimmedForCurrentSession = false;
			return false;
		}

		private static bool IsLegacyCreationInProgress()
		{
			try
			{
				return _legacyInProgressField != null && (bool)_legacyInProgressField.GetValue(null);
			}
			catch
			{
				return false;
			}
		}

		private static void MarkMainPartyVisualDirty()
		{
			Type type = RequireType("TaleWorlds.CampaignSystem.Party.PartyBase, TaleWorlds.CampaignSystem");
			object staticMember = GetStaticMember(type, "MainParty");
			if (staticMember != null)
			{
				InvokeCompatible(staticMember, "SetVisualAsDirty", new object[0]);
			}
		}

		private static string DescribeStages(IList stages)
		{
			if (stages == null)
			{
				return "<null>";
			}
			string[] array = new string[stages.Count];
			for (int i = 0; i < stages.Count; i++)
			{
				object obj = stages[i];
				array[i] = ((obj != null) ? obj.GetType().Name : "<null>");
			}
			return string.Join(",", array);
		}

		private static Type RequireType(string assemblyQualifiedName)
		{
			Type type = Type.GetType(assemblyQualifiedName, throwOnError: false);
			if (type == null)
			{
				throw new TypeLoadException("Required runtime type not found: " + assemblyQualifiedName);
			}
			return type;
		}

		private static FieldInfo FindField(Type type, string name)
		{
			Type type2 = type;
			while (type2 != null)
			{
				FieldInfo field = type2.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					return field;
				}
				type2 = type2.BaseType;
			}
			return null;
		}

		private static PropertyInfo FindProperty(Type type, string name)
		{
			Type type2 = type;
			while (type2 != null)
			{
				PropertyInfo property = type2.GetProperty(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null)
				{
					return property;
				}
				type2 = type2.BaseType;
			}
			return null;
		}

		private static object GetStaticMember(Type type, string name)
		{
			PropertyInfo propertyInfo = FindProperty(type, name);
			if (propertyInfo != null)
			{
				return propertyInfo.GetValue(null, null);
			}
			FieldInfo fieldInfo = FindField(type, name);
			if (fieldInfo != null)
			{
				return fieldInfo.GetValue(null);
			}
			throw new MissingMemberException(type.FullName, name);
		}

		private static object GetMember(object instance, string name)
		{
			if (instance == null)
			{
				return null;
			}
			PropertyInfo propertyInfo = FindProperty(instance.GetType(), name);
			if (propertyInfo != null)
			{
				return propertyInfo.GetValue(instance, null);
			}
			FieldInfo fieldInfo = FindField(instance.GetType(), name);
			if (fieldInfo != null)
			{
				return fieldInfo.GetValue(instance);
			}
			throw new MissingMemberException(instance.GetType().FullName, name);
		}

		private static object InvokeCompatible(object instance, string name, object[] arguments)
		{
			if (instance == null)
			{
				throw new ArgumentNullException("instance");
			}
			MethodInfo methodInfo = FindCompatibleMethod(instance.GetType(), name, arguments);
			if (methodInfo == null)
			{
				throw new MissingMethodException(instance.GetType().FullName, name);
			}
			return methodInfo.Invoke(instance, arguments);
		}

		private static MethodInfo FindCompatibleMethod(Type type, string name, object[] arguments)
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			MethodInfo[] methods = type.GetMethods(bindingAttr);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name != name)
				{
					continue;
				}
				ParameterInfo[] parameters = methodInfo.GetParameters();
				if (parameters.Length != arguments.Length)
				{
					continue;
				}
				bool flag = true;
				for (int j = 0; j < parameters.Length; j++)
				{
					object obj = arguments[j];
					if (obj == null)
					{
						if (parameters[j].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[j].ParameterType) == null)
						{
							flag = false;
							break;
						}
					}
					else if (!parameters[j].ParameterType.IsInstanceOfType(obj))
					{
						try
						{
							arguments[j] = Convert.ChangeType(obj, parameters[j].ParameterType);
						}
						catch
						{
							flag = false;
							break;
						}
					}
				}
				if (flag)
				{
					return methodInfo;
				}
			}
			return null;
		}

		private static Exception Unwrap(Exception exception)
		{
			while (exception is TargetInvocationException && exception.InnerException != null)
			{
				exception = exception.InnerException;
			}
			return exception;
		}

		private static void LogInfo(string message)
		{
			TryInvokeMainLog("Info", new object[1] { message });
		}

		private static void LogError(string message, Exception exception)
		{
			if (!TryInvokeMainLog("Error", new object[2] { message, exception }))
			{
				TryInvokeMainLog("Info", new object[1] { message + ": " + exception });
			}
		}

		private static bool TryInvokeMainLog(string methodName, object[] arguments)
		{
			try
			{
				Type type = Type.GetType("MultiCharacterCampaignTOR.Log, MultiCharacterCampaignTOR", throwOnError: false);
				if (type == null)
				{
					return false;
				}
				MethodInfo methodInfo = FindCompatibleStaticMethod(type, methodName, arguments);
				if (methodInfo == null)
				{
					return false;
				}
				methodInfo.Invoke(null, arguments);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static MethodInfo FindCompatibleStaticMethod(Type type, string name, object[] arguments)
		{
			BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			MethodInfo[] methods = type.GetMethods(bindingAttr);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name != name)
				{
					continue;
				}
				ParameterInfo[] parameters = methodInfo.GetParameters();
				if (parameters.Length != arguments.Length)
				{
					continue;
				}
				bool flag = true;
				for (int j = 0; j < parameters.Length; j++)
				{
					if (arguments[j] != null && !parameters[j].ParameterType.IsInstanceOfType(arguments[j]))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					return methodInfo;
				}
			}
			return null;
		}

		private static void DisplayMessage(string message)
		{
			try
			{
				Type type = Type.GetType("TaleWorlds.Library.InformationMessage, TaleWorlds.Library", throwOnError: false);
				Type type2 = Type.GetType("TaleWorlds.Library.InformationManager, TaleWorlds.Library", throwOnError: false);
				if (!(type == null) && !(type2 == null))
				{
					object obj = Activator.CreateInstance(type, message);
					MethodInfo methodInfo = FindCompatibleStaticMethod(type2, "DisplayMessage", new object[1] { obj });
					if (methodInfo != null)
					{
						methodInfo.Invoke(null, new object[1] { obj });
					}
				}
			}
			catch
			{
			}
		}
	}
}
