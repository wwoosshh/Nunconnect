using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
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

            // 생성자에서는 이미지 로딩하지 않고 Loaded 이벤트에서 처리
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 이미지 로드
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_imageUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 이미지 로딩 캐시 옵션 설정
                bitmap.EndInit();

                ImageViewer.Source = bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 이미지 URL에서 파일명 추출
                string fileName = System.IO.Path.GetFileName(new Uri(_imageUrl).LocalPath);

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp",
                    DefaultExt = System.IO.Path.GetExtension(fileName)
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
                                System.IO.File.WriteAllBytes(saveDialog.FileName, fileBytes);
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
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}