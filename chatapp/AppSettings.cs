using System;

namespace chatapp
{
    public static class AppSettings
    {
        // 개발/서버 환경 설정 (앱 실행 시 설정 가능)
        public static bool IsServerPc { get; set; } = true;

        // 서버 URL 통합 관리
        public static string GetServerUrl()
        {
            return IsServerPc ? "http://localhost:5159" : "http://nunconnect.duckdns.org:5159";
        }

        // 추가 설정 값들
        public static int DefaultRequestTimeout { get; set; } = 10; // 초 단위
        public static int ImageViewerWidth { get; set; } = 800;
        public static int ImageViewerHeight { get; set; } = 600;
    }
}