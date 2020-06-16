using System;

namespace ZoomQuiz
{
	public class Answer
	{
		public string AnswerText { get; private set; }
		public string NormalizedAnswer { get; private set; }
		public AnswerResult AnswerResult { get; set; }
		public DateTime AnswerTime { get; }
		public bool IsAcceptedAnswer
		{
			get
			{
				return AnswerResult == AnswerResult.Correct || AnswerResult == AnswerResult.AlmostCorrect || AnswerResult == AnswerResult.Wrong;
			}
		}
		public Answer(string answer)
		{
			AnswerTime = DateTime.Now;
			AnswerText = answer;
			AnswerResult = AnswerResult.Unmarked;
			NormalizedAnswer = NormalizeAnswer(answer);
		}
		public static string NormalizeAnswer(string answer)
		{
			// first of all remove everything that isn't a space, a letter or a number
			string norm = "";
			foreach (char c in answer)
				if (Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c))
					norm += c;
			// Trim leading/trailing whitespace.
			norm = norm.Trim();
			// Now change any double spaces into single spaces.
			string oldNorm;
			do
			{
				oldNorm = norm;
				norm = norm.Replace("  ", " ");
			} while (oldNorm != norm);
			// Now convert the entire thing to lowercase
			norm = norm.ToLower();
			// now replace accented chars with simpler ones.
			byte[] tempBytes = System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(norm);
			norm = System.Text.Encoding.UTF8.GetString(tempBytes);
			// Now, if it starts with "the", remove it.
			norm = norm.Trim();
			if (norm.StartsWith("the ") && norm.Length > 4)
				norm = norm.Substring(4);
			return norm;
		}
	}
}
