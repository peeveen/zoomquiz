namespace ZoomQuiz
{
	public class MarkingProgress
	{
		public int AnswersReceived { get; private set; }
		public int AnswersMarked { get; private set; }
		public MarkingProgress(int received, int marked)
		{
			AnswersReceived = received;
			AnswersMarked = marked;
		}
	}
}
