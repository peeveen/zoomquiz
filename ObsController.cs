using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ZoomQuiz
{
	public class ObsController
	{
		public OBSWebsocket m_obs = new OBSWebsocket();
		public Mutex m_obsMutex = new Mutex();

		public ObsController()
		{
		}

		private void ObsDoAction(Action action)
		{
			try
			{
				m_obsMutex.WaitOne();
				action();
			}
			finally
			{
				m_obsMutex.ReleaseMutex();
			}
		}

		private T ObsDoFunc<T>(Func<T> f)
		{
			try
			{
				m_obsMutex.WaitOne();
				return f();
			}
			finally
			{
				m_obsMutex.ReleaseMutex();
			}
		}

		public void Connect(string url,string password="")
		{
			ObsDoAction(()=>m_obs.Connect(url, password));
		}

		public bool IsConnected
		{
			get { return ObsDoFunc(()=>m_obs.IsConnected); }
		}

		public void Disconnect()
		{
			ObsDoAction(()=>m_obs.Disconnect());
		}

		public SourceSettings GetSourceSettings(string source)
		{
			return ObsDoFunc(() => m_obs.GetSourceSettings(source));
		}
		public void SetSourceSettings(string source,JObject settings)
		{
			ObsDoAction(() => m_obs.SetSourceSettings(source,settings));
		}
		public List<FilterSettings> GetSourceFilters(string source)
		{
			return ObsDoFunc(() => m_obs.GetSourceFilters(source));
		}
		public void SetSourceFilterSettings(string source,string filter,JObject settings)
		{
			ObsDoAction(() => m_obs.SetSourceFilterSettings(source, filter, settings));
		}
		public void SetVolume(string source,float volume)
		{
			ObsDoAction(() => m_obs.SetVolume(source, volume));
		}
		public void SetCurrentScene(string scene)
		{
			ObsDoAction(() => m_obs.SetCurrentScene(scene));
		}
		public void SetSourceRender(string source,bool visible,string scene)
		{
			ObsDoAction(() => m_obs.SetSourceRender(source, visible, scene));
		}
		public void SetMute(string source, bool mute)
		{
			ObsDoAction(() => m_obs.SetMute(source, mute));
		}
		public VolumeInfo GetVolume(string source)
		{
			return ObsDoFunc(() => m_obs.GetVolume(source));
		}
	}
}
