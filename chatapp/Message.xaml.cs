using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class Message : Window
    {
        private UserData _currentUser;
        private string _roomId;
        private Dictionary<string, string> _userNames = new();
        private HubConnection _connection;

        private string GetServerUrl()
        {
            bool isServerPc = true;
            return isServerPc ? "http://localhost:5159" : "http://nunconnect.duckdns.org:5159";
        }

        private void LoadUserNames()
        {
            string userFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "users.txt");
            if (!File.Exists(userFilePath)) return;

            var json = File.ReadAllText(userFilePath);
            var users = JsonConvert.DeserializeObject<List<UserData>>(json) ?? new();
            _userNames = users.ToDictionary(u => u.Id, u => u.Name);
        }

        private async void LoadChatFromServer()
        {
            try
            {
                using HttpClient client = new();
                var response = await client.GetAsync($"{GetServerUrl()}/api/User/loadMessages?roomId={_roomId}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var chatHistory = JsonConvert.DeserializeObject<List<ChatMessage>>(json);

                    ChatStack.Children.Clear();
                    _chatHistory.Clear();

                    foreach (var chat in chatHistory)
                    {
                        if (string.IsNullOrWhiteSpace(chat.Message))
                            continue; // ✅ 빈 메시지 필터링

                        if (Uri.IsWellFormedUriString(chat.Message, UriKind.Absolute))
                        {
                            AddImageBubble(chat.Sender, chat.Message);
                        }
                        else
                        {
                            AddChatBubble(chat.Sender, chat.Message);
                        }

                        _chatHistory.Add(chat);
                    }
                }
                else
                {
                    MessageBox.Show("채팅 불러오기 실패: " + await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 오류: " + ex.Message);
            }
        }

        private async Task SendChatToServer(string senderId, string chatting)
        {
            try
            {
                if (_connection is { State: HubConnectionState.Connected })
                {
                    await _connection.InvokeAsync("SendMessage", _roomId, senderId, chatting);
                }
                else
                {
                    MessageBox.Show("서버에 연결되어 있지 않습니다.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("메시지 전송 실패: " + ex.Message);
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            _ = SendChatToServer(_currentUser.Id, message);
            MessageInput.Clear();
        }

        private void MessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
        }

        private void AddChatBubble(string senderId, string message)
        {
            string senderName = _userNames.ContainsKey(senderId) ? _userNames[senderId] : senderId;
            bool isCurrentUser = senderId == _currentUser.Id;

            var container = new StackPanel
            {
                Margin = new Thickness(5),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            var nameText = new TextBlock
            {
                Text = senderName,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(5, 0, 5, 2)
            };

            var bubble = new Border
            {
                Background = isCurrentUser
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00CED1"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#008B8B")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                MaxWidth = 300,
                Child = new TextBlock
                {
                    Text = message,
                    FontSize = 14,
                    Foreground = Brushes.Black,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            container.Children.Add(nameText);
            container.Children.Add(bubble);
            ChatStack.Children.Add(container);
            ChatScrollViewer.ScrollToEnd();
        }

        private async void ConnectToSignalR()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl($"{GetServerUrl()}/chathub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>("ReceiveMessage", (senderId, message) =>
            {
                Dispatcher.Invoke(() => LoadChatFromServer());
            });

            _connection.On<string, string>("ReceiveImage", (senderId, imageUrl) =>
            {
                Dispatcher.Invoke(() => AddImageBubble(senderId, imageUrl));
            });

            try
            {
                await _connection.StartAsync();
                await _connection.InvokeAsync("JoinRoom", _roomId, _currentUser.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SignalR 연결 실패: {ex.Message}");
            }
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

        private void OpenProfile_Click(object sender, MouseButtonEventArgs e)
        {
            Profile profileWindow = new Profile(_currentUser);
            profileWindow.Owner = this;
            profileWindow.ShowDialog();
        }

        private void OpenFriend_Click(object sender, MouseButtonEventArgs e)
        {
            // ✅ 현재 로그인한 사용자 정보를 Friend 창으로 넘겨줌
            Friend friendWindow = new Friend(_currentUser, _roomId);
            friendWindow.Show();

            // 현재 Message 창 닫기 (필요 시 주석 처리)
            this.Close();
        }

        private void OpenChatList_Click(object sender, MouseButtonEventArgs e)
        {
            List chatListWindow = new List(_currentUser);
            chatListWindow.Show();
            this.Close();
        }

        private void OpenSettings_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Setting 페이지로 이동");
        }

        private List<ChatMessage> _chatHistory = new();

        public Message(UserData user, string roomId)
        {
            InitializeComponent();
            _currentUser = user ?? throw new ArgumentNullException(nameof(user));
            _roomId = roomId ?? throw new ArgumentNullException(nameof(roomId));

            RoomTitleText.Text = $"채팅방: {_roomId}";

            LoadUserNames();
            LoadChatFromServer();
            ConnectToSignalR(); // ✅ SignalR 실시간 연결
            // ✅ 채팅방 이름 표시
            SetRoomTitle();
        }
        private async void SendImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (dlg.ShowDialog() == true)
            {
                using var client = new HttpClient();
                using var form = new MultipartFormDataContent();
                using var fs = File.OpenRead(dlg.FileName);

                form.Add(new StreamContent(fs), "file", Path.GetFileName(dlg.FileName));

                var response = await client.PostAsync($"{GetServerUrl()}/api/File/upload?roomId={_roomId}&senderId={_currentUser.Id}", form);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic result = JsonConvert.DeserializeObject(json);
                    string imageUrl = result.Url;

                    // ✅ SignalR로 이미지 알림을 명시적으로 전송 (이미 업로드 시 서버가 자동 전송한다면 생략 가능)
                    if (_connection is { State: HubConnectionState.Connected })
                    {
                        await _connection.InvokeAsync("SendImage", _roomId, _currentUser.Id, imageUrl);
                    }

                    // ✅ 본인 채팅창에도 즉시 표시
                    AddImageBubble(_currentUser.Id, imageUrl);
                }
                else
                {
                    MessageBox.Show("이미지 업로드 실패");
                }
            }
        }


        private void AddImageBubble(string senderId, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return;

            var container = new StackPanel
            {
                Margin = new Thickness(5),
                HorizontalAlignment = senderId == _currentUser.Id ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            var nameText = new TextBlock
            {
                Text = _userNames.ContainsKey(senderId) ? _userNames[senderId] : senderId,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(5, 0, 5, 2)
            };

            var image = new Image
            {
                Width = 200,
                Height = 200,
                Cursor = Cursors.Hand,
                Margin = new Thickness(5)
            };

            try
            {
                // ✅ 로컬 환경일 때 nunconnect 주소를 localhost로 변환
                if (Environment.MachineName == "DESKTOP-NV0M9IM") // <-- 여기 'YOUR_PC_NAME'을 자신의 PC 이름으로 변경하세요.
                {
                    if (imageUrl.Contains("nunconnect.duckdns.org:5159"))
                    {
                        imageUrl = imageUrl.Replace("nunconnect.duckdns.org:5159", "localhost:5159");
                    }
                }

                // ✅ 최종적으로 이미지 로드
                if (Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
                {
                    image.Source = new BitmapImage(new Uri(imageUrl));
                }
                else
                {
                    MessageBox.Show($"유효하지 않은 이미지 경로입니다: {imageUrl}");
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 로드 중 오류 발생: {ex.Message}");
                return;
            }

            // ✅ 클릭 시 전체 뷰어로 열기
            image.MouseLeftButtonUp += (s, e) =>
            {
                var viewer = new ImageViewerWindow(imageUrl);
                viewer.ShowDialog();
            };

            container.Children.Add(nameText);
            container.Children.Add(image);

            ChatStack.Children.Add(container);
            ChatScrollViewer.ScrollToEnd();
        }



        private async void SetRoomTitle()
        {
            try
            {
                using HttpClient client = new();
                var response = await client.GetAsync($"{GetServerUrl()}/api/User/getChatList");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var rooms = JsonConvert.DeserializeObject<List<ChatRoom>>(json) ?? new();
                    var room = rooms.FirstOrDefault(r => r.RoomId == _roomId);
                    RoomTitleText.Text = room != null ? room.Name : $"채팅방: {_roomId}";
                }
                else
                {
                    RoomTitleText.Text = "채팅방";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"채팅방 정보를 불러오지 못했습니다: {ex.Message}");
                RoomTitleText.Text = "채팅방";
            }
        }

        public class ChatRoom
        {
            public string RoomId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class ChatMessage
        {
            public string RoomId { get; set; } = string.Empty;
            public string Sender { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}