using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using diplomnarabotki.Services;
using System.Threading.Tasks;
using diplomnarabotki.ViewModels;
using diplomnarabotki.Models;
using Microsoft.EntityFrameworkCore;

namespace diplomnarabotki.Views
{
    public partial class MapPage : Page
    {
        private DatabaseService _dbService;
        private PhotoService _photoService;
        private ObservableCollection<TravelViewModel> _travels;
        private TravelViewModel _currentTravel;
        private string _mapHtmlPath;
        private bool _isMapLoaded = false;

        public MapPage()
        {
            InitializeComponent();
            _dbService = new DatabaseService();
            _photoService = new PhotoService();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadTravelsFromDb();
            LoadMapHtml();
        }

        private async Task LoadTravelsFromDb()
        {
            try
            {
                _travels = await _dbService.LoadAllTravelsAsync();

                System.Diagnostics.Debug.WriteLine("=== LoadTravelsFromDb ===");
                System.Diagnostics.Debug.WriteLine($"Travels count: {_travels.Count}");

                foreach (var t in _travels)
                {
                    System.Diagnostics.Debug.WriteLine($"  Travel: Id={t.Id}, Name={t.Name}, Points={t.RoutePoints?.Count ?? 0}, Strings={t.TravelStrings?.Count ?? 0}");
                }

                CmbTravels.ItemsSource = _travels;
                CmbTravels.DisplayMemberPath = "Name";

                if (_travels.Count > 0)
                {
                    CmbTravels.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadTravelsFromDb: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки путешествий из БД: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                LoadTravelsFromJson();
            }
        }

        private void LoadTravelsFromJson()
        {
            try
            {
                string saveFilePath = "travels.json";
                if (File.Exists(saveFilePath))
                {
                    string json = File.ReadAllText(saveFilePath);
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        IncludeFields = false
                    };
                    var travels = JsonSerializer.Deserialize<ObservableCollection<TravelViewModel>>(json, options);
                    if (travels != null)
                    {
                        _travels = travels;
                        CmbTravels.ItemsSource = _travels;
                        CmbTravels.DisplayMemberPath = "Name";

                        if (_travels.Count > 0)
                        {
                            CmbTravels.SelectedIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки путешествий из JSON: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadMapHtml()
        {
            try
            {
                string resourcesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
                string mapHtmlPath = System.IO.Path.Combine(resourcesPath, "MapPage_Simple.html");

                if (!File.Exists(mapHtmlPath))
                {
                    MessageBox.Show($"Файл не найден: {mapHtmlPath}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string htmlContent = File.ReadAllText(mapHtmlPath);
                WebBrowserMap.ObjectForScripting = new MapScriptInterface(this);
                WebBrowserMap.NavigateToString(htmlContent);

                WebBrowserMap.LoadCompleted += async (s, e) =>
                {
                    _isMapLoaded = true;
                    System.Diagnostics.Debug.WriteLine("WebBrowser LoadCompleted");

                    await Task.Delay(1000);

                    if (_currentTravel?.RoutePoints != null && _currentTravel.RoutePoints.Any())
                    {
                        LoadPointsToMap();
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки карты: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadPointsToMap()
        {
            System.Diagnostics.Debug.WriteLine("=== LoadPointsToMap called ===");

            if (!_isMapLoaded)
            {
                System.Diagnostics.Debug.WriteLine("Map not loaded yet");
                return;
            }

            if (_currentTravel == null)
            {
                System.Diagnostics.Debug.WriteLine("No current travel");
                return;
            }

            try
            {
                if (WebBrowserMap != null)
                {
                    WebBrowserMap.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            WebBrowserMap.InvokeScript("clearAllMarkersSilent");
                            System.Diagnostics.Debug.WriteLine("Cleared map silently");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Clear error: {ex.Message}");
                        }
                    });
                }

                if (_currentTravel.RoutePoints == null || _currentTravel.RoutePoints.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No points to load");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Points count: {_currentTravel.RoutePoints.Count}");
                System.Diagnostics.Debug.WriteLine($"Strings count from DB: {_currentTravel.TravelStrings?.Count ?? 0}");

                foreach (var point in _currentTravel.RoutePoints)
                {
                    System.Diagnostics.Debug.WriteLine($"Point: Id={point.Id}, Title={point.Title}, Order={point.Order}");
                }

                var simplePoints = new List<object>();
                foreach (var point in _currentTravel.RoutePoints)
                {
                    string displayPhoto = "";

                    System.Diagnostics.Debug.WriteLine($"=== Point {point.Id} ===");
                    System.Diagnostics.Debug.WriteLine($"StoredPhotoPath: '{point.StoredPhotoPath}'");
                    System.Diagnostics.Debug.WriteLine($"StoredPhotoPath is null or empty: {string.IsNullOrEmpty(point.StoredPhotoPath)}");

                    // Загружаем фото из файла в base64, если есть путь
                    if (!string.IsNullOrEmpty(point.StoredPhotoPath) && point.StoredPhotoPath.StartsWith("Photos/"))
                    {
                        System.Diagnostics.Debug.WriteLine($"Loading photo from: {point.StoredPhotoPath}");
                        displayPhoto = await _photoService.LoadPhotoAsBase64Async(point.StoredPhotoPath);
                        System.Diagnostics.Debug.WriteLine($"Loaded photo length: {displayPhoto.Length}");
                    }
                    else if (!string.IsNullOrEmpty(point.PhotoUrl) && point.PhotoUrl.StartsWith("data:image"))
                    {
                        System.Diagnostics.Debug.WriteLine($"Using existing base64 from PhotoUrl");
                        displayPhoto = point.PhotoUrl;
                        System.Diagnostics.Debug.WriteLine($"Base64 length: {displayPhoto.Length}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping photo - condition not met");
                    }

                    simplePoints.Add(new
                    {
                        Id = point.Id,
                        Latitude = point.Latitude,
                        Longitude = point.Longitude,
                        Title = point.Title ?? "Place",
                        IconEmoji = point.IconEmoji ?? "📍",
                        Description = point.Description ?? "",
                        IconColor = point.IconColor ?? "#e2e8f0",
                        IconSize = point.IconSize > 0 ? point.IconSize : 36,
                        Status = point.Status ?? "planned",
                        PhotoUrl = displayPhoto,
                        VisitDate = point.VisitDate ?? ""
                    });
                }

                // Создаём словарь ID -> Index для конвертации ID в индексы для JavaScript
                var idToIndex = new Dictionary<int, int>();
                for (int i = 0; i < _currentTravel.RoutePoints.Count; i++)
                {
                    idToIndex[_currentTravel.RoutePoints[i].Id] = i;
                    System.Diagnostics.Debug.WriteLine($"Mapping: Point Id={_currentTravel.RoutePoints[i].Id} -> Index={i}");
                }

                var stringList = new List<object>();
                if (_currentTravel.TravelStrings != null && _currentTravel.TravelStrings.Count > 0)
                {
                    foreach (var s in _currentTravel.TravelStrings)
                    {
                        System.Diagnostics.Debug.WriteLine($"Processing string: From={s.From}, To={s.To}");

                        // Проверяем, что точки с такими ID существуют
                        if (_currentTravel.RoutePoints.Any(p => p.Id == s.From) &&
                            _currentTravel.RoutePoints.Any(p => p.Id == s.To))
                        {
                            stringList.Add(new
                            {
                                fromId = s.From,  // ✅ Отправляем настоящий ID
                                toId = s.To,      // ✅ Отправляем настоящий ID
                                description = s.Description ?? "",
                                color = s.Color ?? "#ed8936",
                                width = s.Width > 0 ? s.Width : 2
                            });
                            System.Diagnostics.Debug.WriteLine($"Added string: ID {s.From}->{s.To}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Cannot find points with IDs {s.From} and {s.To}");
                        }
                    }
                }

                var wrapper = new
                {
                    points = simplePoints,
                    strings = stringList
                };

                var pointsJson = JsonSerializer.Serialize(wrapper);
                System.Diagnostics.Debug.WriteLine($"JSON length: {pointsJson.Length}");
                System.Diagnostics.Debug.WriteLine($"Points in JSON: {simplePoints.Count}");
                System.Diagnostics.Debug.WriteLine($"Strings in JSON: {stringList.Count}");

                await Task.Delay(500);

                if (WebBrowserMap != null)
                {
                    WebBrowserMap.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var result = WebBrowserMap.InvokeScript("loadPoints", new object[] { pointsJson });
                            System.Diagnostics.Debug.WriteLine($"InvokeScript result: {result}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"InvokeScript error: {ex.Message}");
                        }
                    });
                }

                System.Diagnostics.Debug.WriteLine($"Loaded {simplePoints.Count} points and {stringList.Count} strings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadPointsToMap: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            }
        }

        private async void SavePointsFromMap()
        {
            if (!_isMapLoaded || _currentTravel == null) return;

            try
            {
                object result = WebBrowserMap.InvokeScript("exportPoints");
                var pointsJson = result?.ToString();

                System.Diagnostics.Debug.WriteLine("=== SavePointsFromMap ===");
                System.Diagnostics.Debug.WriteLine($"JSON length: {pointsJson?.Length ?? 0}");

                if (string.IsNullOrEmpty(pointsJson))
                {
                    MessageBox.Show("Нет данных для сохранения", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var wrapper = JsonSerializer.Deserialize<PointsWrapper>(pointsJson, options);

                if (wrapper?.Points == null)
                {
                    MessageBox.Show("Нет точек для сохранения", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // ========== 1. ВРЕМЕННО СОХРАНЯЕМ СВЯЗИ (С ИНДЕКСАМИ) ==========
                var tempStrings = new List<TravelStringContract>();
                if (wrapper.Strings != null)
                {
                    tempStrings.AddRange(wrapper.Strings);
                    System.Diagnostics.Debug.WriteLine($"Temporary saved {tempStrings.Count} strings with indices");
                }

                // ========== 2. СОХРАНЯЕМ ТОЛЬКО ТОЧКИ ==========
                var oldPoints = _currentTravel.RoutePoints.ToList();
                foreach (var oldPoint in oldPoints)
                {
                    if (!string.IsNullOrEmpty(oldPoint.StoredPhotoPath))
                    {
                        _photoService.DeletePhoto(oldPoint.StoredPhotoPath);
                    }
                }
                _currentTravel.RoutePoints.Clear();

                // Словарь для маппинга: старый ID -> новый ID
                var oldIdToNewId = new Dictionary<int, int>();

                for (int i = 0; i < wrapper.Points.Count; i++)
                {
                    var incomingPoint = wrapper.Points[i];
                    int oldId = incomingPoint.Id;

                    string savedPhotoPath = "";
                    if (!string.IsNullOrEmpty(incomingPoint.PhotoUrl) && incomingPoint.PhotoUrl.StartsWith("data:image"))
                    {
                        savedPhotoPath = await _photoService.SavePhotoAsync(
                            incomingPoint.PhotoUrl,
                            _currentTravel.Id.ToString(),
                            i.ToString()
                        );
                        System.Diagnostics.Debug.WriteLine($"Saved photo: {savedPhotoPath}");
                    }
                    else if (!string.IsNullOrEmpty(incomingPoint.StoredPhotoPath))
                    {
                        savedPhotoPath = incomingPoint.StoredPhotoPath;
                    }

                    var newPoint = new RoutePointViewModel
                    {
                        Id = 0,
                        Latitude = incomingPoint.Latitude,
                        Longitude = incomingPoint.Longitude,
                        Title = incomingPoint.Title,
                        IconEmoji = incomingPoint.IconEmoji,
                        Description = incomingPoint.Description,
                        IconColor = incomingPoint.IconColor,
                        IconSize = incomingPoint.IconSize,
                        Status = incomingPoint.Status,
                        StoredPhotoPath = savedPhotoPath,
                        VisitDate = incomingPoint.VisitDate,
                        Order = i
                    };

                    _currentTravel.RoutePoints.Add(newPoint);

                    // Сохраняем временно, чтобы получить ID
                    await _dbService.SaveTravelAsync(_currentTravel);

                    // Запоминаем маппинг старого ID на новый ID
                    if (oldId > 0)
                    {
                        oldIdToNewId[oldId] = newPoint.Id;
                    }
                    System.Diagnostics.Debug.WriteLine($"Mapping: old Id={oldId} -> new Id={newPoint.Id}");
                }

                // ========== 3. СОЗДАЁМ СВЯЗИ С НОВЫМИ ID ==========
                _currentTravel.TravelStrings.Clear();

                foreach (var tempString in tempStrings)
                {
                    // Конвертируем индексы/старые ID в новые ID
                    int newFromId = oldIdToNewId.ContainsKey(tempString.From) ? oldIdToNewId[tempString.From] : tempString.From;
                    int newToId = oldIdToNewId.ContainsKey(tempString.To) ? oldIdToNewId[tempString.To] : tempString.To;

                    // Проверяем, что точки с такими ID существуют
                    bool fromExists = _currentTravel.RoutePoints.Any(p => p.Id == newFromId);
                    bool toExists = _currentTravel.RoutePoints.Any(p => p.Id == newToId);

                    System.Diagnostics.Debug.WriteLine($"Creating string: From={newFromId} (exists={fromExists}), To={newToId} (exists={toExists})");

                    if (fromExists && toExists && newFromId != newToId)
                    {
                        _currentTravel.TravelStrings.Add(new TravelStringViewModel
                        {
                            From = newFromId,
                            To = newToId,
                            Description = tempString.Description ?? "",
                            Color = tempString.Color ?? "#ed8936",
                            Width = tempString.Width > 0 ? tempString.Width : 2
                        });
                    }
                }

                // ========== 4. ФИНАЛЬНОЕ СОХРАНЕНИЕ ==========
                await _dbService.SaveTravelAsync(_currentTravel);

                MessageBox.Show($"Сохранено {_currentTravel.RoutePoints.Count} точек и {_currentTravel.TravelStrings.Count} связей!",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем карту
                LoadPointsToMap();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                MessageBox.Show($"Ошибка сохранения точек: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbTravels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTravels.SelectedItem is TravelViewModel selectedTravel)
            {
                _currentTravel = selectedTravel;

                System.Diagnostics.Debug.WriteLine("=== Travel Selected ===");
                System.Diagnostics.Debug.WriteLine($"Name: {_currentTravel.Name}");
                System.Diagnostics.Debug.WriteLine($"Points count: {_currentTravel.RoutePoints?.Count ?? 0}");
                System.Diagnostics.Debug.WriteLine($"Strings count: {_currentTravel.TravelStrings?.Count ?? 0}");

                if (_isMapLoaded)
                {
                    if (WebBrowserMap != null)
                    {
                        WebBrowserMap.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                WebBrowserMap.InvokeScript("clearAllMarkersSilent");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Clear error: {ex.Message}");
                            }
                        });
                    }

                    await Task.Delay(200);
                    LoadPointsToMap();
                }
            }
        }

        private void BtnSavePoints_Click(object sender, RoutedEventArgs e)
        {
            SavePointsFromMap();
        }

        // Удален метод BtnLoadPoints_Click
        // Удален метод BtnBuildRoute_Click

        private async void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTravel != null)
            {
                await _dbService.SaveTravelAsync(_currentTravel);
            }

            if (NavigationService != null)
            {
                NavigationService.GoBack();
            }
        }

        public class PointsWrapper
        {
            public ObservableCollection<RoutePointViewModel> Points { get; set; } = new();
            public List<TravelStringContract> Strings { get; set; } = new();
        }

        public class TravelStringContract
        {
            [System.Text.Json.Serialization.JsonPropertyName("FromId")]
            public int From { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("ToId")]
            public int To { get; set; }

            public string Description { get; set; } = "";
            public string Color { get; set; } = "#ed8936";
            public double Width { get; set; } = 2;
        }

        [System.Runtime.InteropServices.ComVisible(true)]
        public class MapScriptInterface
        {
            private MapPage _mapPage;

            public MapScriptInterface(MapPage mapPage)
            {
                _mapPage = mapPage;
            }

            public void NotifyPointAdded(double lat, double lng, string title)
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (_mapPage._currentTravel != null)
                    {
                        if (_mapPage._currentTravel.RoutePoints == null)
                            _mapPage._currentTravel.RoutePoints = new ObservableCollection<RoutePointViewModel>();

                        _mapPage._currentTravel.RoutePoints.Add(new RoutePointViewModel
                        {
                            Latitude = lat,
                            Longitude = lng,
                            Title = title,
                            Order = _mapPage._currentTravel.RoutePoints.Count
                        });

                        await _mapPage._dbService.SaveTravelAsync(_mapPage._currentTravel);
                    }
                });
            }

            public void NotifyPointsCleared()
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (_mapPage._currentTravel != null)
                    {
                        _mapPage._currentTravel.RoutePoints?.Clear();
                        await _mapPage._dbService.SaveTravelAsync(_mapPage._currentTravel);
                    }
                });
            }

            public void NotifyRouteShown(int pointCount)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"Route built from {pointCount} points");
                });
            }

            public void NotifyRouteShown(int pointCount, double distanceKm)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"Route built from {pointCount} points. Distance: {distanceKm} km");
                    MessageBox.Show($"Route built!\nDistance: {distanceKm} km",
                        "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }

            public void NotifyPointsChanged(string pointsJson)
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (_mapPage._currentTravel != null && !string.IsNullOrEmpty(pointsJson))
                    {
                        try
                        {
                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };

                            var wrapper = JsonSerializer.Deserialize<PointsWrapper>(pointsJson, options);
                            if (wrapper?.Points != null)
                            {
                                _mapPage._currentTravel.RoutePoints = wrapper.Points;
                                await _mapPage._dbService.SaveTravelAsync(_mapPage._currentTravel);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error updating points: {ex.Message}");
                        }
                    }
                });
            }
        }
    }
}