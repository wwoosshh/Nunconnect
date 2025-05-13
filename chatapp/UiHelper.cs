using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace chatapp
{
    public static class UiHelper
    {
        // UI 스레드에서 안전하게 작업 실행
        public static void RunOnUiThread(Action action)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        // 로딩 표시 텍스트블록 생성
        public static TextBlock CreateLoadingTextBlock(string message = "로딩 중...")
        {
            return new TextBlock
            {
                Text = message,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };
        }

        // 오류 표시 텍스트블록 생성
        public static TextBlock CreateErrorTextBlock(string message)
        {
            return new TextBlock
            {
                Text = message,
                Foreground = Brushes.Red,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 10)
            };
        }

        // 버튼에 로딩 상태 설정
        public static void SetButtonLoading(Button button, bool isLoading, string originalText = null)
        {
            if (button == null) return;

            button.IsEnabled = !isLoading;
            button.Content = isLoading ? "⏳" : (originalText ?? button.Content);
        }
    }
}