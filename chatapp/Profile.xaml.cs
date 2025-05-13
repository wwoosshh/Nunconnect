using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using static chatapp.MainWindow;
using System.Xml;
using System.IO;
using Newtonsoft.Json;
using System.Windows.Shapes;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace chatapp
{
    public partial class Profile : Window
    {
        private UserData _user;
        private string _userFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "users.txt");

        public Profile(UserData user)
        {
            InitializeComponent();
            _user = user;

            // UI 바인딩
            UserNameBox.Text = _user.Name;
            StatusMessageBox.Text = _user.StatusMessage;

            if (!string.IsNullOrEmpty(_user.ProfileImage) && File.Exists(_user.ProfileImage))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(_user.ProfileImage));
                    ProfileImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"프로필 이미지 로드 실패: {ex.Message}");
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 프로필 콘텐츠 페이드인
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProfileContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
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

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            _user.Name = UserNameBox.Text;
            _user.StatusMessage = StatusMessageBox.Text;

            try
            {
                // 저장 중 로딩 표시
                ShowLoadingIndicator(true);

                string serverUrl = AppSettings.GetServerUrl();
                string apiUrl = $"{serverUrl}/api/User/update";

                using HttpClient client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Patch, apiUrl)
                {
                    Content = JsonContent.Create(_user)
                };

                var response = await client.SendAsync(request);

                // 로딩 표시 숨기기
                ShowLoadingIndicator(false);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("✅ 프로필이 성공적으로 저장되었습니다.");

                    // 애니메이션과 함께 창 닫기
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.2)
                    };
                    fadeOut.Completed += (s, args) => this.Close();
                    this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"❌ 서버 응답 실패: {response.StatusCode}\n{error}");
                }
            }
            catch (Exception ex)
            {
                ShowLoadingIndicator(false);
                MessageBox.Show($"❗ 오류 발생: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 애니메이션과 함께 창 닫기
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            fadeOut.Completed += (s, args) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void ChangePhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 이미지 로드 및 적용
                    _user.ProfileImage = openFileDialog.FileName;
                    ProfileImage.Source = new BitmapImage(new Uri(_user.ProfileImage));

                    // 이미지 변경 효과 (작은 애니메이션)
                    var scaleUp = new DoubleAnimation(0.9, 1.0, TimeSpan.FromSeconds(0.2));
                    ProfileImage.RenderTransform = new ScaleTransform(1, 1);
                    ProfileImage.RenderTransformOrigin = new Point(0.5, 0.5);
                    ((ScaleTransform)ProfileImage.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleUp);
                    ((ScaleTransform)ProfileImage.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleUp);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"이미지 로드 중 오류 발생: {ex.Message}");
                }
            }
        }

        private void ShowLoadingIndicator(bool show)
        {
            LoadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            if (show)
            {
                // 페이드인 효과
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
                LoadingIndicator.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }
    }
}