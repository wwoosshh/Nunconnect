using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using System.Windows.Threading;

namespace chatapp
{
    public partial class App : Application
    {
        // 현재 버전
        public static string CurrentVersion { get; } = "1.6.1";

        // 버전 응답 클래스
        public class VersionResponse
        {
            public string Version { get; set; } = string.Empty;
            public string DownloadUrl { get; set; } = string.Empty;
            public string ReleaseNotes { get; set; } = string.Empty;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var notificationManager = NotificationManager.Instance;
            // 서버 URL 설정
            AppSettings.IsServerPc = true; // 개발 환경에서는 true, 배포 환경에서는 false 로 설정

            // 업데이트 확인
            await CheckForUpdateAsync();

            // 기타 앱 초기화 코드...
        }

        private async Task CheckForUpdateAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();
                string apiUrl = AppSettings.GetServerUrl();
                var response = await client.GetAsync($"{apiUrl}/api/User/checkVersion");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var versionData = JsonConvert.DeserializeObject<VersionResponse>(json);

                    if (versionData != null && versionData.Version != CurrentVersion)
                    {
                        var result = MessageBox.Show(
                            $"새 버전({versionData.Version})이 있습니다.\n\n{versionData.ReleaseNotes}\n\n지금 업데이트하시겠습니까?",
                            "업데이트 확인",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information
                        );

                        if (result == MessageBoxResult.Yes)
                        {
                            // 업데이트 창 열기
                            var updaterWindow = new UpdaterWindow(versionData.DownloadUrl, versionData.Version);
                            updaterWindow.Show();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("버전 확인 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}