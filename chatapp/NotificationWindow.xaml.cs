using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class NotificationWindow : Window
    {
        private string _roomId;
        private DispatcherTimer _closeTimer;
        private UserData _currentUser;

        public NotificationWindow(string roomName, string message, string roomId, UserData currentUser)
        {
            InitializeComponent();

            // 위치 설정
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = screenWidth - Width - 20;
            Top = screenHeight - Height - 20;

            // 내용 설정
            RoomNameText.Text = roomName;
            MessageText.Text = message;
            _roomId = roomId;
            _currentUser = currentUser;

            // 타이머 설정
            _closeTimer = new DispatcherTimer();
            _closeTimer.Interval = TimeSpan.FromSeconds(5);
            _closeTimer.Tick += (s, e) => { _closeTimer.Stop(); Close(); };
            _closeTimer.Start();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 알림 클릭 시 해당 채팅방으로 이동
            try
            {
                Message msgWindow = new Message(_currentUser, _roomId);
                msgWindow.Show();

                // 열려있는 다른 창 닫기
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != msgWindow && window != this &&
                        (window is ChatList || window is List || window is Friend))
                    {
                        window.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"채팅방 열기 실패: {ex.Message}");
            }

            Close();
        }
        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            // 마우스가 들어왔을 때 타이머 정지
            _closeTimer.Stop();
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            // 마우스가 떠났을 때 타이머 다시 시작
            _closeTimer.Start();
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}