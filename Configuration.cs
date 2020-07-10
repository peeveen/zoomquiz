using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ZoomQuiz
{
	static class Configuration
	{
		public static string QuestionAndAnswerFont { get; private set; } = "Impact";
		public static string LeaderboardFont { get; private set; } = "Bahnschrift Condensed";
		public static string ScoreReportFont { get; private set; } = "Bahnschrift Condensed";
		public static Dictionary<Source, string> SourceNames = new Dictionary<Source, string>();
		public static Dictionary<Scene, string> SceneNames = new Dictionary<Scene, string>();

		private const string QUIZ_CONFIG_SECTION = "Quiz";

		private static void PopulateDictionary<T>(IniFile iniFile,Dictionary<T,string> dictionary,string suffix) where T:Enum
		{
			var tValues = Enum.GetValues(typeof(T));
			foreach (T t in tValues)
			{
				string key = $"{t}{suffix}";
				string value= iniFile.Read(key, QUIZ_CONFIG_SECTION, null);
				if (string.IsNullOrEmpty(value))
					Logger.Log($"Null value found in configuration for {key}");
				dictionary[t] = value;
			}
		}

		static public IEnumerable<string> UnconfiguredScenesOrSources
		{
			get
			{
				IEnumerable<string> unconfiguredSources = SourceNames.Where(kvp => string.IsNullOrEmpty(kvp.Value)).Select(kvp => kvp.Key.ToString());
				IEnumerable<string> unconfiguredScenes = SceneNames.Where(kvp => string.IsNullOrEmpty(kvp.Value)).Select(kvp => kvp.Key.ToString());
				return unconfiguredSources.Concat(unconfiguredScenes);
			}
		}

		static Configuration()
		{
			string configPath = FileUtils.GetFilePath("config", "config.ini");
			if (File.Exists(configPath))
			{
				Logger.Log($"Reading config file \"{configPath}\".");
				IniFile configIni = new IniFile(configPath);
				QuestionAndAnswerFont = configIni.Read("QuestionAndAnswerFont", QUIZ_CONFIG_SECTION, QuestionAndAnswerFont);
				LeaderboardFont = configIni.Read("LeaderboardFont", QUIZ_CONFIG_SECTION, LeaderboardFont);
				ScoreReportFont = configIni.Read("ScoreReportFont", QUIZ_CONFIG_SECTION, ScoreReportFont);
				PopulateDictionary(configIni, SourceNames, "SourceName");
				PopulateDictionary(configIni, SceneNames, "SceneName");
			}
		}
	}
}
