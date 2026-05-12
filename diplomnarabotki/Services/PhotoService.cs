using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using diplomnarabotki.ViewModels;

namespace diplomnarabotki.Services
{
    public class PhotoService
    {
        private readonly string _photosDirectory;

        public PhotoService()
        {
            _photosDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Photos");
            if (!Directory.Exists(_photosDirectory))
            {
                Directory.CreateDirectory(_photosDirectory);
            }
        }

        // Сохранение фото из base64 в файл
        public async Task<string> SavePhotoAsync(string base64Data, string travelId, string pointId)
        {
            if (string.IsNullOrEmpty(base64Data))
                return string.Empty;

            try
            {
                var base64 = base64Data;
                if (base64Data.Contains(","))
                {
                    base64 = base64Data.Substring(base64Data.IndexOf(",") + 1);
                }

                // Определяем расширение файла
                string extension = ".png"; // по умолчанию
                if (base64Data.Contains("image/jpeg") || base64Data.Contains("image/jpg"))
                    extension = ".jpg";
                else if (base64Data.Contains("image/png"))
                    extension = ".png";
                else if (base64Data.Contains("image/gif"))
                    extension = ".gif";
                else if (base64Data.Contains("image/bmp"))
                    extension = ".bmp";
                else if (base64Data.Contains("image/webp"))
                    extension = ".webp";

                System.Diagnostics.Debug.WriteLine($"Extension detected: {extension}");

                var fileName = $"{travelId}_{pointId}_{DateTime.Now.Ticks}{extension}";
                var filePath = Path.Combine(_photosDirectory, fileName);

                var imageBytes = Convert.FromBase64String(base64);
                await File.WriteAllBytesAsync(filePath, imageBytes);

                System.Diagnostics.Debug.WriteLine($"File saved: {fileName}");
                System.Diagnostics.Debug.WriteLine($"Full path: {filePath}");

                return $"Photos/{fileName}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения фото: {ex.Message}");
                return string.Empty;
            }
        }

        // Загрузка фото из файла в base64
        public async Task<string> LoadPhotoAsBase64Async(string relativePath)
        {
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            System.Diagnostics.Debug.WriteLine($"=== LOAD PHOTO DEBUG ===");
            System.Diagnostics.Debug.WriteLine($"Relative: {relativePath}");
            System.Diagnostics.Debug.WriteLine($"Full path: {fullPath}");
            System.Diagnostics.Debug.WriteLine($"File exists: {File.Exists(fullPath)}");

            if (File.Exists(fullPath))
            {
                try
                {
                    var bytes = await File.ReadAllBytesAsync(fullPath);
                    System.Diagnostics.Debug.WriteLine($"File size: {bytes.Length} bytes");
                    var base64 = Convert.ToBase64String(bytes);
                    System.Diagnostics.Debug.WriteLine($"Base64 length: {base64.Length}");
                    return $"data:image/png;base64,{base64}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading file: {ex.Message}");
                    return "";
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"File NOT FOUND");
                return "";
            }
        }

        // Удаление фото
        public void DeletePhoto(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;

            try
            {
                var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления фото: {ex.Message}");
            }
        }

        // Очистка неиспользуемых фото
        public void CleanupUnusedPhotos(ObservableCollection<TravelViewModel> travels)
        {
            if (!Directory.Exists(_photosDirectory))
                return;

            var usedPaths = new HashSet<string>();
            foreach (var travel in travels)
            {
                foreach (var point in travel.RoutePoints)
                {
                    if (!string.IsNullOrEmpty(point.PhotoUrl) && point.PhotoUrl.StartsWith("Photos/"))
                    {
                        usedPaths.Add(point.PhotoUrl);
                    }
                }
            }

            foreach (var file in Directory.GetFiles(_photosDirectory))
            {
                var relativePath = $"Photos/{Path.GetFileName(file)}";
                if (!usedPaths.Contains(relativePath))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
            }
        }
    }
}