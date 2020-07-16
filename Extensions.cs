using System;

namespace ZoomQuiz
{
	static class Extensions
	{
		public static bool IsProperResult(this AnswerResult result)
		{
			return result == AnswerResult.Correct || result == AnswerResult.AlmostCorrect || result == AnswerResult.Wrong;
		}
		public static void With(this QuizMutex m,Action a, bool log = true)
		{
			try
			{
				if (log)
					Logger.Log($"Attempting to get {m.Name} mutex.");
				m.WaitOne();
				a();
			}
			finally
			{
				if (log)
					Logger.Log($"Releasing {m.Name} mutex.");
				m.ReleaseMutex();
			}
		}
		public static T With<T>(this QuizMutex m, Func<T> f, bool log = true)
		{
			try
			{
				if (log)
					Logger.Log($"Attempting to get {m.Name} mutex.");
				m.WaitOne();
				return f();
			}
			finally
			{
				if (log)
					Logger.Log($"Releasing {m.Name} mutex.");
				m.ReleaseMutex();
			}
		}
	}
}
