using System.IO;

namespace ZoomQuiz
{
	static class FileUtils
	{
		public static string GetFolderPath(string folder)
		{
			return Path.Combine(Directory.GetCurrentDirectory(), folder);
		}

		public static string GetFilePath(string folder, string filename)
		{
			return Path.Combine(GetFolderPath(folder), filename);
		}
	}
}
