using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZoomQuiz
{
	class Quiz:IEnumerable
	{
		private Dictionary<int, Question> m_questions = new Dictionary<int, Question>();
		private readonly Dictionary<string, string> m_mediaPaths = new Dictionary<string, string>();

		private static string[] ParseDelimitedString(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				string[] bits = s.Split(',');
				for (int f = 0; f < bits.Length; ++f)
					bits[f] = bits[f].Trim();
				return bits;
			}
			return new string[0];
		}

		private static string FixUnicode(string strIn)
		{
			strIn = strIn.Replace("Â£", "£");
			strIn = strIn.Replace("Ã©", "é");
			return strIn;
		}

		private static MediaType GetMediaTypeFromFilename(string filename)
		{
			if (!String.IsNullOrEmpty(filename))
			{
				string ext = Path.GetExtension(filename).ToLower().Trim('.');
				if (ext == "jpg" || ext == "png" || ext == "bmp" || ext == "tif" || ext == "tiff" || ext == "jpeg" || ext == "gif")
					return MediaType.Image;
				if (ext == "mp3" || ext == "wav" || ext == "ogg" || ext == "m4a" || ext == "wma")
					return MediaType.Audio;
				if (ext == "mp4" || ext == "mkv" || ext == "avi" || ext == "mov" || ext == "m4v")
					return MediaType.Video;
			}
			return MediaType.Unknown;
		}

		public Question this[int index]
		{
			get { return m_questions[index]; }
		}

		public bool HasQuestion(int questionNumber)
		{
			return m_questions.ContainsKey(questionNumber);
		}

		public bool HasMediaFile(string filename)
		{
			return m_mediaPaths.ContainsKey(filename.ToLower());
		}

		public string GetMediaPath(string filename)
		{
			string path = null;
			if (filename != null)
				m_mediaPaths.TryGetValue(filename.ToLower(), out path);
			return path;
		}

		public IEnumerator GetEnumerator()
		{
			return m_questions.GetEnumerator();
		}

		internal Quiz(string iniPath)
		{
			string mediaPath = new FileInfo(iniPath).DirectoryName;
			if (Directory.Exists(mediaPath))
			{
				string[] files = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
				foreach (string file in files)
					if (!Directory.Exists(file))
						m_mediaPaths[Path.GetFileName(file).ToLower()] = file;
			}

			m_questions.Clear();
			IniFile quizIni = new IniFile(iniPath);
			for (int qNum = 1; ; ++qNum)
			{
				string numSection = "" + qNum;
				if (quizIni.KeyExists("Q", numSection))
				{
					string q = FixUnicode(quizIni.Read("Q", numSection).Trim());
					string a = FixUnicode(quizIni.Read("A", numSection).Trim());
					string aa = FixUnicode(quizIni.Read("AA", numSection).Trim());
					string w = FixUnicode(quizIni.Read("W", numSection).Trim());
					string n = FixUnicode(quizIni.Read("Almost", numSection).Trim());
					string qmed = quizIni.Read("QMed", numSection).ToLower().Trim();
					if (String.IsNullOrEmpty(qmed))
					{
						// Backwards compat.
						qmed = quizIni.Read("QAud", numSection).ToLower().Trim();
						if (String.IsNullOrEmpty(qmed))
							qmed = quizIni.Read("QPic", numSection).ToLower().Trim();
					}
					MediaType qmedType = GetMediaTypeFromFilename(qmed);
					string qsup = quizIni.Read("QSupMed", numSection).ToLower().Trim();
					if (String.IsNullOrEmpty(qsup))
					{
						// Backwards compat.
						// Can't have TWO images.
						qsup = qmedType != MediaType.Image ? quizIni.Read("QPic", numSection).ToLower().Trim() : null;
						if (String.IsNullOrEmpty(qsup))
							qsup = quizIni.Read("QBGM", numSection).ToLower().Trim();
					}
					MediaType qsupType = GetMediaTypeFromFilename(qsup);
					string apic = quizIni.Read("APic", numSection).ToLower().Trim();
					string info = FixUnicode(quizIni.Read("Info", numSection).Trim());
					string[] wArray = ParseDelimitedString(w);
					string[] aaArray = ParseDelimitedString(aa);
					string[] nArray = ParseDelimitedString(n);
					for (int f = 0; f < aaArray.Length; ++f)
						aaArray[f] = Answer.NormalizeAnswer(aaArray[f]);
					for (int f = 0; f < nArray.Length; ++f)
						nArray[f] = Answer.NormalizeAnswer(nArray[f]);
					List<string> allAnswers = new List<string>();
					allAnswers.AddRange(aaArray);
					allAnswers.Add(Answer.NormalizeAnswer(a));
					string useLevStr = quizIni.Read("Lev", numSection).ToLower().Trim();
					if (!bool.TryParse(useLevStr, out bool useLev))
						useLev = !allAnswers.Any(answerString => answerString.Length < 4 || int.TryParse(answerString, out int unusedInt));
					QuestionValidity validity = QuestionValidity.Valid;
					if ((!String.IsNullOrEmpty(qmed)) && (!m_mediaPaths.ContainsKey(qmed)))
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((!String.IsNullOrEmpty(qmed)) && qmedType == MediaType.Unknown)
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((!String.IsNullOrEmpty(qmed)) && qmedType == qsupType)
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((String.IsNullOrEmpty(q)) || (allAnswers.Count == 0))
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((!String.IsNullOrEmpty(qsup)) && (!m_mediaPaths.ContainsKey(qsup)))
						validity = QuestionValidity.MissingSupplementary;
					// Can't have supplementary video
					else if (qsupType == MediaType.Video)
						validity = QuestionValidity.MissingSupplementary;
					else if ((!String.IsNullOrEmpty(qsup)) && qsupType == MediaType.Unknown)
						validity = QuestionValidity.MissingSupplementary;
					else if ((!String.IsNullOrEmpty(apic)) && (!m_mediaPaths.ContainsKey(apic)))
						validity = QuestionValidity.MissingSupplementary;
					m_questions[qNum] = new Question(qNum, q, a, allAnswers.ToArray(), nArray, wArray, qmed, qmedType, qsup, qsupType, apic, info, useLev, validity);
				}
				else
					break;
			}
		}
		internal bool HasInvalidQuestions {
			get { return m_questions.Values.Any(q => q.Validity != QuestionValidity.Valid); }
		}
	}
}
