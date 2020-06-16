using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ZoomQuiz
{
	public class AnswerBin
	{
		private readonly Mutex m_answersMutex = new Mutex();
		private readonly Dictionary<string, double> m_ratedAnswers = new Dictionary<string, double>();
		public AnswerBin()
		{
		}
		~AnswerBin()
		{
			m_answersMutex.Dispose();
		}
		public void Add(Answer answer, double levValue)
		{
			try
			{
				m_answersMutex.WaitOne();
				m_ratedAnswers[answer.NormalizedAnswer] = levValue;
			}
			finally
			{
				m_answersMutex.ReleaseMutex();
			}
		}
		public bool Contains(Answer answer)
		{
			try
			{
				m_answersMutex.WaitOne();
				return m_ratedAnswers.Keys.Contains(answer.NormalizedAnswer);
			}
			finally
			{
				m_answersMutex.ReleaseMutex();
			}
		}
		public void GetLevenshteinRange(out double min, out double max)
		{
			min = m_ratedAnswers.Count == 0 ? 0.0 : m_ratedAnswers.Min(ra => ra.Value);
			max = m_ratedAnswers.Count == 0 ? 0.0 : m_ratedAnswers.Max(ra => ra.Value);
		}
		public bool LevContains(Answer answer, out double levValue)
		{
			levValue = 0.0;
			try
			{
				m_answersMutex.WaitOne();
				string norm = answer.NormalizedAnswer;
				foreach (string acceptableAnswer in m_ratedAnswers.Keys)
				{
					if (Levenshtein.LevMatch(norm, acceptableAnswer, out levValue))
						return true;
				}
			}
			finally
			{
				m_answersMutex.ReleaseMutex();
			}
			return false;
		}
	}
}
