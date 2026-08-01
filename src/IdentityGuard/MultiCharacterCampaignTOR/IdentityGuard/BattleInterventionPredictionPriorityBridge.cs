using System;
using System.Linq;
using System.Reflection;

namespace MultiCharacterCampaignTOR.IdentityGuard
{
	internal static class BattleInterventionPredictionPriorityBridge
	{
		private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private static bool _installed;
		private static MethodInfo _showPredictionInquiry;

		internal static void Install()
		{
			if (_installed)
			{
				return;
			}
			try
			{
				MethodInfo original = typeof(BattleInterventionAlert).GetMethod("ShowInquiry", StaticFlags);
				_showPredictionInquiry = typeof(BattleInterventionPrediction).GetMethod("ShowPredictionInquiry", StaticFlags);
				if (original == null || _showPredictionInquiry == null)
				{
					throw new MissingMethodException("The battle-alert inquiry surfaces are unavailable.");
				}

				Type harmonyType = RequireType("HarmonyLib.Harmony, 0Harmony");
				Type harmonyMethodType = RequireType("HarmonyLib.HarmonyMethod, 0Harmony");
				object harmony = Activator.CreateInstance(harmonyType, "xmarre.multicharactercampaign.tor.battlepredictionpriority.v120");
				MethodInfo prefix = typeof(BattleInterventionPredictionPriorityBridge).GetMethod("BeforeShowInquiry", StaticFlags);
				object harmonyPrefix = Activator.CreateInstance(harmonyMethodType, prefix);
				FieldInfo priority = harmonyMethodType.GetField("priority", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (priority == null)
				{
					throw new MissingFieldException(harmonyMethodType.FullName, "priority");
				}
				priority.SetValue(harmonyPrefix, 800);

				MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
					.First((MethodInfo method) => method.Name == "Patch" && method.GetParameters().Length >= 3);
				object[] arguments = new object[patch.GetParameters().Length];
				arguments[0] = original;
				arguments[1] = harmonyPrefix;
				patch.Invoke(harmony, arguments);

				_installed = true;
				RemotePartySwitch.Info("[BattleInterventionPredictionPriorityBridge v1.2.0] Installed the strength-aware alert at Harmony Priority.First, before the v1.1.2 inquiry replacement.");
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionPredictionPriorityBridge] Installation failed", Unwrap(ex));
			}
		}

		private static bool BeforeShowInquiry(object __0)
		{
			try
			{
				_showPredictionInquiry.Invoke(null, new object[1] { __0 });
			}
			catch (Exception ex)
			{
				RemotePartySwitch.Error("[BattleInterventionPredictionPriorityBridge] Strength-aware alert invocation failed", Unwrap(ex));
				RemotePartySwitch.Notify("A shared-character battle was detected, but the strength-aware alert failed. See MultiCharacterCampaignTOR.log.");
			}
			return false;
		}

		private static Type RequireType(string qualifiedName)
		{
			Type type = Type.GetType(qualifiedName, false);
			if (type == null)
			{
				throw new TypeLoadException(qualifiedName);
			}
			return type;
		}

		private static Exception Unwrap(Exception exception)
		{
			while (exception is TargetInvocationException && exception.InnerException != null)
			{
				exception = exception.InnerException;
			}
			return exception;
		}
	}
}
