using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ZoomQuiz
{
	public class ObsController
	{
		public OBSWebsocket m_obs = new OBSWebsocket();
		public Mutex m_obsMutex = new Mutex();

		public void Connect(string url,string password="")
		{
			m_obsMutex.With(()=>m_obs.Connect(url, password));
		}

		public bool IsConnected
		{
			get { return m_obsMutex.With(()=>m_obs.IsConnected); }
		}

		public void Disconnect()
		{
			m_obsMutex.With(()=>m_obs.Disconnect());
		}

		public SourceSettings GetSourceSettings(string source)
		{
			return m_obsMutex.With(() => m_obs.GetSourceSettings(source));
		}
		public void SetSourceSettings(string source,JObject settings)
		{
			m_obsMutex.With(() => m_obs.SetSourceSettings(source,settings));
		}
		public List<FilterSettings> GetSourceFilters(string source)
		{
			return m_obsMutex.With(() => m_obs.GetSourceFilters(source));
		}
		public void SetSourceFilterSettings(string source,string filter,JObject settings)
		{
			m_obsMutex.With(() => m_obs.SetSourceFilterSettings(source, filter, settings));
		}
		public void SetVolume(string source,float volume)
		{
			m_obsMutex.With(() => m_obs.SetVolume(source, volume));
		}
		public void SetCurrentScene(string scene)
		{
			m_obsMutex.With(() => m_obs.SetCurrentScene(scene));
		}
		public void SetSourceRender(string source,string scene, bool visible)
		{
			m_obsMutex.With(() => m_obs.SetSourceRender(source, visible, scene));
		}
		public void SetMute(string source, bool mute)
		{
			m_obsMutex.With(() => m_obs.SetMute(source, mute));
		}
		public VolumeInfo GetVolume(string source)
		{
			return m_obsMutex.With(() => m_obs.GetVolume(source));
		}

		public void HideSource(string sourceName, string sceneName)
		{
			SetSourceRender(sourceName, sceneName,false);
		}

		public void ShowSource(string sourceName, string sceneName)
		{
			SetSourceRender(sourceName, sceneName, true);
		}

		public void SetImageSource(Quiz quiz,string sourceName, string mediaName)
		{
			string path = quiz.GetMediaPath(mediaName);
			if ((string.IsNullOrEmpty(path)) || (!File.Exists(path)))
			{
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				path = Path.Combine(presFolder, "transparent.png");
			}
			SetFileSourceFromPath(sourceName, "file", path);
		}

		public void SetVideoSource(Quiz quiz, string sourceName, string mediaName)
		{
			string[] scenes = new string[] { "QuestionScene", "FullScreenPictureQuestionScene" };
			string path = quiz.GetMediaPath(mediaName);
			if ((string.IsNullOrEmpty(path)) || (!File.Exists(path)))
			{
				foreach (string sceneName in scenes)
					HideSource(sourceName, sceneName);
			}
			else
			{
				SetFileSourceFromPath(sourceName, "local_file", path);
				foreach (string sceneName in scenes)
					ShowSource(sourceName, sceneName);
			}
		}

		public void SetFileSourceFromPath(string sourceName, string setting, string path)
		{
			JObject settings = new JObject()
			{
				{setting,path }
			};
			SetSourceSettings(sourceName, settings);
		}

		public Size GetSourceBoundsSize(string sceneName,string sourceName)
		{
			SceneItemProperties itemProperties=m_obs.GetSceneItemProperties(sourceName, sceneName);
			SceneItemBoundsInfo boundsInfo = itemProperties.Bounds;
			return new Size((int)Math.Ceiling(boundsInfo.Width), (int)Math.Ceiling(boundsInfo.Height));
		}

		public void SetAudioSource(Quiz quiz,string sourceName, string mediaName)
		{
			string path = quiz.GetMediaPath(mediaName);
			if ((string.IsNullOrEmpty(path)) || (!File.Exists(path)))
			{
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				path = Path.Combine(presFolder, "silence.wav");
			}
			SetFileSourceFromPath(sourceName, "local_file", path);
			JObject settings = new JObject()
			{
				{"NonExistent",""+new Random().Next() }
			};
			SetSourceSettings(sourceName, settings);
		}
	}
}
