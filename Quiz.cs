using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ZoomQuiz
{
	public class Quiz:IEnumerable
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

		private int GetNextOrPreviousQuestion(int currentQuestion,bool getNext)
		{
			int next = currentQuestion;
			while (m_questions.ContainsKey(next+=getNext?1:-1))
				if (m_questions[next].IsValid)
					break;
			if (m_questions.ContainsKey(next) && m_questions[next].IsValid)
				return next;
			return -1;
		}

		internal int GetNextQuestionNumber(int currentQuestion)
		{
			return GetNextOrPreviousQuestion(currentQuestion, true);
		}

		internal int GetPrevQuestionNumber(int currentQuestion)
		{
			return GetNextOrPreviousQuestion(currentQuestion, false);
		}

		private static MediaType GetMediaTypeFromFilename(string filename)
		{
			if (!string.IsNullOrEmpty(filename))
			{
				string ext = Path.GetExtension(filename).ToLower().Trim('.');
				if (ext == "jpg" || ext == "png" || ext == "bmp" || ext == "tif" || ext == "tiff" || ext == "jpeg" || ext == "gif" || ext == "webp")
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

		public bool HasMediaFile(string filename)
		{
			return !string.IsNullOrEmpty(filename) && m_mediaPaths.ContainsKey(filename.ToLower());
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
			return m_questions.Values.GetEnumerator();
		}

		public int QuestionCount
		{
			get
			{
				return m_questions.Count;
			}
		}

		internal Quiz(string iniPath,IEnumerable<string> validSources)
		{
			string mediaPath = new FileInfo(iniPath).DirectoryName;
			if (Directory.Exists(mediaPath))
			{
				string[] files = Directory.GetFiles(mediaPath, "*.*",SearchOption.TopDirectoryOnly);
				foreach (string file in files)
					if (!Directory.Exists(file))
						if(GetMediaTypeFromFilename(file)!=MediaType.Unknown)
							m_mediaPaths[Path.GetFileName(file).ToLower()] = file;
			}
			m_questions.Clear();
			IniFile quizIni = new IniFile(iniPath);
			for (int qNum = 1; ; ++qNum)
			{
				string numSection = "" + qNum;
				if (quizIni.KeyExists("Q", numSection))
				{
					string q = quizIni.Read("Q", numSection).Trim().Replace("\\n", "\n");
					string a = quizIni.Read("A", numSection).Trim().Replace("\\n", "\n");
					string aa = quizIni.Read("AA", numSection).Trim().Replace("\\n", "\n");
					string w = quizIni.Read("W", numSection).Trim().Replace("\\n", "\n");
					string n = quizIni.Read("Almost", numSection).Trim().Replace("\\n", "\n");
					string qmed = quizIni.Read("QMed", numSection).ToLower().Trim();
					if (string.IsNullOrEmpty(qmed))
					{
						// Backwards compat.
						qmed = quizIni.Read("QAud", numSection).ToLower().Trim();
						if (string.IsNullOrEmpty(qmed))
							qmed = quizIni.Read("QPic", numSection).ToLower().Trim();
					}
					MediaType qmedType = GetMediaTypeFromFilename(qmed);
					string qsup = quizIni.Read("QSupMed", numSection).ToLower().Trim();
					if (string.IsNullOrEmpty(qsup))
					{
						// Backwards compat.
						// Can't have TWO images.
						qsup = qmedType != MediaType.Image ? quizIni.Read("QPic", numSection).ToLower().Trim() : null;
						if (string.IsNullOrEmpty(qsup))
							qsup = quizIni.Read("QBGM", numSection).ToLower().Trim();
					}
					MediaType qsupType = GetMediaTypeFromFilename(qsup);
					string apic = quizIni.Read("APic", numSection).ToLower().Trim();
					string info = quizIni.Read("Info", numSection).Trim().Replace("\\n", "\n");
					string[] wArray = ParseDelimitedString(w);
					string[] aaArray = ParseDelimitedString(aa);
					string[] nArray = ParseDelimitedString(n);
					for (int f = 0; f < aaArray.Length; ++f)
						aaArray[f] = Answer.NormalizeAnswer(aaArray[f]);
					for (int f = 0; f < nArray.Length; ++f)
						nArray[f] = Answer.NormalizeAnswer(nArray[f]);
					List<string> allAnswers = new List<string>();
					allAnswers.AddRange(aaArray);
					string normAnswer = Answer.NormalizeAnswer(a);
					if(!string.IsNullOrEmpty(normAnswer))
						allAnswers.Add(normAnswer);
					string useLevStr = quizIni.Read("Lev", numSection).ToLower().Trim();
					if (!bool.TryParse(useLevStr, out bool useLev))
						useLev = !allAnswers.Any(answerString => answerString.Length < 4 || int.TryParse(answerString, out int unusedInt));
					QuestionValidity validity = QuestionValidity.Valid;
					if ((!string.IsNullOrEmpty(qmed)) && (!m_mediaPaths.ContainsKey(qmed)))
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((!string.IsNullOrEmpty(qmed)) && qmedType == qsupType)
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((string.IsNullOrEmpty(q)) || (allAnswers.Count == 0))
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((!string.IsNullOrEmpty(qsup)) && (!m_mediaPaths.ContainsKey(qsup)))
						validity = QuestionValidity.MissingSupplementary;
					// Can't have supplementary video
					else if (qsupType == MediaType.Video)
						validity = QuestionValidity.MissingSupplementary;
					else if ((!string.IsNullOrEmpty(apic)) && (!m_mediaPaths.ContainsKey(apic)))
						validity = QuestionValidity.MissingSupplementary;
					MediaType amedType = GetMediaTypeFromFilename(apic);
					string obsSources = quizIni.Read("OBSSources", numSection);
					List<string> obsSourcesOn = new List<string>();
					List<string> obsSourcesOff = new List<string>();
					ParseOBSSources(obsSources,obsSourcesOn,obsSourcesOff);
					if (validSources.Intersect(obsSourcesOn).Count() != obsSourcesOn.Count)
						validity = QuestionValidity.MissingSource;
					if (validSources.Intersect(obsSourcesOff).Count() != obsSourcesOff.Count)
						validity = QuestionValidity.MissingSource;
					m_questions[qNum] = new Question(qNum, q, a, allAnswers.ToArray(), nArray, wArray, qmed, qmedType, qsup, qsupType, apic, amedType, info, useLev, validity,obsSourcesOn,obsSourcesOff);
				}
				else
					break;
			}
		}
		internal bool HasInvalidQuestions {
			get { return m_questions.Values.Any(q => q.Validity != QuestionValidity.Valid); }
		}
		private static void ParseOBSSources(string obsSources,List<string> sourcesOn,List<string> sourcesOff)
		{
			if (!string.IsNullOrEmpty(obsSources))
			{
				obsSources = obsSources.Trim();
				string[] sourceBits = obsSources.Split(',');
				foreach (string sourceBit in sourceBits)
				{
					string sourceBitTrimmed = sourceBit.Trim();
					bool off = sourceBitTrimmed.StartsWith("-");
					sourceBitTrimmed = sourceBitTrimmed.Trim('-', '+').Trim();
					(off ? sourcesOff : sourcesOn).Add(sourceBitTrimmed);
				}
			}
		}
	}
}
