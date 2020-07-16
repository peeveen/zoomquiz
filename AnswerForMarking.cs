namespace ZoomQuiz
{
	public class AnswerForMarking
	{
		public Answer Answer { get; private set; }
		public Contestant Contestant { get; private set; }
		public AnswerForMarking(Contestant contestant, Answer answer)
		{
			Answer = answer;
			Contestant = contestant;
		}
		public override string ToString()
		{
			return $"\"{Answer.AnswerText}\" from {Contestant}";
		}
	}
}
