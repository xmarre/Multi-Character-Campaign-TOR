// RECONSTRUCTED DEVELOPMENT SOURCE
// Decompiled from the exact Multi-Character Campaign - TOR v1.0.41 authoritative binary.
// This is not the lost original authoring source. See Source/SOURCE_INFO.md and CanonicalIL/ for authority.

using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace MultiCharacterCampaignTOR
{
	internal static class Log
	{
		private static string _path;

		private static readonly object Sync = new object();

		public static string FilePath => _path ?? string.Empty;

		public static void Initialize()
		{
			string text = null;
			string text2 = null;
			try
			{
				string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
				string text3 = Path.Combine(folderPath, "Mount and Blade II Bannerlord", "Configs", "ModLogs");
				Directory.CreateDirectory(text3);
				text = (_path = Path.Combine(text3, "MultiCharacterCampaignTOR.log"));
				File.WriteAllText(_path, string.Empty);
			}
			catch
			{
				try
				{
					text2 = (_path = Path.Combine(Path.GetTempPath(), "MultiCharacterCampaignTOR.log"));
					File.WriteAllText(_path, string.Empty);
				}
				catch
				{
					_path = null;
				}
			}
			Info(string.Concat("Log initialized. Version=1.0.32; Runtime=", Environment.Version, "; Process=", Environment.Is64BitProcess, "-bit."));
			Info("Log path=" + ((!string.IsNullOrEmpty(_path)) ? _path : "<unavailable>"));
			if (!string.IsNullOrEmpty(text) && !string.Equals(text, _path, StringComparison.OrdinalIgnoreCase))
			{
				Info("Primary log path failed; fallback path is active. Primary=" + text);
			}
			if (!string.IsNullOrEmpty(text2))
			{
				Info("Fallback log path=" + text2);
			}
		}

		public static void Info(string message)
		{
			Write("INFO", message);
		}

		public static void Warning(string message)
		{
			Write("WARN", message);
		}

		public static void Error(string message, Exception ex)
		{
			string text = string.Empty;
			if (ex != null)
			{
				try
				{
					text = Environment.NewLine + FormatException(ex);
				}
				catch (Exception ex2)
				{
					text = Environment.NewLine + "Exception formatter failed: " + SafeExceptionLine(ex2) + Environment.NewLine + "Original exception: " + SafeExceptionLine(ex);
				}
			}
			Write("ERROR", (message ?? string.Empty) + text);
		}

		private static string FormatException(Exception ex)
		{
			if (ex == null)
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder();
			int num = 0;
			Exception ex2 = ex;
			while (ex2 != null && num < 12)
			{
				if (num > 0)
				{
					stringBuilder.AppendLine("--- INNER EXCEPTION " + num + " ---");
				}
				stringBuilder.AppendLine(SafeExceptionLine(ex2));
				string value = null;
				try
				{
					value = ex2.StackTrace;
				}
				catch
				{
				}
				if (!string.IsNullOrEmpty(value))
				{
					stringBuilder.AppendLine(value);
				}
				TargetInvocationException ex3 = ex2 as TargetInvocationException;
				try
				{
					ex2 = ((ex3 == null || ex3.InnerException == null) ? ex2.InnerException : ex3.InnerException);
				}
				catch
				{
					ex2 = null;
				}
				num++;
			}
			return stringBuilder.ToString().TrimEnd('\r', '\n');
		}

		private static string SafeExceptionLine(Exception ex)
		{
			if (ex == null)
			{
				return "<null exception>";
			}
			string text = "<unknown exception type>";
			string text2 = "<message unavailable>";
			try
			{
				text = ex.GetType().FullName ?? ex.GetType().Name;
			}
			catch
			{
			}
			try
			{
				text2 = ex.Message ?? string.Empty;
			}
			catch
			{
			}
			return text + ": " + text2;
		}

		private static void Write(string level, string message)
		{
			string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + (message ?? string.Empty);
			try
			{
			}
			catch
			{
			}
			try
			{
				Console.WriteLine(text);
			}
			catch
			{
			}
			try
			{
				lock (Sync)
				{
					if (!string.IsNullOrEmpty(_path))
					{
						File.AppendAllText(_path, text + Environment.NewLine);
					}
				}
			}
			catch
			{
			}
		}
	}
}
