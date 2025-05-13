using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
                using HttpClient client = ApiClient.GetClient();

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

                // API 호출
                var response = await ApiClient.GetClient().GetAsync($"{AppSettings.GetServerUrl()}{endpoint}", cancellationToken);

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

                // API 호출
                using var client = ApiClient.GetClient();
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

        private void StartChat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int friendIndex)
            {
                MessageBox.Show($"아직 구현되지 않은 기능입니다: 친구(Index: {friendIndex})와 채팅");
                // TODO: 친구와의 1:1 채팅방 생성 및 이동 구현
            }
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

                using var client = ApiClient.GetClient();
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

                var content = new StringContent(
                    JsonConvert.SerializeObject(request),
                    Encoding.UTF8,
                    "application/json");

                using var client = ApiClient.GetClient();
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
    }
}