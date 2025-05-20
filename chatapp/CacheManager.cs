using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace chatapp
{
    public static class CacheManager
    {
        // 캐시 디렉토리 경로 (로컬 앱 데이터 폴더 내에 생성)
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "chatapp", "cache");

        // URL과 로컬 파일 경로 매핑 (메모리에 유지되는 부분)
        private static readonly ConcurrentDictionary<string, string> _urlToPathMap = new ConcurrentDictionary<string, string>();

        // 최대 캐시 크기 (MB)
        private static readonly int MAX_CACHE_SIZE_MB = 100;

        // 캐시 만료 시간 (일)
        private static readonly int CACHE_EXPIRY_DAYS = 7;

        // 정적 생성자: 캐시 디렉토리 생성
        static CacheManager()
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                    Directory.CreateDirectory(CacheDirectory);

                // 앱 시작 시 캐시 정리 (백그라운드에서)
                Task.Run(() => CleanupExpiredCache());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"캐시 디렉토리 생성 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// URL을 디스크 캐시에서 가져오거나 다운로드하여 로컬 파일 경로 반환
        /// </summary>
        public static async Task<string> GetOrDownloadFileAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            try
            {
                // URL의 해시값으로 캐시 파일명 생성
                string fileName = GetHashedFileName(url);
                string filePath = Path.Combine(CacheDirectory, fileName);

                // 이미 캐시에 있는 경우
                if (File.Exists(filePath))
                {
                    // 파일 접근 시간 업데이트
                    File.SetLastAccessTime(filePath, DateTime.Now);
                    _urlToPathMap[url] = filePath;
                    return filePath;
                }

                // 캐시에 없는 경우 다운로드
                using (var client = new HttpClient())
                {
                    // 타임아웃 설정
                    client.Timeout = TimeSpan.FromSeconds(30);
                    byte[] data = await client.GetByteArrayAsync(url);

                    // 파일 쓰기 전에 디렉토리 다시 확인
                    if (!Directory.Exists(CacheDirectory))
                        Directory.CreateDirectory(CacheDirectory);

                    // 파일 저장
                    await File.WriteAllBytesAsync(filePath, data);
                    _urlToPathMap[url] = filePath;

                    // 캐시 크기 관리 (백그라운드에서)
                    Task.Run(() => CleanupCacheIfNeeded());

                    return filePath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"캐시 파일 다운로드 실패 [{url}]: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// URL을 해시하여 고유한 파일명 생성
        /// </summary>
        private static string GetHashedFileName(string url)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var hashString = BitConverter.ToString(hash).Replace("-", "");

                // 원본 URL에서 확장자 추출 시도
                string extension = ".dat";
                try
                {
                    extension = Path.GetExtension(new Uri(url).LocalPath);
                    if (string.IsNullOrEmpty(extension))
                        extension = ".dat";
                }
                catch
                {
                    // URL에서 확장자를 추출할 수 없는 경우 기본값 사용
                }

                return hashString.Substring(0, 16) + extension;
            }
        }

        /// <summary>
        /// 캐시 디렉토리 크기가 제한을 초과하면 오래된 파일 삭제
        /// </summary>
        private static void CleanupCacheIfNeeded()
        {
            try
            {
                var directoryInfo = new DirectoryInfo(CacheDirectory);
                if (!directoryInfo.Exists)
                    return;

                var files = directoryInfo.GetFiles().OrderBy(f => f.LastAccessTime).ToList();
                long totalSize = files.Sum(f => f.Length);
                long maxSize = MAX_CACHE_SIZE_MB * 1024 * 1024; // MB → Bytes

                // 최대 크기 초과 시 정리
                if (totalSize > maxSize)
                {
                    Console.WriteLine($"캐시 정리 시작: 현재 {totalSize / (1024 * 1024)}MB, 최대 {MAX_CACHE_SIZE_MB}MB");

                    foreach (var file in files)
                    {
                        if (totalSize <= maxSize)
                            break;

                        try
                        {
                            totalSize -= file.Length;
                            file.Delete();
                            Console.WriteLine($"캐시 파일 삭제: {file.Name}, 남은 크기: {totalSize / (1024 * 1024)}MB");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"캐시 파일 삭제 실패: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"캐시 정리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 만료된 캐시 파일 정리 (지정된 일수보다 오래된 파일)
        /// </summary>
        private static void CleanupExpiredCache()
        {
            try
            {
                var directoryInfo = new DirectoryInfo(CacheDirectory);
                if (!directoryInfo.Exists)
                    return;

                var cutoffDate = DateTime.Now.AddDays(-CACHE_EXPIRY_DAYS);
                var oldFiles = directoryInfo.GetFiles()
                    .Where(f => f.LastAccessTime < cutoffDate)
                    .ToList();

                if (oldFiles.Count > 0)
                {
                    Console.WriteLine($"만료된 캐시 파일 {oldFiles.Count}개 삭제 시작");

                    foreach (var file in oldFiles)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"만료 캐시 파일 삭제 실패: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"만료 캐시 정리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 캐시에서 URL에 해당하는 로컬 파일 경로 가져오기 (이미 다운로드된 경우)
        /// </summary>
        public static string GetCachedFilePath(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // 매핑 테이블에서 찾기
            if (_urlToPathMap.TryGetValue(url, out string filePath))
            {
                if (File.Exists(filePath))
                {
                    // 파일 접근 시간 업데이트
                    File.SetLastAccessTime(filePath, DateTime.Now);
                    return filePath;
                }

                // 파일이 삭제된 경우 매핑에서도 제거
                _urlToPathMap.TryRemove(url, out _);
            }

            // 캐시 디렉토리에서 직접 찾기
            string fileName = GetHashedFileName(url);
            string fullPath = Path.Combine(CacheDirectory, fileName);

            if (File.Exists(fullPath))
            {
                // 파일 접근 시간 업데이트
                File.SetLastAccessTime(fullPath, DateTime.Now);
                _urlToPathMap[url] = fullPath;
                return fullPath;
            }

            return null;
        }

        /// <summary>
        /// 캐시 완전 정리 (앱 설정에서 호출 가능)
        /// </summary>
        public static void ClearCache()
        {
            try
            {
                var directoryInfo = new DirectoryInfo(CacheDirectory);
                if (!directoryInfo.Exists)
                    return;

                foreach (var file in directoryInfo.GetFiles())
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"캐시 파일 삭제 실패: {ex.Message}");
                    }
                }

                // 매핑 테이블 초기화
                _urlToPathMap.Clear();

                Console.WriteLine("캐시 정리 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"캐시 정리 중 오류: {ex.Message}");
            }
        }
    }
}