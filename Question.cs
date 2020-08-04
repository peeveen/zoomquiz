using System.Collections.Generic;
using System.Linq;

namespace ZoomQuiz
{
	public class Question
	{
		public string QuestionText { get; private set; }
		public string AnswerText { get; private set; }
		public string QuestionMediaFilename { get; private set; }
		public string QuestionSupplementaryMediaFilename { get; private set; }
		public string AnswerMediaFilename { get; private set; }
		public string AnswerImageFilename { get { return AnswerMediaType == MediaType.Image ? AnswerMediaFilename : null; } }
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
		public IReadOnlyCollection<string> OBSSourcesOn {get;}
		public IReadOnlyCollection<string> OBSSourcesOff {get;}

		private bool HasMedia(Quiz quiz, MediaType mediaType, string mediaFilename, params MediaType[] types)
		{
			return types.Contains(mediaType) && quiz.HasMediaFile(mediaFilename);
		}
		public bool HasAnyQuestionMedia(Quiz quiz, params MediaType[] types)
		{
			return HasQuestionMedia(quiz, types) || HasQuestionSupplementaryMedia(quiz, types);
		}
		public bool HasQuestionMedia(Quiz quiz, params MediaType[] types)
		{
			return HasMedia(quiz, QuestionMediaType, QuestionMediaFilename, types);
		}
		public bool HasQuestionSupplementaryMedia(Quiz quiz, params MediaType[] types)
		{
			return HasMedia(quiz, QuestionSupplementaryMediaType, QuestionSupplementaryMediaFilename, types);
		}
		public bool HasAnswerMedia(Quiz quiz, params MediaType[] types)
		{
			return HasMedia(quiz, AnswerMediaType, AnswerMediaFilename, types);
		}
		public bool HasQuestionBGM(Quiz quiz)
		{
			return quiz.HasMediaFile(QuestionBGMFilename);
		}
		public Question(int number, string questionText, string answerText, string[] answers, string[] almostAnswers, string[] wrongAnswers, string questionMediaFile, MediaType questionMediaType, string questionSupplementaryMediaFile, MediaType questionSupplementaryMediaType, string answerMediaFile, MediaType answerMediaType, string info, bool useLevenshtein, QuestionValidity validity,List<string> obsSourcesOn,List<string> obsSourcesOff)
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
			AnswerMediaFilename = answerMediaFile.Trim();
			Validity = validity;
			Info = info.Trim();
			UseLevenshtein = useLevenshtein;
			OBSSourcesOn = obsSourcesOn;
			OBSSourcesOff = obsSourcesOff;
		}
	}
}
