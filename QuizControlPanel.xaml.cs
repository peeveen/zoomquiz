using System;
using System.Collections.Generic;
using System.Windows;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using ZOOM_SDK_DOTNET_WRAP;
using System.ComponentModel;
using System.Linq;
using System.IO;
using Microsoft.Win32;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Data;

namespace ZoomQuiz
{
	public class Scores
	{
		public const int CORRECT_ANSWER_SCORE = 2;
		public const int ALMOST_CORRECT_ANSWER_SCORE = 1;
	}

	public enum ChatMode
	{
		NoOne = 0,
		HostOnly = 1,
		EveryonePublicly = 2,
		EveryonePubliclyAndPrivately = 3
	}

	public enum AnswerResult
	{
		Correct=0,
		AlmostCorrect=1,
		Wrong=2,
		Funny=3,
		NotAnAnswer=4,
		Unmarked=99
	}

	public enum QuestionValidity
	{
		Valid=0,
		MissingQuestionOrAnswer=1,
		MissingSupplementary=2
	}

	public class Question
	{
		public string QuestionText { get; private set; }
		public string AnswerText { get; private set; }
		public string QuestionImageFilename { get; private set; }
		public string AnswerImageFilename { get; private set; }
		public string QuestionAudioFilename { get; private set; }
		public string QuestionBGMFilename { get; private set; }
		public string[] QuestionAnswers { get; private set; }
		public string[] QuestionAlmostAnswers { get; private set; }
		public string[] QuestionWrongAnswers { get; private set; }
		public string Info { get; private set; }
		public int QuestionNumber { get; private set; }
		public QuestionValidity Validity { get; private set; }
		public Question(int number,string questionText,string answerText,string[] answers,string[] almostAnswers, string[] wrongAnswers,string questionImageFile, string answerImageFile, string audioFile,string bgmAudio,string info,QuestionValidity validity)
		{
			QuestionNumber=number;
			QuestionText = questionText.Trim();
			AnswerText = answerText.Trim();
			QuestionAnswers = answers;
			QuestionWrongAnswers = wrongAnswers;
			QuestionAlmostAnswers = almostAnswers;
			QuestionImageFilename = questionImageFile.Trim();
			QuestionBGMFilename = bgmAudio.Trim();
			AnswerImageFilename = answerImageFile.Trim();
			QuestionAudioFilename = audioFile.Trim();
			Validity = validity;
			Info = info.Trim();
		}
	}

	public class Answer
	{
		public string AnswerText { get; private set; }
		public string NormalizedAnswer { get; private set; }
		public AnswerResult AnswerResult { get; set; }
		public DateTime AnswerTime { get; }
		public bool IsAcceptedAnswer
		{
			get
			{
				return AnswerResult == AnswerResult.Correct || AnswerResult == AnswerResult.AlmostCorrect || AnswerResult == AnswerResult.Wrong;
			}
		}
		public Answer(string answer)
		{
			AnswerTime = DateTime.Now;
			AnswerText = answer;
			AnswerResult= AnswerResult.Unmarked;
			NormalizedAnswer = NormalizeAnswer(answer);
		}
		public static string NormalizeAnswer(string answer)
		{
			// first of all remove everything that isn't a space, a letter or a number
			string norm = "";
			foreach (char c in answer)
				if (Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c))
					norm += c;
			// Trim leading/trailing whitespace.
			norm = norm.Trim();
			// Now change any double spaces into single spaces.
			string oldNorm;
			do
			{
				oldNorm = norm;
				norm = norm.Replace("  ", " ");
			} while (oldNorm != norm);
			// Now convert the entire thing to lowercase
			norm = norm.ToLower();
			// now replace accented chars with simpler ones.
			byte[] tempBytes = System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(norm);
			norm = System.Text.Encoding.UTF8.GetString(tempBytes);
			// Now, if it starts with "the", remove it.
			norm = norm.Trim();
			if (norm.StartsWith("the ") && norm.Length>4)
				norm = norm.Substring(4);
			return norm;
		}
	}

	public class Contestant
	{
		public string Name { get; private set; }
		public uint ID { get; private set; }
		public Contestant(uint id,string name)
		{
			ID = id;
			Name = name;
		}
		public override bool Equals(object obj)
		{
			if(obj is Contestant)
			{
				Contestant c2 = (Contestant)obj;
				// ID is NOT constant between join/leave.
				return c2.Name == Name;// && c2.ID == ID;
			}
			return false;
		}
		public override int GetHashCode()
		{
			return Name.GetHashCode();// + ID.GetHashCode();
		}
	}

	public class AnswerForMarking
	{
		public Answer Answer { get; private set; }
		public Contestant Contestant { get; private set; }
		public AnswerForMarking(Contestant contestant,Answer answer)
		{
			Answer = answer;
			Contestant = contestant;
		}
	}

	public class MarkingProgress
	{
		public int AnswersReceived { get; private set; }
		public int AnswersMarked { get; private set; }
		public MarkingProgress(int received,int marked)
		{
			AnswersReceived = received;
			AnswersMarked = marked;
		}
	}

	public class ContestantScore
	{
		public Contestant Contestant { get; private set; }
		public string Name { get; private set; }
		public int Score { get; private set; }
		public bool Joint { get; private set; }
		public int Position { get; private set; }
		public string LastScoreString { get; private set; }
		public AnswerResult LastResult { get; private set; }
		public string PositionString
		{
			get
			{
				return "" + Position + (Joint ? "=" : "");
			}
		}
		public string LastScore
		{
			get
			{
				return "" + Position + (Joint ? "=" : "");
			}
		}
		public ContestantScore(int position, bool joint, int score, Contestant contestant, AnswerResult lastResult)
		{
			Joint = joint;
			Name = contestant.Name;
			Contestant = contestant;
			Position = position;
			Score = score;
			LastScoreString = GetLastScoreString(lastResult);
			LastResult = lastResult;
		}
		private string GetLastScoreString(AnswerResult result)
		{
			if (result == AnswerResult.Correct)
				return "+" + Scores.CORRECT_ANSWER_SCORE;
			else if (result == AnswerResult.AlmostCorrect)
				return "+" + Scores.ALMOST_CORRECT_ANSWER_SCORE;
			return "";
		}
	}

	public class AnswerBin
	{
		private Mutex m_answersMutex = new Mutex();
		private HashSet<string> m_answers=new HashSet<string>();
		public AnswerBin()
		{

		}
		~AnswerBin()
		{
			m_answersMutex.Dispose();
		}
		public void Add(Answer answer)
		{
			m_answersMutex.WaitOne();
			m_answers.Add(answer.NormalizedAnswer);
			m_answersMutex.ReleaseMutex();
		}
		public bool Contains(Answer answer)
		{
			try
			{
				m_answersMutex.WaitOne();
				return m_answers.Contains(answer.NormalizedAnswer);
			}
			finally
			{
				m_answersMutex.ReleaseMutex();
			}
		}
	}

	public class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable
	{
		public int Compare(TKey x, TKey y)
		{
			int result = x.CompareTo(y);
			if (result == 0)
				return 1;   // Handle equality as being greater
			else
				return -result;
		}
	}

	/// <summary>
	/// Interaction logic for QuizControlPanel.xaml
	/// </summary>
	public partial class QuizControlPanel : Window
	{
		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll", SetLastError = true)]
		static extern bool BringWindowToTop(IntPtr hWnd);
		[DllImport("user32.dll", SetLastError = true)]
		static extern IntPtr SetFocus(IntPtr hWnd);
		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr GetExtraMessageInfo();
		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);
		[DllImport("USER32.DLL")]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		private const int COUNTDOWN_SECONDS = 15;
		private const string SCORES_FILENAME = "scores.txt";
		private const string ANSWERS_FILENAME = "answers.txt";
		private const string QUIZ_FILENAME = "quiz.ini";
		private const string ZoomQuizTitle = "ZoomQuiz";
		private readonly System.Drawing.Size QUESTION_SIZE = new System.Drawing.Size(1600, 360);
		private readonly System.Drawing.Size ANSWER_SIZE = new System.Drawing.Size(1600, 360);
		private readonly System.Drawing.Size LEADERBOARD_SIZE = new System.Drawing.Size(1860, 1000);
		private const int TEXT_OUTLINE_THICKNESS = 5;
		private const string QUESTION_FONT_NAME = "Impact";
		private const string LEADERBOARD_FONT_NAME = "Bahnschrift SemiBold SemiCondensed";
		private const float AUDIO_VOLUME = 0.8f;
		private const float BGM_VOLUME = 0.05f;

		private bool m_quizEnded = false;
		private bool m_countdownActive = false;
		private bool m_questionShowing = false;
		private bool m_answerShowing = false;
		private bool m_leaderboardShowing = false;
		private bool m_fullScreenPictureShowing = false;
		private readonly BackgroundWorker countdownWorker = new BackgroundWorker();
		private readonly BackgroundWorker answerCounter = new BackgroundWorker();
		private readonly BackgroundWorker markingPump = new BackgroundWorker();
		private readonly BackgroundWorker faderWorker = new BackgroundWorker();
		private uint m_myID = 0;
		private bool m_presenting = false;
		private Dictionary<Contestant, List<Answer>> m_answers = new Dictionary<Contestant, List<Answer>>();
		private Mutex m_obsMutex = new Mutex();
		private Mutex m_volumeMutex = new Mutex();
		private Mutex m_answerListMutex = new Mutex();
		private Mutex m_answerFileMutex = new Mutex();
		private AnswerForMarking m_answerForMarking = null;
		private Mutex m_answerForMarkingMutex = new Mutex();
		private AutoResetEvent m_answerReceivedEvent1 = new AutoResetEvent(false);
		private AutoResetEvent m_answerReceivedEvent2 = new AutoResetEvent(false);
		private AutoResetEvent m_answerMarkedEvent = new AutoResetEvent(false);
		private ManualResetEvent m_countdownCompleteEvent = new ManualResetEvent(true);
		private ManualResetEvent m_quitAppEvent = new ManualResetEvent(false);
		private Dictionary<Contestant, int> m_scores = new Dictionary<Contestant, int>();
		private Dictionary<Contestant, AnswerResult> m_lastAnswerResults = new Dictionary<Contestant, AnswerResult>();
		private Dictionary<AnswerResult, AnswerBin> m_answerBins = new Dictionary<AnswerResult, AnswerBin>();
		private OBSWebsocket m_obs= new OBSWebsocket();
		private Dictionary<int, Question> m_quiz = new Dictionary<int, Question>();
		private int m_nextQuestion = 1;
		private Dictionary<string, string> m_mediaPaths = new Dictionary<string, string>();
		private Question m_currentQuestion = null;
		private float m_bgmVolume = BGM_VOLUME;
		private float m_questionBGMVolume = 0;
		private float m_questionAudioVolume = 0;
		private bool m_scoresDirty = true;
		public bool StartedOK { get; private set; }
		private bool m_timeWarnings = false;
		private bool m_chatWarnings = false;

		private bool PresentationOnly { get; set; }

		public QuizControlPanel(bool presentationOnly)
		{
			PresentationOnly = presentationOnly;
			StartedOK = false;
			InitializeComponent();
			ReadScoresFromFile();
			countdownWorker.DoWork += countdownWorker_DoWork;
			countdownWorker.ProgressChanged += countdownWorker_ProgressChanged;
			countdownWorker.WorkerReportsProgress = true;
			countdownWorker.RunWorkerCompleted += countdownWorker_RunWorkerCompleted;
			markingPump.DoWork += markingPump_DoWork;
			markingPump.ProgressChanged += markingPump_ProgressChanged;
			markingPump.WorkerReportsProgress = true;
			markingPump.RunWorkerCompleted += markingPump_RunWorkerCompleted;
			answerCounter.DoWork += answerCounter_DoWork;
			answerCounter.ProgressChanged += answerCounter_ProgressChanged;
			answerCounter.WorkerReportsProgress = true;
			faderWorker.DoWork += FaderWorker_DoWork;
			ClearLeaderboards();
			UpdateLeaderboard(true);
			try
			{
				m_obs.Connect("ws://127.0.0.1:4444", "");
				if (m_obs.IsConnected)
				{
					SetOBSScene("CamScene");
					//File.Delete(Path.Combine(Directory.GetCurrentDirectory(), ANSWERS_FILENAME));
					//ScanMediaPath();
					SetCountdownMedia();
					SetBGMShuffle();
					SetLeaderboardsPath();
					//LoadQuiz();
					faderWorker.RunWorkerAsync();
					StartedOK = true;
				}
				else
					MessageBox.Show("Could not connect to OBS (is it running?).", ZoomQuizTitle);
			}
			catch (AuthFailureException)
			{
				MessageBox.Show("Failed to connect to OBS (authentication failed).", ZoomQuizTitle);
				EndQuiz();
			}
			catch (ErrorResponseException ex)
			{
				MessageBox.Show("Failed to connect to OBS (" + ex.Message + ").", ZoomQuizTitle);
				EndQuiz();
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to connect to OBS (" + ex.Message + ").", ZoomQuizTitle);
				EndQuiz();
			}
		}

		private void ClearLeaderboards()
		{
			string lbFolder = Path.Combine(Directory.GetCurrentDirectory(), "leaderboards");
			string[] files=Directory.GetFiles(lbFolder);
			foreach(string file in files)
				if (File.Exists(file))
					File.Delete(file);
		}

		private void SetLeaderboardsPath()
		{
			string lbFolder = Path.Combine(Directory.GetCurrentDirectory(), "leaderboards");
			SourceSettings lbSourceSettings = m_obs.GetSourceSettings("Leaderboard");
			JObject lbSettings = lbSourceSettings.sourceSettings;
			lbSettings["files"][0]["value"] = lbFolder;
			m_obs.SetSourceSettings("Leaderboard", lbSettings);
		}

		private void SetCountdownMedia()
		{
			string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
			string mp4Path = Path.Combine(presFolder, "Countdown.mp4");
			string maskPath = Path.Combine(presFolder, "circle.png");
			SetOBSFileSourceFromPath("Countdown", "local_file", mp4Path);
			List<FilterSettings> filters=m_obs.GetSourceFilters("Countdown");
			foreach(FilterSettings st in filters)
			{
				if(st.Name.Contains("Image Mask"))
				{
					JObject maskSettings = st.Settings;
					maskSettings["image_path"] = maskPath;
					m_obs.SetSourceFilterSettings("Countdown", st.Name, maskSettings);
				}
			}
		}

		private void FaderWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			const float bgmVolSpeed = 0.01f;
			const float qbgmVolSpeed = 0.01f;
			const float qaudVolSpeed = 0.04f;

			while (!m_quitAppEvent.WaitOne(100))
			{
				try
				{
					m_volumeMutex.WaitOne();
					try
					{
						m_obsMutex.WaitOne();
						VolumeInfo bgmVolInf = m_obs.GetVolume("BGM");
						VolumeInfo qbgmVolInf = m_obs.GetVolume("QuestionBGM");
						VolumeInfo qaVolInf = m_obs.GetVolume("QuestionAudio");
						float nBGMVol = bgmVolInf.Volume;
						float nQBGMVol = qbgmVolInf.Volume;
						float nQAVol = qaVolInf.Volume;
						float diff = nBGMVol - m_bgmVolume;
						if (diff < -bgmVolSpeed)
							m_obs.SetVolume("BGM", nBGMVol + bgmVolSpeed);
						else if (diff > bgmVolSpeed)
							m_obs.SetVolume("BGM", nBGMVol - bgmVolSpeed);
						else if (nBGMVol != m_bgmVolume)
							m_obs.SetVolume("BGM", m_bgmVolume);
						diff = nQBGMVol - m_questionBGMVolume;
						if (diff < -qbgmVolSpeed)
							m_obs.SetVolume("QuestionBGM", nQBGMVol + qbgmVolSpeed);
						else if (diff > qbgmVolSpeed)
							m_obs.SetVolume("QuestionBGM", nQBGMVol - qbgmVolSpeed);
						else if (nQBGMVol != m_questionBGMVolume)
							m_obs.SetVolume("QuestionBGM", m_questionBGMVolume);
						diff = nQAVol - m_questionAudioVolume;
						if (diff > qaudVolSpeed)
							m_obs.SetVolume("QuestionAudio", nQAVol - qaudVolSpeed);
						else if (nQAVol != m_questionAudioVolume)
							m_obs.SetVolume("QuestionAudio", m_questionAudioVolume);
					}
					finally
					{
						m_obsMutex.ReleaseMutex();
					}
				}
				finally
				{
					m_volumeMutex.ReleaseMutex();
				}
			}
		}

		private void SetBGMShuffle()
		{
			string mediaPath = Path.Combine(Directory.GetCurrentDirectory(), "bgm");
			SourceSettings bgmSettings = m_obs.GetSourceSettings("BGM");
			JObject bgmSourceSettings = bgmSettings.sourceSettings;
			bgmSourceSettings["loop"] = true;
			bgmSourceSettings["shuffle"] = true;
			bgmSourceSettings["playlist"][0]["value"] = mediaPath;
			m_obs.SetSourceSettings("BGM", bgmSourceSettings);
		}

		private string FixUnicode(string strIn)
		{
			strIn=strIn.Replace("Â£", "£");
			return strIn;
		}

		private void LoadQuiz(string quizFilePath)
		{
			string[] ParseDelimitedString(string s)
			{
				if (!String.IsNullOrEmpty(s))
				{
					string[] bits = s.Split(',');
					for (int f = 0; f < bits.Length; ++f)
						bits[f] = bits[f].Trim();
					return bits;
				}
				return new string[0];
			}

			m_mediaPaths.Clear();
			string mediaPath = new FileInfo(quizFilePath).DirectoryName;
			if (Directory.Exists(mediaPath))
			{
				string[] files = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
				foreach (string file in files)
					if (!Directory.Exists(file))
						m_mediaPaths[Path.GetFileName(file).ToLower()] = file;
			}

			m_quiz.Clear();
			IniFile quizIni = new IniFile(quizFilePath);
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
					string qpic = quizIni.Read("QPic", numSection).ToLower().Trim();
					string apic = quizIni.Read("APic", numSection).ToLower().Trim();
					string qaud = quizIni.Read("QAud", numSection).ToLower().Trim();
					string qbgm = quizIni.Read("QBGM", numSection).ToLower().Trim();
					string info = FixUnicode(quizIni.Read("Info", numSection).Trim());
					string[] aArray = ParseDelimitedString(a);
					string[] wArray = ParseDelimitedString(w);
					string[] aaArray = ParseDelimitedString(aa);
					string[] nArray = ParseDelimitedString(n);
					for (int f = 0; f < aArray.Length; ++f)
						aArray[f] = Answer.NormalizeAnswer(aArray[f]);
					for (int f = 0; f < aaArray.Length; ++f)
						aaArray[f] = Answer.NormalizeAnswer(aaArray[f]);
					for (int f = 0; f < nArray.Length; ++f)
						nArray[f] = Answer.NormalizeAnswer(nArray[f]);
					List<string> allAnswers = new List<string>();
					allAnswers.AddRange(aArray);
					allAnswers.AddRange(aaArray);
					QuestionValidity validity = QuestionValidity.Valid;
					if ((!String.IsNullOrEmpty(qaud)) && (!m_mediaPaths.ContainsKey(qaud)))
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((String.IsNullOrEmpty(q)) || (allAnswers.Count == 0))
						validity = QuestionValidity.MissingQuestionOrAnswer;
					else if ((!String.IsNullOrEmpty(qpic)) && (!m_mediaPaths.ContainsKey(qpic)))
						validity = QuestionValidity.MissingSupplementary;
					else if ((!String.IsNullOrEmpty(apic)) && (!m_mediaPaths.ContainsKey(apic)))
						validity = QuestionValidity.MissingSupplementary;
					else if ((!String.IsNullOrEmpty(qbgm)) && (!m_mediaPaths.ContainsKey(qbgm)))
						validity = QuestionValidity.MissingSupplementary;
					m_quiz[qNum] = new Question(qNum, q, a, allAnswers.ToArray(), nArray, wArray, qpic, apic, qaud, qbgm, info, validity);
				}
				else
					break;
			}
			UpdateQuizList();
			if (m_quiz.Values.Any(q => q.Validity != QuestionValidity.Valid))
				MessageBox.Show("Warning: invalid questions found.", ZoomQuizTitle);
			m_nextQuestion = 0;
			NextQuestion(m_nextQuestion);
			skipQuestionButton.IsEnabled=newQuestionButton.IsEnabled = m_nextQuestion != -1;
		}

		private void UpdateQuizList()
		{
			quizList.ItemsSource = m_quiz.Values;
			ICollectionView view = CollectionViewSource.GetDefaultView(quizList.ItemsSource);
			view.Refresh();
			quizList.SelectedIndex = 0;
			quizList.ScrollIntoView(m_quiz[1]);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (!PresentationOnly && !m_quizEnded)
				e.Cancel = true;
			else
				m_quitAppEvent.Set();
		}

		public void EndQuiz()
		{
			m_quizEnded = true;
			Close();
			try
			{
				m_obsMutex.WaitOne();
				if(m_obs.IsConnected)
					m_obs.Disconnect();
			}
			finally
			{
				m_obsMutex.ReleaseMutex();
			}
		}

		public void StartQuiz()
		{
			if (!PresentationOnly)
			{
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().Add_CB_onChatStatusChangedNotification(OnChatStatusChanged);
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingWaitingRoomController().EnableWaitingRoomOnEntry(false);

				IMeetingParticipantsControllerDotNetWrap partCon = ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingParticipantsController();
				uint[] participantIDs = partCon.GetParticipantsList();
				for (int f = 0; f < participantIDs.Length; ++f)
				{
					IUserInfoDotNetWrap user = partCon.GetUserByUserID(participantIDs[f]);
					if (user.IsMySelf())
					{
						m_myID = participantIDs[f];
						break;
					}
				}

				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingAudioController().JoinVoip();
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "🥂 Welcome to the quiz!");

				ZOOM_SDK_DOTNET_WRAP.ShowChatDlgParam showDlgParam = new ZOOM_SDK_DOTNET_WRAP.ShowChatDlgParam();
				showDlgParam.rect = new System.Drawing.Rectangle(10, 10, 200, 200);
				ValueType dlgParam = showDlgParam;
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetUIController().ShowChatDlg(ref dlgParam);

				SetChatMode(ChatMode.EveryonePubliclyAndPrivately);
			}
			Show();
		}

		private void ResetAnswerBins(Question currentQuestion)
		{
			var values = Enum.GetValues(typeof(AnswerResult));
			m_answerBins.Clear();
			foreach(AnswerResult result in values)
				m_answerBins[result] = new AnswerBin();
			AnswerBin correctAnswers = m_answerBins[AnswerResult.Correct];
			AnswerBin almostCorrectAnswers = m_answerBins[AnswerResult.AlmostCorrect];
			AnswerBin wrongAnswers = m_answerBins[AnswerResult.Wrong];
			currentQuestion.QuestionAnswers.Select(a => new Answer(a)).ToList().ForEach(a => correctAnswers.Add(a));
			currentQuestion.QuestionAlmostAnswers.Select(a => new Answer(a)).ToList().ForEach(a => almostCorrectAnswers.Add(a));
			currentQuestion.QuestionWrongAnswers.Select(a => new Answer(a)).ToList().ForEach(a => wrongAnswers.Add(a));
		}

		private void SetChatMode(ChatMode mode)
		{
			if (!PresentationOnly)
			{
				IntPtr hChatWnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "ZPConfChatWndClass", "Zoom Group Chat");
				if (hChatWnd != IntPtr.Zero)
				{
					SetForegroundWindow(hChatWnd);
					Keyboard kb = new Keyboard();

					// Three down for HostOnly, four for Public chat
					for (int f = 0; f < (int)mode + 3; ++f)
						kb.Send(Keyboard.VirtualKeyShort.DOWN);
					kb.Send(Keyboard.VirtualKeyShort.RETURN);
				}
				else
					MessageBox.Show("Can't find chat window (it must be separated from the main app).");
			}
		}

		public void OnChatStatusChanged(ValueType status)
		{
			if (!PresentationOnly)
			{
				IntPtr hwnd = new WindowInteropHelper(this).Handle;
				SetForegroundWindow(hwnd);
				BringWindowToTop(hwnd);
			}
		}

		private void AddAnswer(Contestant contestant,Answer answer)
		{
			try
			{
				m_answerListMutex.WaitOne();
				List<Answer> answerList = null;
				m_answers.TryGetValue(contestant, out answerList);
				if (answerList == null)
					answerList = new List<Answer>();
				answerList.Add(answer);
				m_answers[contestant] = answerList;
			}
			finally
			{
				m_answerListMutex.ReleaseMutex();
			}

			m_answerReceivedEvent1.Set();
			m_answerReceivedEvent2.Set();
		}

		public void OnAnswerReceived(IChatMsgInfoDotNetWrap chatMsg)
		{
			uint senderID = chatMsg.GetSenderUserId();
			if (senderID != m_myID)
			{
				string sender = chatMsg.GetSenderDisplayName();
				string answer = chatMsg.GetContent();
				AddAnswer(new Contestant(senderID, sender), new Answer(answer));
			}
		}

		private void StartPresenting()
		{
			if (!PresentationOnly)
			{
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingVideoController().SpotlightVideo(true, m_myID);
				if (muteDuringQuestions.IsChecked == true)
					ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingAudioController().MuteAudio(0, false);
				presentingText.Text = "Stop Presenting";
				presentingButton.Background = System.Windows.Media.Brushes.Pink;
				m_presenting = true;
			}
		}

		private void StopPresenting()
		{
			if (!PresentationOnly)
			{
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingVideoController().SpotlightVideo(false, m_myID);
				if (muteDuringQuestions.IsChecked == true)
					ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingAudioController().MuteAudio(0, true);
				m_presenting = false;
				presentingText.Text = "Start Presenting";
				presentingButton.Background = System.Windows.Media.Brushes.LightGreen;
				presentingButton.IsEnabled = true;
			}
		}

		private void StartQuestionButtonClick(object sender, RoutedEventArgs e)
		{
			m_answers = new Dictionary<Contestant, List<Answer>>();
			HideAnswer();
			skipQuestionButton.IsEnabled = false;
			m_currentQuestion = m_quiz[m_nextQuestion];
			questionTextBox.Text = m_currentQuestion.QuestionText;
			answerTextBox.Text = m_currentQuestion.AnswerText;
			infoTextBox.Text = m_currentQuestion.Info;
			ResetAnswerBins(m_currentQuestion);
			GenerateTextImage(m_currentQuestion.QuestionText, "QuestionText", QUESTION_SIZE, "question.png");
			SetOBSImageSource("QuestionPic", m_currentQuestion.QuestionImageFilename);
			SetOBSAudioSource("QuestionBGM", m_currentQuestion.QuestionBGMFilename);
			SetVolumes(false, m_currentQuestion);
			SetChatMode(ChatMode.HostOnly);
			StartPresenting();
			HideFullScreenPicture(false);
			showPictureButton.IsEnabled = false;
			replayAudioButton.IsEnabled = false;
			showAnswerButton.IsEnabled = presentingButton.IsEnabled = false;
			markingProgressBar.Maximum = 1;
			markingProgressBar.Value = 0;
			markingProgressText.Text = "";
			loadQuizButton.IsEnabled = newQuestionButton.IsEnabled = showLeaderboardButton.IsEnabled = false;
			showQuestionButton.IsEnabled = true;
			if (!PresentationOnly)
			{
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "✏️ Here comes the next question ...");
			}
		}

		private void countdownWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			for (int f = COUNTDOWN_SECONDS; f > 0; --f)
			{
				Thread.Sleep(1000);
				if (((f % 5) == 0) && (f != COUNTDOWN_SECONDS))
					countdownWorker.ReportProgress(f);
			}
		}

		private void answerCounter_DoWork(object sender, DoWorkEventArgs e)
		{
			WaitHandle[] waitEvents = new WaitHandle[] { m_answerReceivedEvent2, m_countdownCompleteEvent,m_quitAppEvent };
			for(; ; )
			{
				int result=WaitHandle.WaitAny(waitEvents);
				if (result >0)
					break;
				int answerCount = 0;
				int markedAnswerCount = 0;
				try
				{
					m_answerListMutex.WaitOne();
					answerCount = m_answers.Sum(kvp2 => kvp2.Value.Count());
					markedAnswerCount = m_answers.Sum(kvp2 => kvp2.Value.Count(a => a.AnswerResult != AnswerResult.Unmarked));
				}
				finally
				{
					m_answerListMutex.ReleaseMutex();
				}

				answerCounter.ReportProgress(100, new MarkingProgress(answerCount,markedAnswerCount));
			}
		}

		private void answerCounter_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			UpdateMarkingProgressUI((MarkingProgress)e.UserState);
		}

		private int GetNextQuestionNumber(int currentQuestion)
		{
			int next= currentQuestion;
			while (m_quiz.ContainsKey(++next))
				if (m_quiz[next].Validity!=QuestionValidity.MissingQuestionOrAnswer)
					break;
			if (!m_quiz.ContainsKey(next))
				next = -1;
			return next;
		}

		private int NextQuestion(int currentQuestion)
		{
			m_nextQuestion = GetNextQuestionNumber(currentQuestion);
			if (m_nextQuestion > 0)
			{
				startQuestionButtonText.Text = "Start Question " + m_nextQuestion;
				quizList.SelectedIndex = m_nextQuestion - 1;
				quizList.ScrollIntoView(quizList.Items.GetItemAt(m_nextQuestion - 1));
			}
			else
			{
				skipQuestionButton.IsEnabled = newQuestionButton.IsEnabled = false;
				loadQuizButton.IsEnabled = true;
			}
			return m_nextQuestion;
		}

		private void countdownWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if(m_timeWarnings)
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "⌛ Time is up!");
			if (m_chatWarnings)
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "💬 Public chat is ON");
			m_countdownCompleteEvent.Set();
			m_countdownActive = false;
			ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().Remove_CB_onChatMsgNotifcation(OnAnswerReceived);
			NextQuestion(m_nextQuestion);
			SetChatMode(ChatMode.EveryonePublicly);
			try
			{
				m_answerForMarkingMutex.WaitOne();
				presentingButton.IsEnabled = m_answerForMarking == null;
			}
			finally
			{
				m_answerForMarkingMutex.ReleaseMutex();
			}
		}

		private void countdownWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (m_timeWarnings)
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "⏳ " + e.ProgressPercentage + " seconds remaining ...");
		}

		private void MarkAnswer(AnswerForMarking answer,AnswerResult result)
		{
			answer.Answer.AnswerResult = result;
			AnswerBin bin = m_answerBins[result];
			if (bin != null)
				bin.Add(answer.Answer);
			if (result == AnswerResult.Funny)
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "😂 Answer from "+answer.Contestant.Name+": \""+answer.Answer.AnswerText+"\"");
			else if (result != AnswerResult.NotAnAnswer)
				// Once a valid answer is accepted (right or wrong), all other answers from that user cannot be considered.
				MarkUserAnswers(answer.Contestant, AnswerResult.NotAnAnswer);
		}

		private bool AutoMarkAnswer(AnswerForMarking answer)
		{
			// If user has already submitted an answer that was accepted, don't accept this new one as an answer.
			if (m_answers.ContainsKey(answer.Contestant))
			{
				List<Answer> contestantAnswers = m_answers[answer.Contestant];
				if (contestantAnswers.Any(a => a.IsAcceptedAnswer))
				{
					MarkAnswer(answer, AnswerResult.NotAnAnswer);
					return true;
				}
			}
			// Otherwise, if the user has submitted an answer that has already been marked, use that marking.
			foreach (KeyValuePair<AnswerResult, AnswerBin> kvp in m_answerBins)
				if (kvp.Value.Contains(answer.Answer))
				{
					MarkAnswer(answer, kvp.Key);
					return true;
				}
			// Otherwise, no, have to do it manually.
			return false;
		}

		private void markingPump_DoWork(object sender, DoWorkEventArgs e)
		{
			void UpdateMarkingProgress(AnswerForMarking nextAnswerForMarking = null)
			{
				int answerCount = m_answers.Sum(kvp2 => kvp2.Value.Count());
				int markedAnswerCount = m_answers.Sum(kvp2 => kvp2.Value.Count(a => a.AnswerResult != AnswerResult.Unmarked));
				markingPump.ReportProgress((int)((double)markedAnswerCount / answerCount) * 100, new MarkingProgress(answerCount, markedAnswerCount));
			}
			void SetAnswerForMarking(AnswerForMarking nextAnswerForMarking)
			{
				markingPump.ReportProgress(0, nextAnswerForMarking);
			}
			bool waitingForMarking = false;
			for(; ;)
			{
				WaitHandle[] events = waitingForMarking? new WaitHandle[] { m_answerMarkedEvent, m_quitAppEvent }:new WaitHandle[] { m_answerReceivedEvent1, m_quitAppEvent,m_countdownCompleteEvent };
				waitingForMarking = false;
				int result = WaitHandle.WaitAny(events);
				if (result == 1)
					break;
				try
				{
					m_answerListMutex.WaitOne();
					foreach (KeyValuePair<Contestant, List<Answer>> kvp in m_answers)
					{
						Answer unmarkedAnswer = kvp.Value.FirstOrDefault(a => a.AnswerResult == AnswerResult.Unmarked);
						if (unmarkedAnswer != null)
						{
							AnswerForMarking answerForMarking = new AnswerForMarking(kvp.Key, unmarkedAnswer);
							if (!AutoMarkAnswer(answerForMarking))
							{
								waitingForMarking = true;
								SetAnswerForMarking(answerForMarking);
							}
							else
								UpdateMarkingProgress();
						}
						else
							UpdateMarkingProgress();
					}
					if (waitingForMarking)
						continue;
					if (m_countdownCompleteEvent.WaitOne(0))
						// Nothing left to mark, and no more answers incoming? We're done.
						break;
				}
				finally
				{
					m_answerListMutex.ReleaseMutex();
				}
				if (result == 2)
					break;
			}
			UpdateMarkingProgress();
		}

		private void ReadScoresFromFile()
		{
			string dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "data");
			string scoresFilePath = Path.Combine(dataFolder, SCORES_FILENAME);
			if (File.Exists(scoresFilePath))
			{
				using (StreamReader sr = File.OpenText(scoresFilePath))
				{
					string strLine;
					while ((strLine = sr.ReadLine()) != null)
					{
						string[] bits = strLine.Split('\t');
						if (bits.Length == 3)
						{
							Contestant c = new Contestant(UInt32.Parse(bits[0]), bits[1]);
							int score = Int32.Parse(bits[2]);
							m_scores[c] = score;
						}
					}
				}
			}
		}

		private void WriteScoresToFile()
		{
			string dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "data");
			string scoresFilePath = Path.Combine(dataFolder, SCORES_FILENAME);
			if (File.Exists(scoresFilePath))
				File.Delete(scoresFilePath);
			using (StreamWriter sw = new StreamWriter(File.OpenWrite(scoresFilePath)))
			{
				foreach (KeyValuePair<Contestant, int> kvp in m_scores)
					sw.WriteLine(kvp.Key.ID + "\t" + kvp.Key.Name + "\t" + kvp.Value);
			}
		}

		private void UpdateLeaderboard(bool drawLeaderboard = false,Contestant contestantToShow=null)
		{
			SortedList<int, List<Contestant>> scores = new SortedList<int, List<Contestant>>();
			foreach (KeyValuePair<Contestant, int> kvp in m_scores)
			{
				List<Contestant> cs = null;
				scores.TryGetValue(kvp.Value,out cs);
				if (cs == null)
				{
					cs = new List<Contestant>();
					scores[kvp.Value] = cs;
				}
				cs.Add(kvp.Key);
			}
			IEnumerable<KeyValuePair<int, List<Contestant>>> rscores=scores.Reverse();
			List<ContestantScore> cscores = new List<ContestantScore>();
			int pos = 1;
			ContestantScore scrollIntoView = null;
			foreach (KeyValuePair<int, List<Contestant>> kvp in rscores) {
				foreach (Contestant c in kvp.Value)
				{
					ContestantScore cscore = new ContestantScore(pos, kvp.Value.Count > 1, kvp.Key, c, GetLastAnswerResult(c));
					if ((contestantToShow != null) && (contestantToShow.Name == c.Name))
						scrollIntoView = cscore;
					cscores.Add(cscore);
				}
				pos += kvp.Value.Count();
			}
			leaderboardList.ItemsSource = cscores;
			if (scrollIntoView != null)
			{
				leaderboardList.SelectedItem = scrollIntoView;
				leaderboardList.ScrollIntoView(scrollIntoView);
			}
			if(drawLeaderboard)
				DrawLeaderboard(cscores);
		}
		private AnswerResult GetLastAnswerResult(Contestant c)
		{
			if(m_lastAnswerResults.ContainsKey(c))
				return m_lastAnswerResults[c];
			return AnswerResult.NotAnAnswer;
		}

		private void DrawScore(Graphics g, Rectangle r, ContestantScore score,bool odd)
		{
			int textOffset = 25;
			g.FillRectangle(odd ? Brushes.WhiteSmoke : Brushes.GhostWhite, r);
			Rectangle posRect = new Rectangle(r.Left, r.Top, r.Height, r.Height);
			Rectangle nameRect = new Rectangle(r.Left + r.Height, r.Top+ textOffset, r.Width - (r.Height*2), r.Height- (textOffset*2));
			Rectangle scoreRect = new Rectangle((r.Left + r.Width)-r.Height, r.Top, r.Height, r.Height);
			g.FillRectangle(odd ? Brushes.Honeydew : Brushes.Azure, posRect);
			g.FillRectangle(odd ? Brushes.Lavender : Brushes.LavenderBlush, scoreRect);
			g.DrawLine(Pens.Black, r.Left, r.Top, r.Left, r.Bottom);
			g.DrawLine(Pens.Black, r.Right, r.Top, r.Right, r.Bottom);
			posRect.Offset(0, textOffset);
			nameRect.Offset(12, 0);
			scoreRect.Offset(0, textOffset);
			if (score!=null)
				using (Font leaderboardFont = new Font(LEADERBOARD_FONT_NAME, 36, System.Drawing.FontStyle.Bold)) {
					StringFormat sf = new StringFormat();
					sf.Trimming = StringTrimming.EllipsisCharacter;
					g.DrawString(score.Name, leaderboardFont, Brushes.Black, nameRect, sf);
					sf.Alignment = StringAlignment.Center;
					sf.Trimming = StringTrimming.None;
					g.DrawString(score.PositionString, leaderboardFont, Brushes.Black, posRect, sf);
					g.DrawString(""+score.Score, leaderboardFont, Brushes.Black, scoreRect, sf);
				}
		}

		private void DrawLeaderboard(List<ContestantScore> scores)
		{
			int n = 0;
			int leaderboardCount = 1;
			StringFormat sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			for (; ; )
			{
				using (Bitmap b = new Bitmap(LEADERBOARD_SIZE.Width, LEADERBOARD_SIZE.Height))
				{
					using (Graphics g = Graphics.FromImage(b))
					{
						g.TextRenderingHint = TextRenderingHint.AntiAlias;
						g.Clear(Color.Transparent);
						Rectangle headerRect = new Rectangle(0, 0, LEADERBOARD_SIZE.Width, 100);
						using (Font leaderboardHeaderFont = new Font(LEADERBOARD_FONT_NAME, 40, System.Drawing.FontStyle.Bold))
						{
							g.FillRectangle(Brushes.PapayaWhip, headerRect);
							g.DrawRectangle(Pens.Black, headerRect.Left, headerRect.Top, headerRect.Width - 1, headerRect.Height - 1);
							headerRect.Offset(0, 20);
							g.DrawString("Leaderboard (page "+leaderboardCount+")", leaderboardHeaderFont, Brushes.Navy, headerRect, sf);
						}
						// Leaves 900 pixels.
						for (int x = 0; x < 3; ++x)
							for (int y = 0; y < 9; ++y)
								DrawScore(g, new Rectangle(x * 620, 100+(y * 100), 620,100), n < scores.Count ? scores[n++] : null, y % 2 == 1);
						g.DrawRectangle(Pens.Black, 0, 0, LEADERBOARD_SIZE.Width-1, LEADERBOARD_SIZE.Height-1);
					}
					string path = Path.Combine(Directory.GetCurrentDirectory(), "leaderboards");
					path = Path.Combine(path, "leaderboard" + leaderboardCount + ".png");
					b.Save(path, ImageFormat.Png);
				}
				if (n >= scores.Count)
					break;
			}
		}
		class AnswerBackupString:IComparable
		{
			public string AnswerString { get; private set; }
			public DateTime AnswerTime { get; private set; }
			public AnswerBackupString(Answer answer,string contestantName)
			{
				AnswerTime = answer.AnswerTime;
				AnswerString = AnswerTime.ToLongTimeString()+": "+ contestantName + " answered \""+ answer.AnswerText+"\" ("+ answer.AnswerResult.ToString()+")";
			}

			public int CompareTo(object obj)
			{
				if(obj is AnswerBackupString){
					return AnswerTime.CompareTo(((AnswerBackupString)obj).AnswerTime);
				}
				return 1;
			}
			public override string ToString()
			{
				return AnswerString;
			}
		}


		private void ApplyScores()
		{
			try
			{
				m_answerFileMutex.WaitOne();
				string dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "data");
				string answersFilePath = Path.Combine(dataFolder, ANSWERS_FILENAME);
				using (StreamWriter sw = File.AppendText(answersFilePath))
				{
					List<AnswerBackupString> answerBackupStrings = new List<AnswerBackupString>();
					foreach (KeyValuePair<Contestant, List<Answer>> kvp in m_answers)
					{
						m_scoresDirty = true;
						m_lastAnswerResults[kvp.Key] = AnswerResult.NotAnAnswer;
						foreach (Answer answer in kvp.Value)
						{
							answerBackupStrings.Add(new AnswerBackupString(answer, kvp.Key.Name));
							int scoreForAnswer = 0;
							if (answer.AnswerResult == AnswerResult.Correct)
								scoreForAnswer = Scores.CORRECT_ANSWER_SCORE;
							else if (answer.AnswerResult == AnswerResult.AlmostCorrect)
								scoreForAnswer = Scores.ALMOST_CORRECT_ANSWER_SCORE;
							else if (answer.AnswerResult == AnswerResult.Wrong)
								scoreForAnswer = 0;
							else
								continue;
							int oldScore=0;
							m_scores.TryGetValue(kvp.Key, out oldScore);
							int newScore = oldScore;
							newScore += scoreForAnswer;
							m_scores[kvp.Key] = newScore;
							m_lastAnswerResults[kvp.Key] = answer.AnswerResult;
							break;
						}
					}
					answerBackupStrings.Sort();
					foreach (AnswerBackupString s in answerBackupStrings)
						sw.WriteLine(s.ToString());
					sw.WriteLine("----------------------------------------------------------------------------");
				}
			}
			finally
			{
				m_answerFileMutex.ReleaseMutex();
			}
			WriteScoresToFile();
			UpdateLeaderboard();
		}

		private void markingPump_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			contestantName.Text = "<contestant name>";
			questionText.Text = "<no answers to mark yet>";
			SetOBSScene("CamScene");
			m_questionShowing = false;
			HideFullScreenPicture(false);

			presentingButton.IsEnabled = showLeaderboardButton.IsEnabled = showAnswerButton.IsEnabled=true;
			loadQuizButton.IsEnabled = skipQuestionButton.IsEnabled=newQuestionButton.IsEnabled = m_nextQuestion != -1;
			showPictureButton.IsEnabled = false;

			restartMarking.IsEnabled = false;
			markingProgressBar.Value = markingProgressBar.Maximum;
			ApplyScores();
			UpdateMarkingProgressUI(null);
		}

		private void UpdateMarkingProgressUI(MarkingProgress markingProgress)
		{
			if (markingProgress != null)
			{
				markingProgressBar.Maximum = markingProgress.AnswersReceived;
				markingProgressBar.Value = markingProgress.AnswersMarked;
				markingProgressText.Text = markingProgress.AnswersMarked + " of " + markingProgress.AnswersReceived;
			}
		}

		private void markingPump_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			try
			{
				m_answerForMarkingMutex.WaitOne();
				object o = e.UserState;
				if (o is MarkingProgress)
				{
					MarkingProgress progress = (MarkingProgress)o;
					UpdateMarkingProgressUI(progress);
				}
				else if (o is AnswerForMarking)
				{
					m_answerForMarking = (AnswerForMarking)o;
					if (m_answerForMarking != null)
					{
						contestantName.Text = m_answerForMarking.Contestant.Name;
						questionText.Text = m_answerForMarking.Answer.AnswerText;
						correctAnswerButton.IsEnabled = almostCorrectAnswerButton.IsEnabled = wrongAnswerButton.IsEnabled = funnyAnswerButton.IsEnabled = notAnAnswerButton.IsEnabled = true;
					}
				}
			}
			finally
			{
				m_answerForMarkingMutex.ReleaseMutex();
			}
		}


		private void StartCountdownButtonClick(object sender, RoutedEventArgs e)
		{
			startCountdownButton.IsEnabled = false;
			if (m_timeWarnings)
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "⏳ " + COUNTDOWN_SECONDS + " seconds remaining ...");
			countdownWorker.RunWorkerAsync();
			m_countdownActive = true;
			bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.QuestionImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.QuestionImageFilename);
			if (m_fullScreenPictureShowing)
				SetOBSScene("CountdownQuestionPictureScene");
			else
				SetOBSScene(hasPic?"CountdownQuestionScene":"CountdownNoPicQuestionScene");
		}

		private void FakeAnswersWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			uint un = 235423;
			List<Contestant> contestants = new List<Contestant>();
			string[] contestantNames = new string[]
			{
				"David Bowie",
				"Ralph Stanley",
				"Clarissa Hetheridge",
				"Mark Ruffalo",
				"Stacy's Mom",
				"Olivia Coleman",
				"Jimmy Dewar",
				"Kristin Hersh",
				"Bob Mortimer",
				"Julius Caesar",
				"Fred Flintstone",
				"Lana Del Rey",
				"Bertie Bassett",
				"Darkwing Duck",
				"King Arthur",
				"Humphrey Lyttleton",
				"Shawn Colvin",
				"Tori Amos",
				"Stewart Lee"
			};
			foreach (string name in contestantNames)
				contestants.Add(new Contestant(un++, name));
			int n = 0;
			Answer[] answers = new Answer[]
			{
				new Answer("alan shearer"),
				new Answer("Shearer"),
				new Answer("Alan Sharrer"),
				new Answer("alan shearer"),
				new Answer("Bobby moore"),
				new Answer("diego maradona"),
				new Answer("Bobby Ball 😂"),
				new Answer("i've got a pot noodle!"),
				new Answer("Alan shearer"),
				new Answer("alan shearer"),
				new Answer("alan shearer"),
				new Answer("a shearer"),
				new Answer("giggsy"),
				new Answer("paul scholes"),
				new Answer("allan shearer"),
				new Answer("bananaman"),
				new Answer("alan shearer"),
				new Answer("shearer"),
				new Answer("ALAN SHERER")
			};
			int[] timings = new int[]
			{
				3785,
				234,
				9,
				109,
				1300,
				90,
				988,
				123,
				2010,
				54,
				111,
				1422,
				61,
				88,
				1082,
				21,
				15,
				578,
				2101
			};
			n = 0;
			foreach(int timing in timings)
			{
				Thread.Sleep(timing);
				AddAnswer(contestants[n],answers[n]);
				++n;
			}
		}

		private void presentingButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_presenting)
				StopPresenting();
			else
				StartPresenting();
		}

		private void MarkUserAnswers(Contestant contestant,AnswerResult result) {
			// All other unmarked answers from the contestant must now be marked wrong,
			// in case they're guessing numeric answers.
			try
			{
				m_answerListMutex.WaitOne();
				List<Answer> answers = m_answers[contestant];
				if (answers != null)
				{
					IEnumerable<Answer> unmarkedAnswers = answers.Where(a => a.AnswerResult == AnswerResult.Unmarked);
					foreach (Answer a in unmarkedAnswers)
						a.AnswerResult = result;
				}
			}
			finally
			{
				m_answerListMutex.ReleaseMutex();
			}
		}

		private void ClearAnswerForMarking()
		{
			try
			{
				m_answerForMarkingMutex.WaitOne();
				if (m_answerForMarking != null)
				{
					m_answerForMarking = null;
					contestantName.Text = "<contestant name>";
					questionText.Text = "<no answers to mark yet>";
					correctAnswerButton.IsEnabled = almostCorrectAnswerButton.IsEnabled = wrongAnswerButton.IsEnabled = funnyAnswerButton.IsEnabled = notAnAnswerButton.IsEnabled = false;
					m_answerMarkedEvent.Set();
				}
			}
			finally
			{
				m_answerForMarkingMutex.ReleaseMutex();
			}
		}

		private void MarkAnswerViaUI(AnswerResult result)
		{
			try
			{
				m_answerForMarkingMutex.WaitOne();
				if (m_answerForMarking != null)
				{
					restartMarking.IsEnabled = true;
					MarkAnswer(m_answerForMarking, result);
					ClearAnswerForMarking();
					m_answerMarkedEvent.Set();
				}
			}
			finally
			{
				m_answerForMarkingMutex.ReleaseMutex();
			}
		}

		private void correctAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.Correct);
		}

		private void almostCorrectAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.AlmostCorrect);
		}

		private void wrongAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.Wrong);
		}

		private void funnyAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.Funny);
		}

		private void notAnAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.NotAnAnswer);
		}

		private void restartMarking_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				m_answerListMutex.WaitOne();
				foreach(KeyValuePair<Contestant,List<Answer>> kvp in m_answers)
					foreach (Answer a in kvp.Value)
						a.AnswerResult = AnswerResult.Unmarked;
				ResetAnswerBins(m_currentQuestion);
			}
			finally
			{
				m_answerListMutex.ReleaseMutex();
			}
			restartMarking.IsEnabled = false;
			ClearAnswerForMarking();
		}

		private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.D1)
			{
				if (correctAnswerButton.IsEnabled)
					MarkAnswerViaUI(AnswerResult.Correct);
			}
			else if (e.Key == System.Windows.Input.Key.D2)
			{
				if (almostCorrectAnswerButton.IsEnabled)
					MarkAnswerViaUI(AnswerResult.AlmostCorrect);
			}
			else if (e.Key == System.Windows.Input.Key.D3)
			{
				if (wrongAnswerButton.IsEnabled)
					MarkAnswerViaUI(AnswerResult.Wrong);
			}
			else if (e.Key == System.Windows.Input.Key.D4)
			{
				if (funnyAnswerButton.IsEnabled)
					MarkAnswerViaUI(AnswerResult.Funny);
			}
			else if (e.Key == System.Windows.Input.Key.D5)
			{
				if (notAnAnswerButton.IsEnabled)
					MarkAnswerViaUI(AnswerResult.NotAnAnswer);
			}
		}

		private void SetVolumes(bool questionShowing,Question currentQuestion)
		{
			try
			{
				m_volumeMutex.WaitOne();
				if (questionShowing && !String.IsNullOrEmpty(currentQuestion.QuestionAudioFilename) && m_mediaPaths.ContainsKey(currentQuestion.QuestionAudioFilename.ToLower()))
				{
					m_bgmVolume = 0;
					m_questionBGMVolume = 0;
					m_questionAudioVolume = AUDIO_VOLUME;
				}
				else if (!String.IsNullOrEmpty(currentQuestion.QuestionBGMFilename) && m_mediaPaths.ContainsKey(currentQuestion.QuestionBGMFilename.ToLower()))
				{
					m_bgmVolume = 0;
					m_questionBGMVolume = BGM_VOLUME;
					m_questionAudioVolume = 0;
				}
				else
				{
					m_bgmVolume = BGM_VOLUME;
					m_questionBGMVolume = 0;
					m_questionAudioVolume = 0;
				}
			}
			finally
			{
				m_volumeMutex.ReleaseMutex();
			}
		}

		private void showQuestionButton_Click(object sender, RoutedEventArgs e)
		{
			if (!PresentationOnly)
			{
				m_countdownCompleteEvent.Reset();
				markingPump.RunWorkerAsync();
				answerCounter.RunWorkerAsync();
			}

			bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.QuestionImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.QuestionImageFilename);
			bool hasAudio = !String.IsNullOrEmpty(m_currentQuestion.QuestionAudioFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.QuestionAudioFilename);
			SetOBSScene(hasPic?"QuestionScene":"NoPicQuestionScene");
			GenerateTextImage(m_currentQuestion.AnswerText, "AnswerText", ANSWER_SIZE, "answer.png");
			SetOBSImageSource("AnswerPic", m_currentQuestion.AnswerImageFilename);
			m_questionShowing = true;
			// Set question audio to silence, wait for play button
			SetQuestionAudio(null);
			SetVolumes(true, m_currentQuestion);
			replayAudioButton.IsEnabled = hasAudio;
			showPictureButton.IsEnabled = !String.IsNullOrEmpty(m_currentQuestion.QuestionImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.QuestionImageFilename.ToLower());
			showQuestionButton.IsEnabled = false;
			if (!PresentationOnly)
			{
				startCountdownButton.IsEnabled = true;
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().Add_CB_onChatMsgNotifcation(OnAnswerReceived);
				if (m_chatWarnings)
					ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "💬 Public chat is now OFF until the answers are in.");
			}
			else
			{
				loadQuizButton.IsEnabled = skipQuestionButton.IsEnabled=newQuestionButton.IsEnabled = showAnswerButton.IsEnabled = true;
				NextQuestion(m_nextQuestion);
			}
		}

		private void SetQuestionAudio(string questionAudioFilename)
		{
			bool hasAudio = !String.IsNullOrEmpty(questionAudioFilename) && m_mediaPaths.ContainsKey(questionAudioFilename.ToLower());
			if (hasAudio)
			{
				try
				{
					m_obsMutex.WaitOne();
					m_obs.SetVolume("QuestionAudio", AUDIO_VOLUME);
				}
				finally
				{
					m_obsMutex.ReleaseMutex();
				}
			}
			SetOBSAudioSource("QuestionAudio", questionAudioFilename);
		}

		private void ShowAnswer()
		{
			HideLeaderboard();
			try
			{
				m_volumeMutex.WaitOne();
				m_bgmVolume = BGM_VOLUME;
				m_questionBGMVolume = 0;
				m_questionAudioVolume = 0;
			}
			finally
			{
				m_volumeMutex.ReleaseMutex();
			}
			bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.AnswerImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.AnswerImageFilename);
			showPictureButton.IsEnabled = hasPic;
			SetOBSScene(hasPic?(m_fullScreenPictureShowing?"AnswerPictureScene":"AnswerScene"):"NoPicAnswerScene");
			showAnswerButton.Background = System.Windows.Media.Brushes.Pink;
			showAnswerText.Text = "Hide Answer";
			m_answerShowing = true;
		}

		private void HideAnswer()
		{
			SetOBSScene("CamScene");
			HideFullScreenPicture(false);
			showAnswerButton.Background = System.Windows.Media.Brushes.LightGreen;
			showAnswerText.Text = "Show Answer";
			showPictureButton.IsEnabled = false;
			m_answerShowing = false;
		}

		private void showAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_answerShowing)
				HideAnswer();
			else
				ShowAnswer();
		}

		private void ShowLeaderboard()
		{
			HideAnswer();
			if (m_scoresDirty)
			{
				UpdateLeaderboard(true);
				m_scoresDirty = false;
			}
			SetOBSScene("LeaderboardScene");
			showLeaderboardButton.Background = System.Windows.Media.Brushes.Pink;
			showLeaderboardText.Text = "Hide Leaderboard";
			showPictureButton.IsEnabled = false;
			m_leaderboardShowing = true;
		}

		private void HideLeaderboard()
		{
			SetOBSScene("CamScene");
			showLeaderboardButton.Background = System.Windows.Media.Brushes.LightGreen;
			showLeaderboardText.Text = "Show Leaderboard";
			showPictureButton.IsEnabled = m_questionShowing && !String.IsNullOrEmpty(m_currentQuestion.QuestionImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.QuestionImageFilename.ToLower());
			m_leaderboardShowing = false;
		}

		private void showLeaderboardButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_leaderboardShowing)
				HideLeaderboard();
			else
				ShowLeaderboard();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if(MessageBox.Show(this,"Reset Scores?", "Confirm", MessageBoxButton.YesNo)==MessageBoxResult.Yes)
			{
				m_scoresDirty = true;
				m_scores.Clear();
				WriteScoresToFile();
				UpdateLeaderboard();
			}
		}

		private void SetOBSScene(string scene)
		{
			try
			{
				m_obsMutex.WaitOne();
				m_obs.SetCurrentScene(scene);
			}
			finally
			{
				m_obsMutex.ReleaseMutex();
			}
		}

		private void SetOBSImageSource(string sourceName, string mediaName)
		{
			string presFolder= Path.Combine(Directory.GetCurrentDirectory(), "presentation");
			string transparentImagePath = Path.Combine(presFolder, "transparent.png");
			string path = null;
			m_mediaPaths.TryGetValue(mediaName.ToLower(), out path);
			if ((String.IsNullOrEmpty(path)) || (!File.Exists(path)))
				path = transparentImagePath;
			SetOBSFileSourceFromPath(sourceName, "file",path);
		}

		private void SetOBSSourceSettings(string sourceName,JObject settings)
		{
			try
			{
				m_obsMutex.WaitOne();
				m_obs.SetSourceSettings(sourceName, settings);
			}
			finally
			{
				m_obsMutex.ReleaseMutex();
			}
		}

		private void SetOBSFileSourceFromPath(string sourceName, string setting,string path)
		{
			JObject settings = new JObject()
			{
				{setting,path }
			};
			SetOBSSourceSettings(sourceName, settings);
		}

		private void SetOBSAudioSource(string sourceName, string mediaName)
		{
			string path = null;
			if(mediaName!=null)
				m_mediaPaths.TryGetValue(mediaName.ToLower(), out path);
			if ((String.IsNullOrEmpty(path)) || (!File.Exists(path)))
			{
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				path = Path.Combine(presFolder, "silence.wav");
			}
			SetOBSFileSourceFromPath(sourceName, "local_file", path);
			JObject settings = new JObject()
			{
				{"NonExistent",""+new Random().Next() }
			};
			SetOBSSourceSettings(sourceName, settings);
		}

		private void GenerateTextImage(string text,string sourceName,System.Drawing.Size size,string filename)
		{
			string[] words = text.Split(' ');
			int textLength = text.Length;
			Brush[] availableColors = new Brush[]
			{
				Brushes.White,
				Brushes.LightGoldenrodYellow,
				Brushes.LightGray,
				Brushes.LightBlue,
				Brushes.LightCyan,
				Brushes.LightGreen,
				Brushes.LightSteelBlue,
				Brushes.LightYellow,
				Brushes.Azure,
				Brushes.AliceBlue,
				Brushes.Cornsilk,
				Brushes.FloralWhite,
				Brushes.GhostWhite,
				Brushes.Honeydew,
				Brushes.Ivory,
				Brushes.Lavender,
				Brushes.LavenderBlush,
				Brushes.LemonChiffon,
				Brushes.Linen,
				Brushes.MintCream,
				Brushes.MistyRose,
				Brushes.OldLace,
				Brushes.PapayaWhip,
				Brushes.SeaShell,
				Brushes.Snow,
				Brushes.WhiteSmoke,
				Brushes.Yellow
			};
			StringFormat sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			int charactersFitted, linesFitted;
			Brush textColor = availableColors[new Random().Next(0, availableColors.Length)];
			using (Bitmap b = new Bitmap(size.Width,size.Height))
			{
				using (Graphics g = Graphics.FromImage(b))
				{
					g.TextRenderingHint = TextRenderingHint.AntiAlias;
					g.Clear(Color.Transparent);
					for (int f = 10; ; f+=4)
					{
						using (Font font = new Font(QUESTION_FONT_NAME, f, System.Drawing.FontStyle.Regular))
						{
							// We need room for the outline
							System.Drawing.Size clientRect = new System.Drawing.Size(size.Width - (TEXT_OUTLINE_THICKNESS * 2), size.Height - (TEXT_OUTLINE_THICKNESS * 2));
							SizeF textSize = g.MeasureString(text, font, clientRect, sf, out charactersFitted, out linesFitted);
							bool wordLimitReached = false;
							foreach(string word in words)
							{
								SizeF wordSize=g.MeasureString(word, font, 1000000);
								if (wordSize.Width >= clientRect.Width)
								{
									wordLimitReached = true;
									break;
								}
							}
							if ((textSize.Width >= clientRect.Width) || (textSize.Height >= clientRect.Height) || (wordLimitReached) || (charactersFitted<textLength))
							{
								using (Font realFont = new Font(QUESTION_FONT_NAME, f-4, System.Drawing.FontStyle.Regular))
								{
									textSize = g.MeasureString(text, realFont, clientRect, sf, out charactersFitted, out linesFitted);
									int nVertOffset = (int)((size.Height - textSize.Height) / 2.0);
									Rectangle rect = new Rectangle(new System.Drawing.Point(0,0),size);
									rect.Offset(0, nVertOffset);
									for (int x = -TEXT_OUTLINE_THICKNESS; x <= TEXT_OUTLINE_THICKNESS; ++x)
										for (int y = -TEXT_OUTLINE_THICKNESS; y <= TEXT_OUTLINE_THICKNESS; ++y)
										{
											Rectangle borderRect = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
											borderRect.Offset(x, y);
											g.DrawString(text, realFont, System.Drawing.Brushes.Black, borderRect, sf);
										}
									g.DrawString(text, realFont, textColor, rect, sf);
									break;
								}
							}
						}
					}
				}
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				string path = Path.Combine(presFolder, filename);
				b.Save(path, ImageFormat.Png);
				SetOBSFileSourceFromPath(sourceName, "file",path);
			}
		}

		private void HideFullScreenPicture(bool setScene=true)
		{
			if(setScene)
				if (m_answerShowing)
				{
					bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.AnswerImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.AnswerImageFilename);
					SetOBSScene(hasPic ? "AnswerScene" : "NoPicAnswerScene");
				}
				else if (m_questionShowing)
				{
					bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.QuestionImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.QuestionImageFilename);
					SetOBSScene(m_countdownActive ? (hasPic ? "CountdownQuestionScene" : "CountdownNoPicQuestionScene") : (hasPic ? "QuestionScene" : "NoPicQuestionScene"));
				}
			showPictureButton.Background = System.Windows.Media.Brushes.LightGreen;
			showPictureText.Text = "Fullscreen Picture";
			m_fullScreenPictureShowing = false;
		}

		private void ShowFullScreenPicture()
		{
			if (m_answerShowing)
			{
				bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.AnswerImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.AnswerImageFilename);
				SetOBSScene(hasPic ? "AnswerPictureScene" : "NoPicAnswerScene");
			}
			else if (m_questionShowing)
			{
				bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.QuestionImageFilename) && m_mediaPaths.ContainsKey(m_currentQuestion.QuestionImageFilename);
				SetOBSScene(m_countdownActive ? "CountdownQuestionPictureScene" : "QuestionPictureScene");
			}
			showPictureButton.Background = System.Windows.Media.Brushes.Pink;
			showPictureText.Text = "Embedded Picture";
			m_fullScreenPictureShowing = true;
		}

		private void showPictureButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_fullScreenPictureShowing)
				HideFullScreenPicture();
			else
				ShowFullScreenPicture();
		}

		private void increaseScoreButton_Click(object sender, RoutedEventArgs e)
		{
			ContestantScore score = (ContestantScore)leaderboardList.SelectedItem;
			m_scores[score.Contestant] = m_scores[score.Contestant] + 1;
			WriteScoresToFile();
			m_scoresDirty = true;
			UpdateLeaderboard(false,score.Contestant);
		}

		private void decreaseScoreButton_Click(object sender, RoutedEventArgs e)
		{
			ContestantScore score = (ContestantScore)leaderboardList.SelectedItem;
			m_scores[score.Contestant] = m_scores[score.Contestant] - 1;
			WriteScoresToFile();
			m_scoresDirty = true;
			UpdateLeaderboard(false,score.Contestant);
		}

		private void leaderboardList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			decreaseScoreButton.IsEnabled = increaseScoreButton.IsEnabled = (leaderboardList.SelectedItems.Count == 1);
		}

		private void MuteBGM(bool mute)
		{
			try
			{
				m_obsMutex.WaitOne();
				m_obs.SetMute("BGM", mute);
				m_obs.SetMute("QuestionBGM", mute);
			}
			finally
			{
				m_obsMutex.ReleaseMutex();
			}
		}

		private void muteBGM_Checked(object sender, RoutedEventArgs e)
		{
			MuteBGM(true);
		}

		private void muteBGM_Unchecked(object sender, RoutedEventArgs e)
		{
			MuteBGM(false);
		}

		private void viewAnswerHistory_Click(object sender, RoutedEventArgs e)
		{
			string dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "data");
			string answersFilePath = Path.Combine(dataFolder, ANSWERS_FILENAME);
			if (File.Exists(answersFilePath))
				System.Diagnostics.Process.Start(answersFilePath);
		}

		private void skipQuestion_Click(object sender, RoutedEventArgs e)
		{
			NextQuestion(m_nextQuestion);
		}

		private void replayAudioButton_Click(object sender, RoutedEventArgs e)
		{
			SetQuestionAudio(m_currentQuestion.QuestionAudioFilename);
		}

		private void dummyAnswersButton_Click(object sender, RoutedEventArgs e)
		{
			BackgroundWorker fakeAnswersWorker = new BackgroundWorker();
			fakeAnswersWorker.DoWork += FakeAnswersWorker_DoWork;
			fakeAnswersWorker.RunWorkerAsync();
		}

		private void showTimeWarnings_Checked(object sender, RoutedEventArgs e)
		{
			m_timeWarnings = true;
		}

		private void showTimeWarnings_Unchecked(object sender, RoutedEventArgs e)
		{
			m_timeWarnings = false;
		}

		private void showChatWarnings_Checked(object sender, RoutedEventArgs e)
		{
			m_chatWarnings = true;
		}

		private void showChatWarnings_Unchecked(object sender, RoutedEventArgs e)
		{
			m_chatWarnings = false;
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "INI files (*.ini)|*.ini";
			if (openFileDialog.ShowDialog() == true)
			{
				LoadQuiz(openFileDialog.FileName);
			}
		}
	}
}
