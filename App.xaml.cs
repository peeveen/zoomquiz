using System;
using System.Windows;
using ZOOM_SDK_DOTNET_WRAP;

namespace ZoomQuiz
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		QuizControlPanel quizControlPanelWindow;

		private string m_loginName;
		private string m_loginPassword;
		private string m_meetingID;

		public void OnAuthenticationReturn(AuthResult ret)
		{
			if (AuthResult.AUTHRET_SUCCESS == ret)
			{
				CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onLoginRet(OnLoginRet);
				LoginParam loginParam = new LoginParam();
				LoginParam4Email emailLogin = new LoginParam4Email
				{
					userName = m_loginName,
					password = m_loginPassword,
					bRememberMe = false
				};
				loginParam.emailLogin = emailLogin;
				loginParam.loginType = LoginType.LoginType_Email;
				CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Login(loginParam);
			}
			else
				MessageBox.Show("Failed to authenticate SDK key/secret.", "ZoomQuiz");
		}

		public void OnMeetingStatusChanged(MeetingStatus status, int iResult)
		{
			switch (status)
			{
				case MeetingStatus.MEETING_STATUS_ENDED:
				case MeetingStatus.MEETING_STATUS_FAILED:
					quizControlPanelWindow.EndQuiz();
					CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onLogout(OnLogout);
					CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().LogOut();
					CZoomSDKeDotNetWrap.Instance.CleanUp();
					break;
				case MeetingStatus.MEETING_STATUS_INMEETING:
					quizControlPanelWindow.StartQuiz();
					break;
			}
		}

		public void OnLoginRet(LOGINSTATUS ret, IAccountInfo pAccountInfo)
		{
			if (LOGINSTATUS.LOGIN_SUCCESS == ret)
			{
				StartParam param = new StartParam
				{
					userType = SDKUserType.SDK_UT_NORMALUSER
				};
				StartParam4NormalUser startParam = new StartParam4NormalUser
				{
					isAudioOff = false,
					isVideoOff = false
				};
				startParam.hDirectShareAppWnd.value=0;
				startParam.isDirectShareDesktop = false;
				if (ulong.TryParse(m_meetingID, out ulong meetingID))
					startParam.meetingNumber = meetingID;
				param.normaluserStart = startParam;
				SDKError err = CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().Start(param);
				if (err == SDKError.SDKERR_SUCCESS)
					CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().Add_CB_onMeetingStatusChanged(OnMeetingStatusChanged);
			}
			else if (LOGINSTATUS.LOGIN_FAILED == ret)
				MessageBox.Show("Failed to login.", "ZoomQuiz");
		}

		public void OnLogout()
		{
			// Console message?
		}

		static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
		{
			Logger.Log($"Unhandled Exception: {args.ExceptionObject}");
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);
			Logger.StartLogging();

			bool presentationOnly = e.Args.Length < 5;
			quizControlPanelWindow = new QuizControlPanel(presentationOnly);
			if (!presentationOnly)
			{
				string zoomDomain = e.Args[0];
				string sdkKey = e.Args[1];
				string sdkSecret = e.Args[2];
				m_loginName = e.Args[3];
				m_loginPassword = e.Args[4];
				m_meetingID = e.Args[5];

				if (quizControlPanelWindow.StartedOK)
				{
					InitParam initParam = new InitParam
					{
						web_domain = zoomDomain
					};
					SDKError err = CZoomSDKeDotNetWrap.Instance.Initialize(initParam);
					if (SDKError.SDKERR_SUCCESS == err)
					{
						//register callback
						CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onAuthenticationReturn(OnAuthenticationReturn);
						AuthParam authParam = new AuthParam
						{
							appKey = sdkKey,
							appSecret = sdkSecret
						};
						CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().SDKAuth(authParam);
					}
					else
						MessageBox.Show("Failed to initialize Zoom SDK.", "ZoomQuiz");
				}
				else
					Shutdown();
			}
			else
			{
				if (quizControlPanelWindow.StartedOK)
				{
					MessageBox.Show("Running in presentation only mode. To run this program with Zoom, use the command line to supply arguments.\nquizhost.exe zoomDomain sdkKey sdkSecret loginName loginPassword", "ZoomQuiz");
					quizControlPanelWindow.StartQuiz();
				}
			}
		}

		private void Application_Exit(object sender, ExitEventArgs e)
		{
			Logger.StopLogging();
		}
	}
}
