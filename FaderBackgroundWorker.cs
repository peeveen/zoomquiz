using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OBSWebsocketDotNet.Types;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ZoomQuiz
{
	class FaderBackgroundWorker:BackgroundWorker
	{
		private IQuizContext Context { get; set; }
		internal FaderBackgroundWorker(IQuizContext context)
		{
			Context = context;
			DoWork += FaderDoWork;
		}
		private void FaderDoWork(object sender, DoWorkEventArgs e)
		{
			const float bgmVolSpeed = 0.01f;
			const float qbgmVolSpeed = 0.01f;
			const float qaudVolSpeed = 0.04f;
			const float qvidVolSpeed = 0.04f;

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
					float diff = nBGMVol - Context.BgmVolume;
					if (diff < -bgmVolSpeed)
						Context.Obs.SetVolume("BGM", nBGMVol + bgmVolSpeed);
					else if (diff > bgmVolSpeed)
						Context.Obs.SetVolume("BGM", nBGMVol - bgmVolSpeed);
					else if (nBGMVol != Context.BgmVolume)
						Context.Obs.SetVolume("BGM", Context.BgmVolume);
					diff = nQBGMVol - Context.QuestionBGMVolume;
					if (diff < -qbgmVolSpeed)
						Context.Obs.SetVolume("QuestionBGM", nQBGMVol + qbgmVolSpeed);
					else if (diff > qbgmVolSpeed)
						Context.Obs.SetVolume("QuestionBGM", nQBGMVol - qbgmVolSpeed);
					else if (nQBGMVol != Context.QuestionBGMVolume)
						Context.Obs.SetVolume("QuestionBGM", Context.QuestionBGMVolume);
					diff = nQAVol - Context.QuestionAudioVolume;
					if (diff > qaudVolSpeed)
						Context.Obs.SetVolume("QuestionAudio", nQAVol - qaudVolSpeed);
					else if (nQAVol != Context.QuestionAudioVolume)
						Context.Obs.SetVolume("QuestionAudio", Context.QuestionAudioVolume);
					diff = nQVVol - Context.QuestionVideoVolume;
					if (diff > qvidVolSpeed)
						Context.Obs.SetVolume("QuestionVid", nQVVol - qvidVolSpeed);
					else if (nQVVol != Context.QuestionVideoVolume)
						Context.Obs.SetVolume("QuestionVid", Context.QuestionVideoVolume);
				}
				finally
				{
					Context.VolumeMutex.ReleaseMutex();
				}
			}
		}


	}
}
