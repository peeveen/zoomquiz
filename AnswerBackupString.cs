using System;

namespace ZoomQuiz
{
	class AnswerBackupString : IComparable
	{
		public string AnswerString { get; private set; }
		public DateTime AnswerTime { get; private set; }
		public AnswerBackupString(Answer answer, string contestantName)
		{
			AnswerTime = answer.AnswerTime;
			AnswerString = AnswerTime.ToLongTimeString() + ": " + contestantName + " answered \"" + answer.AnswerText + "\" (" + answer.AnswerResult.ToString() + ")";
		}

		public int CompareTo(object obj)
		{
			if (obj is AnswerBackupString str)
				return AnswerTime.CompareTo(str.AnswerTime);
			return 1;
		}
		public override string ToString()
		{
			return AnswerString;
		}
	}
}
