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
    public partial class ImageListWindow : Window
    {
        private readonly string _roomId;
        private readonly MainWindow.UserData _currentUser; // MainWindow의 UserData 클래스 사용
        private readonly List<string> _imageUrls = new List<string>();
        private DispatcherTimer _loadingDotsTimer;

        public ImageListWindow(string roomId, MainWindow.UserData currentUser)
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

            // 이미지 로드
            LoadImages();
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

        private async void LoadImages()
        {
            try
            {
                // 로딩 상태 표시
                LoadingIndicator.Visibility = Visibility.Visible;
                NoImagesText.Visibility = Visibility.Collapsed;
                ImagePanel.Children.Clear();
                _imageUrls.Clear();

                // 채팅 메시지 로드
                using (HttpClient client = new HttpClient())
                {
                    string baseUrl = AppSettings.GetServerUrl();

                    var response = await client.GetAsync($"{baseUrl}/api/User/loadMessages?roomId={_roomId}&count=1000");
                    if (!response.IsSuccessStatusCode)
                    {
                        ShowError("이미지 목록을 가져오는 데 실패했습니다.");
                        return;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var messages = JsonConvert.DeserializeObject<List<Message.ChatMessage>>(json) ?? new List<Message.ChatMessage>();

                    // 이미지 URL만 필터링
                    foreach (var message in messages)
                    {
                        if (string.IsNullOrWhiteSpace(message.Message)) continue;

                        if (Uri.IsWellFormedUriString(message.Message, UriKind.Absolute) && IsImageUrl(message.Message))
                        {
                            _imageUrls.Add(message.Message);
                        }
                    }

                    // 이미지 갯수 표시
                    ImageCountText.Text = $"총 {_imageUrls.Count}개";

                    // 이미지 목록 표시
                    foreach (var imageUrl in _imageUrls)
                    {
                        AddImagePreview(imageUrl);
                        // 약간의 지연을 두어 UI 스레드가 응답할 수 있도록 함
                        await Task.Delay(10);
                    }

                    // 로딩 상태 숨김
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                    _loadingDotsTimer.Stop();

                    // 이미지가 없을 경우 메시지 표시
                    if (_imageUrls.Count == 0)
                    {
                        NoImagesText.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"이미지 목록을 불러오는 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void AddImagePreview(string imageUrl)
        {
            // 이미지 컨테이너
            Border container = new Border
            {
                Width = 180,
                Height = 180,
                Style = (Style)FindResource("ImageContainer"),
                Cursor = Cursors.Hand
            };

            Grid imageGrid = new Grid();

            // 이미지
            Image image = new Image
            {
                Width = 160,
                Height = 160,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(10)
            };

            try
            {
                // 로컬 환경일 때 주소 변환
                if (Environment.MachineName == "DESKTOP-NV0M9IM")
                {
                    if (imageUrl.Contains("nunconnect.duckdns.org:5159"))
                    {
                        imageUrl = imageUrl.Replace("nunconnect.duckdns.org:5159", "localhost:5159");
                    }
                }

                image.Source = new BitmapImage(new Uri(imageUrl));
            }
            catch
            {
                // 이미지 로드 실패 시 기본 이미지 표시
                TextBlock errorBlock = new TextBlock
                {
                    Text = "이미지 로드 실패",
                    Foreground = Brushes.Red,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                imageGrid.Children.Add(errorBlock);
                container.Child = imageGrid;
                ImagePanel.Children.Add(container);
                return;
            }

            // 이미지 클릭 이벤트
            container.MouseLeftButtonUp += (s, e) =>
            {
                // 페이드 아웃 애니메이션
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0.7, TimeSpan.FromSeconds(0.2));

                fadeOut.Completed += (s2, e2) =>
                {
                    var viewer = new ImageViewerWindow(imageUrl);
                    viewer.Owner = this;

                    // 갤러리 모드 설정 - 현재 이미지가 포함된 모든 이미지 전달
                    viewer.Loaded += (s3, e3) => viewer.SetGalleryMode(_imageUrls);

                    viewer.ShowDialog();

                    // 페이드 인 애니메이션
                    DoubleAnimation fadeIn = new DoubleAnimation(0.7, 1, TimeSpan.FromSeconds(0.2));
                    this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };

                this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };

            imageGrid.Children.Add(image);
            container.Child = imageGrid;
            ImagePanel.Children.Add(container);
        }

        private bool IsImageUrl(string url)
        {
            string extension = System.IO.Path.GetExtension(url).ToLower();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                   extension == ".bmp" || extension == ".webp" || extension == ".gif";
        }

        private void ShowError(string message)
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            _loadingDotsTimer.Stop();
            MessageBox.Show(message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadImages();
        }

        private void ViewAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_imageUrls.Count == 0)
            {
                MessageBox.Show("표시할 이미지가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 첫 번째 이미지부터 보기
            var viewer = new ImageViewerWindow(_imageUrls[0]);
            viewer.SetGalleryMode(_imageUrls); // 갤러리 모드 설정 (ImageViewerWindow에 이 기능 추가 필요)
            viewer.Owner = this;
            viewer.ShowDialog();
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