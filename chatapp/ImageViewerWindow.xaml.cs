using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace chatapp
{
    public partial class ImageViewerWindow : Window
    {
        private readonly string _imageUrl;

        // 갤러리 모드를 위한 속성 추가
        private List<string> _galleryImages = new List<string>();
        private int _currentImageIndex = 0;
        private bool _isGalleryMode = false;
        public string CurrentImageUrl { get { return _imageUrl; } }

        public ImageViewerWindow(string imageUrl)
        {
            InitializeComponent();
            _imageUrl = imageUrl;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 이미지 로드
            LoadImage(_imageUrl);

            // 갤러리 버튼 초기 상태 설정
            UpdateGalleryButtonsState();
        }
        // 갤러리 모드 설정 메서드 추가
        public void SetGalleryMode(List<string> imageUrls)
        {
            if (imageUrls == null || imageUrls.Count == 0)
                return;

            _galleryImages = new List<string>(imageUrls);
            _currentImageIndex = _galleryImages.IndexOf(_imageUrl);

            if (_currentImageIndex < 0) _currentImageIndex = 0;

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
            if (PrevButton != null) PrevButton.Visibility = Visibility.Visible;
            if (NextButton != null) NextButton.Visibility = Visibility.Visible;
            if (ImageCountText != null)
            {
                ImageCountText.Visibility = Visibility.Visible;
                UpdateImageCounter();
            }

            // 만약 XAML에 요소가 없다면 여기서 동적으로 생성할 수 있음
            // 이 예제에서는 XAML에 이미 요소가 있다고 가정
        }
        // 이미지 카운터 업데이트
        private void UpdateImageCounter()
        {
            if (!_isGalleryMode || ImageCountText == null) return;

            ImageCountText.Text = $"{_currentImageIndex + 1} / {_galleryImages.Count}";
        }
        // 갤러리 버튼 상태 업데이트
        private void UpdateGalleryButtonsState()
        {
            if (!_isGalleryMode)
            {
                // 갤러리 모드가 아니면 버튼 숨기기
                if (PrevButton != null) PrevButton.Visibility = Visibility.Collapsed;
                if (NextButton != null) NextButton.Visibility = Visibility.Collapsed;
                if (ImageCountText != null) ImageCountText.Visibility = Visibility.Collapsed;
                return;
            }

            // 첫 번째 이미지일 때 이전 버튼 비활성화
            if (PrevButton != null)
                PrevButton.IsEnabled = _currentImageIndex > 0;

            // 마지막 이미지일 때 다음 버튼 비활성화
            if (NextButton != null)
                NextButton.IsEnabled = _currentImageIndex < _galleryImages.Count - 1;

            // 이미지 카운터 업데이트
            UpdateImageCounter();
        }
        private async void LoadImage(string imageUrl)
        {
            try
            {
                // 이미지 로더 설정
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imageUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // 이미지 설정
                ImageViewer.Source = bitmap;

                // 이미지 정보 업데이트
                UpdateImageInfo();

                // 로딩 표시자 숨기기
                LoadingIndicator.Visibility = Visibility.Collapsed;

                // 이미지 페이드인 애니메이션
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                ImageViewer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            catch (Exception ex)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                MessageBox.Show($"이미지 로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 이전 이미지 버튼 클릭 핸들러
        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isGalleryMode || _currentImageIndex <= 0) return;

            _currentImageIndex--;
            LoadImage(_galleryImages[_currentImageIndex]);
            UpdateGalleryButtonsState();
        }

        // 다음 이미지 버튼 클릭 핸들러
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isGalleryMode || _currentImageIndex >= _galleryImages.Count - 1) return;

            _currentImageIndex++;
            LoadImage(_galleryImages[_currentImageIndex]);
            UpdateGalleryButtonsState();
        }

        // 키보드 이벤트 추가 (왼쪽/오른쪽 화살표로 이미지 이동)
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isGalleryMode) return;

            if (e.Key == Key.Left)
            {
                PrevButton_Click(sender, e);
            }
            else if (e.Key == Key.Right)
            {
                NextButton_Click(sender, e);
            }
        }
        private void ImageViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ctrl 키가 눌려있는지 확인
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;

                if (e.Delta > 0)
                {
                    // 확대
                    ZoomInButton_Click(sender, e);
                }
                else
                {
                    // 축소
                    ZoomOutButton_Click(sender, e);
                }
            }
        }
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // 다운로드 버튼 비활성화
            DownloadButton.IsEnabled = false;
            DownloadButton.Content = "다운로드 중...";

            try
            {
                // 갤러리 모드에서는 현재 표시중인 이미지를 다운로드
                string imageUrlToDownload = _isGalleryMode ? _galleryImages[_currentImageIndex] : _imageUrl;

                // 이미지 URL에서 파일명 추출
                string fileName = Path.GetFileName(new Uri(imageUrlToDownload).LocalPath);

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*webp",
                    DefaultExt = Path.GetExtension(fileName)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string serverUrl = AppSettings.GetServerUrl();
                    string apiUrl = $"{serverUrl}/api/File/download?fileName={fileName}";

                    using (var client = new HttpClient())
                    {
                        try
                        {
                            var response = await client.GetAsync(apiUrl);

                            if (response.IsSuccessStatusCode)
                            {
                                byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                                File.WriteAllBytes(saveDialog.FileName, fileBytes);
                                MessageBox.Show("이미지가 성공적으로 다운로드되었습니다.", "다운로드 완료", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"이미지 다운로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 다운로드 버튼 복원
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "다운로드";
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

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            ImageScale.ScaleX += 0.1;
            ImageScale.ScaleY += 0.1;
            UpdateImageInfo();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageScale.ScaleX > 0.2)
            {
                ImageScale.ScaleX -= 0.1;
                ImageScale.ScaleY -= 0.1;
                UpdateImageInfo();
            }
        }

        private void UpdateImageInfo()
        {
            if (ImageViewer.Source is BitmapImage image)
            {
                ImageInfoText.Text = $"{(int)(image.Width * ImageScale.ScaleX)} x {(int)(image.Height * ImageScale.ScaleY)} ({Math.Round(ImageScale.ScaleX * 50)}%)";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}