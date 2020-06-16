namespace ZoomQuiz
{
	public class ContestantScore
	{
		public Contestant Contestant { get; private set; }
		public string Name { get; private set; }
		public int Score { get; private set; }
		public bool Joint { get; private set; }
		public int Position { get; private set; }
		public string LastScoreString { get; private set; }
		public AnswerResult LastResult { get; private set; }
		public string PositionString
		{
			get
			{
				return "" + Position + (Joint ? "=" : "");
			}
		}
		public string LastScore
		{
			get
			{
				return "" + Position + (Joint ? "=" : "");
			}
		}
		public ContestantScore(int position, bool joint, int score, Contestant contestant, AnswerResult lastResult)
		{
			Joint = joint;
			Name = contestant.Name;
			Contestant = contestant;
			Position = position;
			Score = score;
			LastScoreString = GetLastScoreString(lastResult);
			LastResult = lastResult;
		}
		private string GetLastScoreString(AnswerResult result)
		{
			if (result == AnswerResult.Correct)
				return "+" + Scores.CORRECT_ANSWER_SCORE;
			else if (result == AnswerResult.AlmostCorrect)
				return "+" + Scores.ALMOST_CORRECT_ANSWER_SCORE;
			return "";
		}
	}
}
