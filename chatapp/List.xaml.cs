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
        private DisplayRoom _currentSelectedRoom;

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

                // 먼저 채팅방 목록을 가져와서 해당 방의 정보 확인
                var chatListResponse = await client.GetAsync($"{baseUrl}/api/User/getChatList");
                if (chatListResponse.IsSuccessStatusCode)
                {
                    var chatListJson = await chatListResponse.Content.ReadAsStringAsync();
                    var allRooms = JsonConvert.DeserializeObject<List<RoomInfo>>(chatListJson);

                    // 입력한 이름과 비밀번호로 방을 찾음
                    var room = allRooms?.FirstOrDefault(r =>
                        r.RoomName == roomName && r.Password == password);

                    // 1:1 채팅방 확인
                    if (room != null && room.IsOneToOne)
                    {
                        // 내 이름이 채팅방 이름에 포함되어 있는지 확인
                        if (!room.RoomName.Contains(_currentUser.Name))
                        {
                            MessageBox.Show("1:1 채팅방은 참여자만 입장할 수 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }

                    // 비밀번호 보호 방식인지 확인
                    if (room != null && room.IsPrivate)
                    {
                        // 이미 비밀번호를 입력받았으므로 추가 검증 없이 진행
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

                // 1. 사용자가 참여한 1:1 채팅방
                var oneToOneRooms = allRooms
                    .Where(r => r.IsOneToOne && currentUser.JoinedRoomIds.Contains(r.RoomId))
                    .Select(r => new DisplayRoom
                    {
                        RoomName = r.RoomName,
                        RoomId = r.RoomId,
                        IsPrivate = r.IsPrivate,
                        IsOneToOne = true
                    }).ToList();

                // 2. 모든 공개 채팅방 (사용자 참여 여부 관계없이)
                var publicRooms = allRooms
                    .Where(r => !r.IsOneToOne)
                    .Select(r => new DisplayRoom
                    {
                        RoomName = r.RoomName,
                        RoomId = r.RoomId,
                        IsPrivate = r.IsPrivate,
                        IsOneToOne = false
                    }).ToList();

                // 두 목록을 UI에 표시 (별도 구현 필요)
                DisplayRoomsByCategory(oneToOneRooms, publicRooms);

                // 로딩 텍스트 숨김
                LoadingText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingText.Visibility = Visibility.Collapsed;
                MessageBox.Show("서버 오류: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 카테고리별로 채팅방 표시
        private void DisplayRoomsByCategory(List<DisplayRoom> oneToOneRooms, List<DisplayRoom> publicRooms)
        {
            // 기존 채팅방 목록 지우기
            RoomListPanel.Children.Clear();

            // 1:1 채팅방 섹션 추가
            AddRoomCategorySection("1:1 채팅방", oneToOneRooms);

            // 공개 채팅방 섹션 추가
            AddRoomCategorySection("공개 채팅방", publicRooms);
        }

        // 카테고리 섹션 UI 추가하는 메서드
        private void AddRoomCategorySection(string title, List<DisplayRoom> rooms)
        {
            // 카테고리 제목
            TextBlock categoryTitle = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 10)
            };
            RoomListPanel.Children.Add(categoryTitle);

            // 채팅방 목록이 비어있는 경우
            if (rooms.Count == 0)
            {
                TextBlock emptyMessage = new TextBlock
                {
                    Text = "채팅방이 없습니다.",
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(10, 5, 0, 15)
                };
                RoomListPanel.Children.Add(emptyMessage);
                return;
            }

            // 채팅방 항목 추가
            foreach (var room in rooms)
            {
                // 채팅방 항목 UI 생성
                Border roomItem = CreateRoomItem(room);
                RoomListPanel.Children.Add(roomItem);
            }
        }

        // 채팅방 아이템 UI 생성
        private Border CreateRoomItem(DisplayRoom room)
        {
            Border roomBorder = new Border
            {
                Margin = new Thickness(0, 8, 0, 0),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(15, 10, 15, 10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 5,
                    ShadowDepth = 0,
                    Opacity = 0.1,
                    Color = Colors.Black
                }
            };

            Grid roomGrid = new Grid();
            roomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            roomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            roomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 아이콘 (1:1 채팅방과 공개방 다른 아이콘 사용)
            Border iconBorder = new Border
            {
                Width = 40,
                Height = 40,
                Background = (SolidColorBrush)Application.Current.Resources["SecondaryColor"],
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(0, 0, 15, 0)
            };

            TextBlock iconText = new TextBlock
            {
                Text = room.IsOneToOne ? "👥" : "💬",
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
            Grid.SetColumn(iconBorder, 0);

            // 채팅방 이름
            StackPanel roomInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock roomName = new TextBlock
            {
                Text = room.RoomName,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            roomInfo.Children.Add(roomName);

            // 비밀번호 보호 여부 표시
            if (room.IsPrivate)
            {
                TextBlock passwordInfo = new TextBlock
                {
                    Text = "비밀번호 보호됨",
                    FontSize = 12,
                    Foreground = Brushes.Gray
                };
                roomInfo.Children.Add(passwordInfo);
            }

            Grid.SetColumn(roomInfo, 1);

            // 입장 버튼
            Button enterButton = new Button
            {
                Content = "입장",
                Width = 70,
                Height = 35,
                Style = Application.Current.Resources["PrimaryButton"] as Style,
                DataContext = room
            };
            enterButton.Click += EnterRoom_Click;
            Grid.SetColumn(enterButton, 2);

            roomGrid.Children.Add(iconBorder);
            roomGrid.Children.Add(roomInfo);
            roomGrid.Children.Add(enterButton);
            roomBorder.Child = roomGrid;

            return roomBorder;
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
        private void EnterRoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DisplayRoom room)
            {
                // 비밀번호 보호된 방인 경우
                if (room.IsPrivate)
                {
                    _currentSelectedRoom = room;

                    // 비밀번호 확인 패널 표시
                    PasswordRoomNameText.Text = room.RoomName;
                    PasswordCheckInput.Password = "";

                    // 모달 오버레이 및 패널 표시
                    ModalOverlay.Visibility = Visibility.Visible;
                    PasswordCheckPanel.Visibility = Visibility.Visible;
                    PasswordCheckPanel.Opacity = 0;

                    // 페이드 인 애니메이션 시작
                    var storyboard = (Storyboard)FindResource("PanelFadeInStoryboard");
                    storyboard.Begin(PasswordCheckPanel);

                    // 포커스 설정
                    PasswordCheckInput.Focus();
                }
                else
                {
                    // 1:1 채팅방 접근 권한 확인
                    if (room.IsOneToOne && !room.RoomName.Contains(_currentUser.Name))
                    {
                        MessageBox.Show("1:1 채팅방은 참여자만 입장할 수 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 비밀번호 보호가 아닌 방이면 바로 입장
                    EnterChatRoom(room.RoomId);
                }
            }
        }

        // 비밀번호 모달 취소 버튼 핸들러
        private void CancelPasswordCheck_Click(object sender, RoutedEventArgs e)
        {
            PasswordCheckPanel.Visibility = Visibility.Collapsed;
            _currentSelectedRoom = null;
        }

        // 비밀번호 모달 확인 버튼 핸들러
        private async void ConfirmPasswordCheck_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSelectedRoom == null)
                return;

            string password = PasswordCheckInput.Password.Trim();

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("비밀번호를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using HttpClient client = new HttpClient();
                string baseUrl = AppSettings.GetServerUrl();

                // 비밀번호 검증 요청
                bool isPasswordCorrect = await VerifyRoomPassword(_currentSelectedRoom.RoomId, _currentSelectedRoom.RoomName, password);

                if (isPasswordCorrect)
                {
                    PasswordCheckPanel.Visibility = Visibility.Collapsed;
                    EnterChatRoom(_currentSelectedRoom.RoomId);
                }
                else
                {
                    MessageBox.Show("비밀번호가 일치하지 않습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PasswordCheckInput.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 채팅방 입장 공통 메서드
        private void EnterChatRoom(string roomId)
        {
            Message msgWindow = new Message(_currentUser, roomId);
            msgWindow.Show();
            this.Close();
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
        private void PasswordCheckInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmPasswordCheck_Click(sender, e);
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

            public bool IsPrivate { get; set; } = false;  // 비밀번호 보호 여부

            public bool IsOneToOne { get; set; } = false; // 추가: 1:1 채팅방 여부
        }

        public class DisplayRoom
        {
            public string RoomName { get; set; }
            public string RoomId { get; set; }
            public bool IsPrivate { get; set; }  // 비밀번호 보호 여부 표시용
            public bool IsOneToOne { get; set; } // 1:1 채팅방 여부 표시용
        }
    }
}