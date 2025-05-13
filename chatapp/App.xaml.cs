using System.Configuration;
using System.Data;
using System.Windows;

namespace chatapp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 서버 URL 설정
            AppSettings.IsServerPc = true; // 개발 환경에서는 true, 배포 환경에서는 false로 설정

            // 기타 앱 초기화 코드...
        }
    }

}
