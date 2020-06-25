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

		private string m_zoomDomain;
		private string m_sdkKey;
		private string m_sdkSecret;
		private string m_loginName;
		private string m_loginPassword;
		private string m_meetingID;
		public void onAuthenticationReturn(AuthResult ret)
		{
			if (AuthResult.AUTHRET_SUCCESS == ret)
			{
				CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onLoginRet(onLoginRet);
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

		public void onMeetingStatusChanged(MeetingStatus status, int iResult)
		{
			switch (status)
			{
				case MeetingStatus.MEETING_STATUS_ENDED:
				case MeetingStatus.MEETING_STATUS_FAILED:
					quizControlPanelWindow.EndQuiz();
					CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onLogout(onLogout);
					CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().LogOut();
					CZoomSDKeDotNetWrap.Instance.CleanUp();
					break;
				case MeetingStatus.MEETING_STATUS_INMEETING:
					quizControlPanelWindow.StartQuiz();
					break;
			}
		}

		public void onLoginRet(LOGINSTATUS ret, IAccountInfo pAccountInfo)
		{
			if (LOGINSTATUS.LOGIN_SUCCESS == ret)
			{
				StartParam param = new StartParam();
				param.userType = SDKUserType.SDK_UT_NORMALUSER;
				StartParam4NormalUser startParam = new StartParam4NormalUser
				{
					isAudioOff = false,
					isVideoOff = false
				};
				startParam.hDirectShareAppWnd.value=0;
				startParam.isDirectShareDesktop = false;
				ulong meetingID = 0;
				if(UInt64.TryParse(m_meetingID, out meetingID))
					startParam.meetingNumber = meetingID;
				param.normaluserStart = startParam;
				SDKError err = CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().Start(param);
				if (err == SDKError.SDKERR_SUCCESS)
					CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().Add_CB_onMeetingStatusChanged(onMeetingStatusChanged);
			}
			else if (LOGINSTATUS.LOGIN_FAILED == ret)
				MessageBox.Show("Failed to login.", "ZoomQuiz");
		}

		public void onLogout()
		{
			// Console message?
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			bool presentationOnly = e.Args.Length < 5;
			quizControlPanelWindow = new QuizControlPanel(presentationOnly);
			if (!presentationOnly)
			{
				m_zoomDomain = e.Args[0];
				m_sdkKey = e.Args[1];
				m_sdkSecret = e.Args[2];
				m_loginName = e.Args[3];
				m_loginPassword = e.Args[4];
				m_meetingID = e.Args[5];

				if (quizControlPanelWindow.StartedOK)
				{
					InitParam initParam = new InitParam();
					initParam.web_domain = m_zoomDomain;
					SDKError err = CZoomSDKeDotNetWrap.Instance.Initialize(initParam);
					if (SDKError.SDKERR_SUCCESS == err)
					{
						//register callback
						CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onAuthenticationReturn(onAuthenticationReturn);
						AuthParam authParam = new AuthParam
						{
							appKey = m_sdkKey,
							appSecret = m_sdkSecret
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
				MessageBox.Show("Running in presentation only mode. To run this program with Zoom, use the command line to supply arguments.\nquizhost.exe zoomDomain sdkKey sdkSecret loginName loginPassword", "ZoomQuiz");
				quizControlPanelWindow.StartQuiz();
			}
		}
	}
}
