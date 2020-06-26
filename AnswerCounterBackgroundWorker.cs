using System.Linq;
using System.Threading;
using System.ComponentModel;

namespace ZoomQuiz
{
	class AnswerCounterBackgroundWorker:QuizBackgroundWorker
	{
		internal AnswerCounterBackgroundWorker(IQuizContext context):base(context,true)
		{
		}
		protected override void DoQuizWork(object sender, DoWorkEventArgs e)
		{
			WaitHandle[] waitEvents = new WaitHandle[] { Context.AnswerCounterAnswerReceivedEvent, Context.CountdownCompleteEvent, Context.QuitAppEvent };
			for (; ; )
			{
				int result = WaitHandle.WaitAny(waitEvents);
				if (result > 0)
					break;
				int answerCount = 0, markedAnswerCount = 0;
				try
				{
					Context.AnswerListMutex.WaitOne();
					answerCount = Context.Answers.Sum(kvp2 => kvp2.Value.Count);
					markedAnswerCount = Context.Answers.Sum(kvp2 => kvp2.Value.Count(a => a.AnswerResult != AnswerResult.Unmarked));
				}
				finally
				{
					Context.AnswerListMutex.ReleaseMutex();
				}

				ReportProgress(100, new MarkingProgress(answerCount, markedAnswerCount));
			}
		}

		protected override void QuizProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			Context.UpdateMarkingProgressUI((MarkingProgress)e.UserState);
		}
	}
}
