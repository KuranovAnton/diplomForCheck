using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace diplomnarabotki.Services
{
    public class PhotoService
    {
        private readonly string _photosDirectory;

        public PhotoService()
        {
            // Папка для хранения фото в директории приложения
            _photosDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Photos");

            // Создаем папку, если её нет
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
                // Удаляем префикс "data:image/png;base64," если есть
                var base64 = base64Data;
                if (base64Data.Contains(","))
                {
                    base64 = base64Data.Substring(base64Data.IndexOf(",") + 1);
                }

                // Определяем расширение файла
                string extension = ".png";
                if (base64Data.Contains("image/jpeg") || base64Data.Contains("image/jpg"))
                    extension = ".jpg";
                else if (base64Data.Contains("image/png"))
                    extension = ".png";
                else if (base64Data.Contains("image/gif"))
                    extension = ".gif";

                // Генерируем уникальное имя файла
                var fileName = $"{travelId}_{pointId}_{DateTime.Now.Ticks}{extension}";
                var filePath = Path.Combine(_photosDirectory, fileName);

                // Конвертируем base64 в байты и сохраняем
                var imageBytes = Convert.FromBase64String(base64);
                await File.WriteAllBytesAsync(filePath, imageBytes);

                // Возвращаем относительный путь
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
            if (string.IsNullOrEmpty(relativePath))
                return string.Empty;

            try
            {
                var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
                if (File.Exists(fullPath))
                {
                    var bytes = await File.ReadAllBytesAsync(fullPath);
                    return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки фото: {ex.Message}");
            }

            return string.Empty;
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
        public void CleanupUnusedPhotos(ObservableCollection<Travel> travels)
        {
            if (!Directory.Exists(_photosDirectory))
                return;

            // Собираем все используемые пути
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

            // Удаляем неиспользуемые файлы
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