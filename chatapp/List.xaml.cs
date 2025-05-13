using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using static chatapp.MainWindow;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace chatapp
{
    public partial class List : Window
    {
        private UserData _currentUser;

        public List(UserData user)
        {
            InitializeComponent();
            _currentUser = user;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 로딩 상태 표시
            LoadingText.Visibility = Visibility.Visible;

            // 채팅방 목록 로드
            LoadUserJoinedRooms();
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

        private void ToggleSidePanel_Click(object sender, RoutedEventArgs e)
        {
            SidePanel.Visibility = Visibility.Visible;
            var slideIn = (Storyboard)FindResource("SlideInStoryboard");
            slideIn.Begin();
        }

        private void CloseSidePanel_Click(object sender, RoutedEventArgs e)
        {
            var slideOut = (Storyboard)FindResource("SlideOutStoryboard");
            slideOut.Completed += (s, _) => SidePanel.Visibility = Visibility.Collapsed;
            slideOut.Begin();
        }

        private void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            // 다른 패널 닫기
            JoinRoomPanel.Visibility = Visibility.Collapsed;
            DeleteRoomPanel.Visibility = Visibility.Collapsed;

            // 패널 표시
            CreateRoomPanel.Visibility = Visibility.Visible;
            CreateRoomPanel.Opacity = 0;
            CreateRoomPanel.Margin = new Thickness(0, 20, 0, 0);

            // 페이드 인 애니메이션 시작
            var storyboard = (Storyboard)FindResource("PanelFadeInStoryboard");
            storyboard.Begin(CreateRoomPanel);
        }

        private void CancelCreateRoom_Click(object sender, RoutedEventArgs e)
        {
            CreateRoomPanel.Visibility = Visibility.Collapsed;
            RoomNameInput.Text = "";
            RoomPasswordInput.Password = "";
            PrivateRoomCheckbox.IsChecked = false;
        }

        private async void ConfirmCreateRoom_Click(object sender, RoutedEventArgs e)
        {
            string roomName = RoomNameInput.Text.Trim();
            string password = RoomPasswordInput.Password.Trim();
            string roomId = GenerateRoomId();
            bool isPrivate = PrivateRoomCheckbox.IsChecked ?? false;

            if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("채팅방 이름과 비밀번호를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string apiUrl = AppSettings.GetServerUrl();

            var request = new
            {
                RoomName = roomName,
                Password = password,
                RoomId = roomId,
                UserId = _currentUser.Id,
                IsPrivate = isPrivate
            };

            try
            {
                using HttpClient client = new HttpClient();
                var response = await client.PostAsJsonAsync($"{apiUrl}/api/User/createRoom", request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeAnonymousType(json, new { RoomId = "" });

                    MessageBox.Show("채팅방 생성 완료!", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    Message roomWindow = new Message(_currentUser, roomId);
                    roomWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("채팅방 생성 실패: " + await response.Content.ReadAsStringAsync(), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void JoinRoom_Click(object sender, RoutedEventArgs e)
        {
            // 다른 패널 닫기
            CreateRoomPanel.Visibility = Visibility.Collapsed;
            DeleteRoomPanel.Visibility = Visibility.Collapsed;

            // 패널 표시
            JoinRoomPanel.Visibility = Visibility.Visible;
            JoinRoomPanel.Opacity = 0;
            JoinRoomPanel.Margin = new Thickness(0, 20, 0, 0);

            // 페이드 인 애니메이션 시작
            var storyboard = (Storyboard)FindResource("PanelFadeInStoryboard");
            storyboard.Begin(JoinRoomPanel);
        }

        private void CancelJoinRoom_Click(object sender, RoutedEventArgs e)
        {
            JoinRoomPanel.Visibility = Visibility.Collapsed;
            JoinRoomNameInput.Text = "";
            JoinRoomPasswordInput.Password = "";
        }

        private async void ConfirmJoinRoom_Click(object sender, RoutedEventArgs e)
        {
            string roomName = JoinRoomNameInput.Text.Trim();
            string password = JoinRoomPasswordInput.Password.Trim();

            if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("모든 정보를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using HttpClient client = new HttpClient();
                string baseUrl = AppSettings.GetServerUrl();

                // 먼저 채팅방 목록을 가져와서 해당 방이 private인지 확인
                var chatListResponse = await client.GetAsync($"{baseUrl}/api/User/getChatList");
                if (chatListResponse.IsSuccessStatusCode)
                {
                    var chatListJson = await chatListResponse.Content.ReadAsStringAsync();
                    var allRooms = JsonConvert.DeserializeObject<List<RoomInfo>>(chatListJson);

                    // 입력한 이름과 비밀번호로 방을 찾음
                    var room = allRooms?.FirstOrDefault(r =>
                        r.RoomName == roomName && r.Password == password);

                    // 1대1 채팅방 확인 (이름 형식으로 판단)
                    if (room != null && room.RoomName.Contains("님과") && room.RoomName.Contains("님의 대화"))
                    {
                        // 내 이름이 채팅방 이름에 포함되어 있는지 확인
                        if (!room.RoomName.Contains(_currentUser.Name))
                        {
                            MessageBox.Show("1대1 채팅방은 참여자만 입장할 수 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                }

                // 서버에 채팅방 입장 요청 보내기
                var requestData = new
                {
                    RoomName = roomName,
                    Password = password,
                    UserId = _currentUser.Id
                };

                var response = await client.PostAsJsonAsync($"{baseUrl}/api/User/joinRoom", requestData);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeAnonymousType(json, new { RoomId = "" });

                    if (result != null && !string.IsNullOrEmpty(result.RoomId))
                    {
                        Message msgWindow = new Message(_currentUser, result.RoomId);
                        msgWindow.Show();
                        this.Close();
                    }
                }
                else
                {
                    MessageBox.Show("채팅방 입장 실패: " + await response.Content.ReadAsStringAsync(), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadUserJoinedRooms()
        {
            try
            {
                using HttpClient client = new HttpClient();
                string baseUrl = AppSettings.GetServerUrl();

                // 현재 로그인한 유저 정보를 서버에서 가져옴
                var userResponse = await client.GetAsync($"{baseUrl}/api/User/getUser?userId={_currentUser.Id}");
                if (!userResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show("유저 정보를 불러오지 못했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    LoadingText.Visibility = Visibility.Collapsed;
                    return;
                }
                var userJson = await userResponse.Content.ReadAsStringAsync();
                var currentUser = JsonConvert.DeserializeObject<UserData>(userJson);

                if (currentUser == null)
                {
                    MessageBox.Show("유저 정보를 해석할 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    LoadingText.Visibility = Visibility.Collapsed;
                    return;
                }

                // 전체 채팅방 리스트를 서버에서 가져옴
                var chatListResponse = await client.GetAsync($"{baseUrl}/api/User/getChatList");
                if (!chatListResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show("채팅방 목록을 불러오지 못했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    LoadingText.Visibility = Visibility.Collapsed;
                    return;
                }
                var chatListJson = await chatListResponse.Content.ReadAsStringAsync();
                var allRooms = JsonConvert.DeserializeObject<List<RoomInfo>>(chatListJson);

                if (allRooms == null)
                {
                    MessageBox.Show("채팅방 목록을 해석할 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    LoadingText.Visibility = Visibility.Collapsed;
                    return;
                }

                // 사용자가 가입한 채팅방만 필터링
                var joinedRooms = allRooms
                    .Where(r => currentUser.JoinedRoomIds.Contains(r.RoomId))
                    .Select(r => new DisplayRoom
                    {
                        RoomName = r.RoomName,
                        RoomId = r.RoomId
                    }).ToList();

                // 채팅방 목록 표시
                RoomListControl.ItemsSource = joinedRooms;

                // 로딩 텍스트 숨김
                LoadingText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingText.Visibility = Visibility.Collapsed;
                MessageBox.Show("서버 오류: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageRooms_Click(object sender, RoutedEventArgs e)
        {
            // 다른 패널 닫기
            CreateRoomPanel.Visibility = Visibility.Collapsed;
            JoinRoomPanel.Visibility = Visibility.Collapsed;

            // 패널 표시
            DeleteRoomPanel.Visibility = Visibility.Visible;
            DeleteRoomPanel.Opacity = 0;
            DeleteRoomPanel.Margin = new Thickness(0, 20, 0, 0);

            // 페이드 인 애니메이션 시작
            var storyboard = (Storyboard)FindResource("PanelFadeInStoryboard");
            storyboard.Begin(DeleteRoomPanel);
        }

        private void CancelDeleteRoom_Click(object sender, RoutedEventArgs e)
        {
            DeleteRoomPanel.Visibility = Visibility.Collapsed;
            DeleteRoomNameInput.Text = "";
            DeleteRoomIdInput.Text = "";
            DeleteRoomPasswordInput.Password = "";
        }

        private async Task<bool> VerifyRoomPassword(string roomId, string roomName, string password)
        {
            try
            {
                using HttpClient client = new HttpClient();
                string apiUrl = AppSettings.GetServerUrl();

                // GET 요청에 쿼리 스트링으로 값 전달
                string requestUrl = $"{apiUrl}/api/User/verifyRoomPassword?roomId={roomId}&roomName={roomName}&password={password}";
                var response = await client.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return bool.Parse(result); // true 또는 false 반환
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return false;
        }

        private async void ConfirmDeleteRoom_Click(object sender, RoutedEventArgs e)
        {
            string roomName = DeleteRoomNameInput.Text.Trim();
            string roomId = DeleteRoomIdInput.Text.Trim();
            string password = DeleteRoomPasswordInput.Password.Trim();

            if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("모든 정보를 입력하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmResult = MessageBox.Show(
                "정말로 이 채팅방을 삭제하시겠습니까? 이 작업은 취소할 수 없습니다.",
                "삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            bool isPasswordCorrect = await VerifyRoomPassword(roomId, roomName, password);

            if (!isPasswordCorrect)
            {
                MessageBox.Show("입력한 정보와 일치하는 채팅방을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 삭제 요청
            using HttpClient client = new HttpClient();
            string apiUrl = AppSettings.GetServerUrl();
            var deleteResponse = await client.PostAsJsonAsync($"{apiUrl}/api/User/deleteRooms", new[] { roomId });

            if (deleteResponse.IsSuccessStatusCode)
            {
                MessageBox.Show("채팅방 삭제 완료!", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                CancelDeleteRoom_Click(null, null); // 패널 숨기기
                LoadUserJoinedRooms(); // 새로고침
            }
            else
            {
                MessageBox.Show("삭제 실패: " + await deleteResponse.Content.ReadAsStringAsync(), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnterRoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DisplayRoom room)
            {
                Message msgWindow = new Message(_currentUser, room.RoomId);
                msgWindow.Show();
                this.Close();
            }
        }

        private string GenerateRoomId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 16).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private void OpenProfile_Click(object sender, MouseButtonEventArgs e)
        {
            Profile profileWindow = new Profile(_currentUser);
            profileWindow.Owner = this;
            profileWindow.ShowDialog();
        }

        private void OpenFriend_Click(object sender, MouseButtonEventArgs e)
        {
            Friend friendWindow = new Friend(_currentUser, null);
            friendWindow.Show();
            this.Close();
        }

        private void OpenChatList_Click(object sender, MouseButtonEventArgs e)
        {
            ChatList chatListWindow = new ChatList(_currentUser);
            chatListWindow.Show();
            this.Close();
        }

        private void OpenSettings_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("설정 기능은 아직 구현 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public class RoomInfo
        {
            [JsonProperty("Name")]
            public string RoomName { get; set; } = string.Empty;

            public string Password { get; set; } = string.Empty;

            public string RoomId { get; set; } = string.Empty;

            public bool IsPrivate { get; set; } = false;
        }

        public class DisplayRoom
        {
            public string RoomName { get; set; }
            public string RoomId { get; set; }
        }
    }
}