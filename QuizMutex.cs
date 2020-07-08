using System;
using System.Threading;

namespace ZoomQuiz
{
	public class QuizMutex:IDisposable
	{
		public string Name { get; private set; }
		private Mutex InternalMutex { get; set; }
		public QuizMutex(string name, bool initiallyOwned=false)
		{
			Name = name;
			InternalMutex = new Mutex(initiallyOwned, name);
		}
		public bool WaitOne()
		{
			return InternalMutex.WaitOne();
		}
		public void ReleaseMutex()
		{
			InternalMutex.ReleaseMutex();
		}

		public void Dispose()
		{
			InternalMutex.Dispose();
		}
	}
}
