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

		public QuizMutex VolumeMutex { get; } = new QuizMutex("Volume");
		public QuizMutex AnswerListMutex { get; } = new QuizMutex("AnswerList");
		public QuizMutex AnswerForMarkingMutex { get; } = new QuizMutex("AnswerForMarking");
		private readonly QuizMutex m_scoreReportMutex = new QuizMutex("ScoreReport");

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
		private readonly System.Drawing.Size QuestionTextSize;
		private readonly System.Drawing.Size NoPicQuestionTextSize;
		private readonly System.Drawing.Size AnswerTextSize;
		private readonly System.Drawing.Size NoPicAnswerTextSize;
		private readonly System.Drawing.Size ScoreReportWithTimesSize;
		private readonly System.Drawing.Size ScoreReportSize;

		public QuizControlPanel(bool presentationOnly)
		{
			PresentationOnly = presentationOnly;
			StartedOK = false;
			if (Configuration.ScenesAndSourcesPopulated)
			{
				InitializeComponent();
				ReadScoresFromFile();
				markingPump = new MarkingPumpBackgroundWorker(this);
				countdownWorker = new CountdownBackgroundWorker(this);
				answerCounter = new AnswerCounterBackgroundWorker(this);
				faderWorker = new FaderBackgroundWorker(this);
				ClearLeaderboards();
				try
				{
					Obs.Connect("ws://127.0.0.1:4444");
					if (Obs.IsConnected)
					{
						Obs.SetCurrentScene(Scene.Camera);
						UpdateLeaderboard(true);
						QuestionTextSize = Obs.GetSourceBoundsSize(Scene.Question, Source.QuestionText);
						NoPicQuestionTextSize = Obs.GetSourceBoundsSize(Scene.NoPictureQuestion, Source.QuestionText);
						AnswerTextSize = Obs.GetSourceBoundsSize(Scene.Answer, Source.AnswerText);
						NoPicAnswerTextSize = Obs.GetSourceBoundsSize(Scene.NoPictureAnswer, Source.AnswerText);
						ScoreReportWithTimesSize = Obs.GetSourceBoundsSize(Scene.ScoreReportOverlay, Source.ScoreReportWithTimes);
						ScoreReportSize = Obs.GetSourceBoundsSize(Scene.ScoreReportOverlay, Source.ScoreReport);
						SetCountdownMedia();
						UpdateScoreReports();
						SetBGMShuffle();
						HideCountdownOverlay();
						faderWorker.RunWorkerAsync();
						StartedOK = true;
					}
					else
						MessageBox.Show("Could not connect to OBS (is it running?).", ZoomQuizTitle);
				}
				catch (AuthFailureException)
				{
					MessageBox.Show("Failed to connect to OBS (authentication failed).", ZoomQuizTitle);
				}
				catch (ErrorResponseException ex)
				{
					MessageBox.Show("Failed to connect to OBS (" + ex.Message + ").", ZoomQuizTitle);
				}
				catch (Exception ex)
				{
					MessageBox.Show("Failed to connect to OBS (" + ex.Message + ").", ZoomQuizTitle);
				}
				if (PresentationOnly)
					presentingButton.IsEnabled = false;
			}
			else
				MessageBox.Show("Some scene and/or source names were not configured.", ZoomQuizTitle);
			if(!StartedOK)
				EndQuiz();
		}

		~QuizControlPanel()
		{
			VolumeMutex.Dispose();
			AnswerListMutex.Dispose();
			AnswerForMarkingMutex.Dispose();
			m_scoreReportMutex.Dispose();
			AnswerReceivedEvent.Dispose();
			AnswerCounterAnswerReceivedEvent.Dispose();
			AnswerMarkedEvent.Dispose();
			CountdownCompleteEvent.Dispose();
			QuitAppEvent.Dispose();
		}

		private void ClearLeaderboards()
		{
			Logger.Log("Deleting any existing leaderboards graphics.");
			string[] files = Directory.GetFiles(FileUtils.GetFolderPath("leaderboards"));
			foreach (string file in files)
				if (File.Exists(file))
					File.Delete(file);
		}

		private void SetCountdownMedia()
		{
			Logger.Log("Setting countdown media files.");
			Obs.SetFileSourceFromPath(Source.Countdown, "local_file", FileUtils.GetFilePath("presentation", "Countdown.mp4"));
			List<FilterSettings> filters = Obs.GetSourceFilters(Source.Countdown);
			foreach (FilterSettings st in filters.Where(f => f.Name.Contains("Image Mask")))
			{
				JObject maskSettings = st.Settings;
				maskSettings["image_path"] = FileUtils.GetFilePath("presentation", "circle.png");
				Obs.SetSourceFilterSettings(Source.Countdown, st.Name, maskSettings);
			}
		}

		private void SetBGMShuffle()
		{
			Logger.Log("Setting BGM media files.");
			SourceSettings bgmSettings = Obs.GetSourceSettings(Source.BGM);
			JObject bgmSourceSettings = bgmSettings.sourceSettings;
			bgmSourceSettings["loop"] = bgmSourceSettings["shuffle"] = true;
			bgmSourceSettings["playlist"][0]["value"] = FileUtils.GetFolderPath("bgm");
			Obs.SetSourceSettings(Source.BGM, bgmSourceSettings);
		}

		private void LoadQuiz(string quizFilePath)
		{
			Logger.Log($"Loading a quiz from \"{quizFilePath}\"");
			Quiz = new Quiz(quizFilePath);
			UpdateQuizList();
			if (Quiz.HasNoQuestions)
				MessageBox.Show("Error: no questions found.", ZoomQuizTitle);
			else if (Quiz.HasInvalidQuestions)
					MessageBox.Show("Warning: invalid questions found.", ZoomQuizTitle);
			m_nextQuestion = 0;
			NextQuestion(m_nextQuestion);
			skipQuestionButton.IsEnabled = newQuestionButton.IsEnabled = m_nextQuestion != -1;
		}

		private void UpdateQuizList()
		{
			Logger.Log("Updating quiz list UI");
			quizList.ItemsSource = Quiz;
			ICollectionView view = CollectionViewSource.GetDefaultView(quizList.ItemsSource);
			view.Refresh();
			if (!Quiz.HasNoQuestions)
			{
				quizList.SelectedIndex = 0;
				quizList.ScrollIntoView(Quiz[1]);
			}
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			Logger.Log("Window closing event has fired.");
			if (!PresentationOnly && !m_quizEnded)
				e.Cancel = true;
			else
				QuitAppEvent.Set();
		}

		public void EndQuiz()
		{
			Logger.Log("Ending the quiz.");
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
				m_myID = partCon.GetParticipantsList().Where(id => partCon.GetUserByUserID(id).IsMySelf()).FirstOrDefault();

				CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingAudioController().JoinVoip();
				SendPublicChat("🥂 Welcome to the quiz!");

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
			Logger.Log("Resetting the answer bins.");
			var values = Enum.GetValues(typeof(AnswerResult));
			AnswerBins.Clear();
			foreach (AnswerResult result in values)
				AnswerBins[result] = new AnswerBin(result);
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
				Logger.Log($"Setting chat mode to {mode}");
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
			Logger.Log($"Adding answer \"{answer}\" from {contestant} to answer list.");
			AnswerListMutex.With(() =>
			{
				if (!Answers.TryGetValue(contestant, out List<Answer> answerList))
					answerList = new List<Answer>();
				answerList.Add(answer);
				Answers[contestant] = answerList;
			});
			Logger.Log("Setting the AnswerReceived event.");
			AnswerReceivedEvent.Set();
			Logger.Log("Setting the AnswerCounterAnswerReceived event.");
			AnswerCounterAnswerReceivedEvent.Set();
		}

		public void OnAnswerReceived(IChatMsgInfoDotNetWrap chatMsg)
		{
			uint senderID = chatMsg.GetSenderUserId();
			if (senderID != m_myID)
			{
				string sender = chatMsg.GetSenderDisplayName();
				string answer = chatMsg.GetContent();
				Logger.Log($"Received answer \"{answer}\" from {sender} ({senderID})");
				AddAnswer(new Contestant(senderID, sender), new Answer(answer));
			}
		}

		private void StartPresenting()
		{
			if (!PresentationOnly)
			{
				Logger.Log("Starting presenting.");
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
				Logger.Log("Stopping presenting.");
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
			Logger.Log("StartQuestion has been clicked.");
			m_scoreReportMutex.With(() => m_scoreReport.Clear());
			UpdateScoreReports();
			m_lastAnswerResults.Clear();
			Answers.Clear();
			HideAnswer();
			m_currentQuestion = Quiz[m_nextQuestion];
			questionTextBox.Text = m_currentQuestion.QuestionText;
			answerTextBox.Text = m_currentQuestion.AnswerText;
			infoTextBox.Text = m_currentQuestion.Info;
			ResetAnswerBins(m_currentQuestion);
			bool hasPicOrVid = m_currentQuestion.HasAnyQuestionMedia(Quiz, MediaType.Video, MediaType.Image);
			GenerateTextImage(m_currentQuestion.QuestionText, Source.QuestionText, "question.png", hasPicOrVid ? QuestionTextSize : NoPicQuestionTextSize);
			Obs.SetImageSource(Quiz, Source.QuestionImage, m_currentQuestion.QuestionImageFilename);
			// Show no video until it's ready.
			Obs.SetVideoSource(Quiz, Source.QuestionVideo, null);
			Obs.SetAudioSource(Quiz, Source.QuestionBGM, m_currentQuestion.QuestionBGMFilename);
			SetVolumes(false, m_currentQuestion);
			HideCountdownOverlay();
			SetChatMode(ChatMode.HostOnly);
			StartPresenting();
			HideFullScreenPicture(false);
			showPictureButton.IsEnabled =
				replayAudioButton.IsEnabled =
				skipQuestionButton.IsEnabled =
				showAnswerButton.IsEnabled =
				presentingButton.IsEnabled =
				loadQuizButton.IsEnabled =
				newQuestionButton.IsEnabled =
				showLeaderboardButton.IsEnabled = false;
			markingProgressBar.Maximum = 1;
			markingProgressBar.Value = 0;
			markingProgressText.Text = "";
			showQuestionButton.IsEnabled = true;
			if (!PresentationOnly)
				SendPublicChat("✏️ Here comes the next question ...");
		}

		public void SendPublicChat(string chatMessage)
		{
			Logger.Log("Sending message \"{chatMessage}\" publicly..");
			CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().SendChatTo(0, chatMessage);
		}

		private int NextQuestion(int currentQuestion)
		{
			Logger.Log("Moving to next question.");
			m_nextQuestion = Quiz.GetNextQuestionNumber(currentQuestion);
			Logger.Log($"Next question number is {m_nextQuestion}.");
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
			Logger.Log("Countdown is complete.");
			CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().GetMeetingChatController().Remove_CB_onChatMsgNotifcation(OnAnswerReceived);
			NextQuestion(m_nextQuestion);
			SetChatMode(ChatMode.EveryonePublicly);
			AnswerForMarkingMutex.With(() => presentingButton.IsEnabled = m_answerForMarking == null);
		}

		private void UpdateScoreReports()
		{
			UpdateScoreReport(false);
			UpdateScoreReport(true);
		}

		private void UpdateScoreReport(bool times)
		{
			Logger.Log($"Updating score report with{(times ? "":"out")} times.");
			m_scoreReportMutex.With(() =>
			{
				using (ScoreReportBitmap bitmap = new ScoreReportBitmap(Configuration.ScoreReportFont, m_scoreReport, times, times ? ScoreReportWithTimesSize : ScoreReportSize))
				{
					string bitmapPath = FileUtils.GetFilePath("presentation", times ? SCORE_REPORT_WITH_TIMES_FILENAME : SCORE_REPORT_FILENAME);
					bitmap.Save(bitmapPath);
					Obs.SetFileSourceFromPath(times ? Source.ScoreReportWithTimes : Source.ScoreReport, "file", bitmapPath);
					if (times)
						Obs.SetFileSourceFromPath(Source.ScrollingScoreReportWithTimes, "file", bitmapPath);
				}
			});
		}

		private void AddToScoreReport(DateTime answerTime, Contestant contestant, AnswerResult result)
		{
			m_scoreReportMutex.With(() =>
			{
				ScoreReportEntry firstReport = m_scoreReport.LastOrDefault();
				TimeSpan offset = firstReport == null ? new TimeSpan(0) : answerTime - firstReport.AnswerTime;
				ScoreReportEntry newScoreReport = new ScoreReportEntry(answerTime, contestant, result, offset);
				Logger.Log($"Adding {newScoreReport.GetScoreReportString(true)} to score report.");
				m_scoreReport.Insert(0, newScoreReport);
				UpdateScoreReports();
			});
		}

		public void MarkAnswer(AnswerForMarking answer, AnswerResult result, double levValue, bool autoCountdown)
		{
			Logger.Log($"Manually marking answer {answer} as {result}.");
			answer.Answer.AnswerResult = result;
			AnswerBin bin = AnswerBins[result];
			if (bin != null)
				bin.Add(answer.Answer, levValue);
			if (result == AnswerResult.Correct)
			{
				AddToScoreReport(answer.Answer.AnswerTime, answer.Contestant, result);
				if (autoCountdown)
				{
					Logger.Log("Correct answer is starting countdown automatically.");
					markingPump.ReportProgress(0, null);
				}
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
			Logger.Log($"Setting {answerForMarking} as the next answer for marking.");
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
			string scoresFilePath = FileUtils.GetFilePath("data", SCORES_FILENAME);
			if (File.Exists(scoresFilePath))
			{
				Logger.Log("Reading existing scores file.");
				using (StreamReader sr = File.OpenText(scoresFilePath))
				{
					string strLine;
					while ((strLine = sr.ReadLine()) != null)
					{
						string[] bits = strLine.Split('\t');
						if (bits.Length == 3)
						{
							Contestant c = new Contestant(uint.Parse(bits[0]), bits[1]);
							int score = int.Parse(bits[2]);
							m_scores[c] = score;
						}
					}
				}
			}
		}

		private void WriteScoresToFile()
		{
			Logger.Log("Writing scores file.");
			string scoresFilePath = FileUtils.GetFilePath("data", SCORES_FILENAME);
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
			Logger.Log("Updating leaderboards.");
			Logger.Log("Constructing scores data.");
			SortedList<int, List<Contestant>> scores = new SortedList<int, List<Contestant>>();
			foreach (KeyValuePair<Contestant, int> kvp in m_scores)
			{
				if (!scores.TryGetValue(kvp.Value, out List<Contestant> cs))
				{
					cs = new List<Contestant>();
					scores[kvp.Value] = cs;
				}
				cs.Add(kvp.Key);
			}
			Logger.Log("Constructing reversed scores data.");
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
			Logger.Log("Setting score data in UI.");
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
			for (int n = 0, leaderboardCount = 1; n < scores.Count; ++leaderboardCount)
				using (LeaderboardBitmap b = new LeaderboardBitmap(Configuration.LeaderboardFont, scores, leaderboardCount, ref n))
				{
					string path = FileUtils.GetFilePath("leaderboards", "leaderboard" + leaderboardCount + ".png");
					b.Save(path);
				}
			SourceSettings lbSourceSettings = Obs.GetSourceSettings(Source.Leaderboard);
			JObject lbSettings = lbSourceSettings.sourceSettings;
			lbSettings["files"][0]["value"] = FileUtils.GetFolderPath("leaderboards");
			Obs.SetSourceSettings(Source.Leaderboard, lbSettings);
		}

		private string GetLevenshteinReport(AnswerResult result)
		{
			AnswerBin bin = AnswerBins[result];
			bin.GetLevenshteinRange(out double minLev, out double maxLev);
			return result.ToString() + " answer Levenshtein values ranged from " + string.Format("{0:0.00}", minLev) + " to " + string.Format("{0:0.00}", maxLev);
		}

		private void ApplyScores()
		{
			Logger.Log($"Applying scores for the last question.");
			string answersFilePath = FileUtils.GetFilePath("data", ANSWERS_FILENAME);
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
						Logger.Log($"Adding {scoreForAnswer} to existing score for {kvp.Key}.");
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
			Logger.Log("Hiding the countdown overlay.");
			Obs.HideSource(Source.CountdownOverlay, Scene.Camera);
			Obs.HideSource(Source.CountdownOverlay, Scene.FullScreenQuestionPicture);
			Obs.HideSource(Source.CountdownOverlay, Scene.FullScreenAnswerPicture);
			Obs.HideSource(Source.ScoreReportWithTimes, Scene.ScoreReportOverlay);
			Obs.HideSource(Source.ScrollingScoreReportWithTimes, Scene.ScoreReportOverlay);
			Obs.ShowSource(Source.ScoreReport, Scene.ScoreReportOverlay);
		}

		private void ShowCountdownOverlay()
		{
			Logger.Log("Showing the countdown overlay.");
			Obs.ShowSource(Source.CountdownOverlay, Scene.Camera);
			Obs.ShowSource(Source.CountdownOverlay, Scene.FullScreenQuestionPicture);
			Obs.ShowSource(Source.CountdownOverlay, Scene.FullScreenAnswerPicture);
		}

		public void StartCountdown()
		{
			if (startCountdownButton.IsEnabled)
			{
				Logger.Log("Starting the countdown.");
				startCountdownButton.IsEnabled = false;
				countdownWorker.RunWorkerAsync();
				ShowCountdownOverlay();
			}
		}

		private void StartCountdownButtonClick(object sender, RoutedEventArgs e)
		{
			Logger.Log("Starting the countdown manually.");
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
			Logger.Log($"Marking all other answers from {contestant} as inconsequential.");
			// All other unmarked answers from the contestant must now be marked wrong,
			// in case they're guessing numeric answers.
			AnswerListMutex.With(() =>
			{
				List<Answer> answers = Answers[contestant];
				if (answers != null)
				{
					IEnumerable<Answer> unmarkedAnswers = answers.Where(a => a.AnswerResult == AnswerResult.Unmarked);
					foreach (Answer a in unmarkedAnswers)
					{
						if (a.IsDeliberatelyFunny)
						{
							markingPump.ReportProgress(0, new FunnyAnswerArgs(a, contestant));
							a.AnswerResult = AnswerResult.Funny;
						}
						else
							a.AnswerResult = AnswerResult.NotAnAnswer;
					}
				}
			});
		}

		private void ClearAnswerForMarking()
		{
			AnswerForMarkingMutex.With(() =>
			{
				Logger.Log("Clearing the answer for marking.");
				if (m_answerForMarking != null)
				{
					m_answerForMarking = null;
					contestantName.Text = "<contestant name>";
					questionText.Text = "<no answers to mark yet>";
					correctAnswerButton.IsEnabled =
						almostCorrectAnswerButton.IsEnabled =
						wrongAnswerButton.IsEnabled =
						funnyAnswerButton.IsEnabled =
						notAnAnswerButton.IsEnabled = false;
					AnswerMarkedEvent.Set();
				}
			});
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
			AnswerForMarkingMutex.With(() =>
			{
				if (m_answerForMarking != null)
				{
					restartMarking.IsEnabled = true;
					MarkAnswer(m_answerForMarking, result, GetBestLevenshtein(result, m_answerForMarking.Answer.NormalizedAnswer), autoCountdown.IsChecked == true);
					ClearAnswerForMarking();
					AnswerMarkedEvent.Set();
				}
			});
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
			Logger.Log("Restarting marking.");
			AnswerListMutex.With(() =>
			{
				foreach (KeyValuePair<Contestant, List<Answer>> kvp in Answers)
					foreach (Answer a in kvp.Value)
						a.AnswerResult = AnswerResult.Unmarked;
				ResetAnswerBins(m_currentQuestion);
			});
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
			VolumeMutex.With(() =>
			{
				bool hasAudioOrVideo = currentQuestion.HasQuestionMedia(Quiz, MediaType.Audio, MediaType.Video);
				if (questionShowing && hasAudioOrVideo)
				{
					QuestionAudioVolume = AUDIO_VOLUME;
					QuestionVideoVolume = VIDEO_VOLUME;
					BgmVolume = QuestionBGMVolume = 0;
				}
				else if (currentQuestion.HasQuestionBGM(Quiz))
				{
					QuestionBGMVolume = BGM_VOLUME;
					BgmVolume = QuestionAudioVolume = QuestionVideoVolume = 0;
				}
				else
				{
					BgmVolume = BGM_VOLUME;
					QuestionBGMVolume = QuestionAudioVolume = QuestionVideoVolume = 0;
				}
			});
		}

		private void ShowQuestionButton_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("ShowQuestion button has been clicked.");
			if (!PresentationOnly)
			{
				CountdownCompleteEvent.Reset();
				markingPump.RunWorkerAsync(new MarkingPumpArgs(m_currentQuestion.UseLevenshtein, autoCountdown.IsChecked == true));
				answerCounter.RunWorkerAsync();
			}

			bool hasPicOrVid = m_currentQuestion.HasAnyQuestionMedia(Quiz, MediaType.Video, MediaType.Image);
			bool hasAudio = m_currentQuestion.HasQuestionMedia(Quiz, MediaType.Audio);
			bool hasVideo = m_currentQuestion.HasQuestionMedia(Quiz, MediaType.Video);
			bool hasAnswerPic = m_currentQuestion.HasAnswerMedia(Quiz, MediaType.Image);
			Obs.SetCurrentScene(hasPicOrVid ? Scene.Question : Scene.NoPictureQuestion);
			GenerateTextImage(m_currentQuestion.AnswerText, Source.AnswerText, "answer.png", hasAnswerPic ? AnswerTextSize : NoPicAnswerTextSize);
			Obs.SetImageSource(Quiz, Source.AnswerImage, m_currentQuestion.AnswerImageFilename);
			m_questionShowing = true;
			// Set question audio to silence, wait for play button
			SetQuestionAudio(null);
			SetVolumes(true, m_currentQuestion);
			replayAudioButton.IsEnabled = hasAudio | hasVideo;
			showPictureButton.IsEnabled = hasPicOrVid;
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
			bool hasAudio = m_currentQuestion.HasQuestionMedia(Quiz, MediaType.Audio);
			if (hasAudio)
				Obs.SetVolume(Source.QuestionAudio, AUDIO_VOLUME);
			Obs.SetAudioSource(Quiz, Source.QuestionAudio, questionAudioFilename);
		}

		private void ShowAnswer()
		{
			Logger.Log("Showing the answer.");

			HideLeaderboard();
			VolumeMutex.With(() =>
			{
				BgmVolume = BGM_VOLUME;
				QuestionBGMVolume = QuestionAudioVolume = QuestionVideoVolume = 0;
			});
			bool hasPic = m_currentQuestion.HasAnswerMedia(Quiz, MediaType.Image);
			showPictureButton.IsEnabled = hasPic;
			Obs.SetCurrentScene(hasPic ? (m_fullScreenPictureShowing ? Scene.FullScreenAnswerPicture : Scene.Answer) : Scene.NoPictureAnswer);
			showAnswerButton.Background = System.Windows.Media.Brushes.Pink;
			showAnswerText.Text = "Hide Answer";
			// The score report can show a maximum of 17 names. More than that requires a scrolling report.
			bool scrollingRequired = m_scoreReport.Count > 17;
			Obs.HideSource(Source.ScoreReport, Scene.ScoreReportOverlay);
			Obs.SetSourceRender(Source.ScoreReportWithTimes, Scene.ScoreReportOverlay, !scrollingRequired);
			Obs.SetSourceRender(Source.ScrollingScoreReportWithTimes, Scene.ScoreReportOverlay, scrollingRequired);
			m_answerShowing = true;
		}

		private void HideAnswer()
		{
			Logger.Log("Hiding the answer.");
			Obs.SetCurrentScene(Scene.Camera);
			HideFullScreenPicture(false);
			showAnswerButton.Background = System.Windows.Media.Brushes.LightGreen;
			showAnswerText.Text = "Show Answer";
			showPictureButton.IsEnabled = false;
			m_answerShowing = false;
		}

		private void ShowAnswerButton_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("ShowAnswer button has been clicked.");
			if (m_answerShowing)
				HideAnswer();
			else
				ShowAnswer();
		}

		private void ShowLeaderboard()
		{
			Logger.Log("Showing the leaderboard.");
			HideAnswer();
			if (m_scoresDirty)
			{
				UpdateLeaderboard(true);
				m_scoresDirty = false;
			}
			Obs.SetCurrentScene(Scene.Leaderboard);
			showLeaderboardButton.Background = System.Windows.Media.Brushes.Pink;
			showLeaderboardText.Text = "Hide Leaderboard";
			showPictureButton.IsEnabled = false;
			m_leaderboardShowing = true;
		}

		private void HideLeaderboard()
		{
			Logger.Log("Hiding the leaderboard.");
			Obs.SetCurrentScene(Scene.Camera);
			showLeaderboardButton.Background = System.Windows.Media.Brushes.LightGreen;
			showLeaderboardText.Text = "Show Leaderboard";
			showPictureButton.IsEnabled = m_questionShowing && m_currentQuestion.HasQuestionMedia(Quiz, MediaType.Image);
			m_leaderboardShowing = false;
		}

		private void ShowLeaderboardButton_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("ShowLeaderboard button has been clicked.");
			if (m_leaderboardShowing)
				HideLeaderboard();
			else
				ShowLeaderboard();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("ResetScores button has been clicked.");
			if (MessageBox.Show(this, "Reset Scores?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
			{
				m_scoresDirty = true;
				m_scores.Clear();
				WriteScoresToFile();
				UpdateLeaderboard();
				string leaderboardsFolder = FileUtils.GetFolderPath("leaderboards");
				string[] files = Directory.GetFiles(leaderboardsFolder, "leaderboard*.png", SearchOption.AllDirectories);
				foreach (string file in files)
					File.Delete(file);
			}
		}

		private void GenerateTextImage(string text, Source source, string filename, System.Drawing.Size textSize)
		{
			Logger.Log($"Generating image for text \"{text}\", and saving to \"{filename}\" for OBS {source} source.");
			string path = FileUtils.GetFilePath("presentation", filename);
			using (TextImageBitmap tiBitmap = new TextImageBitmap(text, Configuration.QuestionAndAnswerFont, textSize))
			{
				tiBitmap.Save(path);
			}
			Obs.SetFileSourceFromPath(source, "file", path);
		}

		private void HideFullScreenPicture(bool setScene = true)
		{
			Logger.Log("Reverting back to small media.");
			if (setScene)
				if (m_answerShowing)
					Obs.SetCurrentScene(m_currentQuestion.HasAnswerMedia(Quiz, MediaType.Image) ? Scene.Answer : Scene.NoPictureAnswer);
				else if (m_questionShowing)
					Obs.SetCurrentScene(m_currentQuestion.HasAnyQuestionMedia(Quiz, MediaType.Video, MediaType.Image) ? Scene.Question : Scene.NoPictureQuestion);
			showPictureButton.Background = System.Windows.Media.Brushes.LightGreen;
			showPictureText.Text = "Fullscreen Picture";
			m_fullScreenPictureShowing = false;
		}

		private void ShowFullScreenPicture()
		{
			Logger.Log("Showing the fullscreen media.");
			if (m_answerShowing)
				Obs.SetCurrentScene(m_currentQuestion.HasAnswerMedia(Quiz, MediaType.Image) ? Scene.FullScreenAnswerPicture : Scene.NoPictureAnswer);
			else if (m_questionShowing)
				Obs.SetCurrentScene(Scene.FullScreenQuestionPicture);
			showPictureButton.Background = System.Windows.Media.Brushes.Pink;
			showPictureText.Text = "Embedded Picture";
			m_fullScreenPictureShowing = true;
		}

		private void ShowPictureButton_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("ShowFullscreen has been clicked.");
			if (m_fullScreenPictureShowing)
				HideFullScreenPicture();
			else
				ShowFullScreenPicture();
		}

		private void AlterSelectedContestantScore(int diff)
		{
			ContestantScore score = (ContestantScore)leaderboardList.SelectedItem;
			Logger.Log($"Adjusting score for {score.Contestant} by {diff}");
			m_scores[score.Contestant] = m_scores[score.Contestant] + diff;
			WriteScoresToFile();
			m_scoresDirty = true;
			UpdateLeaderboard(false, score.Contestant);
		}

		private void IncreaseScoreButton_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("Increase score button has been clicked.");
			AlterSelectedContestantScore(1);
		}

		private void DecreaseScoreButton_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("Decrease score button has been clicked.");
			AlterSelectedContestantScore(-1);
		}

		private void LeaderboardList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			decreaseScoreButton.IsEnabled = increaseScoreButton.IsEnabled = (leaderboardList.SelectedItems.Count == 1);
		}

		private void MuteBGM(bool mute)
		{
			Logger.Log($"{(mute ? "Muting" : "Unmuting")} the BGM.");
			Obs.SetMute(Source.BGM, mute);
			Obs.SetMute(Source.QuestionBGM, mute);
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
			string answersFilePath = FileUtils.GetFilePath("data", ANSWERS_FILENAME);
			if (File.Exists(answersFilePath))
				System.Diagnostics.Process.Start(answersFilePath);
		}

		private void SkipQuestion_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("SkipQuestion button has been clicked.");
			NextQuestion(m_nextQuestion);
		}

		private void SetQuestionVideo(string filename)
		{
			Obs.SetVideoSource(Quiz, Source.QuestionVideo, filename);
			Obs.SetVolume(Source.QuestionVideo, VIDEO_VOLUME);
		}

		private void ReplayAudioButton_Click(object sender, RoutedEventArgs e)
		{
			Logger.Log("ReplayAudio button has been clicked.");
			if (m_currentQuestion.QuestionMediaType == MediaType.Audio)
				SetQuestionAudio(m_currentQuestion.QuestionAudioFilename);
			else if (m_currentQuestion.QuestionMediaType == MediaType.Video)
				SetQuestionVideo(m_currentQuestion.QuestionVideoFilename);
		}

		private void DummyAnswersButton_Click(object sender, RoutedEventArgs e)
		{
			new TestAnswersBackgroundWorker(this).RunWorkerAsync();
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

		private void LoadQuiz_Clicked(object sender, RoutedEventArgs e)
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
			Logger.Log("OnMarkingComplete called.");
			contestantName.Text = "<contestant name>";
			questionText.Text = "<no answers to mark yet>";
			Obs.SetCurrentScene(Scene.Camera);
			m_questionShowing = false;
			HideFullScreenPicture(false);

			presentingButton.IsEnabled =
				showLeaderboardButton.IsEnabled =
				showAnswerButton.IsEnabled =
				loadQuizButton.IsEnabled = true;
			showPictureButton.IsEnabled =
				restartMarking.IsEnabled = false;
			skipQuestionButton.IsEnabled =
				newQuestionButton.IsEnabled = m_nextQuestion != -1;
			markingProgressBar.Value = markingProgressBar.Maximum;
			ApplyScores();
			UpdateMarkingProgressUI(null);
		}
	}
}
