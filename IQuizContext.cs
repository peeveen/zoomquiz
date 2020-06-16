using OBSWebsocketDotNet;
using System.Collections.Generic;
using System.Threading;

namespace ZoomQuiz
{
	interface IQuizContext
	{
		Dictionary<Contestant, List<Answer>> Answers { get; }
		Dictionary<AnswerResult, AnswerBin> AnswerBins { get; }
		AutoResetEvent AnswerMarkedEvent { get; }
		AutoResetEvent AnswerReceivedEvent { get; }
		AutoResetEvent AnswerCounterAnswerReceivedEvent { get; }
		ManualResetEvent QuitAppEvent { get; }
		ManualResetEvent CountdownCompleteEvent { get; }
		Mutex AnswerListMutex { get; }
		Mutex VolumeMutex { get; }
		Mutex ObsMutex { get; }
		Mutex AnswerForMarkingMutex { get; }
		void MarkAnswer(AnswerForMarking answer, AnswerResult result, double levValue, bool autoCountdown);
		void StartCountdown();
		void SendPublicChat(string message);
		void UpdateMarkingProgressUI(MarkingProgress progress);
		void SetAnswerForMarking(AnswerForMarking answerForMarking);
		void OnMarkingComplete();
		void OnCountdownComplete();
		bool ShowTimeWarnings { get; }
		OBSWebsocket Obs { get; }
		float BgmVolume { get; }
		float QuestionBGMVolume { get; }
		float QuestionAudioVolume { get; }
		float QuestionVideoVolume { get; }
		void AddAnswer(Contestant contestant, Answer answer);
	}
}
