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
			void FixVolume(string source,float obsVol,float desiredVol,float volChangeSpeed,bool isBgm=false)
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
				try
				{
					Context.VolumeMutex.WaitOne();
					VolumeInfo bgmVolInf = Context.Obs.GetVolume("BGM");
					VolumeInfo qbgmVolInf = Context.Obs.GetVolume("QuestionBGM");
					VolumeInfo qaVolInf = Context.Obs.GetVolume("QuestionAudio");
					VolumeInfo qvVolInf = Context.Obs.GetVolume("QuestionVid");
					FixVolume("BGM", bgmVolInf.Volume, Context.BgmVolume, bgmVolSpeed,true);
					FixVolume("QuestionBGM", qbgmVolInf.Volume, Context.QuestionBGMVolume, qbgmVolSpeed,true);
					FixVolume("QuestionAudio", qaVolInf.Volume, Context.QuestionAudioVolume, qaudVolSpeed);
					FixVolume("QuestionVid", qvVolInf.Volume, Context.QuestionVideoVolume, qvidVolSpeed);
				}
				finally
				{
					Context.VolumeMutex.ReleaseMutex();
				}
			}
		}
	}
}
