using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class Register : Window
    {
        private bool _userRegistered = false;
        private string _registeredEmail = "";

        public Register()
        {
            InitializeComponent();
        }
        private string HashPassword(string password)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string id = IdTextBox.Text.Trim();
            string pw = PasswordBox.Password.Trim();
            string nickname = NicknameTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw) ||
                string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(email))
            {
                MessageBox.Show("모든 항목을 입력하세요.");
                return;
            }

            string hashedPw = HashPassword(pw);

            var newUser = new
            {
                Id = id,
                Password = hashedPw,
                Name = nickname,
                Email = email
            };

            using HttpClient client = new();
            var content = new StringContent(JsonConvert.SerializeObject(newUser), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{AppSettings.GetServerUrl()}/api/User/register", content);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("회원가입이 완료되었습니다. 인증 메일을 보내세요.");
                _userRegistered = true;
                _registeredEmail = email;

                SendVerificationButton.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show($"회원가입 실패: {await response.Content.ReadAsStringAsync()}");
            }
        }

        private async void SendVerificationButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_userRegistered || string.IsNullOrEmpty(_registeredEmail))
            {
                MessageBox.Show("먼저 회원가입을 완료하세요.");
                return;
            }

            try
            {
                using HttpClient client = new();
                var emailData = new { Email = _registeredEmail };
                var content = new StringContent(JsonConvert.SerializeObject(emailData), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{AppSettings.GetServerUrl()}/api/User/resendVerification", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("인증 메일을 발송했습니다. 인증을 완료 후 로그인하세요.");
                    // 로그인 버튼 활성화
                    LoginButton.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show($"인증 메일 발송 실패: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}");
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string id = IdTextBox.Text.Trim();
                string pw = PasswordBox.Password.Trim();

                using HttpClient client = new();
                var emailCheck = new { Email = _registeredEmail };
                var content = new StringContent(JsonConvert.SerializeObject(emailCheck), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{AppSettings.GetServerUrl()}/api/User/checkEmailConfirmed", content);
                var user = await ValidateCredentialsFromServer(id, pw);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("이메일 인증이 완료되었습니다. 로그인합니다.");
                    ChatList chatWindow = new ChatList(user); // ✅ 로그인 후 이동할 창
                    chatWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("이메일 인증이 완료되지 않았습니다. 이메일을 확인하세요.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}");
            }
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
    }
}
