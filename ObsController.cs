using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ZoomQuiz
{
	public class ObsController
	{
		public OBSWebsocket m_obs = new OBSWebsocket();
		public QuizMutex m_obsMutex = new QuizMutex("OBS");

		public void Connect(string url,string password="")
		{
			Logger.Log($"Connecting to OBS.");
			m_obsMutex.With(()=>m_obs.Connect(url, password));
		}

		public bool IsConnected
		{
			get { return m_obsMutex.With(()=>m_obs.IsConnected); }
		}

		public void Disconnect()
		{
			Logger.Log($"Disconnecting from OBS.");
			m_obsMutex.With(()=>m_obs.Disconnect());
		}

		public SourceSettings GetSourceSettings(Source source)
		{
			string sourceName = Configuration.SourceNames[source];
			Logger.Log($"Getting settings from OBS source \"{sourceName}\"");
			return m_obsMutex.With(() => m_obs.GetSourceSettings(sourceName));
		}
		public void SetSourceSettings(Source source,JObject settings)
		{
			string sourceName = Configuration.SourceNames[source];
			Logger.Log($"Setting OBS source \"{sourceName}\" settings to {settings}");
			m_obsMutex.With(() => m_obs.SetSourceSettings(sourceName, settings));
		}
		public List<FilterSettings> GetSourceFilters(Source source)
		{
			string sourceName = Configuration.SourceNames[source];
			Logger.Log($"Getting OBS source \"{sourceName}\" filters");
			return m_obsMutex.With(() => m_obs.GetSourceFilters(sourceName));
		}
		public void SetSourceFilterSettings(Source source,string filter,JObject settings)
		{
			string sourceName = Configuration.SourceNames[source];
			Logger.Log($"Setting OBS source \"{source}\" filter \"{filter}\" settings to {settings}");
			m_obsMutex.With(() => m_obs.SetSourceFilterSettings(sourceName, filter, settings));
		}
		public void SetVolume(Source source,float volume)
		{
			string sourceName = Configuration.SourceNames[source];
			Logger.Log($"Setting OBS source \"{sourceName}\" volume to {volume}");
			m_obsMutex.With(() => m_obs.SetVolume(sourceName, volume));
		}
		public void SetCurrentScene(Scene scene)
		{
			string sceneName = Configuration.SceneNames[scene];
			Logger.Log($"Setting OBS current scene to \"{scene}\"");
			m_obsMutex.With(() => m_obs.SetCurrentScene(sceneName));
		}
		public void SetSourceRender(Source source, Scene scene, bool visible)
		{
			string sceneName = Configuration.SceneNames[scene];
			string sourceName = Configuration.SourceNames[source];
			Logger.Log($"Setting visibility of OBS source \"{sourceName}\" in scene \"{sceneName}\" to {visible}");
			m_obsMutex.With(() => m_obs.SetSourceRender(sourceName, visible, sceneName));
		}
		public void SetMute(Source source, bool mute)
		{
			string sourceName = Configuration.SourceNames[source];
			if (mute)
				Logger.Log($"Muting OBS source \"{sourceName}\"");
			else
				Logger.Log($"Unmuting OBS source \"{sourceName}\"");
			m_obsMutex.With(() => m_obs.SetMute(sourceName, mute));
		}
		public VolumeInfo GetVolume(Source source)
		{
			string sourceName = Configuration.SourceNames[source];
			// Don't log these, they happen ten times a second.
			return m_obsMutex.With(() => m_obs.GetVolume(sourceName), false);
		}

		public void HideSource(Source source, Scene scene)
		{
			SetSourceRender(source, scene, false);
		}

		public void ShowSource(Source source, Scene scene)
		{
			SetSourceRender(source, scene, true);
		}

		public void SetImageSource(Quiz quiz,Source source, string mediaName)
		{
			Logger.Log($"Setting image in OBS {source} to \"{mediaName}\"");
			string path = quiz.GetMediaPath(mediaName);
			if (string.IsNullOrEmpty(path) || (!File.Exists(path)))
			{
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				path = Path.Combine(presFolder, "transparent.png");
				Logger.Log($"No such image file found. Using transparent image.");
			}
			SetFileSourceFromPath(source, "file", path);
		}

		public void SetVideoSource(Quiz quiz, Source source, string mediaName)
		{
			string sourceName = Configuration.SourceNames[source];
			Logger.Log($"Setting video in OBS {source} source to \"{mediaName}\"");
			Scene[] scenes = new Scene[] { Scene.Question, Scene.FullScreenQuestionPicture };
			string path = quiz.GetMediaPath(mediaName);
			if (string.IsNullOrEmpty(path) || (!File.Exists(path)))
			{
				Logger.Log("No such video file found. Hiding source in all applicable scenes.");
				foreach (Scene scene in scenes)
					HideSource(source, scene);
			}
			else
			{
				SetFileSourceFromPath(source, "local_file", path);
				foreach (Scene scene in scenes)
					ShowSource(source, scene);
			}
		}

		public void SetFileSourceFromPath(Source source, string setting, string path)
		{
			JObject settings = new JObject()
			{
				{setting,path }
			};
			SetSourceSettings(source, settings);
		}

		public Size GetSourceBoundsSize(Scene scene,Source source)
		{
			string sceneName = Configuration.SceneNames[scene];
			string sourceName = Configuration.SourceNames[source];
			Logger.Log($"Getting bounds size of OBS source \"{sourceName}\" in scene \"{sceneName}\"");
			SceneItemProperties itemProperties =m_obs.GetSceneItemProperties(sourceName, sceneName);
			SceneItemBoundsInfo boundsInfo = itemProperties.Bounds;
			Size size=new Size((int)Math.Ceiling(boundsInfo.Width), (int)Math.Ceiling(boundsInfo.Height));
			Logger.Log($"Bounds size is {size}");
			return size;
		}

		public void SetAudioSource(Quiz quiz,Source source, string mediaName)
		{
			Logger.Log($"Setting audio in OBS {source} source to \"{mediaName}\"");
			string path = quiz.GetMediaPath(mediaName);
			if ((string.IsNullOrEmpty(path)) || (!File.Exists(path)))
			{
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				path = Path.Combine(presFolder, "silence.wav");
				Logger.Log($"No such audio file found. Using silence.");
			}
			SetFileSourceFromPath(source, "local_file", path);
			JObject settings = new JObject()
			{
				{"NonExistent",""+new Random().Next() }
			};
			SetSourceSettings(source, settings);
		}
	}
}
