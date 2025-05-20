// NotificationService.cs 파일 생성
using chatapp;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using static chatapp.MainWindow;
using System.Net.Http;

public class NotificationService
{
    private static NotificationService _instance;
    private HubConnection _hubConnection;
    private UserData _currentUser;

    public static NotificationService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new NotificationService();
            }
            return _instance;
        }
    }

    public async Task Initialize(UserData user)
    {
        _currentUser = user;

        // SignalR 연결 설정
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{AppSettings.GetServerUrl()}/chathub")
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        // 메시지 수신 이벤트 처리
        _hubConnection.On<string, string, string>("GlobalMessage", (roomId, senderId, message) =>
        {
            // 자신이 보낸 메시지가 아닌 경우만 알림
            if (senderId != _currentUser.Id)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        string roomName = await GetRoomName(roomId);
                        NotificationManager.Instance.ShowNotification(
                            roomName,
                            message,
                            roomId,
                            _currentUser);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"알림 표시 오류: {ex.Message}");
                    }
                });
            }
        });

        try
        {
            await _hubConnection.StartAsync();
            Console.WriteLine("알림 서비스 연결 성공!");

            // 사용자의 모든 채팅방에 가입
            await JoinUserRooms();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"알림 서비스 연결 오류: {ex.Message}");
        }
    }

    private async Task JoinUserRooms()
    {
        try
        {
            using HttpClient client = new HttpClient();
            string baseUrl = AppSettings.GetServerUrl();

            // 사용자 정보 가져오기
            var userResponse = await client.GetAsync($"{baseUrl}/api/User/getUser?userId={_currentUser.Id}");
            if (userResponse.IsSuccessStatusCode)
            {
                var userJson = await userResponse.Content.ReadAsStringAsync();
                var user = JsonConvert.DeserializeObject<UserData>(userJson);

                if (user?.JoinedRoomIds != null)
                {
                    foreach (var roomId in user.JoinedRoomIds)
                    {
                        // 각 채팅방에 SignalR 그룹 가입
                        await _hubConnection.InvokeAsync("JoinRoom", roomId, _currentUser.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"채팅방 가입 오류: {ex.Message}");
        }
    }

    private async Task<string> GetRoomName(string roomId)
    {
        try
        {
            using HttpClient client = new HttpClient();
            string baseUrl = AppSettings.GetServerUrl();

            var chatListResponse = await client.GetAsync($"{baseUrl}/api/User/getChatList");
            if (chatListResponse.IsSuccessStatusCode)
            {
                var chatListJson = await chatListResponse.Content.ReadAsStringAsync();
                var allRooms = JsonConvert.DeserializeObject<List<RoomInfo>>(chatListJson);

                var room = allRooms?.FirstOrDefault(r => r.RoomId == roomId);
                return room?.RoomName ?? "채팅방";
            }

            return "채팅방";
        }
        catch
        {
            return "채팅방";
        }
    }

    public class RoomInfo
    {
        [JsonProperty("Name")]
        public string RoomName { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
    }
}