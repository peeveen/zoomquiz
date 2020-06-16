using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace ZoomQuiz
{
	class TestAnswersBackgroundWorker:BackgroundWorker
	{
		private IQuizContext Context { get; set; }
		internal TestAnswersBackgroundWorker(IQuizContext context)
		{
			Context = context;
			DoWork += TestAnswersDoWork;
		}
		private void TestAnswersDoWork(object sender, DoWorkEventArgs e)
		{
			uint un = 235423;
			List<Contestant> contestants = new List<Contestant>();
			string[] contestantNames = new string[]
			{
				"David Bowie",
				"Ralph Stanley",
				"Clarissa Hetheridge",
				"Mark Ruffalo",
				"Stacy's Mom",
				"Olivia Coleman",
				"Jimmy Dewar",
				"Kristin Hersh",
				"Bob Mortimer",
				"Julius Caesar",
				"Fred Flintstone",
				"Lana Del Rey",
				"Bertie Bassett",
				"Darkwing Duck",
				"King Arthur",
				"Humphrey Lyttleton",
				"Shawn Colvin",
				"Tori Amos",
				"Stewart Lee"
			};
			foreach (string name in contestantNames)
				contestants.Add(new Contestant(un++, name));
			string[] answers = new string[]
			{
				"paul scholes",
				"Shearer",
				"Alan Sharrer",
				"alan shearer",
				"Bobby moore",
				"diego maradona",
				"Bobby Ball 😂",
				"i've got a pot noodle!",
				"Alan shearer",
				"alan shearer",
				"alan shearer",
				"a shearer",
				"giggsy",
				"paul scholes",
				"allan shearer",
				"bananaman",
				"alan shearer",
				"shearer",
				"ALAN SHERER"
			};
			int[] timings = new int[]
			{
				3785,
				234,
				9,
				109,
				1300,
				90,
				988,
				123,
				2010,
				54,
				111,
				1422,
				61,
				88,
				1082,
				21,
				15,
				578,
				2101
			};
			int n = 0;
			foreach (int timing in timings)
			{
				Thread.Sleep(timing);
				Context.AddAnswer(contestants[n], new Answer(answers[n]));
				++n;
			}
		}
	}
}
