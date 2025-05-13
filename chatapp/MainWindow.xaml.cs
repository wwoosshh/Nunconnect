using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using System.Net.Http.Json;
using static System.Net.WebRequestMethods;
using System.Windows.Input;
using System.Text;
using System.Security.Cryptography;

namespace chatapp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            IdTextBox.TextChanged += IdTextBox_TextChanged;
            PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;

            // ✅ 실행 시 버전 검사
            CheckAppVersion();
        }
        private const string CurrentVersion = "1.4.0"; // ✅ 클라이언트 현재 버전 명시
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
                            FileName = "https://nunconnect.netlify.app/", // ✅ 홈페이지 주소
                            UseShellExecute = true
                        });

                        Application.Current.Shutdown(); // 프로그램 종료
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("버전 확인 실패: " + ex.Message);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string id = IdTextBox.Text.Trim();
            string pw = PasswordBox.Password.Trim();

            var user = await ValidateCredentialsFromServer(id, pw);

            if (user != null)
            {
                ChatList chatWindow = new ChatList(user);
                chatWindow.Show();
                this.Close();
            }
        }
        private string GetServerUrl()
        {
            bool isServerPc = true;
            return isServerPc ? "http://localhost:5159" : "http://nunconnect.duckdns.org:5159";
        }

        private async Task<UserData?> ValidateCredentialsFromServer(string id, string pw)
        {
            string hashedPw = HashPassword(pw); // 로그인 시에도 해싱 후 비교

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
                    MessageBox.Show("로그인 실패: 아이디 또는 비밀번호가 잘못되었습니다.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message);
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
            public string Email { get; set; }
            public string ProfileImage { get; set; } = string.Empty;

            public string StatusMessage { get; set; } = string.Empty;
            public List<string> JoinedRoomIds { get; set; } = new();

        }
        public class RoomInfo
        {
            // 채팅방 이름
            public string RoomName { get; set; } = string.Empty;

            // 고유 채팅방 ID (16자리 문자열)
            public string RoomId { get; set; } = string.Empty;

            // 생성 시각
            public DateTime CreatedAt { get; set; }

            // 마지막 채팅 시각 (선택적 사용)
            public DateTime LastActive { get; set; }
        }
        public class VersionResponse
        {
            public string Version { get; set; } = string.Empty;
        }
    }
}
