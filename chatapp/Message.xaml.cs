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
using IOPath = System.IO.Path;
using Path = System.Windows.Shapes.Path;
// 네임스페이스 충돌 해결을 위한 별칭 설정
using System.Windows.Media.Effects;

namespace chatapp
{
    public partial class Message : Window
    {
        private readonly HubConnection _hubConnection;
        private UserData _currentUser;
        private string _roomId;
        private Dictionary<string, string> _userNames = new Dictionary<string, string>();
        private Dictionary<string, string> _userProfiles = new Dictionary<string, string>();
        private HubConnection _connection;
        private List<ChatMessage> _chatHistory = new List<ChatMessage>();
        private DispatcherTimer _loadingDotsTimer;
        private int _onlineCount = 0;
        public string CurrentRoomId { get; private set; }

        // 페이징 관련 변수
        private bool _isLoadingMessages = false;
        private bool _hasMoreMessages = true;
        private int _pageSize = 30;
        private DateTime? _oldestMessageTime = null;
        private bool _initialLoading = true;
        private bool _isScrollEventEnabled = true;

        public Message(UserData user, string roomId)
        {
            InitializeComponent();
            _currentUser = user ?? throw new ArgumentNullException(nameof(user));
            _roomId = roomId ?? throw new ArgumentNullException(nameof(roomId));

            _chatHistory = new List<ChatMessage>(200);

            // SignalR 설정...
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{AppSettings.GetServerUrl()}/chathub")
                .Build();

            // 메시지 수신 이벤트 처리 
            _hubConnection.On<string, string>("ReceiveMessage", (senderId, message) =>
            {
                // UI 업데이트 코드...

                // 다른 사용자가 보낸 메시지면 알림 표시
                if (senderId != _currentUser.Id)
                {
                    // 채팅방 이름 가져오기
                    Task.Run(async () =>
                    {
                        try
                        {
                            string roomName = await GetRoomName(CurrentRoomId);
                            // 알림 표시
                            NotificationManager.Instance.ShowNotification(
                                roomName,
                                message,
                                CurrentRoomId,
                                _currentUser);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"알림 표시 오류: {ex.Message}");
                        }
                    });
                }
            });
        }
        // 채팅방 이름 가져오기 메서드
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 스크롤 이벤트 추가
            ChatScrollViewer.ScrollChanged += ChatScrollViewer_ScrollChanged;

            // 채팅방 이름 표시
            SetRoomTitle();

            // 사용자 이름 로드
            LoadUserNames();

            // 채팅 내역 로드
            LoadChatFromServer();

            // SignalR 연결
            ConnectToSignalR();

            // 로딩 애니메이션 설정
            InitializeLoadingAnimation();

            // 상단바 초기화
            InitializeTopBar();

            CurrentRoomId = _roomId;
        }
        private void InitializeTopBar()
        {
            // 초기 상태 설정
            _isTopBarExpanded = false;
            TopBarBorder.Height = _defaultTopBarHeight;

            // 확장 아이콘 초기화
            ExpandIcon.RenderTransformOrigin = new Point(0.5, 0.5);
            ExpandIcon.RenderTransform = new RotateTransform(0);

            // 메뉴 패널 초기화
            ExtendedMenuPanel.Visibility = Visibility.Collapsed;
            ExtendedMenuPanel.Opacity = 0;
            // 디버깅 메시지
            Console.WriteLine("상단바 초기화 완료");
        }
        private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_isScrollEventEnabled)
                return;

            // 스크롤이 상단에 가까워지면 이전 메시지 로드
            if (e.VerticalOffset < 50 && !_isLoadingMessages && _hasMoreMessages && !_initialLoading)
            {
                LoadMoreMessages();
            }
        }

        private void InitializeLoadingAnimation()
        {
            _loadingDotsTimer = new DispatcherTimer();
            _loadingDotsTimer.Interval = TimeSpan.FromMilliseconds(500);
            _loadingDotsTimer.Tick += (s, e) =>
            {
                if (LoadingDots.Text == "...")
                    LoadingDots.Text = "";
                else if (LoadingDots.Text == "")
                    LoadingDots.Text = ".";
                else if (LoadingDots.Text == ".")
                    LoadingDots.Text = "..";
                else
                    LoadingDots.Text = "...";
            };
        }

        private async void LoadUserNames()
        {
            try
            {
                // API 클라이언트를 사용해 모든 사용자 정보 가져오기
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = $"{AppSettings.GetServerUrl()}/api/User/getAllUsers";

                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var users = JsonConvert.DeserializeObject<List<UserData>>(jsonResponse) ?? new List<UserData>();

                        // 사용자 ID와 이름의 딕셔너리 생성
                        _userNames = users.ToDictionary(u => u.Id, u => u.Name);

                        // 현재 채팅방에 참여 중인 사용자 수 계산
                        _onlineCount = users.Count(u => u.JoinedRoomIds.Contains(_roomId));

                    }
                    else
                    {
                        MessageBox.Show($"사용자 정보를 불러오는데 실패했습니다: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"사용자 정보를 불러오는 중 오류가 발생했습니다: {ex.Message}");
            }
        }

       
        // URL이 이미지인지 확인하는 도우미 메소드
        private bool IsImageUrl(string url)
        {
            string extension = System.IO.Path.GetExtension(url).ToLower();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                   extension == ".bmp" || extension == ".webp";
        }
        // URL이 동영상인지 확인하는 도우미 메소드
        private bool IsVideoUrl(string url)
        {
            string extension = System.IO.Path.GetExtension(url).ToLower();
            return extension == ".mp4" || extension == ".mov" || extension == ".avi" ||
                   extension == ".mkv" || extension == ".wmv" || extension == ".gif";
        }
        private void ProcessMediaMessage(string senderId, string mediaUrl, DateTime timestamp)
        {
            if (IsImageUrl(mediaUrl))
            {
                CreateImageBubble(senderId, mediaUrl, timestamp);
            }
            else if (IsVideoUrl(mediaUrl))
            {
                CreateVideoBubble(senderId, mediaUrl, timestamp);
            }
            else
            {
                // 일반 URL 또는 다른 형식의 파일인 경우
                CreateChatBubble(senderId, mediaUrl, timestamp);
            }
        }
        private async void LoadChatFromServer()
        {
            try
            {
                _isLoadingMessages = true;
                _initialLoading = true;
                _isScrollEventEnabled = false;

                // 로딩 표시
                TextBlock loadingIndicator = new TextBlock
                {
                    Text = "채팅 로딩 중...",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                };
                ChatStack.Children.Add(loadingIndicator);

                using HttpClient client = new HttpClient();
                // 페이지 크기를 더 작게 설정 (100→30)
                var pageSize = 30;

                // 최신 메시지만 요청
                var response = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/loadMessages?roomId={_roomId}&count={pageSize}&latest=true");

                // 로딩 표시 제거
                ChatStack.Children.Clear();

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var chatHistory = JsonConvert.DeserializeObject<List<ChatMessage>>(json);

                    if (chatHistory == null || chatHistory.Count == 0)
                    {
                        // 환영 메시지 추가
                        AddSystemMessage("채팅방에 오신 것을 환영합니다!");
                        _hasMoreMessages = false;
                        _isLoadingMessages = false;
                        _initialLoading = false;
                        _isScrollEventEnabled = true;
                        return;
                    }

                    // 더 불러올 메시지가 있는지 확인
                    _hasMoreMessages = chatHistory.Count >= pageSize;

                    // 가장 오래된 메시지 시간 저장
                    if (chatHistory.Count > 0)
                    {
                        _oldestMessageTime = chatHistory.Min(m => m.Timestamp);
                    }

                    // 채팅 히스토리에 추가 (최대 개수 제한)
                    _chatHistory.Clear();
                    foreach (var chat in chatHistory)
                    {
                        if (string.IsNullOrWhiteSpace(chat.Message))
                            continue;

                        _chatHistory.Add(chat);
                    }

                    // 날짜별 그룹화 준비
                    DateTime? lastDay = null;

                    // UI에 메시지 추가
                    foreach (var chat in chatHistory.OrderBy(m => m.Timestamp)) // 날짜순 정렬
                    {
                        if (string.IsNullOrWhiteSpace(chat.Message))
                            continue;

                        // 날짜가 바뀌면 날짜 구분선 추가
                        DateTime messageDay = chat.Timestamp.Date;
                        if (lastDay == null || lastDay.Value.Date != messageDay)
                        {
                            var dateGrid = CreateDateSeparator(messageDay);
                            ChatStack.Children.Add(dateGrid);
                            lastDay = messageDay;
                        }

                        // 메시지 종류에 따라 다른 UI 요소 생성
                        UIElement messageElement;
                        if (Uri.IsWellFormedUriString(chat.Message, UriKind.Absolute))
                        {
                            if (IsImageUrl(chat.Message))
                            {
                                messageElement = CreateImageBubble(chat.Sender, chat.Message, chat.Timestamp);
                            }
                            else if (IsVideoUrl(chat.Message))
                            {
                                messageElement = CreateVideoBubble(chat.Sender, chat.Message, chat.Timestamp);
                            }
                            else
                            {
                                messageElement = CreateChatBubble(chat.Sender, chat.Message, chat.Timestamp);
                            }
                        }
                        else
                        {
                            messageElement = CreateChatBubble(chat.Sender, chat.Message, chat.Timestamp);
                        }

                        ChatStack.Children.Add(messageElement);
                    }

                    // 초기 로딩 시 스크롤을 아래로 이동
                    await Task.Delay(100); // UI 업데이트 대기
                    ChatScrollViewer.ScrollToEnd();

                    // 이전 메시지가 더 있다면 상단에 로드 버튼 표시
                    if (_hasMoreMessages)
                    {
                        Button loadMoreButton = new Button
                        {
                            Content = "이전 메시지 로드",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 10, 0, 10),
                            Padding = new Thickness(10, 5, 10, 5),
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                            BorderThickness = new Thickness(0),
                            Cursor = Cursors.Hand
                        };

                        loadMoreButton.Click += (s, e) => LoadMoreMessages();

                        ChatStack.Children.Insert(0, loadMoreButton);
                    }
                }
                else
                {
                    ShowErrorMessage("채팅 불러오기 실패: " + await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("서버 오류: " + ex.Message);
            }
            finally
            {
                _isLoadingMessages = false;
                _initialLoading = false;
                _isScrollEventEnabled = true;
            }
        }
        private void AddVideoBubble(string senderId, string videoUrl, DateTime timestamp)
        {
            UIElement container = CreateVideoBubble(senderId, videoUrl, timestamp);
            if (container != null)
            {
                ChatStack.Children.Add(container);
                ChatScrollViewer.ScrollToEnd();
            }
        }

        private UIElement CreateVideoBubble(string senderId, string videoUrl, DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
                return null;

            string senderName = _userNames.ContainsKey(senderId) ? _userNames[senderId] : senderId;
            bool isCurrentUser = senderId == _currentUser.Id;

            // 메시지 컨테이너
            Grid container = new Grid
            {
                Margin = new Thickness(isCurrentUser ? 5 : 0, 4, isCurrentUser ? 0 : 5, 4),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            StackPanel bubbleStack = new StackPanel();

            // 이름 표시 (현재 사용자 메시지가 아닌 경우만)
            if (!isCurrentUser)
            {
                TextBlock nameBlock = new TextBlock
                {
                    Text = senderName,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Margin = new Thickness(10, 0, 0, 3),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"))
                };
                bubbleStack.Children.Add(nameBlock);
            }

            // 비디오 버블
            Border bubble = new Border
            {
                Background = isCurrentUser
                    ? (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A86E8"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9E9EB")),
                CornerRadius = isCurrentUser
                    ? new CornerRadius(18, 5, 18, 18)
                    : new CornerRadius(5, 18, 18, 18),
                Padding = new Thickness(4),
                Margin = new Thickness(isCurrentUser ? 0 : 10, 0, isCurrentUser ? 10 : 0, 0),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };

            // 그림자 효과
            bubble.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.1,
                Direction = 270
            };

            // 비디오 컨테이너 그리드 (썸네일 + 플레이 버튼)
            Grid videoGrid = new Grid
            {
                Width = 200,
                Height = 150
            };

            // 썸네일 배경 (회색)
            Rectangle thumbnailBg = new Rectangle
            {
                Fill = new SolidColorBrush(Colors.Black),
                Opacity = 0.1
            };
            videoGrid.Children.Add(thumbnailBg);

            // 비디오 타입 아이콘 표시
            TextBlock videoIcon = new TextBlock
            {
                Text = "🎬",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            videoGrid.Children.Add(videoIcon);

            // 파일명 표시
            TextBlock fileNameBlock = new TextBlock
            {
                Text = System.IO.Path.GetFileName(videoUrl),
                FontSize = 12,
                Foreground = isCurrentUser ? Brushes.White : Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(5, 0, 5, 5)
            };
            videoGrid.Children.Add(fileNameBlock);

            // 재생 버튼
            Border playButton = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)),
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(25),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock playIcon = new TextBlock
            {
                Text = "▶",
                FontSize = 18,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            playButton.Child = playIcon;
            videoGrid.Children.Add(playButton);

            bubble.Child = videoGrid;

            // 클릭 시 전체 뷰어로 열기
            videoGrid.MouseLeftButtonUp += (s, e) =>
            {
                var viewer = new MediaViewerWindow(videoUrl);
                viewer.ShowDialog();
            };

            // 시간 표시 추가
            TextBlock timeBlock = new TextBlock
            {
                Text = timestamp.ToString("HH:mm"),
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(isCurrentUser ? 0 : 5, 5, isCurrentUser ? 5 : 0, 0),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // 버블 및 시간 추가
            bubbleStack.Children.Add(bubble);
            bubbleStack.Children.Add(timeBlock);
            container.Children.Add(bubbleStack);

            // 애니메이션 준비 및 시작
            container.Opacity = 0;
            container.RenderTransform = new TranslateTransform(isCurrentUser ? 20 : -20, 0);

            DoubleAnimation opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            DoubleAnimation translateAnimation = new DoubleAnimation(isCurrentUser ? 20 : -20, 0, TimeSpan.FromSeconds(0.3));

            opacityAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            translateAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            container.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            (container.RenderTransform as TranslateTransform).BeginAnimation(TranslateTransform.XProperty, translateAnimation);

            return container;
        }
        private async void LoadMoreMessages()
        {
            if (_isLoadingMessages || !_hasMoreMessages || !_oldestMessageTime.HasValue)
                return;

            try
            {
                _isLoadingMessages = true;
                _isScrollEventEnabled = false;

                // 로딩 표시 추가
                TextBlock loadingIndicator = new TextBlock
                {
                    Text = "이전 메시지 로딩 중...",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                // 이전 로드 버튼 제거 (있다면)
                UIElement oldButton = null;
                if (ChatStack.Children.Count > 0 && ChatStack.Children[0] is Button)
                {
                    oldButton = ChatStack.Children[0];
                    ChatStack.Children.RemoveAt(0);
                }

                // 스크롤 위치 정보 저장
                double currentOffset = ChatScrollViewer.VerticalOffset;
                double currentHeight = ChatScrollViewer.ExtentHeight;
                int initialChildCount = ChatStack.Children.Count;

                ChatStack.Children.Insert(0, loadingIndicator);

                // 이전 메시지 요청 (oldestMessageTime 이전의 메시지)
                // 페이지 크기를 30으로 설정
                var pageSize = 30;
                using HttpClient client = new HttpClient();
                string before = _oldestMessageTime.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff");
                var response = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/loadMessages?roomId={_roomId}&count={pageSize}&before={before}");

                // 로딩 표시 제거
                ChatStack.Children.Remove(loadingIndicator);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var olderMessages = JsonConvert.DeserializeObject<List<ChatMessage>>(json);

                    if (olderMessages == null || olderMessages.Count == 0)
                    {
                        _hasMoreMessages = false;
                        // 더 이상 메시지가 없다는 표시 추가
                        TextBlock noMoreMessages = new TextBlock
                        {
                            Text = "이전 메시지가 없습니다",
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 10, 0, 10)
                        };
                        ChatStack.Children.Insert(0, noMoreMessages);
                        return;
                    }

                    // 가장 오래된 메시지 시간 업데이트
                    if (olderMessages.Count > 0)
                    {
                        _oldestMessageTime = olderMessages.Min(m => m.Timestamp);
                    }

                    // 더 불러올 메시지가 있는지 확인
                    _hasMoreMessages = olderMessages.Count >= pageSize;

                    // 채팅 히스토리에 추가 (최대 개수 제한)
                    foreach (var message in olderMessages.OrderByDescending(m => m.Timestamp)) // 최신순 정렬 후 역순으로 추가
                    {
                        if (string.IsNullOrWhiteSpace(message.Message))
                            continue;

                        _chatHistory.Insert(0, message); // 목록 앞에 추가
                    }

                    // 표시할 최대 메시지 수 제한 (메모리 관리)
                    int maxMessagesInMemory = 200;
                    if (_chatHistory.Count > maxMessagesInMemory)
                    {
                        // 가장 오래된 메시지 제거
                        int itemsToRemove = _chatHistory.Count - maxMessagesInMemory;
                        _chatHistory.RemoveRange(0, itemsToRemove);
                    }

                    // 날짜별 그룹화 및 UI에 메시지 추가
                    int insertIndex = 0;
                    DateTime? lastDay = null;

                    foreach (var chat in olderMessages.OrderBy(m => m.Timestamp)) // 날짜순 정렬
                    {
                        if (string.IsNullOrWhiteSpace(chat.Message))
                            continue;

                        // 날짜가 바뀌면 날짜 구분선 추가
                        DateTime messageDay = chat.Timestamp.Date;
                        if (lastDay == null || lastDay.Value.Date != messageDay)
                        {
                            var dateGrid = CreateDateSeparator(messageDay);
                            ChatStack.Children.Insert(insertIndex, dateGrid);
                            insertIndex++;
                            lastDay = messageDay;
                        }

                        // 메시지 추가
                        UIElement messageElement;
                        if (Uri.IsWellFormedUriString(chat.Message, UriKind.Absolute))
                        {
                            if (IsImageUrl(chat.Message))
                            {
                                messageElement = CreateImageBubble(chat.Sender, chat.Message, chat.Timestamp);
                            }
                            else if (IsVideoUrl(chat.Message))
                            {
                                messageElement = CreateVideoBubble(chat.Sender, chat.Message, chat.Timestamp);
                            }
                            else
                            {
                                messageElement = CreateChatBubble(chat.Sender, chat.Message, chat.Timestamp);
                            }
                        }
                        else
                        {
                            messageElement = CreateChatBubble(chat.Sender, chat.Message, chat.Timestamp);
                        }

                        ChatStack.Children.Insert(insertIndex, messageElement);
                        insertIndex++;
                    }

                    // 이전 메시지가 더 있다면 상단에 로드 버튼 추가
                    if (_hasMoreMessages)
                    {
                        Button loadMoreButton = new Button
                        {
                            Content = "이전 메시지 로드",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 10, 0, 10),
                            Padding = new Thickness(10, 5, 10, 5),
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                            BorderThickness = new Thickness(0),
                            Cursor = Cursors.Hand
                        };

                        loadMoreButton.Click += (s, e) => LoadMoreMessages();

                        ChatStack.Children.Insert(0, loadMoreButton);
                    }

                    // 스크롤 위치 유지
                    await Task.Delay(50); // UI 업데이트 대기
                    double newHeight = ChatScrollViewer.ExtentHeight;
                    double delta = newHeight - currentHeight;
                    ChatScrollViewer.ScrollToVerticalOffset(currentOffset + delta);
                }
                else
                {
                    // 오류 발생 시 이전 로드 버튼 복원
                    if (oldButton != null)
                    {
                        ChatStack.Children.Insert(0, oldButton);
                    }

                    ShowErrorMessage("이전 메시지 불러오기 실패: " + await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("이전 메시지 로딩 오류: " + ex.Message);
            }
            finally
            {
                _isLoadingMessages = false;
                _isScrollEventEnabled = true;
            }
        }

        private void AddDateSeparator(DateTime date)
        {
            UIElement dateGrid = CreateDateSeparator(date);
            ChatStack.Children.Add(dateGrid);
        }

        private UIElement CreateDateSeparator(DateTime date)
        {
            // 오늘 날짜인지 확인
            string dateText;
            if (date.Date == DateTime.Now.Date)
            {
                dateText = "오늘";
            }
            else if (date.Date == DateTime.Now.Date.AddDays(-1))
            {
                dateText = "어제";
            }
            else
            {
                dateText = date.ToString("yyyy년 M월 d일");
            }

            // 날짜 구분선 생성
            Grid dateGrid = new Grid { Margin = new Thickness(0, 15, 0, 15) };

            // 왼쪽 선
            Border leftLine = new Border
            {
                Height = 1,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
                Width = 70,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 오른쪽 선
            Border rightLine = new Border
            {
                Height = 1,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
                Width = 70,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 날짜 텍스트
            TextBlock dateBlock = new TextBlock
            {
                Text = dateText,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            dateGrid.Children.Add(leftLine);
            dateGrid.Children.Add(rightLine);
            dateGrid.Children.Add(dateBlock);

            // 애니메이션 효과
            dateGrid.Opacity = 0;
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            dateGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            return dateGrid;
        }

        private void AddSystemMessage(string message)
        {
            // 시스템 메시지 컨테이너
            Border container = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(30, 10, 30, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 메시지 텍스트
            TextBlock textBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                TextAlignment = TextAlignment.Center
            };

            container.Child = textBlock;

            // 애니메이션 효과
            container.Opacity = 0;
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            container.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            ChatStack.Children.Add(container);
            ChatScrollViewer.ScrollToEnd();
        }

        private async Task SendChatToServer(string senderId, string chatting)
        {
            try
            {
                // 전송 중 표시자 활성화
                ShowSendingIndicator(true);

                if (_connection is { State: HubConnectionState.Connected })
                {
                    await _connection.InvokeAsync("SendMessage", _roomId, senderId, chatting);

                    // 전송 중 표시자 비활성화
                    ShowSendingIndicator(false);
                }
                else
                {
                    ShowSendingIndicator(false);
                    ShowErrorMessage("서버에 연결되어 있지 않습니다.");
                }
            }
            catch (Exception ex)
            {
                ShowSendingIndicator(false);
                ShowErrorMessage("메시지 전송 실패: " + ex.Message);
            }
        }

        // 전송 중 표시자 메서드 수정
        private void ShowSendingIndicator(bool isVisible, string message = "전송 중")
        {
            if (isVisible)
            {
                SendingMessage.Text = message; // 새로 추가할 TextBlock
                SendingIndicator.Visibility = Visibility.Visible;
                _loadingDotsTimer.Start();

                // 페이드인 애니메이션
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
                SendingIndicator.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            else
            {
                _loadingDotsTimer.Stop();

                // 페이드아웃 애니메이션
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
                fadeOut.Completed += (s, e) => SendingIndicator.Visibility = Visibility.Collapsed;
                SendingIndicator.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        // 진행 상태 업데이트 메서드 추가
        private void UpdateSendingIndicator(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                SendingMessage.Text = message;
            });
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            // 메시지 전송 애니메이션
            var button = sender as Button;
            if (button != null)
            {
                var originalWidth = button.Width;
                var originalContent = button.Content;

                button.Content = "✓";
                DoubleAnimation widthAnimation = new DoubleAnimation
                {
                    From = originalWidth,
                    To = 40,
                    Duration = TimeSpan.FromSeconds(0.2),
                    AutoReverse = true
                };
                widthAnimation.Completed += (s, args) => button.Content = originalContent;
                button.BeginAnimation(Button.WidthProperty, widthAnimation);
            }

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

        private async void LoadProfileImage(string userId, Ellipse profileImage)
        {
            try
            {
                // 사용자 정보 로드 (프로필 이미지 URL 가져오기)
                string profileImageUrl = await GetUserProfileImageUrl(userId);

                if (!string.IsNullOrEmpty(profileImageUrl))
                {
                    // UI 스레드에서 이미지 설정
                    using (var client = new HttpClient())
                    {
                        try
                        {
                            var response = await client.GetAsync(profileImageUrl);
                            if (response.IsSuccessStatusCode)
                            {
                                var imageData = await response.Content.ReadAsByteArrayAsync();

                                this.Dispatcher.Invoke(() =>
                                {
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    using (var ms = new MemoryStream(imageData))
                                    {
                                        bitmap.StreamSource = ms;
                                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmap.EndInit();
                                        bitmap.Freeze();
                                    }

                                    ImageBrush brush = new ImageBrush();
                                    brush.ImageSource = bitmap;
                                    profileImage.Fill = brush;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"프로필 이미지 로드 중 오류: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"프로필 이미지 로드 실패: {ex.Message}");
            }
        }

        // 사용자의 프로필 이미지 URL을 가져오는 메서드
        private async Task<string> GetUserProfileImageUrl(string userId)
        {
            try
            {
                // 먼저 로컬 캐시에서 확인
                if (_userProfiles.TryGetValue(userId, out string cachedUrl))
                {
                    return cachedUrl;
                }

                // 서버에서 사용자 정보 조회
                using HttpClient client = new HttpClient();
                string serverUrl = AppSettings.GetServerUrl();
                var response = await client.GetAsync($"{serverUrl}/api/User/getUser?userId={userId}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<UserData>(json);

                    if (user != null && !string.IsNullOrEmpty(user.ProfileImage))
                    {
                        string profileUrl = user.ProfileImage;

                        // 완전한 URL인지 확인
                        if (Uri.IsWellFormedUriString(profileUrl, UriKind.Absolute))
                        {
                            // 이미 완전한 URL
                        }
                        // 파일명만 있는 경우
                        else if (profileUrl.StartsWith("profile_"))
                        {
                            profileUrl = $"{serverUrl}/profiles/{profileUrl}";
                            Console.WriteLine($"프로필 URL 생성: {profileUrl}");
                        }

                        // 이미지 URL이 실제로 접근 가능한지 확인
                        try
                        {
                            var imageResponse = await client.GetAsync(profileUrl);
                            if (imageResponse.IsSuccessStatusCode)
                            {
                                // 캐시에 저장
                                _userProfiles[userId] = profileUrl;
                                return profileUrl;
                            }
                            else
                            {
                                Console.WriteLine($"프로필 이미지 접근 실패: {imageResponse.StatusCode}");
                                return string.Empty; // 이미지 접근 실패시 기본 이미지 사용
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"프로필 이미지 확인 중 오류: {ex.Message}");
                            return string.Empty;
                        }
                    }
                }

                return string.Empty; // 기본 이미지 사용
            }
            catch (Exception ex)
            {
                Console.WriteLine($"사용자 프로필 조회 실패: {ex.Message}");
                return string.Empty;
            }
        }

        private UIElement CreateChatBubble(string senderId, string message, DateTime timestamp)
        {
            string senderName = _userNames.ContainsKey(senderId) ? _userNames[senderId] : senderId;
            bool isCurrentUser = senderId == _currentUser.Id;

            // 메시지 컨테이너
            Grid container = new Grid
            {
                Margin = new Thickness(isCurrentUser ? 5 : 0, 4, isCurrentUser ? 0 : 5, 4),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            StackPanel bubbleStack = new StackPanel();

            // 이름 표시 (현재 사용자 메시지가 아닌 경우만)
            if (!isCurrentUser)
            {
                TextBlock nameBlock = new TextBlock
                {
                    Text = senderName,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Margin = new Thickness(10, 0, 0, 3),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"))
                };
                bubbleStack.Children.Add(nameBlock);
            }

            // 버블 및 시간 래퍼
            Grid bubbleContainer = new Grid();
            bubbleContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bubbleContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 애플 iMessage 스타일 색상으로 통일
            Color myMessageColor = (Color)ColorConverter.ConvertFromString("#34C759"); // Apple 녹색 (iMessage 스타일)
            Color otherMessageColor = (Color)ColorConverter.ConvertFromString("#E9E9EB"); // Apple 회색 (iMessage 스타일)

            // 메시지 버블
            Border bubble = new Border
            {
                Background = isCurrentUser
                    ? new SolidColorBrush(myMessageColor)
                    : new SolidColorBrush(otherMessageColor),
                CornerRadius = isCurrentUser
                    ? new CornerRadius(18, 5, 18, 18)
                    : new CornerRadius(5, 18, 18, 18),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(isCurrentUser ? 0 : 10, 0, isCurrentUser ? 10 : 0, 0),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 280
            };

            // 그림자 효과 추가 (애플 스타일)
            bubble.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 6,
                ShadowDepth = 1,
                Opacity = 0.1,
                Direction = 270
            };

            // 메시지 텍스트
            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                Foreground = isCurrentUser ? Brushes.White : Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            };

            bubble.Child = messageBlock;

            // 시간 표시 추가
            TextBlock timeBlock = new TextBlock
            {
                Text = timestamp.ToString("HH:mm"),
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E93")), // Apple 텍스트 색상
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(isCurrentUser ? 0 : 5, 0, isCurrentUser ? 5 : 0, 3),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // 시간을 버블 옆에 배치
            if (isCurrentUser)
            {
                timeBlock.Margin = new Thickness(0, 0, 5, 3);
                Grid.SetColumn(timeBlock, 0);
                Grid.SetColumn(bubble, 1);
            }
            else
            {
                timeBlock.Margin = new Thickness(5, 0, 0, 3);
                Grid.SetColumn(timeBlock, 1);
                Grid.SetColumn(bubble, 0);
            }

            // 컨테이너에 버블과 시간 추가
            bubbleContainer.Children.Add(bubble);
            bubbleContainer.Children.Add(timeBlock);

            // 레이아웃 추가
            bubbleStack.Children.Add(bubbleContainer);
            container.Children.Add(bubbleStack);

            // 애니메이션 준비
            container.Opacity = 0;
            container.RenderTransform = new TranslateTransform(isCurrentUser ? 20 : -20, 0);

            // 애니메이션 시작
            DoubleAnimation opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            DoubleAnimation translateAnimation = new DoubleAnimation(isCurrentUser ? 20 : -20, 0, TimeSpan.FromSeconds(0.3));

            opacityAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            translateAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            container.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            (container.RenderTransform as TranslateTransform).BeginAnimation(TranslateTransform.XProperty, translateAnimation);

            return container;
        }

        private async void ConnectToSignalR()
        {
            try
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl($"{AppSettings.GetServerUrl()}/chathub")
                    .WithAutomaticReconnect()
                    .Build();

                // 최대 메시지 크기 설정
                _connection.ServerTimeout = TimeSpan.FromMinutes(2);

                // 기존 메시지 이벤트 핸들러
                _connection.On<string, string>("ReceiveMessage", async (senderId, message) =>
                {
                    Dispatcher.Invoke(() => LoadChatFromServer());
                    // 다른 사람이 보낸 메시지이고, 현재 창이 활성화되지 않은 경우에만 알림 표시
                    if (senderId != _currentUser.Id && !IsActive)
                    {
                        string roomName = await GetRoomNameAsync(CurrentRoomId);
                        NotificationManager.Instance.ShowNotification(roomName, message, CurrentRoomId, _currentUser);

                        // UI 스레드에서 알림 창 표시
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var notification = new NotificationWindow(
                                roomName, // 채팅방 이름 
                                message,           // 메시지 내용
                                CurrentRoomId,     // 방 ID
                                _currentUser       // 현재 사용자
                            );
                            notification.Show();
                        });
                    }
                });

                _connection.On<string, string>("ReceiveImage", (senderId, imageUrl) =>
                {
                    Dispatcher.Invoke(() => AddImageBubble(senderId, imageUrl, DateTime.Now));
                });

                // 동영상 수신 이벤트 추가
                _connection.On<string, string>("ReceiveVideo", (senderId, videoUrl) =>
                {
                    Dispatcher.Invoke(() => AddVideoBubble(senderId, videoUrl, DateTime.Now));
                });

                // 메시지 삭제 이벤트 핸들러를 간소화
                _connection.On("RefreshMessages", () =>
                {
                    Console.WriteLine("메시지 삭제 이벤트 수신됨 - 채팅 새로고침");
                    Dispatcher.Invoke(() =>
                    {
                        // 전체 채팅을 다시 로드 (수정된 메시지 포함)
                        LoadChatFromServer();

                        // 시스템 메시지 추가 부분 제거
                        // AddSystemMessage("관리자에 의해 메시지가 삭제되었습니다."); <- 이 줄 제거
                    });
                });

                await _connection.StartAsync();
                await _connection.InvokeAsync("JoinRoom", _roomId, _currentUser.Id);

                // 연결 성공 메시지
                AddSystemMessage("채팅 서버에 연결되었습니다.");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"채팅 서버 연결 실패: {ex.Message}");
            }
        }
        // 채팅방 이름을 가져오는 메서드 추가
        private async Task<string> GetRoomNameAsync(string roomId)
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

                    // 채팅방 정보 찾기
                    var room = allRooms?.FirstOrDefault(r => r.RoomId == roomId);
                    if (room != null)
                    {
                        return room.RoomName;
                    }
                }

                // 채팅방 정보를 찾지 못했을 경우 기본값 반환
                return "채팅방";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"채팅방 이름 가져오기 실패: {ex.Message}");
                return "채팅방"; // 오류 발생 시 기본값 반환
            }
        }
        // 메시지 삭제 처리 메서드
        private void RemoveMessageFromUI(string roomId, string senderId, DateTime timestamp)
        {
            // 1. 채팅 내역에서 해당 메시지 찾기
            var message = _chatHistory.FirstOrDefault(m =>
                m.RoomId == roomId &&
                m.Sender == senderId &&
                Math.Abs((m.Timestamp - timestamp).TotalSeconds) < 1); // 1초 이내 오차 허용

            if (message != null)
            {
                // 내역에서 메시지 제거
                _chatHistory.Remove(message);

                // 2. UI에서 해당 메시지 찾기 및 제거
                int indexToRemove = -1;

                for (int i = 0; i < ChatStack.Children.Count; i++)
                {
                    var element = ChatStack.Children[i];

                    // 메시지 요소의 Tag 속성에 메시지 정보가 저장되어 있지 않으므로,
                    // 여기서는 완벽한 처리가 어려움. 채팅을 새로 로드하는 방식으로 구현
                }

                // 채팅 다시 로드
                LoadChatFromServer();
            }
        }
        private void ToggleSidePanel_Click(object sender, RoutedEventArgs e)
        {
            // 블러 오버레이 표시
            BlurOverlay.Visibility = Visibility.Visible;
            BlurOverlay.Opacity = 0;

            // 메인 컨텐츠에 블러 효과 적용 (안전하게)
            bool blurApplied = TryApplyBlurEffect(ChatStack, 10);

            // 사이드 패널 표시
            SidePanel.Visibility = Visibility.Visible;


            // 오버레이 페이드 인
            DoubleAnimation fadeIn = new DoubleAnimation(0, 0.5, TimeSpan.FromSeconds(0.3));
            BlurOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 슬라이드인 애니메이션
            ThicknessAnimation slideIn = new ThicknessAnimation
            {
                From = new Thickness(-280, 0, 0, 0),
                To = new Thickness(0, 0, 0, 0),
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SidePanel.BeginAnimation(Grid.MarginProperty, slideIn);
        }

        private void CloseSidePanel_Click(object sender, RoutedEventArgs e)
        {
            // 블러 효과 제거 애니메이션
            if (ChatStack.Effect is BlurEffect effect)
            {
                DoubleAnimation blurAnimation = new DoubleAnimation(10, 0, TimeSpan.FromSeconds(0.3));
                blurAnimation.Completed += (s, args) => ChatStack.Effect = null;
                effect.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);
            }

            // 오버레이 페이드 아웃
            DoubleAnimation fadeOut = new DoubleAnimation(0.5, 0, TimeSpan.FromSeconds(0.3));
            fadeOut.Completed += (s, args) => BlurOverlay.Visibility = Visibility.Collapsed;
            BlurOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            // 슬라이드아웃 애니메이션
            ThicknessAnimation slideOut = new ThicknessAnimation
            {
                From = new Thickness(0, 0, 0, 0),
                To = new Thickness(-280, 0, 0, 0),
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            slideOut.Completed += (s, args) => SidePanel.Visibility = Visibility.Collapsed;
            SidePanel.BeginAnimation(Grid.MarginProperty, slideOut);
        }
        // 배경 오버레이 클릭 시 사이드 패널 닫기
        private void BackgroundOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            CloseSidePanel_Click(null, null);
        }
        private bool TryApplyBlurEffect(UIElement element, double radius)
        {
            try
            {
                // 이미 블러 효과가 있는지 확인
                BlurEffect currentEffect = element.Effect as BlurEffect;

                if (currentEffect == null)
                {
                    currentEffect = new BlurEffect { Radius = 0 };
                    element.Effect = currentEffect;
                }

                // 애니메이션 적용
                DoubleAnimation blurAnimation = new DoubleAnimation(
                    currentEffect.Radius, radius, TimeSpan.FromSeconds(0.3));
                currentEffect.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);

                return true;
            }
            catch (Exception)
            {
                // 블러 효과를 지원하지 않는 경우 대체 효과 적용
                // 예: 투명도만 변경
                element.Opacity = radius > 0 ? 0.7 : 1.0;
                return false;
            }
        }
        private void OpenProfile_Click(object sender, MouseButtonEventArgs e)
        {
            // 사이드 패널 닫기
            CloseSidePanel_Click(null, null);

            // 프로필 창 열기
            Profile profileWindow = new Profile(_currentUser);

            // 페이드 아웃 애니메이션
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0.7,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            profileWindow.Owner = this;
            profileWindow.ShowDialog();

            // 페이드 인 애니메이션
            var fadeIn = new DoubleAnimation
            {
                From = 0.7,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void OpenFriend_Click(object sender, MouseButtonEventArgs e)
        {
            // 페이드 아웃 애니메이션
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };

            fadeOut.Completed += (s, args) =>
            {
                Friend friendWindow = new Friend(_currentUser, _roomId);
                friendWindow.Show();
                this.Close();
            };

            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void OpenChatList_Click(object sender, MouseButtonEventArgs e)
        {
            // 페이드 아웃 애니메이션
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };

            fadeOut.Completed += (s, args) =>
            {
                List chatListWindow = new List(_currentUser);
                chatListWindow.Show();
                this.Close();
            };

            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void OpenSettings_Click(object sender, MouseButtonEventArgs e)
        {
            CloseSidePanel_Click(null, null);
            MessageBox.Show("설정 기능은 준비 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LeaveRoom_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("정말 채팅방을 나가시겠습니까?", "채팅방 나가기",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 페이드 아웃 애니메이션
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.2)
                };

                fadeOut.Completed += (s, args) =>
                {
                    List chatListWindow = new List(_currentUser);
                    chatListWindow.Show();
                    this.Close();
                };

                this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private async void SetRoomTitle()
        {
            try
            {
                using HttpClient client = new HttpClient();
                var response = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/getChatList");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var rooms = JsonConvert.DeserializeObject<List<ChatRoom>>(json) ?? new List<ChatRoom>();
                    var room = rooms.FirstOrDefault(r => r.RoomId == _roomId);

                    if (room != null)
                    {
                        // 애니메이션으로 타이틀 변경
                        DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
                        fadeOut.Completed += (s, e) => {
                            RoomTitleText.Text = room.Name;

                            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
                            RoomTitleText.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                        };
                        RoomTitleText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    }
                    else
                    {
                        RoomTitleText.Text = "채팅방";
                    }
                }
                else
                {
                    RoomTitleText.Text = "채팅방";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"채팅방 정보를 불러오지 못했습니다: {ex.Message}");
                RoomTitleText.Text = "채팅방";
            }
        }

        private async void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "모든 미디어 파일|*.jpg;*.jpeg;*.webp;*.png;*.bmp;*.gif;*.mp4;*.mov;*.avi;*.mkv;*.wmv|이미지 파일|*.jpg;*.jpeg;*.webp;*.png;*.bmp|동영상 파일|*.mp4;*.mov;*.avi;*.gif;*.mkv;*.wmv",
                Multiselect = true // 여러 파일 선택 가능하도록 설정
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // 선택된 파일이 없으면 종료
                    if (dlg.FileNames.Length == 0)
                        return;

                    // 진행 상황 표시 업데이트
                    int totalFiles = dlg.FileNames.Length;
                    int currentFile = 0;

                    // 전송 중 표시자 활성화 및 진행 상태 표시
                    ShowSendingIndicator(true, $"파일 전송 중 (0/{totalFiles})");

                    foreach (string filePath in dlg.FileNames)
                    {
                        currentFile++;
                        UpdateSendingIndicator($"파일 전송 중 ({currentFile}/{totalFiles})");

                        using var client = new HttpClient();
                        using var form = new MultipartFormDataContent();
                        using var fs = File.OpenRead(filePath);

                        form.Add(new StreamContent(fs), "file", IOPath.GetFileName(filePath));

                        // 파일 확장자 확인
                        string extension = IOPath.GetExtension(filePath).ToLower();
                        bool isVideo = extension == ".mp4" || extension == ".mov" || extension == ".avi" ||
                                       extension == ".mkv" || extension == ".gif" || extension == ".wmv";

                        HttpResponseMessage response;

                        // 파일 타입에 따라 다른 API 호출
                        if (isVideo)
                        {
                            // 동영상 파일인 경우
                            var videoApiUrl = $"{AppSettings.GetServerUrl()}/api/File/videoupload?roomId={_roomId}&senderId={_currentUser.Id}";
                            response = await client.PostAsync(videoApiUrl, form);
                        }
                        else
                        {
                            // 이미지 파일인 경우
                            var imageApiUrl = $"{AppSettings.GetServerUrl()}/api/File/upload?roomId={_roomId}&senderId={_currentUser.Id}";
                            response = await client.PostAsync(imageApiUrl, form);
                        }

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            dynamic result = JsonConvert.DeserializeObject(json);
                            string fileUrl = result.Url;

                            // SignalR로 파일 알림은 서버에서 처리하므로 여기서는 생략
                            // 본인 채팅창에 즉시 표시 (필요시)
                            if (isVideo)
                            {
                                AddVideoBubble(_currentUser.Id, fileUrl, DateTime.Now);
                            }
                            else // 이미지
                            {
                                AddImageBubble(_currentUser.Id, fileUrl, DateTime.Now);
                            }
                        }
                        else
                        {
                            ShowErrorMessage($"파일 업로드 실패 ({currentFile}/{totalFiles}): {await response.Content.ReadAsStringAsync()}");
                        }
                    }

                    // 전송 중 표시자 비활성화
                    ShowSendingIndicator(false);
                }
                catch (Exception ex)
                {
                    ShowSendingIndicator(false);
                    ShowErrorMessage($"파일 전송 오류: {ex.Message}");
                }
            }
        }
        private void AddImageBubble(string senderId, string imageUrl, DateTime timestamp)
        {
            UIElement container = CreateImageBubble(senderId, imageUrl, timestamp);
            if (container != null)
            {
                ChatStack.Children.Add(container);
                ChatScrollViewer.ScrollToEnd();
            }
        }

        private UIElement CreateImageBubble(string senderId, string imageUrl, DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            string senderName = _userNames.ContainsKey(senderId) ? _userNames[senderId] : senderId;
            bool isCurrentUser = senderId == _currentUser.Id;

            // 메시지 컨테이너
            Grid container = new Grid
            {
                Margin = new Thickness(isCurrentUser ? 5 : 0, 4, isCurrentUser ? 0 : 5, 4),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            StackPanel bubbleStack = new StackPanel();

            // 이름 표시 (현재 사용자 메시지가 아닌 경우만)
            if (!isCurrentUser)
            {
                TextBlock nameBlock = new TextBlock
                {
                    Text = senderName,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Margin = new Thickness(10, 0, 0, 3),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"))
                };
                bubbleStack.Children.Add(nameBlock);
            }

            // 이미지 버블
            Border bubble = new Border
            {
                Background = isCurrentUser
                    ? (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A86E8"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9E9EB")),
                CornerRadius = isCurrentUser
                    ? new CornerRadius(18, 5, 18, 18)
                    : new CornerRadius(5, 18, 18, 18),
                Padding = new Thickness(4),
                Margin = new Thickness(isCurrentUser ? 0 : 10, 0, isCurrentUser ? 10 : 0, 0),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };

            // 그림자 효과
            bubble.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.1,
                Direction = 270
            };

            // 이미지 컨테이너 그리드 (로딩 표시/이미지)
            Grid imageGrid = new Grid
            {
                Width = 200,
                Height = 200
            };

            // 로딩 표시
            TextBlock loadingText = new TextBlock
            {
                Text = "이미지 로딩 중...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isCurrentUser ? Brushes.White : Brushes.Black
            };

            imageGrid.Children.Add(loadingText);

            // 이미지
            Image image = new Image
            {
                Width = 200,
                Height = 200,
                Stretch = Stretch.Uniform,
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed // 이미지 로드 전까지 숨김
            };

            imageGrid.Children.Add(image);
            bubble.Child = imageGrid;

            // 클릭 시 전체 뷰어로 열기
            imageGrid.MouseLeftButtonUp += async (s, e) =>
            {
                // 캐시 경로 확인
                string localPath = await CacheManager.GetOrDownloadFileAsync(imageUrl);
                if (string.IsNullOrEmpty(localPath))
                {
                    MessageBox.Show("이미지를 로드할 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var viewer = new ImageViewerWindow(imageUrl);
                viewer.ShowDialog();
            };

            // 백그라운드에서 이미지 로드
            Task.Run(async () =>
            {
                try
                {
                    // 썸네일 이미지 생성
                    var bitmap = await ImageHelper.LoadImageFromUrlAsync(imageUrl, 200, 200);

                    // UI 스레드에서 이미지 표시
                    this.Dispatcher.Invoke(() =>
                    {
                        if (bitmap != null)
                        {
                            image.Source = bitmap;
                            image.Visibility = Visibility.Visible;
                            loadingText.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            loadingText.Text = "이미지 로드 실패";
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        loadingText.Text = "이미지 로드 실패";
                        Console.WriteLine($"이미지 로드 오류: {ex.Message}");
                    });
                }
            });

            // 시간 표시 추가
            TextBlock timeBlock = new TextBlock
            {
                Text = timestamp.ToString("HH:mm"),
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(isCurrentUser ? 0 : 5, 5, isCurrentUser ? 5 : 0, 0),
                HorizontalAlignment = isCurrentUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // 버블 및 시간 추가
            bubbleStack.Children.Add(bubble);
            bubbleStack.Children.Add(timeBlock);
            container.Children.Add(bubbleStack);

            // 애니메이션 준비
            container.Opacity = 0;
            container.RenderTransform = new TranslateTransform(isCurrentUser ? 20 : -20, 0);

            // 애니메이션 시작
            DoubleAnimation opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            DoubleAnimation translateAnimation = new DoubleAnimation(isCurrentUser ? 20 : -20, 0, TimeSpan.FromSeconds(0.3));

            opacityAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            translateAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            container.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            (container.RenderTransform as TranslateTransform).BeginAnimation(TranslateTransform.XProperty, translateAnimation);

            return container;
        }

        private void DownloadImage(string imageUrl)
        {
            try
            {
                // 이미지 URL에서 파일명 추출
                string fileName = IOPath.GetFileName(new Uri(imageUrl).LocalPath);

                // 저장 대화상자 표시
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp",
                    DefaultExt = IOPath.GetExtension(fileName)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    _ = DownloadImageAsync(imageUrl, fileName, saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"이미지 다운로드 중 오류 발생: {ex.Message}");
            }
        }

        private async Task DownloadImageAsync(string imageUrl, string fileName, string savePath)
        {
            try
            {
                // 전송 중 표시자 활성화
                ShowSendingIndicator(true);

                // API 호출을 위한 URL 준비
                string apiUrl = $"{AppSettings.GetServerUrl()}/api/File/download?fileName={fileName}";

                using (var client = new HttpClient())
                using (var response = await client.GetAsync(apiUrl))
                {
                    // 전송 중 표시자 비활성화
                    ShowSendingIndicator(false);

                    if (response.IsSuccessStatusCode)
                    {
                        // 파일 저장
                        byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                        File.WriteAllBytes(savePath, fileBytes);
                        MessageBox.Show("이미지가 성공적으로 다운로드되었습니다.", "다운로드 완료",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ShowErrorMessage($"다운로드 실패: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowSendingIndicator(false);
                ShowErrorMessage($"이미지 다운로드 중 오류 발생: {ex.Message}");
            }
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
        // 상단바 확장 상태 관련 변수 추가
        private bool _isTopBarExpanded = false;
        private double _defaultTopBarHeight = 80;
        private double _expandedTopBarHeight = 150; // 확장 높이 조정
        // 상단바 클릭 이벤트 핸들러
        // 파란색 상단바 클릭 이벤트 핸들러
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 확장/축소 상태를 토글
            if (_isTopBarExpanded)
            {
                CollapseTopBar();
            }
            else
            {
                ExpandTopBar();
            }
            e.Handled = true; // 이벤트 전파 방지
        }
        private void ChatArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 축소 기능 제거 - 아무 동작 없음
        }
        // 상단바 확장 메서드 수정
        private void ExpandTopBar()
        {
            if (_isTopBarExpanded)
                return;

            _isTopBarExpanded = true;

            // 높이 애니메이션
            DoubleAnimation heightAnimation = new DoubleAnimation
            {
                To = _expandedTopBarHeight,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 메뉴 패널 표시 애니메이션
            ExtendedMenuPanel.Visibility = Visibility.Visible;
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 화살표 아이콘 회전
            DoubleAnimation rotateAnimation = new DoubleAnimation
            {
                To = 180,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 애니메이션 시작
            TopBarBorder.BeginAnimation(Border.HeightProperty, heightAnimation);
            ExtendedMenuPanel.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            // 메뉴 패널 위치 재설정
            ExtendedMenuPanel.VerticalAlignment = VerticalAlignment.Center;
            ExtendedMenuPanel.Margin = new Thickness(0, 20, 0, 0);
            ExtendedMenuPanel.Visibility = Visibility.Visible;
            // 회전 변환 적용
            RotateTransform rotateTransform = ExpandIcon.RenderTransform as RotateTransform;
            if (rotateTransform == null)
            {
                rotateTransform = new RotateTransform();
                ExpandIcon.RenderTransformOrigin = new Point(0.5, 0.5);
                ExpandIcon.RenderTransform = rotateTransform;
            }
            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
        }

        // 상단바 축소 메서드 수정
        private void CollapseTopBar()
        {
            Console.WriteLine("CollapseTopBar 시작 - 현재 상태: " + _isTopBarExpanded);

            if (!_isTopBarExpanded)
            {
                Console.WriteLine("이미 축소된 상태임");
                return;
            }

            _isTopBarExpanded = false;
            Console.WriteLine("상태 변경됨: " + _isTopBarExpanded);

            // 높이 애니메이션
            DoubleAnimation heightAnimation = new DoubleAnimation
            {
                To = _defaultTopBarHeight,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 메뉴 패널 숨김 애니메이션
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 애니메이션 완료 후 메뉴 패널 숨김
            opacityAnimation.Completed += (s, e) => ExtendedMenuPanel.Visibility = Visibility.Collapsed;

            // 화살표 아이콘 회전
            DoubleAnimation rotateAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 애니메이션 시작
            TopBarBorder.BeginAnimation(Border.HeightProperty, heightAnimation);
            ExtendedMenuPanel.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);

            // 회전 변환 적용
            RotateTransform rotateTransform = ExpandIcon.RenderTransform as RotateTransform;
            if (rotateTransform != null)
            {
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
            }
        }

        // 채팅방 정보 버튼 클릭 이벤트 핸들러
        private async void RoomInfoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 채팅방 정보 가져오기
                using HttpClient client = new HttpClient();
                string baseUrl = AppSettings.GetServerUrl();

                // 채팅방 목록 가져오기
                var chatListResponse = await client.GetAsync($"{baseUrl}/api/User/getChatList");
                var userListResponse = await client.GetAsync($"{baseUrl}/api/User/getAllUsers");

                if (!chatListResponse.IsSuccessStatusCode || !userListResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show("채팅방 정보를 가져오는 데 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var chatListJson = await chatListResponse.Content.ReadAsStringAsync();
                var userListJson = await userListResponse.Content.ReadAsStringAsync();

                var allRooms = JsonConvert.DeserializeObject<List<ChatRoom>>(chatListJson);
                var allUsers = JsonConvert.DeserializeObject<List<MainWindow.UserData>>(userListJson);

                // 현재 채팅방 정보 찾기
                var currentRoom = allRooms?.FirstOrDefault(r => r.RoomId == _roomId);
                if (currentRoom == null)
                {
                    MessageBox.Show("채팅방 정보를 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 채팅방 참여자 목록 구하기
                var participantUsers = allUsers?.Where(u => u.JoinedRoomIds.Contains(_roomId)).ToList();
                var participantNames = participantUsers?.Select(u => u.Name).ToList() ?? new List<string>();

                // 정보 표시
                string roomInfo = $"채팅방 이름: {currentRoom.Name}\n" +
                                 $"비밀번호 설정: {(string.IsNullOrEmpty(currentRoom.Password) ? "아니오" : "예")}\n" +
                                 $"1:1 채팅방: {(currentRoom.IsOneToOne ? "예" : "아니오")}\n" +
                                 $"생성일: {currentRoom.CreatedDate.ToString("yyyy-MM-dd HH:mm")}\n" +
                                 $"참가 인원: {participantNames.Count}명\n\n" +
                                 $"참가자 목록:\n{string.Join("\n", participantNames)}";

                MessageBox.Show(roomInfo, $"채팅방 정보 - {currentRoom.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"채팅방 정보를 가져오는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 이미지 목록 버튼 클릭 이벤트 핸들러
        private void ImageListButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 페이드 아웃 애니메이션
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0.7, TimeSpan.FromSeconds(0.2));

                fadeOut.Completed += (s, args) =>
                {
                    // 이미지 목록 창 열기
                    ImageListWindow imageListWindow = new ImageListWindow(_roomId, _currentUser);
                    imageListWindow.Owner = this;

                    // 애니메이션 효과 추가
                    imageListWindow.Opacity = 0;
                    imageListWindow.ShowDialog();

                    // 페이드 인 애니메이션
                    DoubleAnimation fadeIn = new DoubleAnimation(0.7, 1, TimeSpan.FromSeconds(0.2));
                    this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };

                this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 목록을 여는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 동영상 목록 버튼 클릭 이벤트 핸들러
        private void VideoListButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 페이드 아웃 애니메이션
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0.7, TimeSpan.FromSeconds(0.2));

                fadeOut.Completed += (s, args) =>
                {
                    // 동영상 목록 창 열기
                    VideoListWindow videoListWindow = new VideoListWindow(_roomId, _currentUser);
                    videoListWindow.Owner = this;

                    // 애니메이션 효과 추가
                    videoListWindow.Opacity = 0;
                    videoListWindow.ShowDialog();

                    // 페이드 인 애니메이션
                    DoubleAnimation fadeIn = new DoubleAnimation(0.7, 1, TimeSpan.FromSeconds(0.2));
                    this.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };

                this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"동영상 목록을 여는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 상단바 확장/축소 토글 메서드
        private void ToggleTopBarExpansion()
        {
            _isTopBarExpanded = !_isTopBarExpanded;

            // 확장 시 높이 애니메이션
            DoubleAnimation heightAnimation = new DoubleAnimation
            {
                To = _isTopBarExpanded ? _expandedTopBarHeight : _defaultTopBarHeight,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 확장 메뉴 표시/숨김 애니메이션
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                To = _isTopBarExpanded ? 1 : 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 확장 아이콘 회전 애니메이션
            DoubleAnimation rotateAnimation = new DoubleAnimation
            {
                To = _isTopBarExpanded ? 180 : 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 애니메이션 시작
            TopBarBorder.BeginAnimation(Border.HeightProperty, heightAnimation);
            ExtendedMenuPanel.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);

            // 회전 변환 적용
            RotateTransform rotateTransform = ExpandIcon.RenderTransform as RotateTransform;
            if (rotateTransform == null)
            {
                rotateTransform = new RotateTransform();
                ExpandIcon.RenderTransformOrigin = new Point(0.5, 0.5);
                ExpandIcon.RenderTransform = rotateTransform;
            }
            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

            // 메뉴 패널 표시/숨김
            if (_isTopBarExpanded)
            {
                ExtendedMenuPanel.Visibility = Visibility.Visible;
            }
            else
            {
                // 애니메이션 완료 후 숨김
                opacityAnimation.Completed += (s, e) =>
                {
                    if (!_isTopBarExpanded)
                    {
                        ExtendedMenuPanel.Visibility = Visibility.Collapsed;
                    }
                };
            }
        }

        // 추가 메뉴 버튼 이벤트 핸들러
        private void FriendList_Click(object sender, RoutedEventArgs e)
        {
            // 페이드 아웃 애니메이션
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };

            fadeOut.Completed += (s, args) =>
            {
                Friend friendWindow = new Friend(_currentUser, _roomId);
                friendWindow.Show();
                this.Close();
            };

            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("설정 기능은 준비 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 페이드 아웃 애니메이션과 함께 앱 종료
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            fadeOut.Completed += (s, args) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        public class ChatRoom
        {
            public string RoomId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool IsPrivate { get; set; } = false;
            public bool IsOneToOne { get; set; } = false;
            public DateTime CreatedDate { get; set; } = DateTime.Now;
        }

        public class ChatMessage
        {
            public string RoomId { get; set; } = string.Empty;
            public string Sender { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }

            // 캐싱된 로컬 파일 경로 (미디어 메시지용)
            [JsonIgnore] // JSON 직렬화에서 제외
            public string LocalCachePath { get; set; }

            // 메시지 타입 판별 (성능 향상을 위해 캐싱)
            [JsonIgnore]
            private MessageType? _messageType = null;

            [JsonIgnore]
            public MessageType MessageType
            {
                get
                {
                    if (_messageType.HasValue)
                        return _messageType.Value;

                    if (string.IsNullOrWhiteSpace(Message))
                        return MessageType.Text;

                    if (Uri.IsWellFormedUriString(Message, UriKind.Absolute))
                    {
                        string extension = Path.GetExtension(Message).ToLower();

                        // 이미지 확장자 확인
                        if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                            extension == ".webp" || extension == ".bmp")
                        {
                            _messageType = MessageType.Image;
                            return MessageType.Image;
                        }

                        // 비디오 확장자 확인
                        if (extension == ".mp4" || extension == ".mov" || extension == ".avi" ||
                            extension == ".mkv" || extension == ".wmv")
                        {
                            _messageType = MessageType.Video;
                            return MessageType.Video;
                        }

                        // GIF는 특별히 처리
                        if (extension == ".gif")
                        {
                            _messageType = MessageType.Gif;
                            return MessageType.Gif;
                        }

                        // 다른 URL
                        _messageType = MessageType.Url;
                        return MessageType.Url;
                    }

                    // 일반 텍스트
                    _messageType = MessageType.Text;
                    return MessageType.Text;
                }
            }
        }
        public enum MessageType
        {
            Text,
            Image,
            Video,
            Gif,
            Url
        }

        // RoomInfo 클래스 (없다면 추가)
        public class RoomInfo
        {
            [JsonProperty("Name")]
            public string RoomName { get; set; } = string.Empty;
            public string RoomId { get; set; } = string.Empty;
        }
        public class LimitedMessageList
        {
            private readonly List<ChatMessage> _messages;
            private readonly int _maxCapacity;

            public LimitedMessageList(int maxCapacity = 200)
            {
                _messages = new List<ChatMessage>(maxCapacity);
                _maxCapacity = maxCapacity;
            }

            public int Count => _messages.Count;

            public ChatMessage this[int index] => _messages[index];

            public void Add(ChatMessage message)
            {
                _messages.Add(message);
                TrimExcess();
            }

            public void Insert(int index, ChatMessage message)
            {
                _messages.Insert(index, message);
                TrimExcess();
            }
            public ChatMessage FirstOrDefault(Func<ChatMessage, bool> predicate)
            {
                return _messages.FirstOrDefault(predicate);
            }
            public void Clear()
            {
                _messages.Clear();
            }

            public void RemoveRange(int index, int count)
            {
                _messages.RemoveRange(index, count);
            }

            public List<ChatMessage> ToList()
            {
                return new List<ChatMessage>(_messages);
            }

            private void TrimExcess()
            {
                if (_messages.Count > _maxCapacity)
                {
                    int itemsToRemove = _messages.Count - _maxCapacity;
                    _messages.RemoveRange(0, itemsToRemove); // 가장 오래된 메시지 제거
                }
            }

            public IEnumerable<ChatMessage> OrderBy(Func<ChatMessage, object> keySelector)
            {
                return _messages.OrderBy(keySelector);
            }

            public IEnumerable<ChatMessage> OrderByDescending(Func<ChatMessage, object> keySelector)
            {
                return _messages.OrderByDescending(keySelector);
            }

            public DateTime? MinTimestamp()
            {
                if (_messages.Count == 0)
                    return null;

                return _messages.Min(m => m.Timestamp);
            }
            public void Remove(ChatMessage message)
            {
                _messages.Remove(message);
            }
            public static implicit operator List<ChatMessage>(LimitedMessageList list)
            {
                return list.ToList();
            }
        }
    }
}