using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;

namespace chatapp
{
    public static class ApiClient
    {
        // 중요: HttpClient는 응용 프로그램 수명 동안 한 번 인스턴스화하여 재사용해야 함
        private static readonly HttpClient _client = new HttpClient();

        static ApiClient()
        {
            _client.Timeout = TimeSpan.FromSeconds(AppSettings.DefaultRequestTimeout);
        }

        public static async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{AppSettings.GetServerUrl()}{endpoint}";
                var response = await _client.GetAsync(url, cancellationToken);

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("서버 응답 시간이 초과되었습니다.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"API 요청 오류: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"예상치 못한 오류: {ex.Message}");
            }
        }

        public static async Task<T> PostAsync<T>(string endpoint, object data, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{AppSettings.GetServerUrl()}{endpoint}";

                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(url, content, cancellationToken);

                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseJson);
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("서버 응답 시간이 초과되었습니다.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"API 요청 오류: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"예상치 못한 오류: {ex.Message}");
            }
        }

        public static async Task PostAsync(string endpoint, object data, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{AppSettings.GetServerUrl()}{endpoint}";

                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(url, content, cancellationToken);

                response.EnsureSuccessStatusCode();
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("서버 응답 시간이 초과되었습니다.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"API 요청 오류: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"예상치 못한 오류: {ex.Message}");
            }
        }

        // 이 메서드는 사용하지 않는 것이 좋지만, 기존 코드 호환성을 위해 유지
        // 중요: 이 메서드로 반환된 HttpClient를 using 문에서 절대 사용하지 마세요!
        public static HttpClient GetClient()
        {
            return _client;
        }
    }
}