namespace ZoomQuiz
{
	class FunnyAnswerArgs
	{
		public Answer Answer { get; private set; }
		public Contestant Contestant { get; private set; }
		public FunnyAnswerArgs(Answer answer, Contestant contestant)
		{
			Answer = answer;
			Contestant = contestant;
		}
		public override string ToString()
		{
			return "😂 Answer from " + Contestant.Name + ": \"" + Answer.AnswerText.Trim('.') + "\"";
		}
	}
}
