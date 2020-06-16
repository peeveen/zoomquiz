using System.Threading;
using System.ComponentModel;

namespace ZoomQuiz
{
	class CountdownBackgroundWorker : BackgroundWorker
	{
		private const int COUNTDOWN_SECONDS = 15;
		private IQuizContext Context { get; set; }
		internal CountdownBackgroundWorker(IQuizContext context)
		{
			Context = context;
			DoWork += CountdownDoWork;
			ProgressChanged += CountdownProgressChanged;
			WorkerReportsProgress = true;
			RunWorkerCompleted += CountdownRunWorkerCompleted;
		}
		private void CountdownDoWork(object sender, DoWorkEventArgs e)
		{
			if (Context.ShowTimeWarnings)
				Context.SendPublicChat("⏳ " + COUNTDOWN_SECONDS + " seconds remaining ...");
			for (int f = COUNTDOWN_SECONDS; f > 0; --f)
			{
				Thread.Sleep(1000);
				if (((f % 5) == 0) && (f != COUNTDOWN_SECONDS))
					ReportProgress(f);
			}

		}
		private void CountdownProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (Context.ShowTimeWarnings)
				Context.SendPublicChat("⏳ " + e.ProgressPercentage + " seconds remaining ...");
		}
		private void CountdownRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (Context.ShowTimeWarnings)
				Context.SendPublicChat("⌛ Time is up!");
			Context.CountdownCompleteEvent.Set();
			Context.OnCountdownComplete();
		}
	}
}
