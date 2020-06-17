namespace ZoomQuiz
{
	public class Question
	{
		public string QuestionText { get; private set; }
		public string AnswerText { get; private set; }
		public string QuestionMediaFilename { get; private set; }
		public string QuestionSupplementaryMediaFilename { get; private set; }
		public string AnswerImageFilename { get; private set; }
		public MediaType QuestionMediaType { get; private set; }
		public MediaType AnswerMediaType { get; private set; }
		public MediaType QuestionSupplementaryMediaType { get; private set; }
		public string QuestionAudioFilename { get { return QuestionMediaType == MediaType.Audio ? QuestionMediaFilename : null; } }
		public string QuestionVideoFilename { get { return QuestionMediaType == MediaType.Video ? QuestionMediaFilename : null; } }
		public string QuestionImageFilename { get { return QuestionMediaType == MediaType.Image ? QuestionMediaFilename : QuestionSupplementaryImageFilename; } }
		public string QuestionBGMFilename { get { return QuestionSupplementaryMediaType == MediaType.Audio ? QuestionSupplementaryMediaFilename : null; } }
		public string QuestionSupplementaryImageFilename { get { return QuestionSupplementaryMediaType == MediaType.Image ? QuestionSupplementaryMediaFilename : null; } }
		public string[] QuestionAnswers { get; private set; }
		public string[] QuestionAlmostAnswers { get; private set; }
		public string[] QuestionWrongAnswers { get; private set; }
		public string Info { get; private set; }
		public bool UseLevenshtein { get; private set; }
		public int QuestionNumber { get; private set; }
		public QuestionValidity Validity { get; private set; }
		public Question(int number, string questionText, string answerText, string[] answers, string[] almostAnswers, string[] wrongAnswers, string questionMediaFile, MediaType questionMediaType, string questionSupplementaryMediaFile, MediaType questionSupplementaryMediaType, string answerImageFile, MediaType answerMediaType, string info, bool useLevenshtein, QuestionValidity validity)
		{
			QuestionNumber = number;
			QuestionText = questionText.Trim();
			QuestionMediaFilename = questionMediaFile.Trim();
			QuestionMediaType = questionMediaType;
			QuestionSupplementaryMediaFilename = questionSupplementaryMediaFile.Trim();
			QuestionSupplementaryMediaType = questionSupplementaryMediaType;
			AnswerText = answerText.Trim();
			AnswerMediaType = answerMediaType;
			QuestionAnswers = answers;
			QuestionWrongAnswers = wrongAnswers;
			QuestionAlmostAnswers = almostAnswers;
			AnswerImageFilename = answerImageFile.Trim();
			Validity = validity;
			Info = info.Trim();
			UseLevenshtein = useLevenshtein;
		}
	}
}
