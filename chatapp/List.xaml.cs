using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using static chatapp.MainWindow;
using System.Net.Http;
using System.Net.Http.Json;
using static System.Net.WebRequestMethods;

namespace chatapp
{
    public partial class List : Window
    {
        private UserData _currentUser;

        public List(UserData user)
        {
            InitializeComponent();
            _currentUser = user;
            this.Loaded += (s, e) => LoadUserJoinedRooms();
        }
        private void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            CreateRoomPanel.Visibility = Visibility.Visible;
        }

        private void CancelCreateRoom_Click(object sender, RoutedEventArgs e)
        {
            CreateRoomPanel.Visibility = Visibility.Collapsed;
            RoomNameInput.Text = "";
            RoomPasswordInput.Password = "";
        }

        private async void ConfirmCreateRoom_Click(object sender, RoutedEventArgs e)
        {
            string roomName = RoomNameInput.Text.Trim();
            string password = RoomPasswordInput.Password.Trim();
            string roomId = GenerateRoomId();

            if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("채팅방 이름과 비밀번호를 입력해주세요.");
                return;
            }

            string apiUrl = AppSettings.GetServerUrl();

            var request = new
            {
                RoomName = roomName,
                Password = password,
                RoomId = roomId,
                UserId = _currentUser.Id
            };

            try
            {
                using HttpClient client = new HttpClient();
                var response = await client.PostAsJsonAsync($"{apiUrl}/api/User/createRoom", request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeAnonymousType(json, new { RoomId = "" });

                    MessageBox.Show("채팅방 생성 완료!");
                    Message roomWindow = new Message(_currentUser, roomId);
                    roomWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("채팅방 생성 실패: " + await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message);
            }
        }

        private void JoinRoom_Click(object sender, RoutedEventArgs e)
        {
            JoinRoomPanel.Visibility = Visibility.Visible;
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
                MessageBox.Show("모든 정보를 입력해주세요.");
                return;
            }

            try
            {
                using HttpClient client = new HttpClient();
                string baseUrl = AppSettings.GetServerUrl();

                // 🔵 1. 서버에 채팅방 입장 요청 보내기
                var requestData = new
                {
                    RoomName = roomName,
                    Password = password,
                    UserId = _currentUser.Id
                };

                var response = await client.PostAsJsonAsync($"{baseUrl}/api/User/joinRoom", requestData);

                if (response.IsSuccessStatusCode)
                {
                    // ✅ 서버에서 받은 RoomId
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
                    MessageBox.Show("채팅방 입장 실패: " + await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message);
            }
        }

        private async void LoadUserJoinedRooms()
        {
            try
            {
                using HttpClient client = new HttpClient();
                string baseUrl = AppSettings.GetServerUrl(); // 🔥 고정된 ngrok 주소 사용

                // 🔵 1. 현재 로그인한 유저 정보를 서버에서 가져옴
                var userResponse = await client.GetAsync($"{baseUrl}/api/User/getUser?userId={_currentUser.Id}");
                if (!userResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show("유저 정보를 불러오지 못했습니다.");
                    return;
                }
                var userJson = await userResponse.Content.ReadAsStringAsync();
                var currentUser = JsonConvert.DeserializeObject<UserData>(userJson);

                if (currentUser == null)
                {
                    MessageBox.Show("유저 정보를 해석할 수 없습니다.");
                    return;
                }

                // 🔵 2. 전체 채팅방 리스트를 서버에서 가져옴
                var chatListResponse = await client.GetAsync($"{baseUrl}/api/User/getChatList");
                if (!chatListResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show("채팅방 목록을 불러오지 못했습니다.");
                    return;
                }
                var chatListJson = await chatListResponse.Content.ReadAsStringAsync();
                var allRooms = JsonConvert.DeserializeObject<List<RoomInfo>>(chatListJson);

                if (allRooms == null)
                {
                    MessageBox.Show("채팅방 목록을 해석할 수 없습니다.");
                    return;
                }

                // 🔵 3. 사용자가 가입한 채팅방만 필터링
                var joinedRooms = allRooms
                    .Where(r => currentUser.JoinedRoomIds.Contains(r.RoomId))
                    .Select(r => new DisplayRoom
                    {
                        RoomName = r.RoomName,
                        RoomId = r.RoomId
                    }).ToList();

                RoomListControl.ItemsSource = joinedRooms;
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message);
            }
        }

        private void ManageRooms_Click(object sender, RoutedEventArgs e)
        {
            DeleteRoomPanel.Visibility = Visibility.Visible;
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
                MessageBox.Show("서버 오류: " + ex.Message);
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
                MessageBox.Show("모든 정보를 입력하세요.");
                return;
            }

            bool isPasswordCorrect = await VerifyRoomPassword(roomId, roomName, password);

            if (!isPasswordCorrect)
            {
                MessageBox.Show("입력한 정보와 일치하는 채팅방을 찾을 수 없습니다.");
                return;
            }

            // 삭제 요청
            using HttpClient client = new HttpClient();
            string apiUrl = AppSettings.GetServerUrl();
            var deleteResponse = await client.PostAsJsonAsync($"{apiUrl}/api/User/deleteRooms", new[] { roomId });

            if (deleteResponse.IsSuccessStatusCode)
            {
                MessageBox.Show("채팅방 삭제 완료!");
                CancelDeleteRoom_Click(null, null); // 패널 숨기기
                LoadUserJoinedRooms(); // 새로고침
            }
            else
            {
                MessageBox.Show("삭제 실패: " + await deleteResponse.Content.ReadAsStringAsync());
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

        public class RoomInfo
        {
            [JsonProperty("Name")]
            public string RoomName { get; set; } = string.Empty;

            public string Password { get; set; } = string.Empty;

            public string RoomId { get; set; } = string.Empty;
        }

        public class DisplayRoom
        {
            public string RoomName { get; set; }
            public string RoomId { get; set; }
        }
    }
}