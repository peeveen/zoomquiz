using System.Collections.Generic;
using System.Linq;

namespace ZoomQuiz
{
	public class AnswerBin
	{
		private readonly QuizMutex m_answersMutex;
		private readonly Dictionary<string, double> m_ratedAnswers = new Dictionary<string, double>();
		public AnswerBin(AnswerResult result)
		{
			m_answersMutex = new QuizMutex(result.ToString()+"AnswerBin");
		}
		~AnswerBin()
		{
			m_answersMutex.Dispose();
		}
		public void Add(Answer answer, double levValue)
		{
			m_answersMutex.With(() => m_ratedAnswers[answer.NormalizedAnswer] = levValue);
		}
		public bool Contains(Answer answer)
		{
			return m_answersMutex.With(() => m_ratedAnswers.Keys.Contains(answer.NormalizedAnswer));
		}
		public void GetLevenshteinRange(out double min, out double max)
		{
			min = m_ratedAnswers.Count == 0 ? 0.0 : m_ratedAnswers.Min(ra => ra.Value);
			max = m_ratedAnswers.Count == 0 ? 0.0 : m_ratedAnswers.Max(ra => ra.Value);
		}
		public bool LevContains(Answer answer, out double levValue)
		{
			levValue = 0.0;
			double lambdaLevValue = 0.0;
			try
			{
				return m_answersMutex.With(() =>
				{
					string norm = answer.NormalizedAnswer;
					foreach (string acceptableAnswer in m_ratedAnswers.Keys)
						if (Levenshtein.LevMatch(norm, acceptableAnswer, out lambdaLevValue))
							return true;
					return false;
				});
			}
			finally
			{
				levValue = lambdaLevValue;
			}
		}
	}
}
