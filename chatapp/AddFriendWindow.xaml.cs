using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class AddFriendWindow : Window
    {
        private readonly UserData _currentUser;

        public AddFriendWindow(UserData currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 페이드 인 애니메이션 시작
            var storyboard = (Storyboard)FindResource("FadeInStoryboard");
            storyboard.Begin(this);

            // 포커스 설정
            FriendIndexInput.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void RequestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(FriendIndexInput.Text.Trim(), out int targetIndex))
            {
                MessageBox.Show("유효한 Index 번호를 입력하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                FriendIndexInput.Focus();
                return;
            }

            // 자기 자신에게 요청하는지 확인
            if (targetIndex == _currentUser.Index)
            {
                MessageBox.Show("자기 자신에게는 친구 요청을 보낼 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 로딩 인디케이터 표시
                LoadingIndicator.Visibility = Visibility.Visible;

                // HttpClient 사용
                HttpClient client = ApiClient.GetClient();

                // 1. 서버에 유저 존재 여부 확인
                var userCheckResponse = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/getUserNameByIndex?index={targetIndex}");
                if (!userCheckResponse.IsSuccessStatusCode)
                {
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                    MessageBox.Show($"Index {targetIndex}에 해당하는 유저가 존재하지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 친구 이름 가져오기
                string friendName = await userCheckResponse.Content.ReadAsStringAsync();

                // 2. 중복 요청 검증
                var duplicateCheck = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/Friend/isRequestDuplicate?hostIndex={_currentUser.Index}&targetIndex={targetIndex}");
                if (await duplicateCheck.Content.ReadAsStringAsync() == "true")
                {
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                    MessageBox.Show("이미 친구 요청을 보낸 상태입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 3. 이미 친구인지 확인
                var friendCheck = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/Friend/isAlreadyFriend?hostIndex={_currentUser.Index}&targetIndex={targetIndex}");
                if (await friendCheck.Content.ReadAsStringAsync() == "true")
                {
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                    MessageBox.Show("이미 친구인 상태입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 4. 친구 요청 API 호출
                string url = $"{AppSettings.GetServerUrl()}/api/Friend/add";
                var request = new FriendRequest
                {
                    HostIndex = _currentUser.Index,
                    GetIndex = targetIndex,
                    Action = "Waiting"
                };

                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                LoadingIndicator.Visibility = Visibility.Collapsed;

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"{friendName}님에게 친구 요청을 보냈습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
                else
                {
                    MessageBox.Show($"요청 실패: {await response.Content.ReadAsStringAsync()}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                MessageBox.Show($"에러: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public class FriendRequest
        {
            public int HostIndex { get; set; }
            public int GetIndex { get; set; }
            public string Action { get; set; } = string.Empty;
        }
    }
}