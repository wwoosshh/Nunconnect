using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class Friend : Window
    {
        private readonly int _myIndex;
        private readonly UserData _currentUser;
        private readonly string _lastRoomId;
        private readonly Dictionary<int, string> _userNameCache = new Dictionary<int, string>();

        // 사용자 이름 로딩을 위한 취소 토큰
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public Friend(UserData currentUser, string lastRoomId)
        {
            InitializeComponent();
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _lastRoomId = lastRoomId;
            _myIndex = currentUser.Index;

            LoadProfile();

            // 창이 로드된 후 데이터 로드
            this.Loaded += (s, e) => LoadFriendData();
        }

        private void LoadProfile()
        {
            ProfileNameText.Text = _currentUser.Name;
            StatusMessageText.Text = _currentUser.StatusMessage;
        }

        private async void LoadFriendData()
        {
            try
            {
                // 기존 작업 취소
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
                _cts = new CancellationTokenSource();

                // UI에 로딩 표시
                RequestListPanel.Children.Clear();
                RequestListPanel.Children.Add(UiHelper.CreateLoadingTextBlock("친구 요청 불러오는 중..."));

                FriendListPanel.Children.Clear();
                FriendListPanel.Children.Add(UiHelper.CreateLoadingTextBlock("친구 목록 불러오는 중..."));

                // 병렬로 친구 요청과 친구 목록 로드
                await Task.WhenAll(
                    LoadFriendRequests(_cts.Token),
                    LoadFriends(_cts.Token)
                );
            }
            catch (OperationCanceledException)
            {
                // 작업이 취소됨 - 무시
            }
            catch (Exception ex)
            {
                MessageBox.Show($"친구 정보를 불러오는 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            // 진행 중인 작업 취소
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            Window nextWindow;
            if (!string.IsNullOrEmpty(_lastRoomId))
            {
                nextWindow = new Message(_currentUser, _lastRoomId);
            }
            else
            {
                nextWindow = new ChatList(_currentUser);
            }

            Application.Current.MainWindow = nextWindow;
            nextWindow.Show();
            this.Close();
        }

        private void AddFriendButton_Click(object sender, RoutedEventArgs e)
        {
            var addFriendWindow = new AddFriendWindow(_currentUser)
            {
                Owner = this
            };
            addFriendWindow.ShowDialog();

            // 요청 후 목록 새로고침
            LoadFriendData();
        }

        private async Task<string> GetUserNameByIndex(int userIndex, CancellationToken cancellationToken)
        {
            try
            {
                // 캐시에서 이름 확인
                if (_userNameCache.TryGetValue(userIndex, out string cachedName))
                {
                    return cachedName;
                }

                // API에서 이름 가져오기
                string endpoint = $"/api/User/getUserNameByIndex?index={userIndex}";

                // 정적 HttpClient 직접 사용
                var client = ApiClient.GetClient();
                var response = await client.GetAsync(
                    $"{AppSettings.GetServerUrl()}{endpoint}",
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    string name = await response.Content.ReadAsStringAsync();

                    // 캐시에 저장
                    _userNameCache[userIndex] = name;

                    return name;
                }

                // 오류 상태 코드에 따른 다른 처리
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return $"존재하지 않는 사용자 (Index: {userIndex})";
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 취소된 작업 예외 전파
            }
            catch (Exception ex)
            {
                Console.WriteLine($"유저 닉네임 로드 실패: {ex.Message}");
            }

            return $"Unknown (Index: {userIndex})";
        }

        private async Task LoadFriendRequests(CancellationToken cancellationToken)
        {
            try
            {
                string endpoint = $"/api/Friend/requests?userIndex={_myIndex}";

                // 정적 HttpClient 사용
                var client = ApiClient.GetClient();
                var response = await client.GetAsync(
                    $"{AppSettings.GetServerUrl()}{endpoint}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    UiHelper.RunOnUiThread(() => {
                        RequestListPanel.Children.Clear();
                        RequestListPanel.Children.Add(UiHelper.CreateErrorTextBlock(
                            $"요청 목록을 불러올 수 없습니다: {response.StatusCode}"));
                    });
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var requests = JsonConvert.DeserializeObject<List<FriendRequest>>(json);

                UiHelper.RunOnUiThread(() => {
                    RequestListPanel.Children.Clear();

                    if (requests != null && requests.Count > 0)
                    {
                        foreach (var req in requests)
                        {
                            var panel = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0, 5, 0, 5)
                            };

                            var nameTextBlock = new TextBlock
                            {
                                Text = $"요청자: 로드 중...",
                                Foreground = Brushes.White,
                                Width = 200
                            };

                            panel.Children.Add(nameTextBlock);

                            // 비동기적으로 이름 로드
                            LoadUserNameAsync(req.HostIndex, nameTextBlock, cancellationToken);

                            // 버튼 생성
                            var acceptButton = new Button
                            {
                                Content = "수락",
                                Margin = new Thickness(5, 0, 0, 0),
                                Tag = req.HostIndex // 사용자 인덱스 저장
                            };
                            acceptButton.Click += AcceptRequest_Click;

                            var rejectButton = new Button
                            {
                                Content = "거절",
                                Margin = new Thickness(5, 0, 0, 0),
                                Tag = req.HostIndex // 사용자 인덱스 저장
                            };
                            rejectButton.Click += RejectRequest_Click;

                            panel.Children.Add(acceptButton);
                            panel.Children.Add(rejectButton);

                            RequestListPanel.Children.Add(panel);
                        }
                    }
                    else
                    {
                        RequestListPanel.Children.Add(new TextBlock
                        {
                            Text = "요청 없음",
                            Foreground = Brushes.Gray
                        });
                    }
                });
            }
            catch (OperationCanceledException)
            {
                throw; // 취소된 작업 예외 전파
            }
            catch (Exception ex)
            {
                UiHelper.RunOnUiThread(() => {
                    RequestListPanel.Children.Clear();
                    RequestListPanel.Children.Add(UiHelper.CreateErrorTextBlock(
                        $"요청 목록 로드 실패: {ex.Message}"));
                });
            }
        }

        private async void LoadUserNameAsync(int userIndex, TextBlock textBlock, CancellationToken cancellationToken)
        {
            try
            {
                string name = await GetUserNameByIndex(userIndex, cancellationToken);

                UiHelper.RunOnUiThread(() => {
                    if (textBlock != null)
                    {
                        textBlock.Text = $"요청자: {name}";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // 작업 취소됨 - 무시
            }
            catch (Exception ex)
            {
                UiHelper.RunOnUiThread(() => {
                    if (textBlock != null)
                    {
                        textBlock.Text = $"요청자: 로드 실패 ({ex.Message})";
                        textBlock.Foreground = Brushes.Red;
                    }
                });
            }
        }

        private async Task LoadFriends(CancellationToken cancellationToken)
        {
            try
            {
                string endpoint = $"/api/Friend/list?userIndex={_myIndex}";

                // 정적 HttpClient 사용
                var client = ApiClient.GetClient();
                var response = await client.GetAsync(
                    $"{AppSettings.GetServerUrl()}{endpoint}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    UiHelper.RunOnUiThread(() => {
                        FriendListPanel.Children.Clear();
                        FriendListPanel.Children.Add(UiHelper.CreateErrorTextBlock(
                            $"친구 목록을 불러올 수 없습니다: {response.StatusCode}"));
                    });
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var friends = JsonConvert.DeserializeObject<List<int>>(json);

                UiHelper.RunOnUiThread(() => {
                    FriendListPanel.Children.Clear();

                    if (friends != null && friends.Count > 0)
                    {
                        foreach (var friendIndex in friends)
                        {
                            var panel = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0, 5, 0, 5)
                            };

                            var nameTextBlock = new TextBlock
                            {
                                Text = $"친구: 로드 중...",
                                Foreground = Brushes.White,
                                Width = 200
                            };

                            panel.Children.Add(nameTextBlock);

                            // 비동기적으로 이름 로드
                            LoadFriendNameAsync(friendIndex, nameTextBlock, cancellationToken);

                            // 채팅 버튼
                            var chatButton = new Button
                            {
                                Content = "💬",
                                Width = 30,
                                Height = 25,
                                Background = Brushes.Green,
                                Foreground = Brushes.White,
                                Margin = new Thickness(5, 0, 0, 0),
                                ToolTip = "채팅 시작",
                                Tag = friendIndex
                            };
                            chatButton.Click += StartChat_Click;
                            panel.Children.Add(chatButton);

                            // 삭제 버튼
                            var deleteButton = new Button
                            {
                                Content = "❌",
                                Width = 30,
                                Height = 25,
                                Background = Brushes.Red,
                                Foreground = Brushes.White,
                                Margin = new Thickness(5, 0, 0, 0),
                                ToolTip = "친구 삭제",
                                Tag = friendIndex
                            };
                            deleteButton.Click += DeleteFriend_Click;
                            panel.Children.Add(deleteButton);

                            FriendListPanel.Children.Add(panel);
                        }
                    }
                    else
                    {
                        FriendListPanel.Children.Add(new TextBlock
                        {
                            Text = "친구 없음",
                            Foreground = Brushes.Gray
                        });
                    }
                });
            }
            catch (OperationCanceledException)
            {
                throw; // 취소된 작업 예외 전파
            }
            catch (Exception ex)
            {
                UiHelper.RunOnUiThread(() => {
                    FriendListPanel.Children.Clear();
                    FriendListPanel.Children.Add(UiHelper.CreateErrorTextBlock(
                        $"친구 목록 로드 실패: {ex.Message}"));
                });
            }
        }

        private async void LoadFriendNameAsync(int friendIndex, TextBlock textBlock, CancellationToken cancellationToken)
        {
            try
            {
                string name = await GetUserNameByIndex(friendIndex, cancellationToken);

                UiHelper.RunOnUiThread(() => {
                    if (textBlock != null)
                    {
                        textBlock.Text = $"친구: {name}";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // 작업 취소됨 - 무시
            }
            catch (Exception ex)
            {
                UiHelper.RunOnUiThread(() => {
                    if (textBlock != null)
                    {
                        textBlock.Text = $"친구: 로드 실패 ({ex.Message})";
                        textBlock.Foreground = Brushes.Red;
                    }
                });
            }
        }

        private async void StartChat_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int friendIndex))
                return;

            try
            {
                // 버튼 로딩 상태로 변경
                UiHelper.SetButtonLoading(button, true, "💬");

                // 1. 먼저 이미 해당 친구와의 1:1 채팅방이 있는지 확인
                var existingRoom = await CheckExistingPrivateRoom(friendIndex);

                if (existingRoom != null)
                {
                    // 기존 채팅방으로 이동
                    OpenChatRoom(existingRoom.RoomId);
                    return;
                }

                // 2. 친구 이름 가져오기
                string friendName = await GetUserNameByIndex(friendIndex, CancellationToken.None);

                // 3. 1:1 채팅방 생성
                var roomId = await CreatePrivateRoom(friendIndex, friendName);

                if (!string.IsNullOrEmpty(roomId))
                {
                    OpenChatRoom(roomId);
                }
                else
                {
                    MessageBox.Show("채팅방 생성에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    UiHelper.SetButtonLoading(button, false, "💬");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"채팅방 생성 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                if (button != null)
                {
                    UiHelper.SetButtonLoading(button, false, "💬");
                }
            }
        }

        private async Task<ChatRoom> CheckExistingPrivateRoom(int friendIndex)
        {
            try
            {
                // 채팅방 목록 가져오기
                var client = ApiClient.GetClient();
                var response = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/getChatList");

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var rooms = JsonConvert.DeserializeObject<List<ChatRoom>>(json);

                if (rooms == null || !rooms.Any())
                    return null;

                // 현재 유저의 정보 가져오기
                var userResponse = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/getUser?userId={_currentUser.Id}");

                if (!userResponse.IsSuccessStatusCode)
                    return null;

                var userJson = await userResponse.Content.ReadAsStringAsync();
                var user = JsonConvert.DeserializeObject<UserData>(userJson);

                if (user == null || user.JoinedRoomIds == null || !user.JoinedRoomIds.Any())
                    return null;

                // 친구 정보 가져오기 (친구가 참여한 방 목록 체크용)
                var friendUserInfo = await GetFriendUserInfo(friendIndex);

                if (friendUserInfo == null || friendUserInfo.JoinedRoomIds == null)
                    return null;

                // 1:1 채팅방 형식 이름 구성 (양방향 모두 체크)
                string privateChatFormat1 = $"{_currentUser.Name}님과 {friendUserInfo.Name}님의 대화";
                string privateChatFormat2 = $"{friendUserInfo.Name}님과 {_currentUser.Name}님의 대화";

                // 사용자가 참여 중인 방 중에서 1:1 채팅방 찾기
                foreach (var roomId in user.JoinedRoomIds)
                {
                    // 친구도 참여 중인 방인지 확인
                    if (!friendUserInfo.JoinedRoomIds.Contains(roomId))
                        continue;

                    var room = rooms.FirstOrDefault(r => r.RoomId == roomId);

                    if (room != null && (room.Name == privateChatFormat1 || room.Name == privateChatFormat2))
                    {
                        return room; // 이미 존재하는 1:1 채팅방
                    }
                }

                return null; // 기존 1:1 채팅방 없음
            }
            catch (Exception ex)
            {
                Console.WriteLine($"채팅방 확인 오류: {ex.Message}");
                return null;
            }
        }

        private async Task<UserData> GetFriendUserInfo(int friendIndex)
        {
            try
            {
                // 먼저 API로 시도
                var client = ApiClient.GetClient();

                try
                {
                    // getAllUsers API가 있는지 확인
                    var response = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/getAllUsers");

                    if (response.IsSuccessStatusCode)
                    {
                        var usersJson = await response.Content.ReadAsStringAsync();
                        var users = JsonConvert.DeserializeObject<List<UserData>>(usersJson);
                        return users?.FirstOrDefault(u => u.Index == friendIndex);
                    }
                }
                catch
                {
                    // API가 없거나 오류 시 무시하고 계속 진행
                }

                // 대체 방법: 로컬 파일에서 로드 (임시 방편)
                string userFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "users.txt");
                if (File.Exists(userFilePath))
                {
                    var json = File.ReadAllText(userFilePath);
                    var users = JsonConvert.DeserializeObject<List<UserData>>(json) ?? new List<UserData>();
                    return users.FirstOrDefault(u => u.Index == friendIndex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"친구 정보 조회 오류: {ex.Message}");
            }

            return null;
        }

        private async Task<string> CreatePrivateRoom(int friendIndex, string friendName)
        {
            try
            {
                // 랜덤 RoomId 생성
                string roomId = GenerateRoomId();

                // 1:1 채팅방 이름 설정
                string roomName = $"{_currentUser.Name}님과 {friendName}님의 대화";

                // 비밀번호 생성 (두 사용자의 인덱스 해시)
                string password = HashPassword($"{_myIndex}_{friendIndex}");

                // 1. 채팅방 생성
                var client = ApiClient.GetClient();

                // 서버에 IsPrivate, TargetUserIndex 기능이 있는지 확인
                bool isPrivateSupported = false;

                try
                {
                    // 지원 여부 확인 API 호출 (없으면 예외 발생)
                    var checkResponse = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/isPrivateChatSupported");
                    isPrivateSupported = checkResponse.IsSuccessStatusCode;
                }
                catch
                {
                    // 지원하지 않음 - 기본 방식으로 진행
                    isPrivateSupported = false;
                }

                // 요청 객체 생성 (지원 여부에 따라 다르게)
                object createRoomRequest;

                if (isPrivateSupported)
                {
                    createRoomRequest = new
                    {
                        RoomId = roomId,
                        RoomName = roomName,
                        Password = password,
                        UserId = _currentUser.Id,
                        IsPrivate = true,
                        TargetUserIndex = friendIndex
                    };
                }
                else
                {
                    createRoomRequest = new
                    {
                        RoomId = roomId,
                        RoomName = roomName,
                        Password = password,
                        UserId = _currentUser.Id
                    };
                }

                var createResponse = await client.PostAsJsonAsync(
                    $"{AppSettings.GetServerUrl()}/api/User/createRoom",
                    createRoomRequest);

                if (!createResponse.IsSuccessStatusCode)
                    return string.Empty;

                // 서버가 IsPrivate을 지원하지 않을 경우 수동으로 초대
                if (!isPrivateSupported)
                {
                    // 친구의 UserId 조회
                    var friendUserInfo = await GetFriendUserInfo(friendIndex);

                    if (friendUserInfo != null)
                    {
                        // 친구를 채팅방에 초대
                        var joinRequest = new
                        {
                            RoomName = roomName,
                            Password = password,
                            UserId = friendUserInfo.Id
                        };

                        await client.PostAsJsonAsync(
                            $"{AppSettings.GetServerUrl()}/api/User/joinRoom",
                            joinRequest);
                    }
                }

                return roomId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"채팅방 생성 오류: {ex.Message}");
                return string.Empty;
            }
        }

        private string HashPassword(string input)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower().Substring(0, 16);
            }
        }

        private string GenerateRoomId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 16)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private void OpenChatRoom(string roomId)
        {
            Message chatWindow = new Message(_currentUser, roomId);
            chatWindow.Show();
            this.Close();
        }

        private async void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int friendIndex))
                return;

            var result = MessageBox.Show("정말로 이 친구를 삭제하시겠습니까?", "친구 삭제",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                // 버튼 로딩 상태 설정
                UiHelper.SetButtonLoading(button, true);

                string endpoint = "/api/Friend/delete";

                var request = new FriendRequest
                {
                    HostIndex = _currentUser.Index,
                    GetIndex = friendIndex,
                    Action = "Delete"
                };

                // 정적 HttpClient 사용
                var client = ApiClient.GetClient();
                var content = new StringContent(
                    JsonConvert.SerializeObject(request),
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync(
                    $"{AppSettings.GetServerUrl()}{endpoint}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("친구를 삭제했습니다.", "알림",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadFriendData(); // 목록 새로고침
                }
                else
                {
                    // 오류 상태 코드에 따른 다른 메시지
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        MessageBox.Show("존재하지 않는 친구입니다.", "오류",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show($"삭제 실패: {await response.Content.ReadAsStringAsync()}",
                            "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    // 버튼 상태 복원
                    UiHelper.SetButtonLoading(button, false, "❌");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // 버튼 상태 복원
                UiHelper.SetButtonLoading(button, false, "❌");
            }
        }

        private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int hostIndex))
                return;

            await RespondToRequest(button, hostIndex, "Accepted");
        }

        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int hostIndex))
                return;

            await RespondToRequest(button, hostIndex, "Rejected");
        }

        private async Task RespondToRequest(Button button, int hostIndex, string action)
        {
            try
            {
                // 버튼 로딩 상태 설정
                string originalText = action == "Accepted" ? "수락" : "거절";
                UiHelper.SetButtonLoading(button, true);

                string endpoint = "/api/Friend/respond";

                var request = new FriendRequest
                {
                    HostIndex = hostIndex,
                    GetIndex = _myIndex,
                    Action = action
                };

                // 정적 HttpClient 사용
                var client = ApiClient.GetClient();
                var content = new StringContent(
                    JsonConvert.SerializeObject(request),
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync(
                    $"{AppSettings.GetServerUrl()}{endpoint}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    string message = action == "Accepted" ?
                        "친구 요청을 수락했습니다." :
                        "친구 요청을 거절했습니다.";

                    MessageBox.Show(message, "알림",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadFriendData(); // 목록 새로고침
                }
                else
                {
                    MessageBox.Show($"처리 실패: {await response.Content.ReadAsStringAsync()}",
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);

                    // 버튼 상태 복원
                    UiHelper.SetButtonLoading(button, false, originalText);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // 버튼 상태 복원
                UiHelper.SetButtonLoading(button, false,
                    action == "Accepted" ? "수락" : "거절");
            }
        }

        public class FriendRequest
        {
            public int HostIndex { get; set; }
            public int GetIndex { get; set; }
            public string Action { get; set; } = string.Empty;
        }

        public class ChatRoom
        {
            public string RoomId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool IsPrivate { get; set; } = false;
        }
    }
}