﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.IO;

namespace chatapp
{
    public partial class MainWindow : Window
    {
        private const string USER_CONFIG_FILE = "user.cfg"; // 사용자 정보 파일 이름
        private const int MAX_LOGIN_ATTEMPTS = 5;           // 최대 로그인 시도 횟수
        private const int LOGIN_COOLDOWN_SECONDS = 60;      // 제한 시간 (초)
        private const int MIN_LOGIN_INTERVAL_SECONDS = 2;   // 최소 로그인 시도 간격 (초)
        private const int LOGIN_ATTEMPT_EXPIRY_DAYS = 7;    // 로그인 시도 기록 보관 기간 (일)

        public MainWindow()
        {
            InitializeComponent();
            IdTextBox.TextChanged += IdTextBox_TextChanged;
            PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 애니메이션 시작
            var logoStoryboard = (Storyboard)FindResource("LogoFadeInStoryboard");
            logoStoryboard.Begin();

            var loginFormStoryboard = (Storyboard)FindResource("LoginFormFadeInStoryboard");
            loginFormStoryboard.Begin();

            // 버전 검사
            //CheckAppVersion();

            // 자동 로그인 시도
            TryAutoLogin();

            // 포커스 설정
            IdTextBox.Focus();
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

        /*private async void CheckAppVersion()
        {
            try
            {
                using HttpClient client = new HttpClient();
                string apiUrl = AppSettings.GetServerUrl();
                var response = await client.GetAsync($"{apiUrl}/api/User/checkVersion");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var versionData = JsonConvert.DeserializeObject<VersionResponse>(json);

                    if (versionData != null && versionData.Version != App.CurrentVersion) // App.CurrentVersion 사용
                    {
                        // 다운로드 URL이 없으면 기본 URL 생성
                        if (string.IsNullOrEmpty(versionData.DownloadUrl))
                        {
                            versionData.DownloadUrl = $"https://nunconnect.netlify.app/Connect_{versionData.Version}_ver.exe";
                        }

                        var result = MessageBox.Show(
                            $"현재 사용 중인 버전({App.CurrentVersion})은 구버전입니다.\n" + // App.CurrentVersion 사용
                            $"최신 버전({versionData.Version})으로 업데이트하시겠습니까?\n\n" +
                            $"{versionData.ReleaseNotes}",
                            "업데이트 확인", MessageBoxButton.YesNo, MessageBoxImage.Information
                        );
                        if (result == MessageBoxResult.Yes)
                        {
                            // 업데이트 창 열기
                            var updaterWindow = new UpdaterWindow(versionData.DownloadUrl, versionData.Version);
                            updaterWindow.Show();
                            this.Close(); // 로그인 창 닫기
                        }
                        else
                        {
                            // 홈페이지 열고 프로그램 종료
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://nunconnect.netlify.app/",
                                UseShellExecute = true
                            });
                            Application.Current.Shutdown();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("버전 확인 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }*/
        // 자동 로그인 시도 메서드
        private async void TryAutoLogin()
        {
            try
            {
                // 사용자 정보 파일 존재 여부 확인
                string configPath = GetConfigFilePath();
                if (!File.Exists(configPath))
                    return; // 파일이 없으면 자동 로그인 불가

                // 파일에서 사용자 정보 로드
                var userInfo = LoadUserInfo();
                if (userInfo == null || string.IsNullOrEmpty(userInfo.Id) || string.IsNullOrEmpty(userInfo.Password))
                    return; // 필수 정보가 없으면 자동 로그인 불가

                // 로딩 표시
                LoadingIndicator.Visibility = Visibility.Visible;

                // 서버에 로그인 요청 (이미 해싱된 비밀번호 사용)
                var user = await ValidateCredentialsFromServer(userInfo.Id, userInfo.Password, true);

                // 로딩 표시 숨김
                LoadingIndicator.Visibility = Visibility.Collapsed;

                if (user != null)
                {
                    // 로그인 성공, 채팅 목록 화면으로 이동
                    ChatList chatWindow = new ChatList(user);
                    chatWindow.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                // 자동 로그인 실패 시 일반 로그인으로 진행
                MessageBox.Show($"자동 로그인 실패: {ex.Message}", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string id = IdTextBox.Text.Trim();
            string pw = PasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            {
                MessageBox.Show("아이디와 비밀번호를 입력하세요.", "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 로그인 시도 제한 확인
            if (!CheckLoginAttemptLimit())
            {
                return;
            }

            // 로그인 버튼 비활성화 및 로딩 표시
            LoginButton.IsEnabled = false;
            LoadingIndicator.Visibility = Visibility.Visible;

            // 비밀번호 해싱 후 서버 로그인 (일반 로그인)
            var user = await ValidateCredentialsFromServer(id, pw, false);

            // 로딩 숨김 및 버튼 다시 활성화
            LoadingIndicator.Visibility = Visibility.Collapsed;
            LoginButton.IsEnabled = true;

            if (user != null)
            {
                // 로그인 성공, 사용자 정보 저장 및 시도 횟수 초기화
                SaveUserInfo(id, HashPassword(pw), user);
                ResetLoginAttempts(id);

                // 로그인 성공 함수 호출
                await LoginSuccess(user);
            }
            else
            {
                // 로그인 실패, 시도 횟수 증가
                RecordFailedLoginAttempt(id);
            }
        }

        // 로그인 시도 제한 확인 메서드
        private bool CheckLoginAttemptLimit()
        {
            // 기존 사용자 정보 로드
            var userInfo = LoadUserInfo();
            if (userInfo == null)
            {
                userInfo = new UserConfig();
                SaveUserConfig(userInfo);
            }

            // 일주일 이상 된 로그인 시도 기록 제거
            CleanupOldLoginAttempts(userInfo);

            // 1. 연속 실패 로그인 시도 개수 확인
            if (userInfo.FailedAttempts >= MAX_LOGIN_ATTEMPTS)
            {
                // 마지막 실패 시점으로부터 경과 시간 확인
                TimeSpan timeSinceLastFailed = DateTime.Now - userInfo.LastFailedAttempt;
                if (timeSinceLastFailed.TotalSeconds < LOGIN_COOLDOWN_SECONDS)
                {
                    int remainingSeconds = LOGIN_COOLDOWN_SECONDS - (int)timeSinceLastFailed.TotalSeconds;
                    MessageBox.Show($"로그인 시도 횟수를 초과하였습니다. {remainingSeconds}초 후에 다시 시도해주세요.",
                                  "로그인 제한", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                else
                {
                    // 제한 시간이 지났으므로 카운터 초기화
                    userInfo.FailedAttempts = 0;
                    SaveUserConfig(userInfo);
                }
            }

            // 2. 연속 로그인 시도 간격 확인 (최소 간격 적용)
            if (userInfo.LoginAttempts.Count > 0)
            {
                DateTime lastAttempt = userInfo.LoginAttempts.Last();
                TimeSpan timeSinceLastAttempt = DateTime.Now - lastAttempt;

                if (timeSinceLastAttempt.TotalSeconds < MIN_LOGIN_INTERVAL_SECONDS)
                {
                    MessageBox.Show($"너무 빠른 로그인 시도입니다. 잠시 후 다시 시도해주세요.",
                                  "로그인 제한", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            // 현재 로그인 시도 기록
            userInfo.LoginAttempts.Add(DateTime.Now);
            SaveUserConfig(userInfo);

            return true;
        }

        // 로그인 실패 기록 메서드
        private void RecordFailedLoginAttempt(string id)
        {
            var userInfo = LoadUserInfo() ?? new UserConfig();

            // 실패 횟수 증가 및 마지막 실패 시간 업데이트
            userInfo.FailedAttempts++;
            userInfo.LastFailedAttempt = DateTime.Now;

            SaveUserConfig(userInfo);
        }

        // 로그인 성공 시 시도 횟수 초기화 메서드
        private void ResetLoginAttempts(string id)
        {
            var userInfo = LoadUserInfo();
            if (userInfo != null)
            {
                userInfo.FailedAttempts = 0;
                SaveUserConfig(userInfo);
            }
        }

        // 사용자 설정 파일 저장 메서드 (기존 SaveUserInfo 메서드와 별도로 구현)
        private void SaveUserConfig(UserConfig userInfo)
        {
            try
            {
                // JSON으로 직렬화
                string json = JsonConvert.SerializeObject(userInfo, Formatting.Indented);

                // 파일에 저장
                string configPath = GetConfigFilePath();
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"사용자 정보 저장 실패: {ex.Message}", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 오래된 로그인 시도 기록 정리 메서드
        private void CleanupOldLoginAttempts(UserConfig userInfo)
        {
            if (userInfo.LoginAttempts == null)
            {
                userInfo.LoginAttempts = new List<DateTime>();
                return;
            }

            // 일주일 이전 날짜 계산
            DateTime cutoffDate = DateTime.Now.AddDays(-LOGIN_ATTEMPT_EXPIRY_DAYS);

            // 일주일 이상 지난 로그인 시도 제거
            userInfo.LoginAttempts = userInfo.LoginAttempts
                .Where(date => date > cutoffDate)
                .ToList();

            SaveUserConfig(userInfo);
        }

        // 로그인 성공 후 알림 서비스 초기화
        private async Task LoginSuccess(UserData user)
        {
            // 알림 서비스 초기화
            await NotificationService.Instance.Initialize(user);

            // 채팅 목록 화면으로 이동
            ChatList chatWindow = new ChatList(user);
            chatWindow.Show();
            this.Close();
        }

        private async Task<UserData> ValidateCredentialsFromServer(string id, string pw, bool isAutoLogin)
        {
            string hashedPw = isAutoLogin ? pw : HashPassword(pw); // 자동 로그인이면 이미 해싱된 상태

            try
            {
                using HttpClient client = new HttpClient();
                var requestData = new { Id = id, Password = hashedPw };
                string apiUrl = AppSettings.GetServerUrl();

                var response = await client.PostAsJsonAsync($"{apiUrl}/api/User/login", requestData);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var user = JsonConvert.DeserializeObject<UserData>(jsonString);

                    // 로그인 성공 시 비밀번호 찾기 버튼 숨김
                    FindCredentialsButton.Visibility = Visibility.Collapsed;

                    return user;
                }
                else
                {
                    // 자동 로그인에서는 오류 메시지 표시하지 않음
                    if (!isAutoLogin)
                    {
                        // 서버에서 오는 에러 메시지 표시
                        string errorMessage = await response.Content.ReadAsStringAsync();
                        if (string.IsNullOrEmpty(errorMessage))
                        {
                            errorMessage = "아이디 또는 비밀번호가 잘못되었습니다.";
                        }

                        MessageBox.Show(errorMessage, "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Warning);

                        // 로그인 실패 시 비밀번호 찾기 버튼 표시
                        FindCredentialsButton.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!isAutoLogin)
                {
                    MessageBox.Show($"서버 연결 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);

                    // 서버 연결 오류 시에도 비밀번호 찾기 버튼 표시
                    FindCredentialsButton.Visibility = Visibility.Visible;
                }
                else
                {
                    throw; // 자동 로그인에서는 예외를 다시 던짐
                }
            }

            return null;
        }
        private void FindCredentialsButton_Click(object sender, RoutedEventArgs e)
        {
            // 비밀번호 재설정 창 열기
            ResetPassword resetPasswordWindow = new ResetPassword();
            resetPasswordWindow.ShowDialog();
        }
        private string HashPassword(string password)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private void SignupButton_Click(object sender, RoutedEventArgs e)
        {
            Register registerWindow = new Register();
            registerWindow.ShowDialog();
        }

        private void IdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            IdHint.Visibility = string.IsNullOrEmpty(IdTextBox.Text)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, new RoutedEventArgs());
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordHint.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        // 사용자 정보 저장 메서드
        private void SaveUserInfo(string id, string hashedPassword, UserData userData)
        {
            try
            {
                // 저장할 사용자 정보 객체 생성
                var userInfo = new UserConfig
                {
                    Id = id,
                    Password = hashedPassword,
                    Name = userData.Name,
                    Email = userData.Email,
                    Index = userData.Index,
                    LastLogin = DateTime.Now
                };

                // JSON으로 직렬화
                string json = JsonConvert.SerializeObject(userInfo, Formatting.Indented);

                // 파일에 저장
                string configPath = GetConfigFilePath();
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"사용자 정보 저장 실패: {ex.Message}", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 사용자 정보 로드 메서드
        private UserConfig LoadUserInfo()
        {
            try
            {
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    return JsonConvert.DeserializeObject<UserConfig>(json);
                }
            }
            catch (Exception)
            {
                // 로드 실패 시 파일 삭제
                try
                {
                    File.Delete(GetConfigFilePath());
                }
                catch { }
            }

            return null;
        }

        // 설정 파일 경로 반환 메서드
        private string GetConfigFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, USER_CONFIG_FILE);
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

            // 로그인 시도 관련 필드 추가
            public List<DateTime> LoginAttempts { get; set; } = new List<DateTime>();
            public int FailedAttempts { get; set; } = 0;
            public DateTime LastFailedAttempt { get; set; } = DateTime.MinValue;
        }

        public class UserData
        {
            public int Index { get; set; }
            public string Id { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string ProfileImage { get; set; } = string.Empty;
            public string StatusMessage { get; set; } = string.Empty;
            public List<string> JoinedRoomIds { get; set; } = new();
        }

        public class VersionResponse
        {
            public string Version { get; set; } = string.Empty;
            public string DownloadUrl { get; set; } = string.Empty;
            public string ReleaseNotes { get; set; } = string.Empty;
        }
    }
}