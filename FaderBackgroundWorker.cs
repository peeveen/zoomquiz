using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OBSWebsocketDotNet.Types;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Drawing.Text;

namespace ZoomQuiz
{
	class FaderBackgroundWorker:BackgroundWorker
	{
		const float bgmVolSpeed = 0.01f;
		const float qbgmVolSpeed = 0.01f;
		const float qaudVolSpeed = 0.04f;
		const float qvidVolSpeed = 0.04f;

		private IQuizContext Context { get; set; }
		internal FaderBackgroundWorker(IQuizContext context)
		{
			Context = context;
			DoWork += FaderDoWork;
		}
		private void FaderDoWork(object sender, DoWorkEventArgs e)
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
					float nBGMVol = bgmVolInf.Volume;
					float nQBGMVol = qbgmVolInf.Volume;
					float nQAVol = qaVolInf.Volume;
					float nQVVol = qvVolInf.Volume;
					FixVolume("BGM", nBGMVol, Context.BgmVolume, bgmVolSpeed,true);
					FixVolume("QuestionBGM", nQBGMVol, Context.QuestionBGMVolume, qbgmVolSpeed,true);
					FixVolume("QuestionAudio", nQAVol, Context.QuestionAudioVolume, qaudVolSpeed);
					FixVolume("QuestionVid", nQVVol, Context.QuestionVideoVolume, qvidVolSpeed);
				}
				finally
				{
					Context.VolumeMutex.ReleaseMutex();
				}
			}
		}


	}
}
