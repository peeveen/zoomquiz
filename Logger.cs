using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ZoomQuiz
{
	static class Logger
	{
		const string LOG_FILENAME = "quiz_log.txt";
		static readonly QuizMutex m_logMutex = new QuizMutex("Log");
		static readonly List<string> m_logMessages = new List<string>();
		static readonly ManualResetEvent m_stopLoggingEvent = new ManualResetEvent(false);
		static readonly string m_logFilePath = FileUtils.GetFilePath("log", LOG_FILENAME);
		public static void Log(string message)
		{
			m_logMutex.With(() =>
			{
				m_logMessages.Add($"{LogTimestamp}, T{ThreadID}: {message}");
			}, false);
		}
		static string LogTimestamp
		{
			get
			{
				DateTime now = DateTime.Now;
				return $"{now.Hour:D2}:{now.Minute:D2}:{now.Second:D2}.{now.Millisecond:D3}";
			}
		}
		static string ThreadID
		{
			get
			{
				return Thread.CurrentThread.ManagedThreadId.ToString("X4");
			}
		}
		public static void StartLogging()
		{
			string logDirectory = FileUtils.GetFolderPath("log");
			if (!Directory.Exists(logDirectory))
				Directory.CreateDirectory(logDirectory);
			new Thread(new ThreadStart(LoggingThread)).Start();
		}
		public static void StopLogging()
		{
			m_stopLoggingEvent.Set();
		}
		private static void ProcessLogMessages()
		{
			List<string> logMessages = m_logMutex.With(() =>
			{
				if (m_logMessages.Count > 0)
				{
					List<string> logCopy = new List<string>(m_logMessages);
					m_logMessages.Clear();
					return logCopy;
				}
				return null;
			}, false);
			if (logMessages != null)
				File.AppendAllLines(m_logFilePath, logMessages);
		}
		static void LoggingThread()
		{
			while (!m_stopLoggingEvent.WaitOne(100))
				ProcessLogMessages();
			ProcessLogMessages();
		}
	}
}
