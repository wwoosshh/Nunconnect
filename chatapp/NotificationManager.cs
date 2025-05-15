using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static chatapp.MainWindow;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace chatapp
{
    public class NotificationManager
    {
        private static NotificationManager _instance;
        private List<NotificationWindow> _activeNotifications = new List<NotificationWindow>();

        private NotificationManager() { }

        public static NotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new NotificationManager();
                }
                return _instance;
            }
        }

        public void ShowNotification(string roomName, string message, string roomId, UserData currentUser)
        {
            // 앱이 포커스 되어 있으면 알림 표시하지 않음
            if (Application.Current.MainWindow?.IsActive == true)
            {
                // 현재 활성화 창이 Message이고 해당 방이면 알림 표시하지 않음
                if (Application.Current.MainWindow is Message messageWindow &&
                    messageWindow.CurrentRoomId == roomId)
                {
                    return;
                }
            }

            // 메시지 내용 길이 제한
            if (message.Length > 30)
            {
                message = message.Substring(0, 30) + "...";
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                // 기존 알림 위치 조정
                foreach (var notification in _activeNotifications)
                {
                    notification.Top -= 85; // 알림 높이 + 여백
                }

                // 새 알림 창 생성
                var notificationWindow = new NotificationWindow(roomName, message, roomId, currentUser);
                notificationWindow.Closed += (sender, e) => _activeNotifications.Remove(notificationWindow);
                notificationWindow.Show();

                // 알림 리스트에 추가
                _activeNotifications.Add(notificationWindow);

                // 최대 3개까지만 표시
                while (_activeNotifications.Count > 3)
                {
                    var oldestNotification = _activeNotifications[0];
                    oldestNotification.Close();
                    _activeNotifications.RemoveAt(0);
                }
            });
        }
    }
}