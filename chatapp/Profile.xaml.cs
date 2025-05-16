using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using static chatapp.MainWindow;
using System.Xml;
using System.IO;
using Newtonsoft.Json;
using System.Windows.Shapes;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;

// 네임스페이스 충돌 해결을 위한 별칭 설정
using JsonFormatting = Newtonsoft.Json.Formatting;
using IOPath = System.IO.Path;

namespace chatapp
{
    public partial class Profile : Window
    {
        private UserData _user;
        private const string USER_CONFIG_FILE = "user.cfg"; // 사용자 정보 파일 이름

        public Profile(UserData user)
        {
            InitializeComponent();
            _user = user;

            // UI 바인딩
            UserNameBox.Text = _user.Name;
            StatusMessageBox.Text = _user.StatusMessage;

            // 프로필 이미지 로드
            LoadProfileImage();
        }
        private async void LoadProfileImage()
        {
            try
            {
                if (string.IsNullOrEmpty(_user.ProfileImage))
                {
                    Console.WriteLine("프로필 이미지 경로가 비어 있습니다.");
                    return;
                }

                Console.WriteLine($"프로필 이미지: {_user.ProfileImage}");

                // 서버 URL 가져오기
                string serverUrl = AppSettings.GetServerUrl();
                string imageUrl;

                // 완전한 URL인지 확인
                if (Uri.IsWellFormedUriString(_user.ProfileImage, UriKind.Absolute))
                {
                    imageUrl = _user.ProfileImage;
                    Console.WriteLine("완전한 URL 형식 사용");
                }
                // 파일명만 있는 경우
                else if (_user.ProfileImage.StartsWith("profile_"))
                {
                    imageUrl = $"{serverUrl}/profile{_user.ProfileImage}";
                    Console.WriteLine($"파일명으로 URL 생성: {imageUrl}");
                }
                else
                {
                    Console.WriteLine("알 수 없는 이미지 형식");
                    return;
                }

                // 이미지 로드
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10); // 타임아웃 설정

                    try
                    {
                        // 이미지에 접근 가능한지 확인
                        var response = await client.GetAsync(imageUrl);

                        if (response.IsSuccessStatusCode)
                        {
                            // 이미지 데이터 로드
                            var imageData = await response.Content.ReadAsByteArrayAsync();

                            Application.Current.Dispatcher.Invoke(() => {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                using (var ms = new MemoryStream(imageData))
                                {
                                    bitmap.StreamSource = ms;
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.EndInit();
                                    bitmap.Freeze(); // UI 스레드에서 사용하기 위해 반드시 필요
                                }

                                ProfileImage.Source = bitmap;
                                Console.WriteLine("이미지 로드 성공");
                            });
                        }
                        else
                        {
                            Console.WriteLine($"이미지 접근 실패: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"이미지 요청 중 오류: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"프로필 이미지 로드 전체 오류: {ex.Message}");
            }
        }
        private async void LoadUserFromServer()
        {
            try
            {
                ShowLoadingIndicator(true);

                // 서버 API를 통해 사용자 정보 가져오기
                var updatedUser = await GetUserDataFromServer(_user.Id);

                if (updatedUser != null)
                {
                    // 서버에서 가져온 최신 정보로 업데이트
                    _user = updatedUser;

                    // UI 바인딩
                    UserNameBox.Text = _user.Name;
                    StatusMessageBox.Text = _user.StatusMessage ?? "";

                    // 프로필 이미지 로드
                    LoadProfileImage();
                }
                else
                {
                    // API 호출 실패 시 기존 데이터 사용
                    UserNameBox.Text = _user.Name;
                    StatusMessageBox.Text = _user.StatusMessage ?? "";
                    LoadProfileImage();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"사용자 정보를 불러오는 중 오류가 발생했습니다: {ex.Message}");

                // 오류 발생 시 기존 데이터 사용
                UserNameBox.Text = _user.Name;
                StatusMessageBox.Text = _user.StatusMessage ?? "";
                LoadProfileImage();
            }
            finally
            {
                ShowLoadingIndicator(false);
            }
        }
        // 서버 API를 통해 사용자 정보 가져오기
        private async Task<UserData> GetUserDataFromServer(string userId)
        {
            try
            {
                string serverUrl = AppSettings.GetServerUrl();
                string apiUrl = $"{serverUrl}/api/User/getUser?userId={userId}";

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var user = JsonConvert.DeserializeObject<UserData>(json);
                        return user;
                    }
                    else
                    {
                        Console.WriteLine($"사용자 정보 로드 실패: {response.StatusCode}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버에서 사용자 정보 가져오기 실패: {ex.Message}");
                return null;
            }
        }
        // 로컬 이미지를 서버에 업로드하는 메서드
        private async void UploadLocalProfileImage(string localImagePath)
        {
            try
            {
                // 로컬 이미지 파일이 실제로 존재하는지 확인
                if (!File.Exists(localImagePath))
                    return;

                ShowLoadingIndicator(true);

                // 이미지 서버에 업로드
                var imageUrl = await UploadProfileImageToServer(localImagePath);

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    // 업로드 성공 시 사용자 정보 업데이트
                    _user.ProfileImage = imageUrl;

                    // 서버에 사용자 정보 업데이트
                    await UpdateUserProfileOnServer();

                    // 이미지 표시
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageUrl);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ProfileImage.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"로컬 프로필 이미지 업로드 실패: {ex.Message}");
            }
            finally
            {
                ShowLoadingIndicator(false);
            }
        }

        // 서버에 사용자 프로필 정보 업데이트
        private async Task<bool> UpdateUserProfileOnServer()
        {
            try
            {
                string serverUrl = AppSettings.GetServerUrl();
                string apiUrl = $"{serverUrl}/api/User/update";

                using (HttpClient client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Patch, apiUrl)
                    {
                        Content = JsonContent.Create(_user)
                    };

                    var response = await client.SendAsync(request);
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"사용자 프로필 업데이트 실패: {ex.Message}");
                return false;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 프로필 콘텐츠 페이드인
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProfileContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
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

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            _user.Name = UserNameBox.Text;
            _user.StatusMessage = StatusMessageBox.Text;

            try
            {
                // 저장 중 로딩 표시
                ShowLoadingIndicator(true);

                string serverUrl = AppSettings.GetServerUrl();
                string apiUrl = $"{serverUrl}/api/User/update";

                using HttpClient client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Patch, apiUrl)
                {
                    Content = JsonContent.Create(_user)
                };

                var response = await client.SendAsync(request);

                // 로딩 표시 숨기기
                ShowLoadingIndicator(false);

                if (response.IsSuccessStatusCode)
                {
                    // 서버 업데이트 성공 시 로컬 user.cfg 파일도 업데이트
                    UpdateUserConfig();

                    MessageBox.Show("✅ 프로필이 성공적으로 저장되었습니다.");

                    // 애니메이션과 함께 창 닫기
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.2)
                    };
                    fadeOut.Completed += (s, args) => this.Close();
                    this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"❌ 서버 응답 실패: {response.StatusCode}\n{error}");
                }
            }
            catch (Exception ex)
            {
                ShowLoadingIndicator(false);
                MessageBox.Show($"❗ 오류 발생: {ex.Message}");
            }
        }

        // 로컬 user.cfg 파일 업데이트 메서드
        private void UpdateUserConfig()
        {
            try
            {
                string configPath = GetConfigFilePath();

                // 파일이 존재하지 않으면 업데이트 할 필요 없음
                if (!File.Exists(configPath))
                    return;

                // 기존 설정 파일 읽기
                string json = File.ReadAllText(configPath);
                var userConfig = JsonConvert.DeserializeObject<UserConfig>(json);

                // 설정 파일이 없거나 다른 사용자의 설정이면 업데이트하지 않음
                if (userConfig == null || userConfig.Id != _user.Id)
                    return;

                // 변경된 정보 업데이트
                userConfig.Name = _user.Name;
                userConfig.ProfileImage = _user.ProfileImage;
                userConfig.StatusMessage = _user.StatusMessage;
                userConfig.LastModified = DateTime.Now;

                // 업데이트된 정보 저장 - Formatting 네임스페이스 충돌 해결
                string updatedJson = JsonConvert.SerializeObject(userConfig, JsonFormatting.Indented);
                File.WriteAllText(configPath, updatedJson);
            }
            catch (Exception ex)
            {
                // 로컬 설정 업데이트 실패는 서버 통신에 영향을 주지 않도록 조용히 로깅만 함
                Console.WriteLine($"로컬 설정 파일 업데이트 실패: {ex.Message}");
            }
        }

        // 설정 파일 경로 반환 메서드
        private string GetConfigFilePath()
        {
            return IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, USER_CONFIG_FILE);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 애니메이션과 함께 창 닫기
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            fadeOut.Completed += (s, args) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private async void ChangePhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 이미지 파일 선택 후 로딩 표시
                    ShowLoadingIndicator(true);

                    // 선택한 이미지 파일을 서버에 업로드
                    var imageUrl = await UploadProfileImageToServer(openFileDialog.FileName);

                    Console.WriteLine($"서버에서 반환된 이미지 URL: {imageUrl}");

                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        // 성공적으로 업로드된 경우 이미지 URL을 사용자 정보에 저장
                        _user.ProfileImage = imageUrl;

                        // 이미지 로드 및 표시
                        LoadProfileImage(); // 수정된 메서드 호출

                        // 서버에 프로필 정보 업데이트
                        bool updateSuccess = await UpdateUserProfileOnServer();
                        if (updateSuccess)
                        {
                            MessageBox.Show("프로필 이미지가 성공적으로 업데이트되었습니다.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"이미지 업로드 중 오류 발생: {ex.Message}");
                }
                finally
                {
                    ShowLoadingIndicator(false);
                }
            }
        }

        // 프로필 이미지를 서버에 업로드하는 메서드
        private async Task<string> UploadProfileImageToServer(string imagePath)
        {
            try
            {
                string serverUrl = AppSettings.GetServerUrl();
                string apiUrl = $"{serverUrl}/api/File/uploadProfileImage?userId={_user.Id}";

                using (HttpClient client = new HttpClient())
                using (var content = new MultipartFormDataContent())
                {
                    // 파일 스트림 열기
                    using (var fileStream = File.OpenRead(imagePath))
                    {
                        // 파일 이름 가져오기
                        string fileName = IOPath.GetFileName(imagePath);

                        // 멀티파트 폼에 파일 추가
                        var fileContent = new StreamContent(fileStream);
                        content.Add(fileContent, "file", fileName);

                        // 서버에 업로드 요청
                        var response = await client.PostAsync(apiUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            // 서버 응답에서 이미지 URL 추출
                            var jsonResponse = await response.Content.ReadAsStringAsync();
                            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                            return result.Url;
                        }
                        else
                        {
                            string errorMessage = await response.Content.ReadAsStringAsync();
                            throw new Exception($"서버 응답 실패: {response.StatusCode}\n{errorMessage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"이미지 업로드 실패: {ex.Message}");
                throw;
            }
        }

        private void ShowLoadingIndicator(bool show)
        {
            LoadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            if (show)
            {
                // 페이드인 효과
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
                LoadingIndicator.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        // 사용자 설정 파일 클래스
        public class UserConfig
        {
            public string Id { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty; // 이미 해싱된 비밀번호
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public int Index { get; set; }
            public DateTime LastLogin { get; set; }
            public DateTime LastModified { get; set; }
            public string ProfileImage { get; set; } = string.Empty;
            public string StatusMessage { get; set; } = string.Empty;
        }
    }
}