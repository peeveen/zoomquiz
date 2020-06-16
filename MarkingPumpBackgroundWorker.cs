﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.ComponentModel;

namespace ZoomQuiz
{
	class MarkingPumpBackgroundWorker:BackgroundWorker
	{
		private IQuizContext Context { get; set; }
		internal MarkingPumpBackgroundWorker(IQuizContext context)
		{
			Context = context;
			DoWork += MarkingPumpDoWork;
			ProgressChanged += MarkingPumpProgressChanged;
			WorkerReportsProgress = true;
			RunWorkerCompleted += MarkingPumpRunWorkerCompleted;
		}
		private void MarkingPumpDoWork(object sender, DoWorkEventArgs e)
		{
			MarkingPumpArgs markingPumpArgs = (MarkingPumpArgs)e.Argument;
			bool lev = markingPumpArgs.UseLevenshtein;
			bool autoCountdown = markingPumpArgs.AutoCountdown;
			void UpdateMarkingProgress(AnswerForMarking nextAnswerForMarking = null)
			{
				int answerCount = Context.Answers.Sum(kvp2 => kvp2.Value.Count);
				int markedAnswerCount = Context.Answers.Sum(kvp2 => kvp2.Value.Count(a => a.AnswerResult != AnswerResult.Unmarked));
				int percentage = answerCount == 0 ? 0 : (int)((double)markedAnswerCount / answerCount);
				ReportProgress(percentage * 100, new MarkingProgress(answerCount, markedAnswerCount));
			}
			void SetAnswerForMarking(AnswerForMarking nextAnswerForMarking)
			{
				ReportProgress(0, nextAnswerForMarking);
			}
			bool waitingForMarking = false;
			WaitHandle[] events = new WaitHandle[] { Context.AnswerMarkedEvent, Context.AnswerReceivedEvent, Context.QuitAppEvent, Context.CountdownCompleteEvent };
			for (; ; )
			{
				int result = WaitHandle.WaitAny(events);
				if (result == 2)
					break;
				if (result == 3)
				{
					if (!waitingForMarking)
						break;
					events = new WaitHandle[] { Context.AnswerMarkedEvent, Context.AnswerReceivedEvent, Context.QuitAppEvent };
				}
				if (result == 0)
					waitingForMarking = false;
				try
				{
					Context.AnswerListMutex.WaitOne();
					foreach (KeyValuePair<Contestant, List<Answer>> kvp in Context.Answers)
					{
						Answer unmarkedAnswer = kvp.Value.FirstOrDefault(a => a.AnswerResult == AnswerResult.Unmarked);
						if (unmarkedAnswer != null)
						{
							AnswerForMarking answerForMarking = new AnswerForMarking(kvp.Key, unmarkedAnswer);
							if (!AutoMarkAnswer(answerForMarking, lev, autoCountdown))
							{
								if (!waitingForMarking)
								{
									waitingForMarking = true;
									SetAnswerForMarking(answerForMarking);
								}
							}
							else
								UpdateMarkingProgress();
						}
						else
							UpdateMarkingProgress();
					}
					if (waitingForMarking)
						continue;
					if (Context.CountdownCompleteEvent.WaitOne(0))
						// Nothing left to mark, and no more answers incoming? We're done.
						break;
				}
				finally
				{
					Context.AnswerListMutex.ReleaseMutex();
				}
				if (result == 2)
					break;
			}
			UpdateMarkingProgress();
		}

		private bool AutoMarkAnswer(AnswerForMarking answer, bool useLev, bool autoCountdown)
		{
			bool startsWithDot = answer.Answer.AnswerText.StartsWith(".");
			// If user has already submitted an answer that was accepted, don't accept this new one as an answer.
			if (!startsWithDot)
			{
				if (Context.Answers.ContainsKey(answer.Contestant))
				{
					List<Answer> contestantAnswers = Context.Answers[answer.Contestant];
					if (contestantAnswers.Any(a => a.IsAcceptedAnswer))
					{
						Context.MarkAnswer(answer, startsWithDot ? AnswerResult.Funny : AnswerResult.NotAnAnswer, 0.0, autoCountdown);
						return true;
					}
				}
				// Otherwise, if the user has submitted an answer that has already been marked, use that marking.
				double levValue = 0.0;
				foreach (KeyValuePair<AnswerResult, AnswerBin> kvp in Context.AnswerBins)
					if (kvp.Value.Contains(answer.Answer) || (useLev && kvp.Value.LevContains(answer.Answer, out levValue)))
					{
						Context.MarkAnswer(answer, kvp.Key, levValue, autoCountdown);
						return true;
					}
			}
			else
			{
				Context.MarkAnswer(answer, AnswerResult.Funny, 0.0, autoCountdown);
				return true;
			}
			// Otherwise, no, have to do it manually.
			return false;
		}

		private void MarkingPumpProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			try
			{
				Context.AnswerForMarkingMutex.WaitOne();
				object o = e.UserState;
				if (o is CountdownStartArgs)
					Context.StartCountdown();
				else if (o is FunnyAnswerArgs)
					Context.SendPublicChat(o.ToString());
				else if (o is MarkingProgress progress)
					Context.UpdateMarkingProgressUI(progress);
				else if (o is AnswerForMarking marking)
					Context.SetAnswerForMarking(marking);
			}
			finally
			{
				Context.AnswerForMarkingMutex.ReleaseMutex();
			}
		}

		private void MarkingPumpRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			Context.OnMarkingComplete();
		}
	}
}
