using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace chatapp
{
    public partial class MediaViewerWindow : Window
    {
        private readonly string _mediaUrl;
        private DispatcherTimer _timer;
        private bool _isDraggingSlider = false;
        private bool _isPlaying = false;
        private bool _isGif = false;
        private WebBrowser _gifBrowser = null;

        // 갤러리 모드를 위한 속성 추가
        private List<string> _galleryMedias = new List<string>();
        private int _currentMediaIndex = 0;
        private bool _isGalleryMode = false;

        public MediaViewerWindow(string mediaUrl)
        {
            InitializeComponent();
            _mediaUrl = mediaUrl;

            // 파일이 GIF인지 확인
            _isGif = Path.GetExtension(mediaUrl).ToLower() == ".gif";

            // 파일 이름을 타이틀에 표시
            string fileName = Path.GetFileName(new Uri(_mediaUrl).LocalPath);
            TitleText.Text = $"미디어 뷰어 - {fileName}";

            // 타이머 초기화 (재생 시간 업데이트용)
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick;

            // 키보드 이벤트 등록
            this.KeyDown += Window_KeyDown;
        }
        // 갤러리 모드 설정 메서드 추가
        public void SetGalleryMode(List<string> mediaUrls)
        {
            if (mediaUrls == null || mediaUrls.Count == 0)
                return;

            _galleryMedias = new List<string>(mediaUrls);
            _currentMediaIndex = _galleryMedias.IndexOf(_mediaUrl);

            if (_currentMediaIndex < 0) _currentMediaIndex = 0;

            _isGalleryMode = true;

            // 갤러리 UI 요소 표시
            ShowGalleryControls();

            // 버튼 상태 업데이트
            UpdateGalleryButtonsState();
        }

        // 갤러리 컨트롤 표시 메서드
        private void ShowGalleryControls()
        {
            // 이미 XAML에 요소가 있다고 가정하고 표시 상태만 변경
            if (PrevMediaButton != null) PrevMediaButton.Visibility = Visibility.Visible;
            if (NextMediaButton != null) NextMediaButton.Visibility = Visibility.Visible;
            if (MediaCountText != null)
            {
                MediaCountText.Visibility = Visibility.Visible;
                UpdateMediaCounter();
            }

            // 만약 XAML에 요소가 없다면 여기서 동적으로 생성할 수 있음
            // 이 예제에서는 XAML에 이미 요소가 있다고 가정
        }

        // 미디어 카운터 업데이트
        private void UpdateMediaCounter()
        {
            if (!_isGalleryMode || MediaCountText == null) return;

            MediaCountText.Text = $"{_currentMediaIndex + 1} / {_galleryMedias.Count}";
        }

        // 갤러리 버튼 상태 업데이트
        private void UpdateGalleryButtonsState()
        {
            if (!_isGalleryMode)
            {
                // 갤러리 모드가 아니면 버튼 숨기기
                if (PrevMediaButton != null) PrevMediaButton.Visibility = Visibility.Collapsed;
                if (NextMediaButton != null) NextMediaButton.Visibility = Visibility.Collapsed;
                if (MediaCountText != null) MediaCountText.Visibility = Visibility.Collapsed;
                return;
            }

            // 첫 번째 미디어일 때 이전 버튼 비활성화
            if (PrevMediaButton != null)
                PrevMediaButton.IsEnabled = _currentMediaIndex > 0;

            // 마지막 미디어일 때 다음 버튼 비활성화
            if (NextMediaButton != null)
                NextMediaButton.IsEnabled = _currentMediaIndex < _galleryMedias.Count - 1;

            // 미디어 카운터 업데이트
            UpdateMediaCounter();
        }

        // 이전 미디어 버튼 클릭 핸들러
        private void PrevMediaButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isGalleryMode || _currentMediaIndex <= 0) return;

            // 현재 재생 중이던 미디어 중지
            StopCurrentMedia();

            _currentMediaIndex--;

            // 새 창으로 미디어 열기
            OpenNewMediaViewer(_galleryMedias[_currentMediaIndex]);
        }

        // 다음 미디어 버튼 클릭 핸들러
        private void NextMediaButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isGalleryMode || _currentMediaIndex >= _galleryMedias.Count - 1) return;

            // 현재 재생 중이던 미디어 중지
            StopCurrentMedia();

            _currentMediaIndex++;

            // 새 창으로 미디어 열기
            OpenNewMediaViewer(_galleryMedias[_currentMediaIndex]);
        }

        // 현재 미디어 중지
        private void StopCurrentMedia()
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.Stop();
                _timer.Stop();
            }
        }

        // 새 미디어 뷰어 창 열기
        private void OpenNewMediaViewer(string mediaUrl)
        {
            var newViewer = new MediaViewerWindow(mediaUrl);

            // 갤러리 모드 설정 전달
            if (_isGalleryMode)
            {
                newViewer.Loaded += (s, e) =>
                {
                    newViewer.SetGalleryMode(_galleryMedias);
                };
            }

            // 위치 및 크기 전달
            newViewer.Left = this.Left;
            newViewer.Top = this.Top;
            newViewer.Width = this.Width;
            newViewer.Height = this.Height;

            // 새 창 열고 현재 창 닫기
            newViewer.Show();
            this.Close();
        }

        // 키보드 이벤트 추가 (왼쪽/오른쪽 화살표로 미디어 이동)
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isGalleryMode) return;

            if (e.Key == Key.Left)
            {
                PrevMediaButton_Click(sender, e);
            }
            else if (e.Key == Key.Right)
            {
                NextMediaButton_Click(sender, e);
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 미디어 로드
            LoadMedia();

            // 갤러리 버튼 초기 상태 설정
            UpdateGalleryButtonsState();
        }

        private void LoadMedia()
        {
            try
            {
                // 로딩 표시 활성화
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

                if (_isGif)
                {
                    // GIF인 경우 Image 컨트롤 사용
                    SetupGifViewer(mediaUrl);
                }
                else
                {
                    // 일반 미디어인 경우 MediaElement 사용
                    SetupVideoPlayer(mediaUrl);
                }
            }
            catch (Exception ex)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                MessageBox.Show($"미디어 로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"예외 상세: {ex}");
            }
        }

        private void SetupGifViewer(string mediaUrl)
        {
            try
            {
                // 비디오 컨트롤 관련 요소만 숨기기
                MediaPlayer.Visibility = Visibility.Collapsed;
                TimelineSlider.Visibility = Visibility.Collapsed;
                PlayPauseButton.Visibility = Visibility.Collapsed;
                StopButton.Visibility = Visibility.Collapsed;
                TimeInfoText.Visibility = Visibility.Collapsed;

                // 볼륨 슬라이더의 부모 패널 찾기 (Grid의 StackPanel)
                if (VolumeSlider.Parent is FrameworkElement volumeParent)
                {
                    volumeParent.Visibility = Visibility.Collapsed;
                }

                // 다운로드 버튼은 유지
                DownloadButton.Visibility = Visibility.Visible;

                // WebBrowser로 GIF 표시
                if (_gifBrowser == null)
                {
                    _gifBrowser = new WebBrowser();
                    Grid contentGrid = (Grid)LogicalTreeHelper.FindLogicalNode(MainGrid, "ContentGrid");
                    if (contentGrid != null)
                    {
                        contentGrid.Children.Add(_gifBrowser);
                    }
                    else
                    {
                        // ContentGrid가 없으면 Grid.Row=1 위치에 추가
                        Grid.SetRow(_gifBrowser, 1);
                        MainGrid.Children.Add(_gifBrowser);
                    }
                }

                // HTML로 GIF 애니메이션 표시
                string htmlContent = $@"
                <html>
                <head>
                    <style>
                        body {{ 
                            margin: 0;
                            padding: 0;
                            background-color: #222222;
                            overflow: hidden;
                            display: flex;
                            justify-content: center;
                            align-items: center;
                            height: 100vh;
                        }}
                        img {{
                            max-width: 100%;
                            max-height: 100%;
                            object-fit: contain;
                        }}
                    </style>
                </head>
                <body>
                    <img src=""{mediaUrl}"" alt=""GIF 애니메이션"" />
                </body>
                </html>";

                _gifBrowser.NavigateToString(htmlContent);

                // 로딩 표시 비활성화
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                MessageBox.Show($"GIF 로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"GIF 로드 예외 상세: {ex}");
            }
        }

        private void SetupVideoPlayer(string mediaUrl)
        {
            // 비디오 컨트롤 표시
            MediaControls.Visibility = Visibility.Visible;
            MediaPlayer.Visibility = Visibility.Visible;

            // GIF 이미지 숨기기
            GifImage.Visibility = Visibility.Collapsed;

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

        private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;

            // GIF인 경우 GIF 뷰어로 다시 시도
            if (_isGif)
            {
                SetupGifViewer(_mediaUrl);
                return;
            }

            MessageBox.Show($"미디어 로드 실패: {e.ErrorException.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine($"미디어 URL: {_mediaUrl}");
            Console.WriteLine($"오류 상세: {e.ErrorException}");
        }

        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            // GIF라면 이벤트를 무시
            if (_isGif) return;

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

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // 다운로드 버튼 비활성화
            DownloadButton.IsEnabled = false;
            DownloadButton.Content = "다운로드 중...";

            try
            {
                // 갤러리 모드에서는 현재 표시중인 미디어를 다운로드
                string mediaUrlToDownload = _isGalleryMode ? _galleryMedias[_currentMediaIndex] : _mediaUrl;

                // 동영상 URL에서 파일명 추출
                string fileName = Path.GetFileName(new Uri(mediaUrlToDownload).LocalPath);

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    DefaultExt = Path.GetExtension(fileName)
                };

                // 확장자에 따라 필터 설정
                if (_isGif)
                {
                    saveDialog.Filter = "GIF 이미지|*.gif";
                }
                else
                {
                    saveDialog.Filter = "동영상 파일|*.mp4;*.mov;*.avi;*.mkv;*.wmv";
                }

                if (saveDialog.ShowDialog() == true)
                {
                    string serverUrl = AppSettings.GetServerUrl();
                    string apiUrl;

                    // GIF는 이미지 다운로드 API 사용, 나머지는 비디오 다운로드 API 사용
                    if (_isGif)
                    {
                        apiUrl = $"{serverUrl}/api/File/download?fileName={fileName}";
                    }
                    else
                    {
                        apiUrl = $"{serverUrl}/api/File/videodownload?fileName={fileName}";
                    }

                    using (var client = new HttpClient())
                    {
                        try
                        {
                            var response = await client.GetAsync(apiUrl);

                            if (response.IsSuccessStatusCode)
                            {
                                byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                                File.WriteAllBytes(saveDialog.FileName, fileBytes);
                                MessageBox.Show("파일이 성공적으로 다운로드되었습니다.", "다운로드 완료", MessageBoxButton.OK, MessageBoxImage.Information);
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 다운로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 다운로드 버튼 복원
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "다운로드";
            }
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // GIF라면 이벤트를 무시
            if (_isGif) return;

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
            // GIF라면 이벤트를 무시
            if (_isGif) return;

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
            // GIF라면 이벤트를 무시
            if (_isGif) return;

            MediaPlayer.Stop();
            MediaPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            PlayPauseButton.Content = "▶";
            _timer.Stop();
            UpdateTimeInfo();
        }

        private void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            // GIF라면 이벤트를 무시
            if (_isGif) return;

            _isDraggingSlider = true;
        }

        private void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // GIF라면 이벤트를 무시
            if (_isGif) return;

            _isDraggingSlider = false;
            MediaPlayer.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
            UpdateTimeInfo();
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // GIF라면 이벤트를 무시
            if (_isGif) return;

            if (_isDraggingSlider)
            {
                // 사용자가 드래그 중일 때는 시간 정보만 업데이트
                TimeInfoText.Text = $"{TimeSpan.FromSeconds(TimelineSlider.Value):mm\\:ss} / {MediaPlayer.NaturalDuration.TimeSpan:mm\\:ss}";
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // GIF라면 이벤트를 무시
            if (_isGif) return;

            if (MediaPlayer != null)
            {
                MediaPlayer.Volume = VolumeSlider.Value;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // GIF라면 이벤트를 무시
            if (_isGif) return;

            if (!_isDraggingSlider && MediaPlayer.Source != null)
            {
                TimelineSlider.Value = MediaPlayer.Position.TotalSeconds;
                UpdateTimeInfo();
            }
        }

        private void UpdateTimeInfo()
        {
            // GIF라면 이벤트를 무시
            if (_isGif) return;

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
            if (!_isGif) MediaPlayer.Close();
            _timer.Stop();
            this.Close();
        }
    }
}