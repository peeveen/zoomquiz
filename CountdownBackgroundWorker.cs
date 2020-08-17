using System.Threading;
using System.ComponentModel;

namespace ZoomQuiz
{
	class CountdownBackgroundWorker : QuizBackgroundWorker
	{
		private const int COUNTDOWN_SECONDS = 15;
		internal CountdownBackgroundWorker(IQuizContext context) : base(context, true)
		{
		}
		protected override void DoQuizWork(object sender, DoWorkEventArgs e)
		{
			if (Context.ShowTimeWarnings)
				Context.SendPublicChat("⏳ " + COUNTDOWN_SECONDS + " seconds remaining ...");
			for (int f = COUNTDOWN_SECONDS; f > 0; --f)
			{
				Thread.Sleep(1000);
				if (((f % 5) == 0) && (f != COUNTDOWN_SECONDS))
					ReportProgress(f);
			}
			// Add a second for lag.
			Thread.Sleep(1000);
		}
		protected override void QuizProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (Context.ShowTimeWarnings)
				Context.SendPublicChat("⏳ " + e.ProgressPercentage + " seconds remaining ...");
		}
		protected override void QuizWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (Context.ShowTimeWarnings)
				Context.SendPublicChat("⌛ Time is up!");
			Context.CountdownCompleteEvent.Set();
			Context.OnCountdownComplete();
		}
	}
}
