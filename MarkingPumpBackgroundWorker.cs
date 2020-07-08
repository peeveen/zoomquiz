using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.ComponentModel;

namespace ZoomQuiz
{
	class MarkingPumpBackgroundWorker : QuizBackgroundWorker
	{
		internal MarkingPumpBackgroundWorker(IQuizContext context) : base(context, true)
		{
		}
		protected override void DoQuizWork(object sender, DoWorkEventArgs e)
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
				Logger.Log($"MarkingPump worker received event {result}");
				if (result == 0)
					waitingForMarking = false;
				else if (result == 2)
				{
					Logger.Log($"Quitting app. Stopping the marking pump.");
					break;
				}
				else if (result == 3)
				{
					if (!waitingForMarking)
					{
						Logger.Log($"Countdown complete, and no more answers to mark. Stopping the marking pump.");
						break;
					}
					Logger.Log($"Countdown complete, but still some answers to mark. Removing countdown complete event from potential events.");
					events = new WaitHandle[] { Context.AnswerMarkedEvent, Context.AnswerReceivedEvent, Context.QuitAppEvent };
				}
				if (Context.AnswerListMutex.With(() =>
				{
					foreach (KeyValuePair<Contestant, List<Answer>> kvp in Context.Answers)
					{
						Answer unmarkedAnswer = kvp.Value.FirstOrDefault(a => a.AnswerResult == AnswerResult.Unmarked);
						if (unmarkedAnswer != null)
						{
							AnswerForMarking answerForMarking = new AnswerForMarking(kvp.Key, unmarkedAnswer);
							Logger.Log($"Marking pump has found an unmarked answer: {answerForMarking}");
							if (!AutoMarkAnswer(answerForMarking, lev, autoCountdown))
							{
								Logger.Log("Answer could not be auto-marked.");
								if (!waitingForMarking)
								{
									waitingForMarking = true;
									Logger.Log("We are now waiting for marking.");
									SetAnswerForMarking(answerForMarking);
								}
							}
							else
								UpdateMarkingProgress();
						}
						else
							UpdateMarkingProgress();
					}
					// Nothing left to mark, and no more answers incoming? We're done.
					return (!waitingForMarking) && Context.CountdownCompleteEvent.WaitOne(0);
				}))
				{
					Logger.Log("Last answers have been auto-marked, and countdown is complete. Stopping the marking pump.");
					break;
				}
			}
			UpdateMarkingProgress();
		}

		private bool AutoMarkAnswer(AnswerForMarking answer, bool useLev, bool autoCountdown)
		{
			bool deliberatelyFunny = answer.Answer.IsDeliberatelyFunny;
			// If user has already submitted an answer that was accepted, don't accept this new one as an answer.
			if (!deliberatelyFunny)
			{
				if (Context.Answers.ContainsKey(answer.Contestant))
				{
					List<Answer> contestantAnswers = Context.Answers[answer.Contestant];
					if (contestantAnswers.Any(a => a.IsAcceptedAnswer))
					{
						AnswerResult result = deliberatelyFunny ? AnswerResult.Funny : AnswerResult.NotAnAnswer;
						Logger.Log($"Marking answer {answer} as {result} ... we already have an accepted answer from that contestant.");
						Context.MarkAnswer(answer, result, 0.0, autoCountdown);
						return true;
					}
				}
				// Otherwise, if the user has submitted an answer that has already been marked, use that marking.
				double levValue = 0.0;
				foreach (KeyValuePair<AnswerResult, AnswerBin> kvp in Context.AnswerBins)
					if (kvp.Value.Contains(answer.Answer) || (useLev && kvp.Value.LevContains(answer.Answer, out levValue)))
					{
						Logger.Log($"Marking answer {answer} as {kvp.Key} ... it matches an accepted answer.");
						Context.MarkAnswer(answer, kvp.Key, levValue, autoCountdown);
						return true;
					}
			}
			else
			{
				Logger.Log($"Marking deliberately funny answer {answer} as {AnswerResult.Funny}.");
				Context.MarkAnswer(answer, AnswerResult.Funny, 0.0, autoCountdown);
				return true;
			}
			// Otherwise, no, have to do it manually.
			Logger.Log("Failed to auto-mark answer.");
			return false;
		}

		protected override void QuizProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			Context.AnswerForMarkingMutex.With(() =>
			{
				object o = e.UserState;
				if (o == null)
				{
					Logger.Log("Marking pump is starting the countdown.");
					Context.StartCountdown();
				}
				else if (o is FunnyAnswerArgs)
				{
					Logger.Log("Marking pump is broadcasting a funny answer.");
					Context.SendPublicChat(o.ToString());
				}
				else if (o is MarkingProgress progress)
				{
					Logger.Log("Marking pump is updating the marking progress UI.");
					Context.UpdateMarkingProgressUI(progress);
				}
				else if (o is AnswerForMarking marking)
				{
					Logger.Log("Marking pump is setting the answer for marking.");
					Context.SetAnswerForMarking(marking);
				}
			});
		}

		protected override void QuizWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			Logger.Log("Marking pump is finished.");
			Context.OnMarkingComplete();
		}
	}
}
