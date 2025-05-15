using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace chatapp
{
    public partial class MediaViewerWindow : Window
    {
        private readonly string _mediaUrl;
        private DispatcherTimer _timer;
        private bool _isDraggingSlider = false;
        private bool _isPlaying = false;

        public MediaViewerWindow(string mediaUrl)
        {
            InitializeComponent();
            _mediaUrl = mediaUrl;

            // 파일 이름을 타이틀에 표시
            string fileName = Path.GetFileName(new Uri(_mediaUrl).LocalPath);
            TitleText.Text = $"미디어 뷰어 - {fileName}";

            // 타이머 초기화 (재생 시간 업데이트용)
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 미디어 로드
            LoadMedia();
        }

        private void LoadMedia()
        {
            try
            {
                // 로딩 표시 확인
                LoadingIndicator.Visibility = Visibility.Visible;

                // 로컬 환경일 때 주소 변환
                string mediaUrl = _mediaUrl;
                if (Environment.MachineName == "DESKTOP-NV0M9IM")
                {
                    if (mediaUrl.Contains("nunconnect.duckdns.org:5159"))
                    {
                        mediaUrl = mediaUrl.Replace("nunconnect.duckdns.org:5159", "localhost:5159");
                    }
                }

                // 디버깅 정보 출력
                Console.WriteLine($"로드할 미디어 URL: {mediaUrl}");

                // 미디어 소스 설정
                MediaPlayer.Source = new Uri(mediaUrl);

                // 볼륨 설정
                MediaPlayer.Volume = VolumeSlider.Value;

                // 로딩이 지연되는 경우를 위한 타임아웃 설정
                DispatcherTimer loadTimer = new DispatcherTimer();
                loadTimer.Interval = TimeSpan.FromSeconds(10); // 10초 타임아웃
                loadTimer.Tick += (s, e) => {
                    loadTimer.Stop();
                    if (LoadingIndicator.Visibility == Visibility.Visible)
                    {
                        // 아직도 로딩 중이라면 수동으로 재생 시도
                        LoadingIndicator.Visibility = Visibility.Collapsed;

                        // 사용자에게 메시지 표시
                        MessageBox.Show("미디어 로딩이 지연되고 있습니다. 재생 버튼을 눌러 시도해 보세요.",
                                        "정보", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };
                loadTimer.Start();

                // 재생 시도
                MediaPlayer.Play();
                MediaPlayer.Pause(); // 자동 재생 방지 (사용자가 버튼 누를 때까지)
            }
            catch (Exception ex)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                MessageBox.Show($"미디어 로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"예외 상세: {ex}");
            }
        }
        private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            MessageBox.Show($"미디어 로드 실패: {e.ErrorException.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine($"미디어 URL: {_mediaUrl}");
            Console.WriteLine($"오류 상세: {e.ErrorException}");
        }
        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            // 미디어 로드 완료
            LoadingIndicator.Visibility = Visibility.Collapsed;

            // 재생 시간 슬라이더 설정
            TimelineSlider.Minimum = 0;
            TimelineSlider.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;

            // 시간 정보 표시 업데이트
            UpdateTimeInfo();

            // 자동 재생 시작
            MediaPlayer.Play();
            _isPlaying = true;
            PlayPauseButton.Content = "⏸";

            // 타이머 시작
            _timer.Start();
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // 재생 종료 시 처리
            MediaPlayer.Position = TimeSpan.Zero;
            MediaPlayer.Stop();
            _isPlaying = false;
            PlayPauseButton.Content = "▶";
            _timer.Stop();
            UpdateTimeInfo();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying)
            {
                MediaPlayer.Pause();
                _isPlaying = false;
                PlayPauseButton.Content = "▶";
            }
            else
            {
                MediaPlayer.Play();
                _isPlaying = true;
                PlayPauseButton.Content = "⏸";
                _timer.Start();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Stop();
            MediaPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            PlayPauseButton.Content = "▶";
            _timer.Stop();
            UpdateTimeInfo();
        }

        private void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _isDraggingSlider = true;
        }

        private void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _isDraggingSlider = false;
            MediaPlayer.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
            UpdateTimeInfo();
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSlider)
            {
                // 사용자가 드래그 중일 때는 시간 정보만 업데이트
                TimeInfoText.Text = $"{TimeSpan.FromSeconds(TimelineSlider.Value):mm\\:ss} / {MediaPlayer.NaturalDuration.TimeSpan:mm\\:ss}";
            }
        }
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 다운로드 버튼 비활성화
                DownloadButton.IsEnabled = false;
                DownloadButton.Content = "다운로드 중...";

                // 동영상 URL에서 파일명 추출
                string fileName = Path.GetFileName(new Uri(_mediaUrl).LocalPath);

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    Filter = "동영상 파일|*.mp4;*.mov;*.avi;*.mkv;*.wmv",
                    DefaultExt = Path.GetExtension(fileName)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string serverUrl = AppSettings.GetServerUrl();
                    string apiUrl = $"{serverUrl}/api/File/videodownload?fileName={fileName}";

                    using (var client = new HttpClient())
                    {
                        try
                        {
                            var response = await client.GetAsync(apiUrl);

                            if (response.IsSuccessStatusCode)
                            {
                                byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                                File.WriteAllBytes(saveDialog.FileName, fileBytes);
                                MessageBox.Show("동영상이 성공적으로 다운로드되었습니다.", "다운로드 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show($"다운로드 실패: {response.StatusCode}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"서버 통신 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                // 다운로드 버튼 복원
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "다운로드";
            }
            catch (Exception ex)
            {
                // 다운로드 버튼 복원
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "다운로드";

                MessageBox.Show($"동영상 다운로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.Volume = VolumeSlider.Value;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isDraggingSlider && MediaPlayer.Source != null)
            {
                TimelineSlider.Value = MediaPlayer.Position.TotalSeconds;
                UpdateTimeInfo();
            }
        }

        private void UpdateTimeInfo()
        {
            if (MediaPlayer.Source != null && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                TimeInfoText.Text = $"{MediaPlayer.Position:mm\\:ss} / {MediaPlayer.NaturalDuration.TimeSpan:mm\\:ss}";
            }
            else
            {
                TimeInfoText.Text = "00:00 / 00:00";
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 재생 중지 및 창 닫기
            MediaPlayer.Close();
            _timer.Stop();
            this.Close();
        }
    }
}