using System;
using System.Drawing;

namespace ZoomQuiz
{
	class ScoreReportEntry : IComparable
	{
		public Contestant Contestant { get; private set; }
		public AnswerResult Result { get; private set; }
		public TimeSpan AnswerTimeOffset { get; private set; }
		public DateTime AnswerTime { get; private set; }
		public Brush Colour
		{
			get
			{
				if (Result == AnswerResult.Correct)
					return Brushes.LawnGreen;
				if (Result == AnswerResult.AlmostCorrect)
					return Brushes.Yellow;
				if (Result == AnswerResult.Wrong)
					return Brushes.Red;
				return Brushes.LightGray;
			}
		}
		public ScoreReportEntry(DateTime answerTime, Contestant contestant, AnswerResult result, TimeSpan answerTimeOffset)
		{
			AnswerTime = answerTime;
			AnswerTimeOffset = answerTimeOffset;
			Contestant = contestant;
			Result = result;
		}
		public string GetScoreReportString(bool includeTime)
		{
			string str = "";
			if (Result == AnswerResult.Correct)
				str = "✓";
			if (Result == AnswerResult.AlmostCorrect)
				str = "✓";
			if (Result == AnswerResult.Wrong)
				str = "✕";
			str += " " + Contestant.Name;
			if (includeTime)
				if (AnswerTimeOffset.TotalSeconds > 0)
					str += " (+" + string.Format("{0:0.00}", AnswerTimeOffset.TotalSeconds) + "s)";
			return str;
		}
		public int CompareTo(object obj)
		{
			if (obj is ScoreReportEntry entry)
				return -AnswerTimeOffset.CompareTo(entry.AnswerTimeOffset);
			return 1;
		}
	}
}
