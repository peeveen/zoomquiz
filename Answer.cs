using System;
using System.Linq;

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
		static string RepeatReplace(string strIn, string replace, string replaceWith)
		{
			string oldIn;
			do
			{
				oldIn = strIn;
				strIn = strIn.Replace(replace, replaceWith);
			} while (oldIn != strIn);
			return strIn;
		}
		public static string NormalizeAnswer(string answer)
		{
			// first of all, replace hyphens with spaces.
			string norm = RepeatReplace(answer, "--", "-");
			norm = norm.Replace('-', ' ');
			// Now change any double spaces into single spaces.
			norm = RepeatReplace(norm, "  ", " ");
			// first of all remove everything that isn't a space, a letter or a number
			norm = norm.ToCharArray().Aggregate("", (s, c) => s += char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c.ToString() : "", (a) => a);
			// Trim leading/trailing whitespace.
			norm = norm.Trim();
			// Now convert the entire thing to lowercase
			norm = norm.ToLower();
			// now replace accented chars with simpler ones.
			byte[] tempBytes = System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(norm);
			norm = System.Text.Encoding.UTF8.GetString(tempBytes);
			// Now, if it starts with "the", remove it.
			norm = norm.Trim();
			if (norm.StartsWith("the ") && norm.Length > 4)
				norm = norm.Substring(4);
			// Now change "and"s to ampersands
			norm = norm.Replace(" and ", " & ");
			return norm;
		}
	}
}
