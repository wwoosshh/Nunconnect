using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace chatapp
{
    public partial class ImageViewerWindow : Window
    {
        public ImageViewerWindow(string imageUrl)
        {
            InitializeComponent();
            try
            {
                ViewerImage.Source = new BitmapImage(new Uri(imageUrl));
            }
            catch
            {
                MessageBox.Show("이미지를 불러오는 데 실패했습니다.");
            }
        }

        private void ViewerImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 이미지 클릭 시 창 닫기 (원하면 삭제 가능)
            this.Close();
        }
    }
}
