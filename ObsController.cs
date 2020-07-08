using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Xml.Serialization.Advanced;

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

		public SourceSettings GetSourceSettings(string source)
		{
			Logger.Log($"Getting settings from OBS source \"{source}\"");
			return m_obsMutex.With(() => m_obs.GetSourceSettings(source));
		}
		public void SetSourceSettings(string source,JObject settings)
		{
			Logger.Log($"Setting OBS source \"{source}\" settings to {settings}");
			m_obsMutex.With(() => m_obs.SetSourceSettings(source,settings));
		}
		public List<FilterSettings> GetSourceFilters(string source)
		{
			Logger.Log($"Getting OBS source \"{source}\" filters");
			return m_obsMutex.With(() => m_obs.GetSourceFilters(source));
		}
		public void SetSourceFilterSettings(string source,string filter,JObject settings)
		{
			Logger.Log($"Setting OBS source \"{source}\" filter \"{filter}\" settings to {settings}");
			m_obsMutex.With(() => m_obs.SetSourceFilterSettings(source, filter, settings));
		}
		public void SetVolume(string source,float volume)
		{
			Logger.Log($"Setting OBS source \"{source}\" volume to {volume}");
			m_obsMutex.With(() => m_obs.SetVolume(source, volume));
		}
		public void SetCurrentScene(string scene)
		{
			Logger.Log($"Setting OBS current scene to \"{scene}\"");
			m_obsMutex.With(() => m_obs.SetCurrentScene(scene));
		}
		public void SetSourceRender(string source,string scene, bool visible)
		{
			Logger.Log($"Setting visibility of OBS source \"{source}\" in scene \"{scene}\" to {visible}");
			m_obsMutex.With(() => m_obs.SetSourceRender(source, visible, scene));
		}
		public void SetMute(string source, bool mute)
		{
			if(mute)
				Logger.Log($"Muting OBS source \"{source}\"");
			else
				Logger.Log($"Unmuting OBS source \"{source}\"");
			m_obsMutex.With(() => m_obs.SetMute(source, mute));
		}
		public VolumeInfo GetVolume(string source)
		{
			// Don't log these, they happen ten times a second.
			return m_obsMutex.With(() => m_obs.GetVolume(source), false);
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
			Logger.Log($"Setting image in OBS source \"{sourceName}\" to \"{mediaName}\"");
			string path = quiz.GetMediaPath(mediaName);
			if (string.IsNullOrEmpty(path) || (!File.Exists(path)))
			{
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				path = Path.Combine(presFolder, "transparent.png");
				Logger.Log($"No such image file found. Using transparent image.");
			}
			SetFileSourceFromPath(sourceName, "file", path);
		}

		public void SetVideoSource(Quiz quiz, string sourceName, string mediaName)
		{
			Logger.Log($"Setting video in OBS source \"{sourceName}\" to \"{mediaName}\"");
			string[] scenes = new string[] { "QuestionScene", "FullScreenPictureQuestionScene" };
			string path = quiz.GetMediaPath(mediaName);
			if (string.IsNullOrEmpty(path) || (!File.Exists(path)))
			{
				Logger.Log("No such video file found. Hiding source in all applicable scenes.");
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
			Logger.Log($"Getting bounds size of OBS source \"{sourceName}\" in scene \"{sceneName}\"");
			SceneItemProperties itemProperties =m_obs.GetSceneItemProperties(sourceName, sceneName);
			SceneItemBoundsInfo boundsInfo = itemProperties.Bounds;
			Size size=new Size((int)Math.Ceiling(boundsInfo.Width), (int)Math.Ceiling(boundsInfo.Height));
			Logger.Log($"Bounds size is {size}");
			return size;
		}

		public void SetAudioSource(Quiz quiz,string sourceName, string mediaName)
		{
			Logger.Log($"Setting audio in OBS source \"{sourceName}\" to \"{mediaName}\"");
			string path = quiz.GetMediaPath(mediaName);
			if ((string.IsNullOrEmpty(path)) || (!File.Exists(path)))
			{
				string presFolder = Path.Combine(Directory.GetCurrentDirectory(), "presentation");
				path = Path.Combine(presFolder, "silence.wav");
				Logger.Log($"No such audio file found. Using silence.");
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
