using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using System.Net.Http;
using static chatapp.MainWindow;
using System.Windows.Media.Effects;

namespace chatapp
{
    public partial class Friend : Window
    {
        private readonly UserData _currentUser;
        private readonly string _lastRoomId;

        public Friend(UserData currentUser, string lastRoomId)
        {
            InitializeComponent();
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _lastRoomId = lastRoomId;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 프로필 정보 설정
            LoadProfile();

            // 친구 목록 및 요청 로드
            LoadFriendData();
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
        private UIElement FindMainContent()
        {
            // 옵션 1: 직접 메인 ScrollViewer 찾기 (큰 영역에 블러 적용하려면)
            var scrollViewer = this.FindName("FriendListScrollViewer") as ScrollViewer;
            if (scrollViewer != null)
                return scrollViewer;

            // 옵션 2: 프로필 + 친구 요청 패널 + 친구 목록 패널 모두 찾기 (세부 영역에 블러 적용하려면)
            var panels = new List<UIElement>();

            var friendListPanel = this.FindName("FriendListPanel") as UIElement;
            if (friendListPanel != null)
                panels.Add(friendListPanel);

            var requestListPanel = this.FindName("RequestListPanel") as UIElement;
            if (requestListPanel != null)
                panels.Add(requestListPanel);

            if (panels.Count > 0)
                return panels[0]; // 첫 번째 찾은 패널 반환

            // 기본값: 전체 Window에 적용 (최후의 수단)
            return this;
        }
        private void ToggleSidePanel_Click(object sender, RoutedEventArgs e)
        {
            // 현재 사이드 패널 상태 확인
            if (SidePanel.Visibility == Visibility.Visible)
            {
                CloseSidePanel_Click(null, null);
                return;
            }

            Console.WriteLine("사이드 패널 열기");

            // 요소 초기화
            SidePanel.Visibility = Visibility.Visible;
            SidePanel.Margin = new Thickness(-250, 0, 0, 0);
            BlurOverlay.Visibility = Visibility.Visible;
            BlurOverlay.Opacity = 0;

            // 부모 그리드 찾기 (메인 콘텐츠)
            FrameworkElement mainContent = null;

            // 첫 번째 시도: 스크롤뷰어 찾기
            var scrollViewer = this.FindName("FriendListScrollViewer") as ScrollViewer ??
                               VisualTreeHelper.GetChild(this, 0) as ScrollViewer;

            if (scrollViewer != null)
            {
                mainContent = scrollViewer;
            }
            else
            {
                // 두 번째 시도: 메인 그리드 찾기
                var grid = this.Content as Grid;
                if (grid != null && grid.Children.Count > 0)
                {
                    // 첫 번째 그리드의 두 번째 행 (콘텐츠 영역)
                    var mainGrid = grid.Children[0] as Grid;
                    if (mainGrid != null && mainGrid.RowDefinitions.Count > 1)
                    {
                        for (int i = 0; i < mainGrid.Children.Count; i++)
                        {
                            var element = mainGrid.Children[i];
                            if (Grid.GetRow(element) == 1) // 두 번째 행 (인덱스 1)
                            {
                                mainContent = element as FrameworkElement;
                                break;
                            }
                        }
                    }
                }
            }

            // 메인 콘텐츠 블러 효과 적용
            bool blurApplied = false;
            if (mainContent != null)
            {
                try
                {
                    var blurEffect = new BlurEffect { Radius = 0 };
                    mainContent.Effect = blurEffect;

                    DoubleAnimation blurAnimation = new DoubleAnimation(0, 10, TimeSpan.FromSeconds(0.3));
                    blurEffect.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);

                    blurApplied = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"블러 효과 적용 실패: {ex.Message}");
                    // 블러 효과 실패 시 대체 효과 (투명도)
                    mainContent.Opacity = 0.7;
                }
            }

            // 오버레이 페이드 인
            DoubleAnimation fadeIn = new DoubleAnimation(0, 0.5, TimeSpan.FromSeconds(0.3));
            BlurOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 사이드 패널 슬라이드 인
            ThicknessAnimation slideIn = new ThicknessAnimation
            {
                From = new Thickness(-250, 0, 0, 0),
                To = new Thickness(0, 0, 0, 0),
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SidePanel.BeginAnimation(Grid.MarginProperty, slideIn);
        }

        private void CloseSidePanel_Click(object sender, RoutedEventArgs e)
        {
            // 메인 콘텐츠 찾기
            var mainContent = FindMainContent();

            // 블러 효과 제거 애니메이션
            if (mainContent.Effect is BlurEffect effect)
            {
                DoubleAnimation blurAnimation = new DoubleAnimation(10, 0, TimeSpan.FromSeconds(0.3));
                blurAnimation.Completed += (s, args) => mainContent.Effect = null;
                effect.BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);
            }

            // 오버레이 페이드 아웃
            DoubleAnimation fadeOut = new DoubleAnimation(BlurOverlay.Opacity, 0, TimeSpan.FromSeconds(0.3));
            fadeOut.Completed += (s, args) => BlurOverlay.Visibility = Visibility.Collapsed;
            BlurOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            // 슬라이드아웃 애니메이션
            ThicknessAnimation slideOut = new ThicknessAnimation
            {
                From = new Thickness(0, 0, 0, 0),
                To = new Thickness(-250, 0, 0, 0),
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            slideOut.Completed += (s, args) => {
                SidePanel.Visibility = Visibility.Collapsed;
                SidePanel.Margin = new Thickness(-250, 0, 0, 0); // 초기 위치로 리셋
            };
            SidePanel.BeginAnimation(Grid.MarginProperty, slideOut);
        }

        // 배경 오버레이 클릭 시 사이드 패널 닫기
        private void BackgroundOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            // 이벤트 전파 중단
            e.Handled = true;
            CloseSidePanel_Click(null, null);
        }

        private void LoadProfile()
        {
            ProfileNameText.Text = _currentUser.Name;
            StatusMessageText.Text = string.IsNullOrEmpty(_currentUser.StatusMessage)
                ? "상태 메시지가 없습니다."
                : _currentUser.StatusMessage;
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
        private async void LoadFriendData()
        {
            try
            {
                // 로딩 표시
                LoadingIndicator.Visibility = Visibility.Visible;

                // 친구 요청 로드
                await LoadFriendRequests();

                // 친구 목록 로드
                await LoadFriends();

                // 로딩 숨김
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                MessageBox.Show($"친구 정보를 불러오는 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadFriendRequests()
        {
            try
            {
                // 기존 요청 패널 비우기
                RequestListPanel.Children.Clear();
                RequestListPanel.Children.Add(RequestLoadingText);

                string endpoint = $"/api/Friend/requests?userIndex={_currentUser.Index}";
                string baseUrl = AppSettings.GetServerUrl();

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync($"{baseUrl}{endpoint}");

                    if (!response.IsSuccessStatusCode)
                    {
                        RequestLoadingText.Text = "요청 정보를 불러올 수 없습니다.";
                        return;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var requests = JsonConvert.DeserializeObject<List<FriendRequest>>(json);

                    // 로딩 텍스트 제거
                    RequestListPanel.Children.Remove(RequestLoadingText);

                    if (requests != null && requests.Count > 0)
                    {
                        foreach (var req in requests)
                        {
                            // 사용자 이름 가져오기
                            string userName = await GetUserNameByIndex(req.HostIndex);

                            // 요청 항목 UI 생성
                            Border requestItem = new Border
                            {
                                Style = (Style)FindResource("FriendListItem"),
                                Margin = new Thickness(0, 0, 0, 10)
                            };

                            Grid itemGrid = new Grid();
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                            // 사용자 정보
                            StackPanel userInfo = new StackPanel { Orientation = Orientation.Horizontal };

                            // 프로필 아이콘
                            Border profileIcon = new Border
                            {
                                Width = 40,
                                Height = 40,
                                Background = (SolidColorBrush)FindResource("SecondaryColor"),
                                CornerRadius = new CornerRadius(20),
                                Margin = new Thickness(0, 0, 10, 0)
                            };
                            profileIcon.Child = new TextBlock
                            {
                                Text = "👤",
                                FontSize = 18,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            userInfo.Children.Add(profileIcon);

                            // 사용자 이름
                            TextBlock nameText = new TextBlock
                            {
                                Text = userName,
                                FontSize = 16,
                                FontWeight = FontWeights.SemiBold,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            userInfo.Children.Add(nameText);

                            itemGrid.Children.Add(userInfo);
                            Grid.SetColumn(userInfo, 0);

                            // 버튼 영역
                            StackPanel buttonPanel = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right
                            };

                            // 수락 버튼
                            Button acceptButton = new Button
                            {
                                Content = "수락",
                                Style = (Style)FindResource("PrimaryButton"),
                                Background = (SolidColorBrush)FindResource("AccentColor"),
                                Width = 70,
                                Height = 35,
                                Margin = new Thickness(0, 0, 5, 0),
                                Tag = req.HostIndex
                            };
                            acceptButton.Click += AcceptRequest_Click;
                            buttonPanel.Children.Add(acceptButton);

                            // 거절 버튼
                            Button rejectButton = new Button
                            {
                                Content = "거절",
                                Style = (Style)FindResource("PrimaryButton"),
                                Background = (SolidColorBrush)FindResource("ErrorColor"),
                                Width = 70,
                                Height = 35,
                                Margin = new Thickness(5, 0, 0, 0),
                                Tag = req.HostIndex
                            };
                            rejectButton.Click += RejectRequest_Click;
                            buttonPanel.Children.Add(rejectButton);

                            itemGrid.Children.Add(buttonPanel);
                            Grid.SetColumn(buttonPanel, 1);

                            requestItem.Child = itemGrid;
                            RequestListPanel.Children.Add(requestItem);
                        }
                    }
                    else
                    {
                        TextBlock noRequests = new TextBlock
                        {
                            Text = "받은 친구 요청이 없습니다.",
                            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 10, 0, 10)
                        };
                        RequestListPanel.Children.Add(noRequests);
                    }
                }
            }
            catch (Exception ex)
            {
                RequestLoadingText.Text = $"요청 목록 로드 실패: {ex.Message}";
            }
        }

        private async Task LoadFriends()
        {
            try
            {
                // 기존 친구 패널 비우기
                FriendListPanel.Children.Clear();
                FriendListPanel.Children.Add(FriendLoadingText);

                string endpoint = $"/api/Friend/list?userIndex={_currentUser.Index}";
                string baseUrl = AppSettings.GetServerUrl();

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync($"{baseUrl}{endpoint}");

                    if (!response.IsSuccessStatusCode)
                    {
                        FriendLoadingText.Text = "친구 목록을 불러올 수 없습니다.";
                        return;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var friends = JsonConvert.DeserializeObject<List<int>>(json);

                    // 로딩 텍스트 제거
                    FriendListPanel.Children.Remove(FriendLoadingText);

                    if (friends != null && friends.Count > 0)
                    {
                        foreach (var friendIndex in friends)
                        {
                            // 친구 이름 가져오기
                            string friendName = await GetUserNameByIndex(friendIndex);

                            // 친구 항목 UI 생성
                            Border friendItem = new Border
                            {
                                Style = (Style)FindResource("FriendListItem"),
                                Margin = new Thickness(0, 0, 0, 10)
                            };

                            Grid itemGrid = new Grid();
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                            // 친구 정보
                            StackPanel friendInfo = new StackPanel { Orientation = Orientation.Horizontal };

                            // 프로필 아이콘
                            Border profileIcon = new Border
                            {
                                Width = 40,
                                Height = 40,
                                Background = (SolidColorBrush)FindResource("SecondaryColor"),
                                CornerRadius = new CornerRadius(20),
                                Margin = new Thickness(0, 0, 10, 0)
                            };
                            profileIcon.Child = new TextBlock
                            {
                                Text = "👤",
                                FontSize = 18,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            friendInfo.Children.Add(profileIcon);

                            // 친구 이름
                            TextBlock nameText = new TextBlock
                            {
                                Text = friendName,
                                FontSize = 16,
                                FontWeight = FontWeights.SemiBold,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            friendInfo.Children.Add(nameText);

                            itemGrid.Children.Add(friendInfo);
                            Grid.SetColumn(friendInfo, 0);

                            // 버튼 영역
                            StackPanel buttonPanel = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right
                            };

                            // 채팅 버튼
                            Button chatButton = new Button
                            {
                                Content = "채팅",
                                Style = (Style)FindResource("PrimaryButton"),
                                Width = 70,
                                Height = 35,
                                Margin = new Thickness(0, 0, 5, 0),
                                Tag = friendIndex
                            };
                            chatButton.Click += StartChat_Click;
                            buttonPanel.Children.Add(chatButton);

                            // 삭제 버튼
                            Button deleteButton = new Button
                            {
                                Content = "삭제",
                                Style = (Style)FindResource("PrimaryButton"),
                                Background = (SolidColorBrush)FindResource("ErrorColor"),
                                Width = 70,
                                Height = 35,
                                Margin = new Thickness(5, 0, 0, 0),
                                Tag = friendIndex
                            };
                            deleteButton.Click += DeleteFriend_Click;
                            buttonPanel.Children.Add(deleteButton);

                            itemGrid.Children.Add(buttonPanel);
                            Grid.SetColumn(buttonPanel, 1);

                            friendItem.Child = itemGrid;
                            FriendListPanel.Children.Add(friendItem);
                        }
                    }
                    else
                    {
                        TextBlock noFriends = new TextBlock
                        {
                            Text = "친구 목록이 비어있습니다.",
                            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 10, 0, 10)
                        };
                        FriendListPanel.Children.Add(noFriends);
                    }
                }
            }
            catch (Exception ex)
            {
                FriendLoadingText.Text = $"친구 목록 로드 실패: {ex.Message}";
            }
        }

        private async Task<string> GetUserNameByIndex(int userIndex)
        {
            try
            {
                string endpoint = $"/api/User/getUserNameByIndex?index={userIndex}";
                string baseUrl = AppSettings.GetServerUrl();

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync($"{baseUrl}{endpoint}");

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                    return $"사용자 #{userIndex}";
                }
            }
            catch
            {
                return $"사용자 #{userIndex}";
            }
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

        private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int hostIndex))
                return;

            try
            {
                button.IsEnabled = false;
                LoadingIndicator.Visibility = Visibility.Visible;

                await RespondToRequest(hostIndex, "Accepted");

                MessageBox.Show("친구 요청을 수락했습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadFriendData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"요청 처리 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                button.IsEnabled = true;
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int hostIndex))
                return;

            try
            {
                button.IsEnabled = false;
                LoadingIndicator.Visibility = Visibility.Visible;

                await RespondToRequest(hostIndex, "Rejected");

                MessageBox.Show("친구 요청을 거절했습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadFriendData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"요청 처리 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                button.IsEnabled = true;
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RespondToRequest(int hostIndex, string action)
        {
            string endpoint = "/api/Friend/respond";
            string baseUrl = AppSettings.GetServerUrl();

            var request = new FriendRequest
            {
                HostIndex = hostIndex,
                GetIndex = _currentUser.Index,
                Action = action
            };

            using (HttpClient client = new HttpClient())
            {
                var content = new StringContent(
                    JsonConvert.SerializeObject(request),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync($"{baseUrl}{endpoint}", content);
                response.EnsureSuccessStatusCode();
            }
        }

        private async void StartChat_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int friendIndex))
                return;

            try
            {
                button.IsEnabled = false;
                LoadingIndicator.Visibility = Visibility.Visible;

                // 1:1 채팅방 생성 또는 기존 채팅방 열기
                string roomId = await CreateOrGetPrivateChatRoom(friendIndex);

                if (!string.IsNullOrEmpty(roomId))
                {
                    Message msgWindow = new Message(_currentUser, roomId);
                    msgWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("채팅방을 생성할 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    button.IsEnabled = true;
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"채팅방 생성 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                button.IsEnabled = true;
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<string> CreateOrGetPrivateChatRoom(int friendIndex)
        {
            // 1. 먼저 이미 존재하는 1:1 채팅방이 있는지 확인
            var existingRoom = await CheckExistingPrivateRoom(friendIndex);
            if (existingRoom != null)
            {
                return existingRoom.RoomId;
            }

            // 2. 존재하지 않으면 새로 생성
            string friendName = await GetUserNameByIndex(friendIndex);
            string roomId = GenerateRoomId();
            string roomName = $"{_currentUser.Name}님과 {friendName}님의 대화";
            string password = $"{_currentUser.Index}_{friendIndex}_{DateTime.Now.Ticks}";

            string baseUrl = AppSettings.GetServerUrl();

            // 1:1 채팅방에는 비밀번호 보호 설정 안함
            var request = new
            {
                RoomId = roomId,
                RoomName = roomName,
                Password = password,
                UserId = _currentUser.Id,
                IsPrivate = false,  // 비밀번호 보호 비활성화
                IsOneToOne = true,  // 1:1 채팅방 여부는 유지
                TargetUserIndex = friendIndex
            };

            using (HttpClient client = new HttpClient())
            {
                var content = new StringContent(
                    JsonConvert.SerializeObject(request),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync($"{baseUrl}/api/User/createRoom", content);
                response.EnsureSuccessStatusCode();

                return roomId;
            }
        }

        private async Task<RoomInfo> CheckExistingPrivateRoom(int friendIndex)
        {
            string baseUrl = AppSettings.GetServerUrl();

            // 채팅방 목록 가져오기
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync($"{baseUrl}/api/User/getChatList");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var rooms = JsonConvert.DeserializeObject<List<RoomInfo>>(json);

                // 사용자 정보 가져오기
                var userResponse = await client.GetAsync($"{baseUrl}/api/User/getUser?userId={_currentUser.Id}");
                userResponse.EnsureSuccessStatusCode();

                var userJson = await userResponse.Content.ReadAsStringAsync();
                var user = JsonConvert.DeserializeObject<UserData>(userJson);

                // 친구 이름 가져오기
                string friendName = await GetUserNameByIndex(friendIndex);

                // 1:1 채팅방 형식 이름
                string privateChatFormat1 = $"{_currentUser.Name}님과 {friendName}님의 대화";
                string privateChatFormat2 = $"{friendName}님과 {_currentUser.Name}님의 대화";

                // 사용자가 참여중인 채팅방 중 1:1 채팅방 찾기
                if (user.JoinedRoomIds != null)
                {
                    foreach (var room in rooms.Where(r => user.JoinedRoomIds.Contains(r.RoomId)))
                    {
                        if (room.RoomName == privateChatFormat1 || room.RoomName == privateChatFormat2)
                        {
                            return room;
                        }
                    }
                }

                return null;
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
                button.IsEnabled = false;
                LoadingIndicator.Visibility = Visibility.Visible;

                string endpoint = "/api/Friend/delete";
                string baseUrl = AppSettings.GetServerUrl();

                var request = new FriendRequest
                {
                    HostIndex = _currentUser.Index,
                    GetIndex = friendIndex,
                    Action = "Delete"
                };

                using (HttpClient client = new HttpClient())
                {
                    var content = new StringContent(
                        JsonConvert.SerializeObject(request),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    var response = await client.PostAsync($"{baseUrl}{endpoint}", content);
                    response.EnsureSuccessStatusCode();

                    MessageBox.Show("친구를 삭제했습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadFriendData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"삭제 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                button.IsEnabled = true;
                LoadingIndicator.Visibility = Visibility.Collapsed;
            }
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
                nextWindow = new ChatList(_currentUser);
            }

            nextWindow.Show();
            this.Close();
        }

        private void OpenProfile_Click(object sender, MouseButtonEventArgs e)
        {
            Profile profileWindow = new Profile(_currentUser);
            profileWindow.Owner = this;
            profileWindow.ShowDialog();

            // 프로필이 업데이트될 수 있으므로 프로필 정보 다시 로드
            LoadProfile();
        }

        private void OpenFriend_Click(object sender, MouseButtonEventArgs e)
        {
            // 이미 친구 창이므로 아무것도 하지 않음
            CloseSidePanel_Click(null, null);
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

        private string GenerateRoomId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 16).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // 데이터 모델
        public class FriendRequest
        {
            public int HostIndex { get; set; }
            public int GetIndex { get; set; }
            public string Action { get; set; } = string.Empty;
        }

        public class RoomInfo
        {
            [JsonProperty("Name")]
            public string RoomName { get; set; } = string.Empty;
            public string RoomId { get; set; } = string.Empty;
        }
    }
}