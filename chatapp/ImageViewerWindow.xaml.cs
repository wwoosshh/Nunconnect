using System;
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

        public ImageViewerWindow(string imageUrl)
        {
            InitializeComponent();
            _imageUrl = imageUrl;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 이미지 로드
            LoadImage();
        }

        private async void LoadImage()
        {
            try
            {
                // 이미지 로더 설정
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_imageUrl);
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
            try
            {
                // 다운로드 버튼 비활성화
                DownloadButton.IsEnabled = false;
                DownloadButton.Content = "다운로드 중...";

                // 이미지 URL에서 파일명 추출
                string fileName = Path.GetFileName(new Uri(_imageUrl).LocalPath);

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

                // 다운로드 버튼 복원
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "다운로드";
            }
            catch (Exception ex)
            {
                // 다운로드 버튼 복원
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "다운로드";

                MessageBox.Show($"이미지 다운로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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