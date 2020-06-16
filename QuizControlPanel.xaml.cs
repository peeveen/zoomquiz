﻿using System;
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
using System.Windows.Data;

namespace ZoomQuiz
{
	/// <summary>
	/// Interaction logic for QuizControlPanel.xaml
	/// </summary>
	public partial class QuizControlPanel : Window, IQuizContext
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

		private const string SCORE_REPORT_FILENAME = "scoreReport.png";
		private const string SCORE_REPORT_WITH_TIMES_FILENAME = "scoreReportWithTimes.png";
		private const string SCORES_FILENAME = "scores.txt";
		private const string ANSWERS_FILENAME = "answers.txt";
		private const string ZoomQuizTitle = "ZoomQuiz";
		private const float AUDIO_VOLUME = 0.8f;
		private const float VIDEO_VOLUME = 1.0f;
		private const float BGM_VOLUME = 0.05f;

		public Mutex VolumeMutex { get; } = new Mutex();
		public Mutex AnswerListMutex { get; } = new Mutex();
		public Mutex AnswerForMarkingMutex { get; } = new Mutex();
		private readonly Mutex m_answerFileMutex = new Mutex();
		private readonly Mutex m_scoreReportMutex = new Mutex();

		private readonly BackgroundWorker countdownWorker;
		private readonly BackgroundWorker answerCounter;
		private readonly BackgroundWorker markingPump;
		private readonly BackgroundWorker faderWorker;

		public AutoResetEvent AnswerReceivedEvent { get; } = new AutoResetEvent(false);
		public AutoResetEvent AnswerCounterAnswerReceivedEvent { get; } = new AutoResetEvent(false);
		public AutoResetEvent AnswerMarkedEvent { get; } = new AutoResetEvent(false);
		public ManualResetEvent CountdownCompleteEvent { get; } = new ManualResetEvent(true);
		public ManualResetEvent QuitAppEvent { get; } = new ManualResetEvent(false);
		public float BgmVolume { get; private set; } = BGM_VOLUME;
		public float QuestionBGMVolume { get; private set; } = 0;
		public float QuestionAudioVolume { get; private set; } = 0;
		public float QuestionVideoVolume { get; private set; } = 0;

		public Dictionary<Contestant, List<Answer>> Answers { get; } = new Dictionary<Contestant, List<Answer>>();
		public Dictionary<AnswerResult, AnswerBin> AnswerBins { get; } = new Dictionary<AnswerResult, AnswerBin>();
		private Quiz Quiz;
		private readonly Dictionary<Contestant, int> m_scores = new Dictionary<Contestant, int>();
		private readonly List<ScoreReportEntry> m_scoreReport = new List<ScoreReportEntry>();
		private readonly Dictionary<Contestant, AnswerResult> m_lastAnswerResults = new Dictionary<Contestant, AnswerResult>();

		public ObsController Obs { get; } = new ObsController();
		public bool ShowTimeWarnings { get; private set; } = false;

		private bool m_quizEnded = false;
		private bool m_questionShowing = false;
		private bool m_answerShowing = false;
		private bool m_leaderboardShowing = false;
		private bool m_fullScreenPictureShowing = false;
		private uint m_myID = 0;
		private bool m_presenting = false;
		private AnswerForMarking m_answerForMarking = null;
		private int m_nextQuestion = 1;
		private Question m_currentQuestion = null;
		private bool m_scoresDirty = true;
		public bool StartedOK { get; private set; }
		private bool m_chatWarnings = false;
		private bool PresentationOnly { get; set; }

		public QuizControlPanel(bool presentationOnly)
		{
			PresentationOnly = presentationOnly;
			StartedOK = false;
			InitializeComponent();
			ReadScoresFromFile();
			markingPump = new MarkingPumpBackgroundWorker(this);
			countdownWorker = new CountdownBackgroundWorker(this);
			answerCounter = new AnswerCounterBackgroundWorker(this);
			faderWorker = new FaderBackgroundWorker(this);
			ClearLeaderboards();
			UpdateLeaderboard(true);
			try
			{
				Obs.Connect("ws://127.0.0.1:4444");
				if (Obs.IsConnected)
				{
					Obs.SetCurrentScene("CamScene");
					//File.Delete(Path.Combine(Directory.GetCurrentDirectory(), ANSWERS_FILENAME));
					//ScanMediaPath();
					SetCountdownMedia();
					UpdateScoreReports();
					SetScoreReportMedia();
					SetBGMShuffle();
					SetLeaderboardsPath();
					HideCountdownOverlay();
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
			if (PresentationOnly)
				presentingButton.IsEnabled = false;
		}

		private void ClearLeaderboards()
		{
			string lbFolder = Path.Combine(Directory.GetCurrentDirectory(), "leaderboards");
			string[] files = Directory.GetFiles(lbFolder);
			foreach (string file in files)
				if (File.Exists(file))
					File.Delete(file);
		}

		private void SetLeaderboardsPath()
		{
			string lbFolder = Path.Combine(Directory.GetCurrentDirectory(), "leaderboards");
			SourceSettings lbSourceSettings = Obs.GetSourceSettings("Leaderboard");
			JObject lbSettings = lbSourceSettings.sourceSettings;
			lbSettings["files"][0]["value"] = lbFolder;
			Obs.SetSourceSettings("Leaderboard", lbSettings);
		}

		private void SetScoreReportMedia()
		{
			string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
			SetOBSFileSourceFromPath("ScoreReport", "file", Path.Combine(presFolder, SCORE_REPORT_FILENAME));
		}

		private void SetCountdownMedia()
		{
			string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
			string mp4Path = Path.Combine(presFolder, "Countdown.mp4");
			string maskPath = Path.Combine(presFolder, "circle.png");
			SetOBSFileSourceFromPath("Countdown", "local_file", mp4Path);
			List<FilterSettings> filters = Obs.GetSourceFilters("Countdown");
			foreach (FilterSettings st in filters)
			{
				if (st.Name.Contains("Image Mask"))
				{
					JObject maskSettings = st.Settings;
					maskSettings["image_path"] = maskPath;
					Obs.SetSourceFilterSettings("Countdown", st.Name, maskSettings);
				}
			}
		}

		private void SetBGMShuffle()
		{
			string mediaPath = Path.Combine(Directory.GetCurrentDirectory(), "bgm");
			SourceSettings bgmSettings = Obs.GetSourceSettings("BGM");
			JObject bgmSourceSettings = bgmSettings.sourceSettings;
			bgmSourceSettings["loop"] = true;
			bgmSourceSettings["shuffle"] = true;
			bgmSourceSettings["playlist"][0]["value"] = mediaPath;
			Obs.SetSourceSettings("BGM", bgmSourceSettings);
		}

		private void LoadQuiz(string quizFilePath)
		{
			Quiz = new Quiz(quizFilePath);

			UpdateQuizList();
			if (Quiz.HasInvalidQuestions)
				MessageBox.Show("Warning: invalid questions found.", ZoomQuizTitle);
			m_nextQuestion = 0;
			NextQuestion(m_nextQuestion);
			skipQuestionButton.IsEnabled = newQuestionButton.IsEnabled = m_nextQuestion != -1;
		}

		private void UpdateQuizList()
		{
			quizList.ItemsSource = Quiz;
			ICollectionView view = CollectionViewSource.GetDefaultView(quizList.ItemsSource);
			view.Refresh();
			quizList.SelectedIndex = 0;
			quizList.ScrollIntoView(Quiz[1]);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (!PresentationOnly && !m_quizEnded)
				e.Cancel = true;
			else
				QuitAppEvent.Set();
		}

		public void EndQuiz()
		{
			m_quizEnded = true;
			Close();
			if (Obs.IsConnected)
				Obs.Disconnect();
		}

		public void StartQuiz()
		{
			if (!PresentationOnly)
			{
				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().Add_CB_onChatStatusChangedNotification(OnChatStatusChanged);
				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingWaitingRoomController().EnableWaitingRoomOnEntry(false);

				IMeetingParticipantsControllerDotNetWrap partCon = CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingParticipantsController();
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

				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingAudioController().JoinVoip();
				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, "🥂 Welcome to the quiz!");

				ShowChatDlgParam showDlgParam = new ShowChatDlgParam
				{
					rect = new Rectangle(10, 10, 200, 200)
				};
				ValueType dlgParam = showDlgParam;
				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetUIController().ShowChatDlg(ref dlgParam);

				SetChatMode(ChatMode.EveryonePubliclyAndPrivately);
			}
			Show();
		}

		private void ResetAnswerBins(Question currentQuestion)
		{
			var values = Enum.GetValues(typeof(AnswerResult));
			AnswerBins.Clear();
			foreach (AnswerResult result in values)
				AnswerBins[result] = new AnswerBin();
			AnswerBin correctAnswers = AnswerBins[AnswerResult.Correct];
			AnswerBin almostCorrectAnswers = AnswerBins[AnswerResult.AlmostCorrect];
			AnswerBin wrongAnswers = AnswerBins[AnswerResult.Wrong];
			currentQuestion.QuestionAnswers.Select(a => new Answer(a)).ToList().ForEach(a => correctAnswers.Add(a, 0.0));
			currentQuestion.QuestionAlmostAnswers.Select(a => new Answer(a)).ToList().ForEach(a => almostCorrectAnswers.Add(a, 0.0));
			currentQuestion.QuestionWrongAnswers.Select(a => new Answer(a)).ToList().ForEach(a => wrongAnswers.Add(a, 0.0));
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
					if (m_chatWarnings)
						SendPublicChat(mode == ChatMode.HostOnly ? "💬 Public chat is now OFF until the answers are in." : "💬 Public chat is ON");
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

		public void AddAnswer(Contestant contestant, Answer answer)
		{
			try
			{
				AnswerListMutex.WaitOne();
				Answers.TryGetValue(contestant, out List<Answer> answerList);
				if (answerList == null)
					answerList = new List<Answer>();
				answerList.Add(answer);
				Answers[contestant] = answerList;
			}
			finally
			{
				AnswerListMutex.ReleaseMutex();
			}

			AnswerReceivedEvent.Set();
			AnswerCounterAnswerReceivedEvent.Set();
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
				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingVideoController().SpotlightVideo(true, m_myID);
				if (muteDuringQuestions.IsChecked == true)
					CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingAudioController().MuteAudio(0, false);
				presentingText.Text = "Stop Presenting";
				presentingButton.Background = System.Windows.Media.Brushes.Pink;
				m_presenting = true;
			}
		}

		private void StopPresenting()
		{
			if (!PresentationOnly)
			{
				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingVideoController().SpotlightVideo(false, m_myID);
				if (muteDuringQuestions.IsChecked == true)
					CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingAudioController().MuteAudio(0, true);
				m_presenting = false;
				presentingText.Text = "Start Presenting";
				presentingButton.Background = System.Windows.Media.Brushes.LightGreen;
				presentingButton.IsEnabled = true;
			}
		}

		private void StartQuestionButtonClick(object sender, RoutedEventArgs e)
		{
			try
			{
				m_scoreReportMutex.WaitOne();
				m_scoreReport.Clear();
			}
			finally
			{
				m_scoreReportMutex.ReleaseMutex();
			}
			UpdateScoreReports();
			m_lastAnswerResults.Clear();
			Answers.Clear();
			HideAnswer();
			skipQuestionButton.IsEnabled = false;
			m_currentQuestion = Quiz[m_nextQuestion];
			questionTextBox.Text = m_currentQuestion.QuestionText;
			answerTextBox.Text = m_currentQuestion.AnswerText;
			infoTextBox.Text = m_currentQuestion.Info;
			ResetAnswerBins(m_currentQuestion);
			GenerateTextImage(m_currentQuestion.QuestionText, "QuestionText", "question.png");
			SetOBSImageSource("QuestionPic", m_currentQuestion.QuestionImageFilename);
			// Show no video until it's ready.
			SetOBSVideoSource("QuestionVid", null);
			SetOBSAudioSource("QuestionBGM", m_currentQuestion.QuestionBGMFilename);
			SetVolumes(false, m_currentQuestion);
			HideCountdownOverlay();
			SetChatMode(ChatMode.HostOnly);
			StartPresenting();
			HideFullScreenPicture(false);
			showPictureButton.IsEnabled = false;
			replayAudioButton.IsEnabled = false;
			showAnswerButton.IsEnabled = presentingButton.IsEnabled = false;
			markingProgressBar.Maximum = 1;
			markingProgressBar.Value = 0;
			markingProgressText.Text = "";
			loadQuizButton.IsEnabled =
				newQuestionButton.IsEnabled = showLeaderboardButton.IsEnabled = false;
			showQuestionButton.IsEnabled = true;
			if (!PresentationOnly)
				SendPublicChat("✏️ Here comes the next question ...");
		}

		public void SendPublicChat(string chatMessage)
		{
			CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, chatMessage);
		}

		private int NextQuestion(int currentQuestion)
		{
			m_nextQuestion = Quiz.GetNextQuestionNumber(currentQuestion);
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

		public void OnCountdownComplete()
		{
			CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().Remove_CB_onChatMsgNotifcation(OnAnswerReceived);
			NextQuestion(m_nextQuestion);
			SetChatMode(ChatMode.EveryonePublicly);
			try
			{
				AnswerForMarkingMutex.WaitOne();
				presentingButton.IsEnabled = m_answerForMarking == null;
			}
			finally
			{
				AnswerForMarkingMutex.ReleaseMutex();
			}
		}

		private void UpdateScoreReports()
		{
			UpdateScoreReport(false, SCORE_REPORT_FILENAME);
			UpdateScoreReport(true, SCORE_REPORT_WITH_TIMES_FILENAME);
		}

		private void UpdateScoreReport(bool times, string outputFilename)
		{
			try
			{
				m_scoreReportMutex.WaitOne();
				using (ScoreReportBitmap bitmap = new ScoreReportBitmap(m_scoreReport, times))
				{
					string path = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
					string bitmapPath = Path.Combine(path, outputFilename);
					bitmap.Save(bitmapPath);
				}
			}
			finally
			{
				m_scoreReportMutex.ReleaseMutex();
			}
		}

		private void AddToScoreReport(DateTime answerTime, Contestant contestant, AnswerResult result)
		{
			try
			{
				m_scoreReportMutex.WaitOne();
				ScoreReportEntry firstReport = m_scoreReport.LastOrDefault();
				TimeSpan offset = firstReport == null ? new TimeSpan(0) : answerTime - firstReport.AnswerTime;
				m_scoreReport.Insert(0, new ScoreReportEntry(answerTime, contestant, result, offset));
				UpdateScoreReports();
			}
			finally
			{
				m_scoreReportMutex.ReleaseMutex();
			}
		}

		public void MarkAnswer(AnswerForMarking answer, AnswerResult result, double levValue, bool autoCountdown)
		{
			answer.Answer.AnswerResult = result;
			AnswerBin bin = AnswerBins[result];
			if (bin != null)
				bin.Add(answer.Answer, levValue);
			if (result == AnswerResult.Correct)
			{
				AddToScoreReport(answer.Answer.AnswerTime, answer.Contestant, result);
				if (autoCountdown)
					markingPump.ReportProgress(0, new CountdownStartArgs());
			}
			else if (result == AnswerResult.AlmostCorrect)
				AddToScoreReport(answer.Answer.AnswerTime, answer.Contestant, result);
			else if (result == AnswerResult.Funny)
				markingPump.ReportProgress(0, new FunnyAnswerArgs(answer.Answer, answer.Contestant));
			else if (result != AnswerResult.NotAnAnswer)
				// Once a valid answer is accepted (right or wrong), all other answers from that user cannot be considered.
				MarkOtherUserAnswers(answer.Contestant);
		}

		public void SetAnswerForMarking(AnswerForMarking answerForMarking)
		{
			m_answerForMarking = answerForMarking;
			if (m_answerForMarking != null)
			{
				contestantName.Text = m_answerForMarking.Contestant.Name;
				questionText.Text = m_answerForMarking.Answer.AnswerText;
				correctAnswerButton.IsEnabled = almostCorrectAnswerButton.IsEnabled = wrongAnswerButton.IsEnabled = funnyAnswerButton.IsEnabled = notAnAnswerButton.IsEnabled = true;
			}
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

		private void UpdateLeaderboard(bool drawLeaderboard = false, Contestant contestantToShow = null)
		{
			SortedList<int, List<Contestant>> scores = new SortedList<int, List<Contestant>>();
			foreach (KeyValuePair<Contestant, int> kvp in m_scores)
			{
				scores.TryGetValue(kvp.Value, out List<Contestant> cs);
				if (cs == null)
				{
					cs = new List<Contestant>();
					scores[kvp.Value] = cs;
				}
				cs.Add(kvp.Key);
			}
			IEnumerable<KeyValuePair<int, List<Contestant>>> rscores = scores.Reverse();
			List<ContestantScore> cscores = new List<ContestantScore>();
			int pos = 1;
			ContestantScore scrollIntoView = null;
			foreach (KeyValuePair<int, List<Contestant>> kvp in rscores)
			{
				foreach (Contestant c in kvp.Value)
				{
					ContestantScore cscore = new ContestantScore(pos, kvp.Value.Count > 1, kvp.Key, c, GetLastAnswerResult(c));
					if ((contestantToShow != null) && (contestantToShow.Name == c.Name))
						scrollIntoView = cscore;
					cscores.Add(cscore);
				}
				pos += kvp.Value.Count;
			}
			leaderboardList.ItemsSource = cscores;
			if (scrollIntoView != null)
			{
				leaderboardList.SelectedItem = scrollIntoView;
				leaderboardList.ScrollIntoView(scrollIntoView);
			}
			if (drawLeaderboard)
				DrawLeaderboard(cscores);
		}

		private AnswerResult GetLastAnswerResult(Contestant c)
		{
			if (m_lastAnswerResults.ContainsKey(c))
				return m_lastAnswerResults[c];
			return AnswerResult.NotAnAnswer;
		}

		private void DrawLeaderboard(List<ContestantScore> scores)
		{
			int n = 0;
			int leaderboardCount = 1;
			for (; ; )
			{
				using (LeaderboardBitmap b = new LeaderboardBitmap(scores,leaderboardCount,ref n))
				{
					string path = Path.Combine(Directory.GetCurrentDirectory(), "leaderboards");
					path = Path.Combine(path, "leaderboard" + leaderboardCount + ".png");
					b.Save(path);
					++leaderboardCount;
				}
				if (n >= scores.Count)
					break;
			}
		}

		private string GetLevenshteinReport(AnswerResult result)
		{
			AnswerBin bin = AnswerBins[result];
			bin.GetLevenshteinRange(out double minLev, out double maxLev);
			return result.ToString() + " answer Levenshtein values ranged from " + string.Format("{0:0.00}", minLev) + " to " + string.Format("{0:0.00}", maxLev);
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
					foreach (KeyValuePair<Contestant, List<Answer>> kvp in Answers)
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
							m_scores.TryGetValue(kvp.Key, out int oldScore);
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
					sw.WriteLine(GetLevenshteinReport(AnswerResult.Correct));
					sw.WriteLine(GetLevenshteinReport(AnswerResult.AlmostCorrect));
					sw.WriteLine(GetLevenshteinReport(AnswerResult.Wrong));
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

		public void UpdateMarkingProgressUI(MarkingProgress markingProgress)
		{
			if (markingProgress != null)
			{
				markingProgressBar.Maximum = markingProgress.AnswersReceived;
				markingProgressBar.Value = markingProgress.AnswersMarked;
				markingProgressText.Text = markingProgress.AnswersMarked + " of " + markingProgress.AnswersReceived;
			}
		}

		private void HideCountdownOverlay()
		{
			HideOBSSource("CountdownOverlay", "CamScene");
			HideOBSSource("CountdownOverlay", "FullScreenPictureQuestionScene");
			HideOBSSource("CountdownOverlay", "FullScreenPictureAnswerScene");
			Obs.SetSourceRender("ScoreReport", true, "ScoreReportOverlay");
			Obs.SetSourceRender("ScoreReportWithTimes", false, "ScoreReportOverlay");
			Obs.SetSourceRender("ScrollingScoreReportWithTimes", false, "ScoreReportOverlay");
		}

		private void ShowCountdownOverlay()
		{
			ShowOBSSource("CountdownOverlay", "CamScene");
			ShowOBSSource("CountdownOverlay", "FullScreenPictureQuestionScene");
			ShowOBSSource("CountdownOverlay", "FullScreenPictureAnswerScene");
		}

		public void StartCountdown()
		{
			if (startCountdownButton.IsEnabled)
			{
				startCountdownButton.IsEnabled = false;
				countdownWorker.RunWorkerAsync();
				ShowCountdownOverlay();
			}
		}

		private void StartCountdownButtonClick(object sender, RoutedEventArgs e)
		{
			StartCountdown();
		}

		private void PresentingButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_presenting)
				StopPresenting();
			else
				StartPresenting();
		}

		private void MarkOtherUserAnswers(Contestant contestant)
		{
			// All other unmarked answers from the contestant must now be marked wrong,
			// in case they're guessing numeric answers.
			try
			{
				AnswerListMutex.WaitOne();
				List<Answer> answers = Answers[contestant];
				if (answers != null)
				{
					IEnumerable<Answer> unmarkedAnswers = answers.Where(a => a.AnswerResult == AnswerResult.Unmarked);
					foreach (Answer a in unmarkedAnswers)
					{
						if (a.AnswerText.StartsWith("."))
						{
							markingPump.ReportProgress(0, new FunnyAnswerArgs(a, contestant));
							a.AnswerResult = AnswerResult.Funny;
						}
						else
							a.AnswerResult = AnswerResult.NotAnAnswer;
					}
				}
			}
			finally
			{
				AnswerListMutex.ReleaseMutex();
			}
		}

		private void ClearAnswerForMarking()
		{
			try
			{
				AnswerForMarkingMutex.WaitOne();
				if (m_answerForMarking != null)
				{
					m_answerForMarking = null;
					contestantName.Text = "<contestant name>";
					questionText.Text = "<no answers to mark yet>";
					correctAnswerButton.IsEnabled = almostCorrectAnswerButton.IsEnabled = wrongAnswerButton.IsEnabled = funnyAnswerButton.IsEnabled = notAnAnswerButton.IsEnabled = false;
					AnswerMarkedEvent.Set();
				}
			}
			finally
			{
				AnswerForMarkingMutex.ReleaseMutex();
			}
		}

		private double GetBestLevenshtein(AnswerResult result, string normAnswer)
		{
			string[] comparisonStrings;
			if (result == AnswerResult.Correct)
				comparisonStrings = m_currentQuestion.QuestionAnswers;
			else if (result == AnswerResult.AlmostCorrect)
				comparisonStrings = m_currentQuestion.QuestionAlmostAnswers;
			else if (result == AnswerResult.Wrong)
				comparisonStrings = m_currentQuestion.QuestionWrongAnswers;
			else
				return 0.0;
			double bestLev = (comparisonStrings.Length == 0 ? 0.0 : comparisonStrings.Min(str => Levenshtein.CalculateLevenshtein(str, normAnswer) / (double)str.Length));
			return bestLev;
		}

		private void MarkAnswerViaUI(AnswerResult result)
		{
			try
			{
				AnswerForMarkingMutex.WaitOne();
				if (m_answerForMarking != null)
				{
					restartMarking.IsEnabled = true;
					MarkAnswer(m_answerForMarking, result, GetBestLevenshtein(result, m_answerForMarking.Answer.NormalizedAnswer), autoCountdown.IsChecked == true);
					ClearAnswerForMarking();
					AnswerMarkedEvent.Set();
				}
			}
			finally
			{
				AnswerForMarkingMutex.ReleaseMutex();
			}
		}

		private void CorrectAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.Correct);
		}

		private void AlmostCorrectAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.AlmostCorrect);
		}

		private void WrongAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.Wrong);
		}

		private void FunnyAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.Funny);
		}

		private void NotAnAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			MarkAnswerViaUI(AnswerResult.NotAnAnswer);
		}

		private void RestartMarking_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				AnswerListMutex.WaitOne();
				foreach (KeyValuePair<Contestant, List<Answer>> kvp in Answers)
					foreach (Answer a in kvp.Value)
						a.AnswerResult = AnswerResult.Unmarked;
				ResetAnswerBins(m_currentQuestion);
			}
			finally
			{
				AnswerListMutex.ReleaseMutex();
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

		private void SetVolumes(bool questionShowing, Question currentQuestion)
		{
			try
			{
				VolumeMutex.WaitOne();
				bool hasAudioOrVideo = (currentQuestion.QuestionMediaType == MediaType.Audio || currentQuestion.QuestionMediaType == MediaType.Video) && Quiz.HasMediaFile(currentQuestion.QuestionMediaFilename);
				if (questionShowing && hasAudioOrVideo)
				{
					BgmVolume = 0;
					QuestionBGMVolume = 0;
					QuestionAudioVolume = AUDIO_VOLUME;
					QuestionVideoVolume = VIDEO_VOLUME;
				}
				else if (!String.IsNullOrEmpty(currentQuestion.QuestionBGMFilename) && Quiz.HasMediaFile(currentQuestion.QuestionBGMFilename))
				{
					BgmVolume = 0;
					QuestionBGMVolume = BGM_VOLUME;
					QuestionAudioVolume = 0;
					QuestionVideoVolume = 0;
				}
				else
				{
					BgmVolume = BGM_VOLUME;
					QuestionBGMVolume = 0;
					QuestionAudioVolume = 0;
					QuestionVideoVolume = 0;
				}
			}
			finally
			{
				VolumeMutex.ReleaseMutex();
			}
		}

		private void ShowQuestionButton_Click(object sender, RoutedEventArgs e)
		{
			if (!PresentationOnly)
			{
				CountdownCompleteEvent.Reset();
				markingPump.RunWorkerAsync(new MarkingPumpArgs(m_currentQuestion.UseLevenshtein, autoCountdown.IsChecked == true));
				answerCounter.RunWorkerAsync();
			}

			bool hasPicOrVid = (m_currentQuestion.QuestionMediaType == MediaType.Image || m_currentQuestion.QuestionMediaType == MediaType.Video) && Quiz.HasMediaFile(m_currentQuestion.QuestionMediaFilename);
			bool hasSupPicOrVid = (m_currentQuestion.QuestionSupplementaryMediaType == MediaType.Image || m_currentQuestion.QuestionSupplementaryMediaType == MediaType.Video) && Quiz.HasMediaFile(m_currentQuestion.QuestionSupplementaryMediaFilename);
			bool hasAudio = !String.IsNullOrEmpty(m_currentQuestion.QuestionAudioFilename) && Quiz.HasMediaFile(m_currentQuestion.QuestionAudioFilename);
			bool hasVideo = m_currentQuestion.QuestionMediaType == MediaType.Video && Quiz.HasMediaFile(m_currentQuestion.QuestionMediaFilename);
			Obs.SetCurrentScene(hasPicOrVid || hasSupPicOrVid ? "QuestionScene" : "NoPicQuestionScene");
			GenerateTextImage(m_currentQuestion.AnswerText, "AnswerText", "answer.png");
			SetOBSImageSource("AnswerPic", m_currentQuestion.AnswerImageFilename);
			m_questionShowing = true;
			// Set question audio to silence, wait for play button
			SetQuestionAudio(null);
			SetVolumes(true, m_currentQuestion);
			replayAudioButton.IsEnabled = hasAudio | hasVideo;
			showPictureButton.IsEnabled = hasPicOrVid || hasSupPicOrVid;
			showQuestionButton.IsEnabled = false;
			if (!PresentationOnly)
			{
				startCountdownButton.IsEnabled = true;
				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().Add_CB_onChatMsgNotifcation(OnAnswerReceived);
			}
			else
			{
				loadQuizButton.IsEnabled = skipQuestionButton.IsEnabled = newQuestionButton.IsEnabled = showAnswerButton.IsEnabled = true;
				NextQuestion(m_nextQuestion);
			}
		}

		private void SetQuestionAudio(string questionAudioFilename)
		{
			bool hasAudio = !String.IsNullOrEmpty(questionAudioFilename) && Quiz.HasMediaFile(questionAudioFilename);
			if (hasAudio)
				Obs.SetVolume("QuestionAudio", AUDIO_VOLUME);
			SetOBSAudioSource("QuestionAudio", questionAudioFilename);
		}

		private void ShowAnswer()
		{
			HideLeaderboard();
			try
			{
				VolumeMutex.WaitOne();
				BgmVolume = BGM_VOLUME;
				QuestionBGMVolume = 0;
				QuestionAudioVolume = 0;
				QuestionVideoVolume = 0;
			}
			finally
			{
				VolumeMutex.ReleaseMutex();
			}
			bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.AnswerImageFilename) && Quiz.HasMediaFile(m_currentQuestion.AnswerImageFilename);
			showPictureButton.IsEnabled = hasPic;
			Obs.SetCurrentScene(hasPic ? (m_fullScreenPictureShowing ? "FullScreenPictureAnswerScene" : "AnswerScene") : "NoPicAnswerScene");
			showAnswerButton.Background = System.Windows.Media.Brushes.Pink;
			showAnswerText.Text = "Hide Answer";
			// The score report can show a maximum of 17 names. More than that requires a scrolling report.
			bool scrollingRequired = m_scoreReport.Count > 17;
			Obs.SetSourceRender("ScoreReport", false, "ScoreReportOverlay");
			Obs.SetSourceRender("ScoreReportWithTimes", !scrollingRequired, "ScoreReportOverlay");
			Obs.SetSourceRender("ScrollingScoreReportWithTimes", scrollingRequired, "ScoreReportOverlay");
			m_answerShowing = true;
		}

		private void HideAnswer()
		{
			Obs.SetCurrentScene("CamScene");
			HideFullScreenPicture(false);
			showAnswerButton.Background = System.Windows.Media.Brushes.LightGreen;
			showAnswerText.Text = "Show Answer";
			showPictureButton.IsEnabled = false;
			m_answerShowing = false;
		}

		private void ShowAnswerButton_Click(object sender, RoutedEventArgs e)
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
			Obs.SetCurrentScene("LeaderboardScene");
			showLeaderboardButton.Background = System.Windows.Media.Brushes.Pink;
			showLeaderboardText.Text = "Hide Leaderboard";
			showPictureButton.IsEnabled = false;
			m_leaderboardShowing = true;
		}

		private void HideLeaderboard()
		{
			Obs.SetCurrentScene("CamScene");
			showLeaderboardButton.Background = System.Windows.Media.Brushes.LightGreen;
			showLeaderboardText.Text = "Show Leaderboard";
			showPictureButton.IsEnabled = m_questionShowing && !String.IsNullOrEmpty(m_currentQuestion.QuestionImageFilename) && Quiz.HasMediaFile(m_currentQuestion.QuestionImageFilename);
			m_leaderboardShowing = false;
		}

		private void ShowLeaderboardButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_leaderboardShowing)
				HideLeaderboard();
			else
				ShowLeaderboard();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (MessageBox.Show(this, "Reset Scores?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
			{
				m_scoresDirty = true;
				m_scores.Clear();
				WriteScoresToFile();
				UpdateLeaderboard();
			}
		}

		private void HideOBSSource(string sourceName, string sceneName)
		{
			Obs.SetSourceRender(sourceName, false, sceneName);
		}

		private void ShowOBSSource(string sourceName, string sceneName)
		{
			Obs.SetSourceRender(sourceName, true, sceneName);
		}

		private void SetOBSImageSource(string sourceName, string mediaName)
		{
			string path = Quiz.GetMediaPath(mediaName);
			if ((String.IsNullOrEmpty(path)) || (!File.Exists(path)))
			{
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				path = Path.Combine(presFolder, "transparent.png");
			}
			SetOBSFileSourceFromPath(sourceName, "file", path);
		}

		private void SetOBSVideoSource(string sourceName, string mediaName)
		{
			string[] scenes = new string[] { "QuestionScene", "FullScreenPictureQuestionScene" };
			string path = Quiz.GetMediaPath(mediaName);
			if ((String.IsNullOrEmpty(path)) || (!File.Exists(path)))
			{
				foreach (string sceneName in scenes)
					HideOBSSource(sourceName, sceneName);
			}
			else
			{
				SetOBSFileSourceFromPath(sourceName, "local_file", path);
				foreach (string sceneName in scenes)
					ShowOBSSource(sourceName, sceneName);
			}
		}

		private void SetOBSFileSourceFromPath(string sourceName, string setting, string path)
		{
			JObject settings = new JObject()
			{
				{setting,path }
			};
			Obs.SetSourceSettings(sourceName, settings);
		}

		private void SetOBSAudioSource(string sourceName, string mediaName)
		{
			string path = Quiz.GetMediaPath(mediaName);
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
			Obs.SetSourceSettings(sourceName, settings);
		}

		private void GenerateTextImage(string text, string sourceName, string filename)
		{
			string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
			string path = Path.Combine(presFolder, filename);
			using (TextImageBitmap tiBitmap=new TextImageBitmap(text))
			{
				tiBitmap.Save(path);
			}
			SetOBSFileSourceFromPath(sourceName, "file", path);
		}

		private void HideFullScreenPicture(bool setScene = true)
		{
			if (setScene)
				if (m_answerShowing)
				{
					bool hasPic = !String.IsNullOrEmpty(m_currentQuestion.AnswerImageFilename) && Quiz.HasMediaFile(m_currentQuestion.AnswerImageFilename);
					Obs.SetCurrentScene(hasPic ? "AnswerScene" : "NoPicAnswerScene");
				}
				else if (m_questionShowing)
				{
					bool hasPicOrVid = (m_currentQuestion.QuestionMediaType == MediaType.Image || m_currentQuestion.QuestionMediaType == MediaType.Video) && Quiz.HasMediaFile(m_currentQuestion.QuestionMediaFilename);
					bool hasSupPicOrVid = (m_currentQuestion.QuestionSupplementaryMediaType == MediaType.Image || m_currentQuestion.QuestionSupplementaryMediaType == MediaType.Video) && Quiz.HasMediaFile(m_currentQuestion.QuestionSupplementaryMediaFilename);
					Obs.SetCurrentScene(hasPicOrVid || hasSupPicOrVid ? "QuestionScene" : "NoPicQuestionScene");
				}
			showPictureButton.Background = System.Windows.Media.Brushes.LightGreen;
			showPictureText.Text = "Fullscreen Picture";
			m_fullScreenPictureShowing = false;
		}

		private void ShowFullScreenPicture()
		{
			if (m_answerShowing)
			{
				bool hasPic = !string.IsNullOrEmpty(m_currentQuestion.AnswerImageFilename) && Quiz.HasMediaFile(m_currentQuestion.AnswerImageFilename);
				Obs.SetCurrentScene(hasPic ? "FullScreenPictureAnswerScene" : "NoPicAnswerScene");
			}
			else if (m_questionShowing)
				Obs.SetCurrentScene("FullScreenPictureQuestionScene");
			showPictureButton.Background = System.Windows.Media.Brushes.Pink;
			showPictureText.Text = "Embedded Picture";
			m_fullScreenPictureShowing = true;
		}

		private void ShowPictureButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_fullScreenPictureShowing)
				HideFullScreenPicture();
			else
				ShowFullScreenPicture();
		}

		private void IncreaseScoreButton_Click(object sender, RoutedEventArgs e)
		{
			ContestantScore score = (ContestantScore)leaderboardList.SelectedItem;
			m_scores[score.Contestant] = m_scores[score.Contestant] + 1;
			WriteScoresToFile();
			m_scoresDirty = true;
			UpdateLeaderboard(false, score.Contestant);
		}

		private void DecreaseScoreButton_Click(object sender, RoutedEventArgs e)
		{
			ContestantScore score = (ContestantScore)leaderboardList.SelectedItem;
			m_scores[score.Contestant] = m_scores[score.Contestant] - 1;
			WriteScoresToFile();
			m_scoresDirty = true;
			UpdateLeaderboard(false, score.Contestant);
		}

		private void LeaderboardList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			decreaseScoreButton.IsEnabled = increaseScoreButton.IsEnabled = (leaderboardList.SelectedItems.Count == 1);
		}

		private void MuteBGM(bool mute)
		{
			Obs.SetMute("BGM", mute);
			Obs.SetMute("QuestionBGM", mute);
		}

		private void MuteBGM_Checked(object sender, RoutedEventArgs e)
		{
			MuteBGM(true);
		}

		private void MuteBGM_Unchecked(object sender, RoutedEventArgs e)
		{
			MuteBGM(false);
		}

		private void ViewAnswerHistory_Click(object sender, RoutedEventArgs e)
		{
			string dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "data");
			string answersFilePath = Path.Combine(dataFolder, ANSWERS_FILENAME);
			if (File.Exists(answersFilePath))
				System.Diagnostics.Process.Start(answersFilePath);
		}

		private void SkipQuestion_Click(object sender, RoutedEventArgs e)
		{
			NextQuestion(m_nextQuestion);
		}

		private void SetQuestionVideo(string filename)
		{
			SetOBSVideoSource("QuestionVid", filename);
			Obs.SetVolume("QuestionVid", VIDEO_VOLUME);
		}

		private void ReplayAudioButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_currentQuestion.QuestionMediaType == MediaType.Audio)
				SetQuestionAudio(m_currentQuestion.QuestionAudioFilename);
			else if (m_currentQuestion.QuestionMediaType == MediaType.Video)
				SetQuestionVideo(m_currentQuestion.QuestionVideoFilename);
		}

		private void DummyAnswersButton_Click(object sender, RoutedEventArgs e)
		{
			BackgroundWorker testAnswersWorker = new TestAnswersBackgroundWorker(this);
			testAnswersWorker.RunWorkerAsync();
		}

		private void ShowTimeWarnings_Checked(object sender, RoutedEventArgs e)
		{
			ShowTimeWarnings = true;
		}

		private void ShowTimeWarnings_Unchecked(object sender, RoutedEventArgs e)
		{
			ShowTimeWarnings = false;
		}

		private void ShowChatWarnings_Checked(object sender, RoutedEventArgs e)
		{
			m_chatWarnings = true;
		}

		private void ShowChatWarnings_Unchecked(object sender, RoutedEventArgs e)
		{
			m_chatWarnings = false;
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "INI files (*.ini)|*.ini"
			};
			if (openFileDialog.ShowDialog() == true)
				LoadQuiz(openFileDialog.FileName);
		}

		public void OnMarkingComplete()
		{
			contestantName.Text = "<contestant name>";
			questionText.Text = "<no answers to mark yet>";
			Obs.SetCurrentScene("CamScene");
			m_questionShowing = false;
			HideFullScreenPicture(false);

			presentingButton.IsEnabled = showLeaderboardButton.IsEnabled = showAnswerButton.IsEnabled = true;
			skipQuestionButton.IsEnabled = newQuestionButton.IsEnabled = m_nextQuestion != -1;
			loadQuizButton.IsEnabled = true;
			showPictureButton.IsEnabled = false;

			restartMarking.IsEnabled = false;
			markingProgressBar.Value = markingProgressBar.Maximum;
			ApplyScores();
			UpdateMarkingProgressUI(null);
		}
	}
}
