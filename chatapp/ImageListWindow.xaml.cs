using Newtonsoft.Json;
using System;
using IOPath = System.IO.Path; // 별칭 사용
using UIPath = System.Windows.Shapes.Path; // 필요시 이 별칭도 사용
using System.IO; // Path.GetExtension용
using System.ComponentModel; // INotifyPropertyChanged용
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading.Tasks;// INotifyPropertyChanged를 위해 필요

// 다른 필요한 using 문 유지

namespace chatapp
{
    public class ImageItem : INotifyPropertyChanged
    {
        private BitmapImage _image;
        private bool _isLoading;
        private bool _isLoaded;

        public string Url { get; set; }

        public BitmapImage Image
        {
            get => _image;
            set
            {
                _image = value;
                OnPropertyChanged(nameof(Image));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public bool IsLoaded
        {
            get => _isLoaded;
            set
            {
                _isLoaded = value;
                OnPropertyChanged(nameof(IsLoaded));
            }
        }

        public async Task LoadImageAsync()
        {
            if (IsLoaded || IsLoading || string.IsNullOrEmpty(Url))
                return;

            IsLoading = true;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 메모리 관리를 위해 이미지 캐싱
                bitmap.UriSource = new Uri(Url);
                bitmap.DecodePixelWidth = 180; // 썸네일 크기로 제한
                bitmap.EndInit();
                bitmap.Freeze(); // 중요: UI 스레드 간 공유 가능하도록 Freeze

                Image = bitmap;
                IsLoaded = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"이미지 로드 오류: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public partial class ImageListWindow : Window
    {
        private readonly string _roomId;
        private readonly MainWindow.UserData _currentUser; // MainWindow의 UserData 클래스 사용
        private readonly List<string> _imageUrls = new List<string>();
        private DispatcherTimer _loadingDotsTimer;
        private ObservableCollection<ImageItem> _images = new ObservableCollection<ImageItem>();

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

            // 데이터 바인딩 설정
            ImageItems.ItemsSource = _images;

            // 이미 정의된 ScrollViewer(ItemsPanel)에 직접 이벤트 연결
            ItemsPanel.ScrollChanged += ScrollViewer_ScrollChanged;

            // 이미지 로드
            LoadImages();
        }

        // 스크롤 이벤트 핸들러 추가
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // 화면에 보이는 아이템 로드
                LoadVisibleImages(scrollViewer);
            }
        }
        // 화면에 보이는 이미지 로드
        private async void LoadVisibleImages(ScrollViewer scrollViewer)
        {
            try
            {
                double viewportTop = scrollViewer.VerticalOffset;
                double viewportBottom = viewportTop + scrollViewer.ViewportHeight;

                // ScrollViewer 내에 있는 아이템 확인
                for (int i = 0; i < _images.Count; i++)
                {
                    var imageItem = _images[i];

                    if (!imageItem.IsLoaded && !imageItem.IsLoading)
                    {
                        // 아이템의 위치 계산 (근사치)
                        double itemTop = (i / (int)(scrollViewer.ViewportWidth / 180)) * 180;
                        double itemBottom = itemTop + 180;

                        // 화면에 보이는지 확인
                        if ((itemTop >= viewportTop && itemTop <= viewportBottom) ||
                            (itemBottom >= viewportTop && itemBottom <= viewportBottom))
                        {
                            // 이미지 로드
                            await imageItem.LoadImageAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"이미지 로드 오류: {ex.Message}");
            }
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

        // LoadImages 메서드 수정
        private async void LoadImages()
        {
            try
            {
                // 로딩 상태 표시
                LoadingIndicator.Visibility = Visibility.Visible;
                NoImagesText.Visibility = Visibility.Collapsed;
                _images.Clear();
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

                            // ImageItem 생성
                            _images.Add(new ImageItem { Url = message.Message });
                        }
                    }

                    // 이미지 갯수 표시
                    ImageCountText.Text = $"총 {_imageUrls.Count}개";

                    // 로딩 상태 숨김
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                    _loadingDotsTimer.Stop();

                    // 이미지가 없을 경우 메시지 표시
                    if (_imageUrls.Count == 0)
                    {
                        NoImagesText.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // 처음 화면에 보이는 이미지만 로드
                        await Task.Delay(100); // UI 업데이트 대기

                        if (ImageItems.ItemsPanel != null && ImageItems.ItemsPanel.FindName("ItemsPanel") is ScrollViewer sv)
                        {
                            LoadVisibleImages(sv);
                        }
                        else
                        {
                            // 첫 5개 이미지만 로드 (기본값)
                            for (int i = 0; i < Math.Min(5, _images.Count); i++)
                            {
                                await _images[i].LoadImageAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"이미지 목록을 불러오는 중 오류가 발생했습니다: {ex.Message}");
            }
        }
        // 이미지 클릭 이벤트 핸들러
        private async void Image_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem imageItem)
            {
                string imageUrl = imageItem.Url;

                // 페이드 아웃 애니메이션
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0.7, TimeSpan.FromSeconds(0.2));

                fadeOut.Completed += (s2, e2) =>
                {
                    var viewer = new ImageViewerWindow(imageUrl);
                    viewer.Owner = this;

                    // 갤러리 모드 설정 - 모든 이미지 URL 전달
                    viewer.Loaded += (s3, e3) => viewer.SetGalleryMode(_imageUrls);

                    viewer.ShowDialog();

                    // 페이드 인 애니메이션
                    DoubleAnimation fadeIn = new DoubleAnimation(0.7, 1, TimeSpan.FromSeconds(0.2));
                    this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };

                this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }
        private bool IsImageUrl(string url)
        {
            string extension = IOPath.GetExtension(url).ToLower(); // 명시적으로 IOPath 사용
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                   extension == ".bmp" || extension == ".webp" || extension == ".gif";
        }

        private void ShowError(string message)
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            _loadingDotsTimer.Stop();
            MessageBox.Show(message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
        // 창이 닫힐 때 명시적으로 리소스 정리
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 이미지 리소스 해제
            foreach (var imageItem in _images)
            {
                imageItem.Image = null;
            }

            _images.Clear();

            // GC 힌트
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // 새로고침 시 메모리 정리
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // 기존 이미지 리소스 해제
            foreach (var imageItem in _images)
            {
                imageItem.Image = null;
            }

            _images.Clear();

            // GC 힌트
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 이미지 다시 로드
            LoadImages();
        }
    }
}