using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class ChatList : Window
    {
        private UserData _currentUser;
        private string _roomId = string.Empty;

        public ChatList(UserData user)
        {
            InitializeComponent();
            _currentUser = user ?? throw new ArgumentNullException(nameof(user));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 사용자 정보 표시
            UserNameTextBlock.Text = _currentUser.Name;

            // 프로필 이미지 표시 (있는 경우)
            if (!string.IsNullOrEmpty(_currentUser.ProfileImage) && File.Exists(_currentUser.ProfileImage))
            {
                try
                {
                    var image = new BitmapImage(new Uri(_currentUser.ProfileImage));
                    UserProfileIcon.Fill = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"프로필 이미지 로드 실패: {ex.Message}");
                }
            }

            // 채팅 기록 로드
            LoadChatFromFile();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

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

            // 현재 시간
            DateTime now = DateTime.Now;

            // 화면에 표시
            AddChatBubble(message, true, now); // 현재 시간 전달
            SaveChatToFile(_currentUser.Id, message); // 메시지 저장

            MessageInput.Clear();
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
        }

        private void AddChatBubble(string message, bool isUser, DateTime timestamp)
        {
            // 부모 컨테이너
            Grid bubbleContainer = new Grid
            {
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // 시간 표시 - 저장된 타임스탬프 사용
            TextBlock timeBlock = new TextBlock
            {
                Text = timestamp.ToString("HH:mm"),
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E93")),
                Margin = new Thickness(isUser ? 0 : 8, 0, isUser ? 8 : 0, 4),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // 채팅 버블
            Border bubble = new Border
            {
                Background = isUser
                    ? (Brush)Application.Current.Resources["PrimaryColor"]
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9E9EB")),
                CornerRadius = isUser
                    ? new CornerRadius(18, 5, 18, 18)
                    : new CornerRadius(5, 18, 18, 18),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 280,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.1,
                    Direction = 270
                },
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = isUser ? Brushes.White : Brushes.Black,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14
                }
            };

            // 컨테이너에 추가
            StackPanel messageContainer = new StackPanel();
            messageContainer.Children.Add(bubble);
            messageContainer.Children.Add(timeBlock);

            bubbleContainer.Children.Add(messageContainer);

            // 애니메이션 적용
            bubbleContainer.Opacity = 0;
            bubbleContainer.RenderTransform = new TranslateTransform(isUser ? 20 : -20, 0);

            ChatStack.Children.Add(bubbleContainer);

            // 등장 애니메이션
            DoubleAnimation opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            DoubleAnimation translateAnimation = new DoubleAnimation(isUser ? 20 : -20, 0, TimeSpan.FromSeconds(0.3));

            // 부드러운 애니메이션 효과
            opacityAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            translateAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            bubbleContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            (bubbleContainer.RenderTransform as TranslateTransform).BeginAnimation(TranslateTransform.XProperty, translateAnimation);

            // 애니메이션 이후 스크롤 이동
            AnimateScrollToBottom();
        }
        public static class ScrollViewerBehavior
        {
            public static readonly DependencyProperty VerticalOffsetProperty =
                DependencyProperty.RegisterAttached("VerticalOffset", typeof(double),
                    typeof(ScrollViewerBehavior), new UIPropertyMetadata(0.0, OnVerticalOffsetChanged));

            public static double GetVerticalOffset(DependencyObject obj)
            {
                return (double)obj.GetValue(VerticalOffsetProperty);
            }

            public static void SetVerticalOffset(DependencyObject obj, double value)
            {
                obj.SetValue(VerticalOffsetProperty, value);
            }

            private static void OnVerticalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
            {
                if (target is ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
                }
            }
        }
        private void AnimateScrollToBottom()
        {
            // UI 스레드에서 동작하도록 보장
            Dispatcher.InvokeAsync(() =>
            {
                // 스크롤이 애니메이션으로 이동하도록 설정
                double targetPosition = ChatScrollViewer.ScrollableHeight;

                // 현재 위치가 이미 맨 아래에 가까우면 즉시 이동
                if (Math.Abs(ChatScrollViewer.VerticalOffset - targetPosition) < 50)
                {
                    ChatScrollViewer.ScrollToEnd();
                    return;
                }

                // 부드러운 애니메이션으로 스크롤 이동
                DoubleAnimation scrollAnimation = new DoubleAnimation
                {
                    To = targetPosition,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard storyboard = new Storyboard();
                storyboard.Children.Add(scrollAnimation);
                Storyboard.SetTarget(scrollAnimation, ChatScrollViewer);
                Storyboard.SetTargetProperty(scrollAnimation, new PropertyPath(ScrollViewerBehavior.VerticalOffsetProperty));

                storyboard.Begin();
            }, System.Windows.Threading.DispatcherPriority.Background);
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
        private void MessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
        }
        private void ToggleSidePanel_Click(object sender, RoutedEventArgs e)
        {
            // 현재 SidePanel 상태 확인
            if (SidePanel.Visibility == Visibility.Visible)
            {
                // 이미 열려있으면 닫기
                CloseSidePanel_Click(null, null);
                return;
            }

            // SidePanel이 닫혀있으면 열기
            Console.WriteLine("사이드 패널 열기");

            // 초기 상태 설정
            SidePanel.Margin = new Thickness(-250, 0, 0, 0);
            SidePanel.Visibility = Visibility.Visible;
            BlurOverlay.Visibility = Visibility.Visible;
            BlurOverlay.Opacity = 0;

            // 블러 효과 적용
            ChatStack.Effect = new BlurEffect { Radius = 0 };

            // 애니메이션 적용
            // 1. 블러 효과 애니메이션
            DoubleAnimation blurAnimation = new DoubleAnimation(0, 10, TimeSpan.FromSeconds(0.3));
            ((BlurEffect)ChatStack.Effect).BeginAnimation(BlurEffect.RadiusProperty, blurAnimation);

            // 2. 오버레이 페이드 인
            DoubleAnimation fadeIn = new DoubleAnimation(0, 0.5, TimeSpan.FromSeconds(0.3));
            BlurOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 3. 사이드 패널 슬라이드 인
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

        private void OpenProfile_Click(object sender, MouseButtonEventArgs e)
        {
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
            MessageBox.Show("설정 기능은 준비 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveChatToFile(string sender, string message)
        {
            string chatFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "chatdata.txt");

            List<ChatMessage> chatHistory = new List<ChatMessage>();

            // 기존 데이터 불러오기
            if (File.Exists(chatFilePath))
            {
                var json = File.ReadAllText(chatFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    chatHistory = JsonConvert.DeserializeObject<List<ChatMessage>>(json) ?? new List<ChatMessage>();
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
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var chatHistory = JsonConvert.DeserializeObject<List<ChatMessage>>(json);

                    if (chatHistory != null && chatHistory.Any())
                    {
                        foreach (var chat in chatHistory)
                        {
                            bool isUser = chat.Sender == _currentUser.Id;
                            // 저장된 타임스탬프 전달
                            AddChatBubble(chat.Message, isUser, chat.Timestamp);
                        }

                        // 스크롤을 위한 애니메이션 적용
                        AnimateScrollToBottom();
                    }
                    else
                    {
                        // 환영 메시지 추가
                        AddChatBubble("Connect에 오신 것을 환영합니다! 이제 채팅을 시작할 수 있습니다.", false, DateTime.Now);
                    }
                }
                else
                {
                    // 환영 메시지 추가
                    AddChatBubble("Connect에 오신 것을 환영합니다! 이제 채팅을 시작할 수 있습니다.", false, DateTime.Now);
                }
            }
            else
            {
                // 환영 메시지 추가
                AddChatBubble("Connect에 오신 것을 환영합니다! 이제 채팅을 시작할 수 있습니다.", false, DateTime.Now);
            }
        }

        private void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("파일 첨부 기능은 준비 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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

        public class ChatMessage
        {
            public string Sender { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}