using System.ComponentModel;

namespace ZoomQuiz
{
	abstract class QuizBackgroundWorker : BackgroundWorker
	{
		protected IQuizContext Context { get; set; }
		protected QuizBackgroundWorker(IQuizContext context,bool reportsProgress=false)
		{
			Context = context;
			WorkerReportsProgress = reportsProgress;
			DoWork += DoQuizWork;
			RunWorkerCompleted += QuizWorkerCompleted;
			ProgressChanged += QuizProgressChanged;
		}

		protected virtual void QuizWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
		}

		protected abstract void DoQuizWork(object sender, DoWorkEventArgs e);

		protected virtual void QuizProgressChanged(object sender, ProgressChangedEventArgs e)
		{
		}
	}
}
