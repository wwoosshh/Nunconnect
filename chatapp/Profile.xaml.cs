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

namespace chatapp
{
    public partial class Profile : Window
    {
        private UserData _user;
        private string _userFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "users.txt");
        private string GetServerUrl()
        {
            // 서버인지 클라이언트인지 수동으로 설정
            bool isServerPc = true; // 🔥 서버 본체라면 true, 외부 클라이언트는 false

            if (isServerPc)
            {
                return "http://localhost:5159";
            }
            else
            {
                return "http://nunconnect.duckdns.org:5159";
            }
        }

        public Profile(UserData user)
        {
            InitializeComponent();
            _user = user;

            // UI 바인딩
            UserNameBox.Text = _user.Name;
            StatusMessageBox.Text = _user.StatusMessage;
            if (!string.IsNullOrEmpty(_user.ProfileImage) && File.Exists(_user.ProfileImage))
            {
                ProfileImage.Source = new BitmapImage(new Uri(_user.ProfileImage));
            }
        }


        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            _user.Name = UserNameBox.Text;
            _user.StatusMessage = StatusMessageBox.Text;

            try
            {
                string serverUrl = GetServerUrl();
                string apiUrl = $"{serverUrl}/api/User/update";

                using HttpClient client = new HttpClient();

                var request = new HttpRequestMessage(HttpMethod.Patch, apiUrl)
                {
                    Content = JsonContent.Create(_user) // ✅ JSON으로 변환
                };

                var response = await client.SendAsync(request); // PATCH 요청 실행

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("✅ 프로필이 성공적으로 저장되었습니다.");
                    this.Close();
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"❌ 서버 응답 실패: {response.StatusCode}\n{error}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❗ 오류 발생: {ex.Message}");
            }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // 창 닫기
        }
        private void ChangePhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg)|*.png;*.jpeg";

            if (openFileDialog.ShowDialog() == true)
            {
                _user.ProfileImage = openFileDialog.FileName;
                ProfileImage.Source = new BitmapImage(new Uri(_user.ProfileImage));
            }
        }

    }
}
