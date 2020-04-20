using System;
using System.Threading;
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
			if (ZOOM_SDK_DOTNET_WRAP.AuthResult.AUTHRET_SUCCESS == ret)
			{
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onLoginRet(onLoginRet);
				ZOOM_SDK_DOTNET_WRAP.LoginParam loginParam = new ZOOM_SDK_DOTNET_WRAP.LoginParam();
				ZOOM_SDK_DOTNET_WRAP.LoginParam4Email emailLogin = new ZOOM_SDK_DOTNET_WRAP.LoginParam4Email
				{
					userName = m_loginName,
					password = m_loginPassword,
					bRememberMe = false
				};
				loginParam.emailLogin = emailLogin;
				loginParam.loginType = LoginType.LoginType_Email;
				ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Login(loginParam);
			}
			else
				MessageBox.Show("Failed to authenticate SDK key/secret.");
		}

		public void onMeetingStatusChanged(MeetingStatus status, int iResult)
		{
			switch (status)
			{
				case ZOOM_SDK_DOTNET_WRAP.MeetingStatus.MEETING_STATUS_ENDED:
				case ZOOM_SDK_DOTNET_WRAP.MeetingStatus.MEETING_STATUS_FAILED:
					quizControlPanelWindow.EndQuiz();
					ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onLogout(onLogout);
					ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().LogOut();
					ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.CleanUp();
					break;
				case ZOOM_SDK_DOTNET_WRAP.MeetingStatus.MEETING_STATUS_INMEETING:
					quizControlPanelWindow.StartQuiz();
					break;
			}
		}

		public void onLoginRet(LOGINSTATUS ret, IAccountInfo pAccountInfo)
		{
			if (ZOOM_SDK_DOTNET_WRAP.LOGINSTATUS.LOGIN_SUCCESS == ret)
			{
				ZOOM_SDK_DOTNET_WRAP.StartParam param = new ZOOM_SDK_DOTNET_WRAP.StartParam();
				param.userType = ZOOM_SDK_DOTNET_WRAP.SDKUserType.SDK_UT_NORMALUSER;
				ZOOM_SDK_DOTNET_WRAP.StartParam4NormalUser startParam = new ZOOM_SDK_DOTNET_WRAP.StartParam4NormalUser
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
				ZOOM_SDK_DOTNET_WRAP.SDKError err = ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().Start(param);
				if (err == SDKError.SDKERR_SUCCESS)
					ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetMeetingServiceWrap().Add_CB_onMeetingStatusChanged(onMeetingStatusChanged);
			}
			else if (ZOOM_SDK_DOTNET_WRAP.LOGINSTATUS.LOGIN_FAILED == ret)
				MessageBox.Show("Failed to login.");
		}

		public void onLogout()
		{
			// Console message?
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			if (e.Args.Length >= 5)
			{
				m_zoomDomain = e.Args[0];
				m_sdkKey = e.Args[1];
				m_sdkSecret = e.Args[2];
				m_loginName = e.Args[3];
				m_loginPassword = e.Args[4];
				m_meetingID = e.Args[5];

				quizControlPanelWindow = new QuizControlPanel();
				if (quizControlPanelWindow.StartedOK)
				{
					ZOOM_SDK_DOTNET_WRAP.InitParam initParam = new ZOOM_SDK_DOTNET_WRAP.InitParam();
					initParam.web_domain = m_zoomDomain;
					ZOOM_SDK_DOTNET_WRAP.SDKError err = ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.Initialize(initParam);
					if (ZOOM_SDK_DOTNET_WRAP.SDKError.SDKERR_SUCCESS == err)
					{
						//register callback
						ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().Add_CB_onAuthenticationReturn(onAuthenticationReturn);
						ZOOM_SDK_DOTNET_WRAP.AuthParam authParam = new ZOOM_SDK_DOTNET_WRAP.AuthParam
						{
							appKey = m_sdkKey,
							appSecret = m_sdkSecret
						};
						ZOOM_SDK_DOTNET_WRAP.CZoomSDKeDotNetWrap.Instance.GetAuthServiceWrap().SDKAuth(authParam);
					}
					else
						MessageBox.Show("Failed to initialize Zoom SDK.");
				}
				else
					Shutdown();
			}
			else
				MessageBox.Show("To run this program, use the command line to supply arguments.\nquizhost.exe zoomDomain sdkKey sdkSecret loginName loginPassword");
		}
	}
}
