using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using static chatapp.MainWindow;

namespace chatapp
{
    public partial class AddFriendWindow : Window
    {
        private readonly UserData _currentUser;

        public AddFriendWindow(UserData currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
        }

        private async void RequestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(FriendIndexInput.Text.Trim(), out int targetIndex))
            {
                MessageBox.Show("유효한 Index 번호를 입력하세요.");
                return;
            }

            try
            {
                using HttpClient client = new();

                // ✅ 1. 서버에 유저 존재 여부 확인
                var userCheckResponse = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/User/getUserByIndex?index={targetIndex}");
                if (!userCheckResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Index {targetIndex}에 해당하는 유저가 존재하지 않습니다.");
                    return;
                }

                // ✅ 2. 중복 요청 검증 (서버 API 활용)
                var duplicateCheck = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/Friend/isRequestDuplicate?hostIndex={_currentUser.Index}&targetIndex={targetIndex}");
                if (await duplicateCheck.Content.ReadAsStringAsync() == "true")
                {
                    MessageBox.Show("이미 친구 요청을 보낸 상태입니다.");
                    return;
                }

                // ✅ 3. 이미 친구인지 확인
                var friendCheck = await client.GetAsync($"{AppSettings.GetServerUrl()}/api/Friend/isAlreadyFriend?hostIndex={_currentUser.Index}&targetIndex={targetIndex}");
                if (await friendCheck.Content.ReadAsStringAsync() == "true")
                {
                    MessageBox.Show("이미 친구인 상태입니다.");
                    return;
                }

                // ✅ 4. 친구 요청 API 호출
                string url = $"{AppSettings.GetServerUrl()}/api/Friend/add";
                var request = new FriendRequest
                {
                    HostIndex = _currentUser.Index,
                    GetIndex = targetIndex,
                    Action = "Waiting"
                };

                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("친구 요청을 보냈습니다.");
                    this.Close();
                }
                else
                {
                    MessageBox.Show($"요청 실패: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"에러: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public class FriendRequest
        {
            public int HostIndex { get; set; }
            public int GetIndex { get; set; }
            public string Action { get; set; } = string.Empty;
        }
    }
}
