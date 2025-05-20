using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Net.Http.Json;

namespace chatapp
{
    public partial class ResetPassword : Window
    {
        public ResetPassword()
        {
            InitializeComponent();

            // 힌트 텍스트 이벤트 핸들러 등록
            EmailForIdTextBox.TextChanged += (s, e) => {
                EmailForIdHint.Visibility = string.IsNullOrEmpty(EmailForIdTextBox.Text) ? Visibility.Visible : Visibility.Hidden;
            };

            IdForPasswordTextBox.TextChanged += (s, e) => {
                IdForPasswordHint.Visibility = string.IsNullOrEmpty(IdForPasswordTextBox.Text) ? Visibility.Visible : Visibility.Hidden;
            };

            EmailForPasswordTextBox.TextChanged += (s, e) => {
                EmailForPasswordHint.Visibility = string.IsNullOrEmpty(EmailForPasswordTextBox.Text) ? Visibility.Visible : Visibility.Hidden;
            };
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
            this.Close();
        }

        private async void FindIdButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailForIdTextBox.Text.Trim();

            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("이메일 주소를 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 로딩 시작
                LoadingIndicator.Visibility = Visibility.Visible;
                LoadingText.Text = "아이디 찾는 중...";

                // 서버에 아이디 찾기 요청
                var result = await FindIdByEmail(email);

                // 결과 표시
                IdResultPanel.Visibility = Visibility.Visible;
                if (result.Success)
                {
                    IdResultText.Text = $"회원님의 아이디: {result.Id}\n인증 메일이 해당 이메일 주소로 발송되었습니다. 확인해 주세요.";
                }
                else
                {
                    IdResultText.Text = result.Message;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"아이디 찾기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                IdResultPanel.Visibility = Visibility.Collapsed;
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        // ResetPasswordButton_Click 메서드 수정
        private async void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            string id = IdForPasswordTextBox.Text.Trim();
            string email = EmailForPasswordTextBox.Text.Trim();

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(email))
            {
                MessageBox.Show("아이디와 이메일 주소를 모두 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 로딩 시작
                LoadingIndicator.Visibility = Visibility.Visible;
                LoadingText.Text = "비밀번호 재설정 요청 중...";

                // 서버에 비밀번호 재설정 요청
                var result = await RequestPasswordReset(id, email);

                // 결과 표시
                PasswordResultPanel.Visibility = Visibility.Visible;
                if (result.Success)
                {
                    PasswordResultText.Text = $"비밀번호 재설정 링크가 {email}로 전송되었습니다. 이메일을 확인하여 비밀번호를 재설정해 주세요.";
                }
                else
                {
                    PasswordResultText.Text = result.Message;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"비밀번호 재설정 요청 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                PasswordResultPanel.Visibility = Visibility.Collapsed;
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        // 비밀번호 재설정 요청 메서드 수정
        private async Task<ResetPasswordResult> RequestPasswordReset(string id, string email)
        {
            using HttpClient client = new HttpClient();
            var requestData = new { Id = id, Email = email };
            string apiUrl = AppSettings.GetServerUrl();

            var response = await client.PostAsJsonAsync($"{apiUrl}/api/User/resetPassword", requestData);

            var jsonString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<ResetPasswordResult>(jsonString) ??
                    new ResetPasswordResult { Success = false, Message = "서버 응답을 처리할 수 없습니다." };
            }
            else
            {
                var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(jsonString);
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = errorResponse?.Message ?? "계정 정보를 확인할 수 없습니다."
                };
            }
        }
        // 이메일로 아이디 찾기 API 호출
        private async Task<FindIdResult> FindIdByEmail(string email)
        {
            using HttpClient client = new HttpClient();
            var requestData = new { Email = email };
            string apiUrl = AppSettings.GetServerUrl();

            var response = await client.PostAsJsonAsync($"{apiUrl}/api/User/findId", requestData);

            var jsonString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<FindIdResult>(jsonString) ??
                    new FindIdResult { Success = false, Message = "서버 응답을 처리할 수 없습니다." };
            }
            else
            {
                var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(jsonString);
                return new FindIdResult
                {
                    Success = false,
                    Message = errorResponse?.Message ?? "등록된 이메일을 찾을 수 없습니다."
                };
            }
        }

        // 아이디와 이메일로 비밀번호 재설정 API 호출
        private async Task<ResetPasswordResult> ResetPasswordByIdAndEmail(string id, string email)
        {
            using HttpClient client = new HttpClient();
            var requestData = new { Id = id, Email = email };
            string apiUrl = AppSettings.GetServerUrl();

            var response = await client.PostAsJsonAsync($"{apiUrl}/api/User/resetPassword", requestData);

            var jsonString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<ResetPasswordResult>(jsonString) ??
                    new ResetPasswordResult { Success = false, Message = "서버 응답을 처리할 수 없습니다." };
            }
            else
            {
                var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(jsonString);
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = errorResponse?.Message ?? "계정 정보를 확인할 수 없습니다."
                };
            }
        }

        // API 응답 클래스들
        public class FindIdResult
        {
            public bool Success { get; set; }
            public string Id { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        public class ResetPasswordResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public class ErrorResponse
        {
            public string Message { get; set; } = string.Empty;
        }
    }
}