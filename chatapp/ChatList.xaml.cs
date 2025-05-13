using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using static chatapp.MainWindow;
using System.IO;
using System;
using System.Windows.Input;
using System.Windows.Documents;

namespace chatapp
{
    public partial class ChatList : Window
    {
        private string _username, _roomId;
        public ChatList(UserData user)
        {
            InitializeComponent();
            _currentUser = user;
            LoadChatFromFile();
            UserNameTextBlock.Text = _currentUser.Name + "님";
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            // 화면에 표시
            AddChatBubble(message, true); // 사용자가 입력한 메시지이므로 true
            SaveChatToFile(_currentUser.Id, message); // 메시지 저장


            MessageInput.Clear();
        }

        private void AddChatBubble(string message, bool isUser)
        {
            var bubble = new Border
            {
                Background = isUser ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00CED1")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#008B8B")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(5),
                MaxWidth = 300,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.Black,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            ChatStack.Children.Add(bubble);
            ChatScrollViewer.ScrollToEnd();
        }
        private void ToggleSidePanel_Click(object sender, RoutedEventArgs e)
        {
            SidePanel.Visibility = Visibility.Visible;

            var slideIn = (Storyboard)FindResource("SlideInStoryboard");
            slideIn.Begin();
        }

        // 파일 추가 버튼 클릭 시 (추후 구현 예정)
        private void FileAddButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("파일 첨부 기능은 아직 구현되지 않았습니다.");
        }
        private void CloseSidePanel_Click(object sender, RoutedEventArgs e)
        {
            var slideOut = (Storyboard)FindResource("SlideOutStoryboard");
            slideOut.Completed += (s, _) => SidePanel.Visibility = Visibility.Collapsed;
            slideOut.Begin();
        }
        private void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            Profile profileWindow = new Profile(_currentUser);
            profileWindow.Owner = this;
            profileWindow.ShowDialog(); // 모달창으로 열기

            // 프로필 창이 닫힌 후 할 작업이 있다면 여기에 작성
        }

        private void OpenFriend_Click(object sender, MouseButtonEventArgs e)
        {
            // ✅ 현재 로그인한 사용자 정보를 Friend 창으로 넘겨줌
            Friend friendWindow = new Friend(_currentUser, _roomId);
            friendWindow.Show();

            // 현재 Message 창 닫기 (필요 시 주석 처리)
            this.Close();
        }

        private void OpenChatList_Click(object sender, RoutedEventArgs e)
        {
            // 현재 유저 정보를 넘기려면 UserData 타입 필드가 필요함
            List chatListWindow = new List(_currentUser); // UserData를 생성자로 전달한다고 가정
            chatListWindow.Show();
            this.Close(); // 현재 ChatList 창 닫기
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("설정으로 이동");
        }
        private UserData _currentUser;

        private void SaveChatToFile(string sender, string message)
        {
            string chatFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "chatdata.txt");

            List<ChatMessage> chatHistory = new();

            // 기존 데이터 불러오기
            if (File.Exists(chatFilePath))
            {
                var json = File.ReadAllText(chatFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    chatHistory = JsonConvert.DeserializeObject<List<ChatMessage>>(json) ?? new();
                }
            }

            // 새 메시지 추가
            chatHistory.Add(new ChatMessage
            {
                Sender = sender,
                Message = message,
                Timestamp = DateTime.Now
            });

            // 다시 저장
            File.WriteAllText(chatFilePath, JsonConvert.SerializeObject(chatHistory, Formatting.Indented));
        }
        private void LoadChatFromFile()
        {
            string chatFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "chatdata.txt");

            if (File.Exists(chatFilePath))
            {
                var json = File.ReadAllText(chatFilePath);
                var chatHistory = JsonConvert.DeserializeObject<List<ChatMessage>>(json);

                foreach (var chat in chatHistory)
                {
                    bool isUser = chat.Sender == _currentUser.Id;
                    AddChatBubble(chat.Message, isUser);
                }
            }
        }



        public class ChatMessage
        {
            public string Sender { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}
