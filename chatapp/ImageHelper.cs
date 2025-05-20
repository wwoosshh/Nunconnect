// 새 파일: ImageHelper.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace chatapp
{
    public static class ImageHelper
    {
        public static async Task<BitmapImage> LoadImageFromUrlAsync(string url, int maxWidth = 0, int maxHeight = 0)
        {
            try
            {
                // 캐시에서 로컬 파일 경로 가져오기
                string localPath = await CacheManager.GetOrDownloadFileAsync(url);
                if (string.IsNullOrEmpty(localPath))
                    return null;

                // 리사이징 필요 여부 확인
                if (maxWidth > 0 && maxHeight > 0)
                {
                    return LoadResizedImage(localPath, maxWidth, maxHeight);
                }
                else
                {
                    // 원본 크기로 로드
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // 파일 스트림을 즉시 닫음
                    bitmap.UriSource = new Uri(localPath);
                    bitmap.EndInit();
                    bitmap.Freeze(); // UI 스레드와 백그라운드 스레드 간 공유 가능하도록
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"이미지 로드 실패 [{url}]: {ex.Message}");
                return null;
            }
        }

        public static BitmapImage LoadResizedImage(string filePath, int maxWidth, int maxHeight)
        {
            try
            {
                // 원본 이미지 크기 확인
                BitmapImage originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.UriSource = new Uri(filePath);
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.EndInit();

                // 원본이 이미 충분히 작으면 그대로 반환
                if (originalImage.PixelWidth <= maxWidth && originalImage.PixelHeight <= maxHeight)
                {
                    originalImage.Freeze();
                    return originalImage;
                }

                // 리사이징 비율 계산 (가로세로 비율 유지)
                double widthRatio = (double)maxWidth / originalImage.PixelWidth;
                double heightRatio = (double)maxHeight / originalImage.PixelHeight;
                double ratio = Math.Min(widthRatio, heightRatio);

                // 이미지 리사이징
                BitmapImage resizedImage = new BitmapImage();

                TransformedBitmap transformedBitmap = new TransformedBitmap(
                    originalImage,
                    new ScaleTransform(ratio, ratio)
                );

                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = 85; // 품질 레벨 (0-100)
                encoder.Frames.Add(BitmapFrame.Create(transformedBitmap));

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    encoder.Save(memoryStream);
                    memoryStream.Position = 0;

                    resizedImage.BeginInit();
                    resizedImage.CacheOption = BitmapCacheOption.OnLoad;
                    resizedImage.StreamSource = memoryStream;
                    resizedImage.EndInit();
                    resizedImage.Freeze();
                }

                return resizedImage;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"이미지 리사이징 실패: {ex.Message}");
                return null;
            }
        }

        public static async Task<BitmapImage> CreateThumbnailAsync(string url, int size = 100)
        {
            return await LoadImageFromUrlAsync(url, size, size);
        }
    }
}