using OBSWebsocketDotNet.Types;
using System.ComponentModel;

namespace ZoomQuiz
{
	class FaderBackgroundWorker:QuizBackgroundWorker
	{
		const float bgmVolSpeed = 0.01f;
		const float qbgmVolSpeed = 0.01f;
		const float qaudVolSpeed = 0.04f;
		const float qvidVolSpeed = 0.04f;

		internal FaderBackgroundWorker(IQuizContext context):base(context)
		{
		}
		protected override void DoQuizWork(object sender, DoWorkEventArgs e)
		{
			void FixVolume(Source source,float obsVol,float desiredVol,float volChangeSpeed,bool isBgm=false)
			{
				float diff = obsVol - desiredVol;
				if (diff < -volChangeSpeed)
					Context.Obs.SetVolume(source, obsVol + volChangeSpeed);
				else if (isBgm && diff > volChangeSpeed)
					Context.Obs.SetVolume(source, obsVol - volChangeSpeed);
				else if (obsVol != desiredVol)
					Context.Obs.SetVolume(source, desiredVol);
			}
			while (!Context.QuitAppEvent.WaitOne(100))
			{
				// Don't log these, they happen ten times a second.
				Context.VolumeMutex.With(() =>
				{
					VolumeInfo bgmVolInf = Context.Obs.GetVolume(Source.BGM);
					VolumeInfo qbgmVolInf = Context.Obs.GetVolume(Source.QuestionBGM);
					VolumeInfo qaVolInf = Context.Obs.GetVolume(Source.QuestionAudio);
					VolumeInfo qvVolInf = Context.Obs.GetVolume(Source.QuestionVideo);
					FixVolume(Source.BGM, bgmVolInf.Volume, Context.BgmVolume, bgmVolSpeed, true);
					FixVolume(Source.QuestionBGM, qbgmVolInf.Volume, Context.QuestionBGMVolume, qbgmVolSpeed, true);
					FixVolume(Source.QuestionAudio, qaVolInf.Volume, Context.QuestionAudioVolume, qaudVolSpeed);
					FixVolume(Source.QuestionVideo, qvVolInf.Volume, Context.QuestionVideoVolume, qvidVolSpeed);
				}, false);
			}
		}
	}
}
