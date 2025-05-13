using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Collections.Generic;

namespace chatapp
{
    public partial class MainWindow : Window
    {
        private const string CurrentVersion = "1.4.0";

        public MainWindow()
        {
            InitializeComponent();
            IdTextBox.TextChanged += IdTextBox_TextChanged;
            PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 애니메이션 시작
            var logoStoryboard = (Storyboard)FindResource("LogoFadeInStoryboard");
            logoStoryboard.Begin();

            var loginFormStoryboard = (Storyboard)FindResource("LoginFormFadeInStoryboard");
            loginFormStoryboard.Begin();

            // 버전 검사
            CheckAppVersion();

            // 포커스 설정
            IdTextBox.Focus();
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
            Application.Current.Shutdown();
        }

        private async void CheckAppVersion()
        {
            try
            {
                using HttpClient client = new HttpClient();
                string apiUrl = AppSettings.GetServerUrl();
                var response = await client.GetAsync($"{apiUrl}/api/User/checkVersion");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var versionData = JsonConvert.DeserializeObject<VersionResponse>(json);

                    if (versionData != null && versionData.Version != CurrentVersion)
                    {
                        MessageBox.Show(
                            $"현재 사용 중인 버전({CurrentVersion})은 구버전입니다.\n" +
                            $"최신 버전({versionData.Version})으로 업데이트 해주세요.",
                            "업데이트 필요", MessageBoxButton.OK, MessageBoxImage.Warning
                        );

                        // 홈페이지 열고 프로그램 종료
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://nunconnect.netlify.app/",
                            UseShellExecute = true
                        });

                        Application.Current.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("버전 확인 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string id = IdTextBox.Text.Trim();
            string pw = PasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            {
                MessageBox.Show("아이디와 비밀번호를 입력하세요.", "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 로그인 버튼 비활성화 및 로딩 표시
            LoginButton.IsEnabled = false;
            LoadingIndicator.Visibility = Visibility.Visible;

            var user = await ValidateCredentialsFromServer(id, pw);

            // 로딩 숨김 및 버튼 다시 활성화
            LoadingIndicator.Visibility = Visibility.Collapsed;
            LoginButton.IsEnabled = true;

            if (user != null)
            {
                ChatList chatWindow = new ChatList(user);
                chatWindow.Show();
                this.Close();
            }
        }

        private async Task<UserData> ValidateCredentialsFromServer(string id, string pw)
        {
            string hashedPw = HashPassword(pw);

            try
            {
                using HttpClient client = new HttpClient();
                var requestData = new { Id = id, Password = hashedPw };
                string apiUrl = AppSettings.GetServerUrl();

                var response = await client.PostAsJsonAsync($"{apiUrl}/api/User/login", requestData);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<UserData>(jsonString);
                    return user;
                }
                else
                {
                    // 서버에서 오는 에러 메시지 표시
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(errorMessage))
                    {
                        errorMessage = "아이디 또는 비밀번호가 잘못되었습니다.";
                    }

                    MessageBox.Show(errorMessage, "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서버 연결 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private string HashPassword(string password)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private void SignupButton_Click(object sender, RoutedEventArgs e)
        {
            Register registerWindow = new Register();
            registerWindow.ShowDialog();
        }

        private void IdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            IdHint.Visibility = string.IsNullOrEmpty(IdTextBox.Text)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, new RoutedEventArgs());
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordHint.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        public class UserData
        {
            public int Index { get; set; }
            public string Id { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string ProfileImage { get; set; } = string.Empty;
            public string StatusMessage { get; set; } = string.Empty;
            public List<string> JoinedRoomIds { get; set; } = new();
        }

        public class VersionResponse
        {
            public string Version { get; set; } = string.Empty;
        }
    }
}