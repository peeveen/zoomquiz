using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZoomQuiz
{
	public class Configuration
	{
		public string QuestionAndAnswerFont { get; private set; } = "Impact";
		public string LeaderboardFont { get; private set; } = "Arial";
		public string ScoreReportFont { get; private set; } = "Arial";
		public string BGMPath { get; private set; } = "";
		public Dictionary<Source, string> SourceNames = new Dictionary<Source, string>();
		public Dictionary<Scene, string> SceneNames = new Dictionary<Scene, string>();

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

		public IEnumerable<string> UnconfiguredScenesOrSources
		{
			get
			{
				IEnumerable<string> unconfiguredSources = SourceNames.Where(kvp => string.IsNullOrEmpty(kvp.Value)).Select(kvp => kvp.Key.ToString());
				IEnumerable<string> unconfiguredScenes = SceneNames.Where(kvp => string.IsNullOrEmpty(kvp.Value)).Select(kvp => kvp.Key.ToString());
				return unconfiguredSources.Concat(unconfiguredScenes);
			}
		}

		internal Configuration(string configPath)
		{
			if (File.Exists(configPath))
			{
				Logger.Log($"Reading config file \"{configPath}\".");
				IniFile configIni = new IniFile(configPath);
				QuestionAndAnswerFont = configIni.Read("QuestionAndAnswerFont", QUIZ_CONFIG_SECTION, QuestionAndAnswerFont);
				LeaderboardFont = configIni.Read("LeaderboardFont", QUIZ_CONFIG_SECTION, LeaderboardFont);
				ScoreReportFont = configIni.Read("ScoreReportFont", QUIZ_CONFIG_SECTION, ScoreReportFont);
				PopulateDictionary(configIni, SourceNames, "SourceName");
				PopulateDictionary(configIni, SceneNames, "SceneName");
				BGMPath = Environment.ExpandEnvironmentVariables(configIni.Read("BGMPath", QUIZ_CONFIG_SECTION, BGMPath));
			}
			else
				throw new Exception("Config file not found.");
		}
	}
}
