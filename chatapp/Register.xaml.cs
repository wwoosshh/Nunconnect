using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class Register : Window
    {
        private bool _userRegistered = false;
        private string _registeredEmail = "";
        // 마지막 회원가입 요청 시간을 추적하기 위한 클래스 변수 추가
        private DateTime _lastRegistrationAttempt = DateTime.MinValue;
        private const int REGISTRATION_COOLDOWN_SECONDS = 60; // 1분 제한

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
        private bool ValidateInputs()
        {
            bool isValid = true;

            // 회원가입 요청 시간 제한 확인
            TimeSpan timeSinceLastAttempt = DateTime.Now - _lastRegistrationAttempt;
            if (timeSinceLastAttempt.TotalSeconds < REGISTRATION_COOLDOWN_SECONDS)
            {
                int remainingSeconds = REGISTRATION_COOLDOWN_SECONDS - (int)timeSinceLastAttempt.TotalSeconds;
                MessageBox.Show($"너무 빈번한 요청입니다. {remainingSeconds}초 후에 다시 시도해주세요.");
                return false;
            }

            // --- ID 검증 ---
            // 1. 길이 검증
            if (IdTextBox.Text.Length < 4 || IdTextBox.Text.Length > 20)
            {
                MessageBox.Show("아이디는 4~20자 사이어야 합니다.");
                return false;
            }

            // 2. 영문자와 숫자만 허용 (정규식 패턴)
            if (!System.Text.RegularExpressions.Regex.IsMatch(IdTextBox.Text, @"^[a-zA-Z0-9]+$"))
            {
                MessageBox.Show("아이디는 영문자와 숫자만 사용 가능합니다.");
                return false;
            }

            // 3. 적어도 하나의 영문자 포함 확인
            if (!System.Text.RegularExpressions.Regex.IsMatch(IdTextBox.Text, @"[a-zA-Z]"))
            {
                MessageBox.Show("아이디에는 최소 하나의 영문자가 포함되어야 합니다.");
                return false;
            }

            // --- 비밀번호 검증 ---
            // 1. 길이 검증
            if (PasswordBox.Password.Length < 8 || PasswordBox.Password.Length > 30)
            {
                MessageBox.Show("비밀번호는 8~30자 사이여야 합니다.");
                return false;
            }

            // 2. 영문자와 숫자만 허용 (정규식 패턴)
            if (!System.Text.RegularExpressions.Regex.IsMatch(PasswordBox.Password, @"^[a-zA-Z0-9]+$"))
            {
                MessageBox.Show("비밀번호는 영문자와 숫자만 사용 가능합니다.");
                return false;
            }

            // 3. 영문자와 숫자 조합 확인
            if (!System.Text.RegularExpressions.Regex.IsMatch(PasswordBox.Password, @"[a-zA-Z]") ||
                !System.Text.RegularExpressions.Regex.IsMatch(PasswordBox.Password, @"[0-9]"))
            {
                MessageBox.Show("비밀번호는 영문자와 숫자를 모두 포함해야 합니다.");
                return false;
            }

            // --- 닉네임 검증 ---
            // 1. 길이 검증
            if (NicknameTextBox.Text.Length < 2 || NicknameTextBox.Text.Length > 20)
            {
                MessageBox.Show("닉네임은 2~20자 사이여야 합니다.");
                return false;
            }

            // 2. XSS 방지를 위한 안전한 문자만 허용
            if (ContainsUnsafeCharacters(NicknameTextBox.Text))
            {
                MessageBox.Show("닉네임에 사용할 수 없는 특수문자가 포함되어 있습니다.");
                return false;
            }

            // --- 이메일 검증 ---
            // 1. 기본 이메일 형식 검증
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                EmailTextBox.Text, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                MessageBox.Show("유효한 이메일 주소를 입력하세요.");
                return false;
            }

            // 2. 이메일 길이 검증
            if (EmailTextBox.Text.Length > 100)
            {
                MessageBox.Show("이메일 주소가 너무 깁니다. (최대 100자)");
                return false;
            }

            return true;
        }

        // 안전하지 않은 문자를 포함하는지 확인하는 도우미 함수
        private bool ContainsUnsafeCharacters(string input)
        {
            // SQL 인젝션이나 XSS 공격에 사용될 수 있는 문자 패턴
            string[] dangerousPatterns = {
        "<script", "javascript:", "SELECT", "INSERT", "UPDATE", "DELETE", "DROP",
        "--", "=", "<", ">", "{", "}", "\\", "\'", "\"", ";"
    };

            foreach (var pattern in dangerousPatterns)
            {
                if (input.ToUpper().Contains(pattern.ToUpper()))
                    return true;
            }

            return false;
        }

        // 입력 문자열을 안전하게 처리하는 메서드
        private string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // HTML 태그 제거
            input = System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);

            // SQL 인젝션 방지용 문자 처리
            input = input.Replace("'", "''");
            input = input.Replace("--", "");
            input = input.Replace(";", "");
            input = input.Replace("/*", "");
            input = input.Replace("*/", "");

            return input;
        }

        // 등록 버튼 클릭 이벤트 수정
        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            // 입력 유효성 검증
            if (!ValidateInputs())
                return;

            // 현재 시간 기록
            _lastRegistrationAttempt = DateTime.Now;

            // 안전한 입력 준비
            string id = SanitizeInput(IdTextBox.Text.Trim());
            string pw = PasswordBox.Password.Trim(); // 비밀번호는 해싱되므로 별도 처리 필요 없음
            string nickname = SanitizeInput(NicknameTextBox.Text.Trim());
            string email = SanitizeInput(EmailTextBox.Text.Trim());

            string hashedPw = HashPassword(pw);

            // 서버에 전송할 객체 생성
            var newUser = new
            {
                Id = id,
                Password = hashedPw,
                Name = nickname,
                Email = email
            };

            try
            {
                // 로딩 표시
                LoadingIndicator.Visibility = Visibility.Visible;
                LoadingText.Text = "회원가입 처리 중...";

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
                    string errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"회원가입 실패: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류 발생: {ex.Message}");
            }
            finally
            {
                // 로딩 표시 해제
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 헤더 애니메이션
            var headerStoryboard = new Storyboard();
            var headerAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(headerAnimation, HeaderPanel);
            Storyboard.SetTargetProperty(headerAnimation, new PropertyPath("Opacity"));
            headerStoryboard.Children.Add(headerAnimation);
            headerStoryboard.Begin();

            // 등록 패널 애니메이션 (약간 지연)
            var registerStoryboard = new Storyboard();
            var registerAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5),
                BeginTime = TimeSpan.FromSeconds(0.2),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(registerAnimation, RegisterPanel);
            Storyboard.SetTargetProperty(registerAnimation, new PropertyPath("Opacity"));
            registerStoryboard.Children.Add(registerAnimation);
            registerStoryboard.Begin();

            // 버튼 패널 애니메이션 (더 지연)
            var buttonsStoryboard = new Storyboard();
            var buttonsAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5),
                BeginTime = TimeSpan.FromSeconds(0.4),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(buttonsAnimation, ButtonsPanel);
            Storyboard.SetTargetProperty(buttonsAnimation, new PropertyPath("Opacity"));
            buttonsStoryboard.Children.Add(buttonsAnimation);
            buttonsStoryboard.Begin();
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            fadeOut.Completed += (s, args) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
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
