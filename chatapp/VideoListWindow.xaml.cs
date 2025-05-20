using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
// 다른 필요한 using 문 유지

namespace chatapp
{
    public partial class VideoListWindow : Window
    {
        private readonly string _roomId;
        private readonly MainWindow.UserData _currentUser; // MainWindow의 UserData 클래스 사용
        private readonly List<string> _videoUrls = new List<string>();
        private DispatcherTimer _loadingDotsTimer;

        public VideoListWindow(string roomId, MainWindow.UserData currentUser)
        {
            InitializeComponent();

            _roomId = roomId;
            _currentUser = currentUser;

            // 로딩 애니메이션 설정
            InitializeLoadingAnimation();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 페이드 인 애니메이션
            this.Opacity = 0;
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            this.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 동영상 로드
            LoadVideos();
        }

        private void InitializeLoadingAnimation()
        {
            _loadingDotsTimer = new DispatcherTimer();
            _loadingDotsTimer.Interval = TimeSpan.FromMilliseconds(500);
            _loadingDotsTimer.Tick += (s, e) =>
            {
                if (LoadingDots.Text == "...")
                    LoadingDots.Text = "";
                else if (LoadingDots.Text == "")
                    LoadingDots.Text = ".";
                else if (LoadingDots.Text == ".")
                    LoadingDots.Text = "..";
                else
                    LoadingDots.Text = "...";
            };
            _loadingDotsTimer.Start();
        }

        private async void LoadVideos()
        {
            try
            {
                // 로딩 상태 표시
                LoadingIndicator.Visibility = Visibility.Visible;
                NoVideosText.Visibility = Visibility.Collapsed;
                VideoPanel.Children.Clear();
                _videoUrls.Clear();

                // 채팅 메시지 로드
                using (HttpClient client = new HttpClient())
                {
                    string baseUrl = AppSettings.GetServerUrl();

                    var response = await client.GetAsync($"{baseUrl}/api/User/loadMessages?roomId={_roomId}&count=1000");
                    if (!response.IsSuccessStatusCode)
                    {
                        ShowError("동영상 목록을 가져오는 데 실패했습니다.");
                        return;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var messages = JsonConvert.DeserializeObject<List<Message.ChatMessage>>(json) ?? new List<Message.ChatMessage>();

                    // 동영상 URL만 필터링
                    foreach (var message in messages)
                    {
                        if (string.IsNullOrWhiteSpace(message.Message)) continue;

                        if (Uri.IsWellFormedUriString(message.Message, UriKind.Absolute) && IsVideoUrl(message.Message))
                        {
                            _videoUrls.Add(message.Message);
                        }
                    }

                    // 동영상 갯수 표시
                    VideoCountText.Text = $"총 {_videoUrls.Count}개";

                    // 동영상 목록 표시
                    foreach (var videoUrl in _videoUrls)
                    {
                        AddVideoPreview(videoUrl);
                        // 약간의 지연을 두어 UI 스레드가 응답할 수 있도록 함
                        await Task.Delay(10);
                    }

                    // 로딩 상태 숨김
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                    _loadingDotsTimer.Stop();

                    // 동영상이 없을 경우 메시지 표시
                    if (_videoUrls.Count == 0)
                    {
                        NoVideosText.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"동영상 목록을 불러오는 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void AddVideoPreview(string videoUrl)
        {
            // 동영상 컨테이너
            Border container = new Border
            {
                Width = 240,
                Height = 180,
                Style = (Style)FindResource("VideoContainer"),
                Cursor = Cursors.Hand
            };

            Grid videoGrid = new Grid();

            // 파일명 표시
            TextBlock fileNameBlock = new TextBlock
            {
                Text = System.IO.Path.GetFileName(videoUrl),
                FontSize = 14,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10, 0, 10, 15),
                TextAlignment = TextAlignment.Center
            };

            // 재생 버튼
            Border playButton = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)),
                Width = 60,
                Height = 60,
                CornerRadius = new CornerRadius(30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock playIcon = new TextBlock
            {
                Text = "▶",
                FontSize = 24,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6200EA")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)  // 아이콘 약간 오른쪽으로 조정
            };

            playButton.Child = playIcon;

            // 동영상 유형 아이콘
            TextBlock videoTypeIcon = new TextBlock
            {
                Text = "🎬",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(15, 15, 0, 0)
            };

            // 애니메이션 효과 (재생 버튼)
            playButton.RenderTransformOrigin = new Point(0.5, 0.5);
            playButton.RenderTransform = new ScaleTransform(1, 1);

            playButton.MouseEnter += (s, e) =>
            {
                DoubleAnimation scaleX = new DoubleAnimation(1, 1.1, TimeSpan.FromSeconds(0.2));
                DoubleAnimation scaleY = new DoubleAnimation(1, 1.1, TimeSpan.FromSeconds(0.2));

                ScaleTransform transform = playButton.RenderTransform as ScaleTransform;
                if (transform != null)
                {
                    transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                }
            };

            playButton.MouseLeave += (s, e) =>
            {
                DoubleAnimation scaleX = new DoubleAnimation(1.1, 1, TimeSpan.FromSeconds(0.2));
                DoubleAnimation scaleY = new DoubleAnimation(1.1, 1, TimeSpan.FromSeconds(0.2));

                ScaleTransform transform = playButton.RenderTransform as ScaleTransform;
                if (transform != null)
                {
                    transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                }
            };

            videoGrid.Children.Add(fileNameBlock);
            videoGrid.Children.Add(playButton);
            videoGrid.Children.Add(videoTypeIcon);
            container.Child = videoGrid;

            // 동영상 클릭 이벤트
            container.MouseLeftButtonUp += (s, e) =>
            {
                // 페이드 아웃 애니메이션
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0.7, TimeSpan.FromSeconds(0.2));

                fadeOut.Completed += (s2, e2) =>
                {
                    var viewer = new MediaViewerWindow(videoUrl);
                    viewer.Owner = this;

                    // 갤러리 모드 설정 - 현재 비디오가 포함된 모든 비디오 전달
                    viewer.Loaded += (s3, e3) => viewer.SetGalleryMode(_videoUrls);

                    viewer.ShowDialog();

                    // 페이드 인 애니메이션
                    DoubleAnimation fadeIn = new DoubleAnimation(0.7, 1, TimeSpan.FromSeconds(0.2));
                    this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };

                this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };

            VideoPanel.Children.Add(container);
        }

        private bool IsVideoUrl(string url)
        {
            string extension = System.IO.Path.GetExtension(url).ToLower();
            return extension == ".mp4" || extension == ".mov" || extension == ".avi" ||
                   extension == ".mkv" || extension == ".wmv";
        }

        private void ShowError(string message)
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            _loadingDotsTimer.Stop();
            MessageBox.Show(message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadVideos();
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
            // 페이드 아웃 애니메이션
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            fadeOut.Completed += (s, args) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}