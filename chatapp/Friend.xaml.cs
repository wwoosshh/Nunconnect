using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class Friend : Window
    {
        private readonly int _myIndex;
        private readonly UserData _currentUser;
        private readonly string _lastRoomId;

        public Friend(UserData currentUser, string lastRoomId)
        {
            InitializeComponent();
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _lastRoomId = lastRoomId;
            _myIndex = currentUser.Index;

            LoadProfile();
            LoadFriendData();
        }

        private string GetServerUrl()
        {
            bool isServerPc = true;
            return isServerPc ? "http://localhost:5159" : "http://nunconnect.duckdns.org:5159";
        }

        private void LoadProfile()
        {
            ProfileNameText.Text = _currentUser.Name;
            StatusMessageText.Text = _currentUser.StatusMessage;
        }

        private async void LoadFriendData()
        {
            await LoadFriendRequests();
            await LoadFriends();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Window nextWindow;
            if (!string.IsNullOrEmpty(_lastRoomId))
            {
                nextWindow = new Message(_currentUser, _lastRoomId);
            }
            else
            {
                nextWindow = new ChatList(_currentUser); // ✅ 메인 채팅 리스트로 이동
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

        private async void SendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(FriendIndexInput.Text.Trim(), out int targetIndex))
            {
                MessageBox.Show("유효한 Index 번호를 입력하세요.");
                return;
            }

            try
            {
                string url = $"{GetServerUrl()}/api/Friend/add";
                using HttpClient client = new();

                var request = new FriendRequest
                {
                    HostIndex = _currentUser.Index,
                    GetIndex = targetIndex,
                    Action = "Waiting"
                };

                var content = new StringContent(JsonConvert.SerializeObject(request), System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("친구 요청을 보냈습니다.");
                    AddFriendModal.Visibility = Visibility.Collapsed;
                    LoadFriendData();
                }
                else
                {
                    MessageBox.Show($"요청 실패: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}");
            }
        }

        private void CancelFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            AddFriendModal.Visibility = Visibility.Collapsed;
        }

        private async Task<string> GetUserNameByIndex(int userIndex)
        {
            try
            {
                using HttpClient client = new();
                var response = await client.GetAsync($"{GetServerUrl()}/api/User/getUserNameByIndex?index={userIndex}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"유저 닉네임 로드 실패: {ex.Message}");
            }
            return $"Unknown (Index: {userIndex})";
        }

        private async Task LoadFriendRequests()
        {
            try
            {
                string url = $"{GetServerUrl()}/api/Friend/requests?userIndex={_myIndex}";
                using HttpClient client = new();
                var json = await client.GetStringAsync(url);
                var requests = JsonConvert.DeserializeObject<List<FriendRequest>>(json);

                RequestListPanel.Children.Clear();

                if (requests != null && requests.Count > 0)
                {
                    foreach (var req in requests)
                    {
                        string requesterName = await GetUserNameByIndex(req.HostIndex);

                        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
                        panel.Children.Add(new TextBlock
                        {
                            Text = $"요청자: {requesterName}",
                            Foreground = System.Windows.Media.Brushes.White,
                            Width = 200
                        });

                        var acceptButton = new Button { Content = "수락", Margin = new Thickness(5, 0, 0, 0) };
                        acceptButton.Click += (s, e) => RespondToRequest(req.HostIndex, "Accepted");

                        var rejectButton = new Button { Content = "거절", Margin = new Thickness(5, 0, 0, 0) };
                        rejectButton.Click += (s, e) => RespondToRequest(req.HostIndex, "Rejected");

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
                        Foreground = System.Windows.Media.Brushes.Gray
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"요청 목록 로드 실패: {ex.Message}");
            }
        }

        private async Task LoadFriends()
        {
            try
            {
                string url = $"{GetServerUrl()}/api/Friend/list?userIndex={_myIndex}";
                using HttpClient client = new();
                var json = await client.GetStringAsync(url);
                var friends = JsonConvert.DeserializeObject<List<int>>(json);

                FriendListPanel.Children.Clear();

                if (friends != null && friends.Count > 0)
                {
                    foreach (var friendIndex in friends)
                    {
                        string friendName = await GetUserNameByIndex(friendIndex);

                        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
                        panel.Children.Add(new TextBlock
                        {
                            Text = $"친구: {friendName}",
                            Foreground = System.Windows.Media.Brushes.White,
                            Width = 200
                        });

                        var deleteButton = new Button
                        {
                            Content = "X",
                            Width = 30,
                            Height = 25,
                            Background = System.Windows.Media.Brushes.Red,
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new Thickness(5, 0, 0, 0)
                        };
                        deleteButton.Click += (s, e) => DeleteFriend(friendIndex);

                        panel.Children.Add(deleteButton);
                        FriendListPanel.Children.Add(panel);
                    }
                }
                else
                {
                    FriendListPanel.Children.Add(new TextBlock
                    {
                        Text = "친구 없음",
                        Foreground = System.Windows.Media.Brushes.Gray
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"친구 목록 로드 실패: {ex.Message}");
            }
        }
        private async void DeleteFriend(int friendIndex)
        {
            var result = MessageBox.Show("정말로 이 친구를 삭제하시겠습니까?", "친구 삭제", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                string url = $"{GetServerUrl()}/api/Friend/delete";
                using HttpClient client = new();

                var request = new FriendRequest
                {
                    HostIndex = _currentUser.Index,
                    GetIndex = friendIndex,
                    Action = "Delete"
                };

                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("친구를 삭제했습니다.");
                    LoadFriendData(); // ✅ 목록 새로고침
                }
                else
                {
                    MessageBox.Show($"삭제 실패: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}");
            }
        }

        private async void RespondToRequest(int hostIndex, string action)
        {
            try
            {
                string url = $"{GetServerUrl()}/api/Friend/respond";
                using HttpClient client = new();
                var request = new FriendRequest
                {
                    HostIndex = hostIndex,
                    GetIndex = _myIndex,
                    Action = action
                };

                var content = new StringContent(JsonConvert.SerializeObject(request), System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"친구 요청 {action} 성공!");
                    LoadFriendData();
                }
                else
                {
                    MessageBox.Show("처리 실패.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}");
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
