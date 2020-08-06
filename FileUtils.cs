using System;
using System.IO;

namespace ZoomQuiz
{
	static class FileUtils
	{
		private static readonly string appDataFolder = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"FullHouse Entertainment", "QuizControlPanel");

		static FileUtils()
		{
			Directory.CreateDirectory(appDataFolder);
		}

		public static string GetFolderPath(string folder)
		{
			return Path.Combine(Directory.GetCurrentDirectory(), folder);
		}

		public static string GetFilePath(string folder, string filename)
		{
			return Path.Combine(GetFolderPath(folder), filename);
		}

		public static string GetAppDataFile(string filename)
		{
			return Path.Combine(appDataFolder, filename);
		}
		public static string GetAppDataFolder(string filename)
		{
			string strFolder=Path.Combine(appDataFolder, filename);
			Directory.CreateDirectory(strFolder);
			return strFolder;
		}
	}
}
