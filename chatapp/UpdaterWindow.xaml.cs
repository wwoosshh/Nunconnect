using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;

namespace chatapp
{
    public partial class UpdaterWindow : Window
    {
        private readonly string _downloadUrl;
        private readonly string _newVersion;
        private readonly string _tempFilePath;
        private bool _isDownloadComplete = false;
        private HttpClient _httpClient;
        private DispatcherTimer _timer;

        public UpdaterWindow(string downloadUrl, string newVersion)
        {
            InitializeComponent();

            _downloadUrl = downloadUrl;
            _newVersion = newVersion;
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"Connect_{_newVersion}_Setup.exe");

            _httpClient = new HttpClient();

            // 로그 초기화
            AppendLog($"업데이트를 시작합니다. 현재 버전에서 {_newVersion} 버전으로 업데이트합니다.");
            AppendLog($"다운로드 URL: {_downloadUrl}");

            // 창이 로드되면 다운로드 시작
            this.Loaded += (s, e) => StartDownload();

            // 타이머 설정 (로그와 진행 상황 업데이트용)
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // 진행 상황 애니메이션
            if (!_isDownloadComplete && ProgressBar.Value < 90)
            {
                ProgressBar.Value += 0.5;
            }
        }

        private async void StartDownload()
        {
            try
            {
                AppendLog("다운로드를 시작합니다...");

                // 이미 파일이 존재하면 삭제
                if (File.Exists(_tempFilePath))
                {
                    File.Delete(_tempFilePath);
                    AppendLog("기존 임시 파일을 삭제했습니다.");
                }

                // 파일 다운로드 시작
                var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"다운로드 실패: {response.StatusCode}");
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using (var fileStream = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var downloadStream = await response.Content.ReadAsStreamAsync())
                {
                    byte[] buffer = new byte[8192];
                    long bytesRead = 0;
                    int count;

                    while ((count = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, count);
                        bytesRead += count;

                        if (totalBytes > 0)
                        {
                            double percentage = (double)bytesRead / totalBytes * 100;
                            UpdateProgress(percentage);
                        }
                    }
                }

                _isDownloadComplete = true;
                UpdateProgress(100);
                StatusText.Text = "다운로드 완료. 설치 준비 중...";
                AppendLog("다운로드 완료되었습니다.");

                // 설치 프로그램 실행 준비
                PrepareInstallation();
            }
            catch (Exception ex)
            {
                _isDownloadComplete = true;
                StatusText.Text = "다운로드 실패";
                AppendLog($"오류 발생: {ex.Message}");

                MessageBox.Show(
                    $"업데이트 다운로드 중 오류가 발생했습니다:\n{ex.Message}\n\n홈페이지에서 직접 다운로드하시겠습니까?",
                    "다운로드 오류",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error
                );

                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://nunconnect.netlify.app/",
                    UseShellExecute = true
                });

                this.Close();
            }
        }

        private void PrepareInstallation()
        {
            try
            {
                // 설치 파일이 존재하는지 확인
                if (!File.Exists(_tempFilePath))
                {
                    throw new FileNotFoundException("다운로드된 설치 파일을 찾을 수 없습니다.");
                }

                StatusText.Text = "설치 준비 완료. 재시작 버튼을 클릭하세요.";
                AppendLog("설치 준비가 완료되었습니다. 재시작 버튼을 클릭하면 업데이트가 진행됩니다.");
                RestartButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                AppendLog($"설치 준비 오류: {ex.Message}");
                MessageBox.Show(
                    $"설치 준비 중 오류가 발생했습니다:\n{ex.Message}",
                    "설치 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void UpdateProgress(double percentage)
        {
            ProgressBar.Value = percentage;
            AppendLog($"다운로드 진행률: {percentage:F1}%");
        }

        private void AppendLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        private void StartInstallation()
        {
            try
            {
                AppendLog("설치 프로그램을 실행합니다...");

                // 설치 파일이 존재하는지 다시 확인
                if (!File.Exists(_tempFilePath))
                {
                    throw new FileNotFoundException("다운로드된 설치 파일을 찾을 수 없습니다: " + _tempFilePath);
                }

                // 로그에 절대 경로 표시 (디버깅용)
                AppendLog($"설치 파일 경로: {Path.GetFullPath(_tempFilePath)}");

                // 먼저 프로세스를 시작
                Process installerProcess = new Process();
                installerProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = _tempFilePath,
                    // 사용자에게 설치 과정이 보이도록 silent 옵션 제거
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                    UseShellExecute = true
                };

                // 프로세스가 시작되었는지 확인
                bool started = installerProcess.Start();

                if (started)
                {
                    AppendLog("설치 프로그램이 성공적으로 시작되었습니다.");

                    // 잠시 대기 후 앱 종료 (설치 프로그램이 제대로 시작할 시간을 줍니다)
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // 현재 프로그램 종료
                            Application.Current.Shutdown();
                        });
                    });
                }
                else
                {
                    throw new Exception("설치 프로그램을 시작할 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"설치 시작 오류: {ex.Message}");
                MessageBox.Show(
                    $"설치 프로그램 실행 중 오류가 발생했습니다:\n{ex.Message}\n\n홈페이지에서 직접 다운로드하시겠습니까?",
                    "설치 오류",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error
                );

                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://nunconnect.netlify.app/",
                    UseShellExecute = true
                });
            }
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            StartInstallation();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "업데이트를 취소하시겠습니까?",
                "업데이트 취소",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                this.Close();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _timer.Stop();
            _httpClient.Dispose();
            base.OnClosing(e);
        }
    }
}