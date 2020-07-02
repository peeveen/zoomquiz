using System;
using System.Threading;

namespace ZoomQuiz
{
	static class Extensions
	{
		public static void With(this Mutex m,Action a)
		{
			try
			{
				m.WaitOne();
				a();
			}
			finally
			{
				m.ReleaseMutex();
			}
		}
		public static T With<T>(this Mutex m, Func<T> f)
		{
			try
			{
				m.WaitOne();
				return f();
			}
			finally
			{
				m.ReleaseMutex();
			}
		}
	}
}
