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
using Microsoft.EntityFrameworkCore;

namespace diplomnarabotki
{
    public partial class MapPage : Page
    {
        private DatabaseService _dbService;
        private PhotoService _photoService;
        private ObservableCollection<Travel> _travels;
        private Travel _currentTravel;
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
                    var travels = JsonSerializer.Deserialize<ObservableCollection<Travel>>(json, options);
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
                    simplePoints.Add(new
                    {
                        Latitude = point.Latitude,
                        Longitude = point.Longitude,
                        Title = point.Title ?? "Place",
                        IconEmoji = point.IconEmoji ?? "📍",
                        Description = point.Description ?? "",
                        IconColor = point.IconColor ?? "#e2e8f0",
                        IconSize = point.IconSize > 0 ? point.IconSize : 36,
                        Status = point.Status ?? "planned",
                        PhotoUrl = point.PhotoUrl ?? "",
                        VisitDate = point.VisitDate ?? ""
                    });
                }

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

                        if (idToIndex.ContainsKey(s.From) && idToIndex.ContainsKey(s.To))
                        {
                            stringList.Add(new
                            {
                                from = idToIndex[s.From],
                                to = idToIndex[s.To],
                                description = s.Description ?? "",
                                color = s.Color ?? "#ed8936",
                                width = s.Width > 0 ? s.Width : 2
                            });
                            System.Diagnostics.Debug.WriteLine($"Added string: Index {idToIndex[s.From]}->{idToIndex[s.To]}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Cannot convert string from {s.From} to {s.To}");
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
                object result = null;
                try
                {
                    result = WebBrowserMap.InvokeScript("exportPoints");
                }
                catch (Exception jsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JS Error: {jsEx.Message}");
                    MessageBox.Show("Ошибка при экспорте точек с карты. Попробуйте обновить страницу.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var pointsJson = result?.ToString();

                if (string.IsNullOrEmpty(pointsJson))
                {
                    MessageBox.Show("Нет данных для сохранения", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Raw JSON from JS: {pointsJson}");

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var wrapper = JsonSerializer.Deserialize<PointsWrapper>(pointsJson, options);

                    System.Diagnostics.Debug.WriteLine($"Points from JS: {wrapper?.Points?.Count ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"Strings from JS: {wrapper?.Strings?.Count ?? 0}");

                    if (wrapper?.Strings != null)
                    {
                        foreach (var s in wrapper.Strings)
                        {
                            System.Diagnostics.Debug.WriteLine($"String from JS: From={s.From}, To={s.To}");
                        }
                    }

                    if (wrapper?.Points != null)
                    {
                        foreach (var point in wrapper.Points)
                        {
                            if (!string.IsNullOrEmpty(point.PhotoUrl) && point.PhotoUrl.StartsWith("data:image"))
                            {
                                var photoPath = await _photoService.SavePhotoAsync(
                                    point.PhotoUrl,
                                    _currentTravel.Id.ToString(),
                                    point.Order.ToString()
                                );
                                point.PhotoUrl = photoPath;
                            }
                        }

                        _currentTravel.RoutePoints = wrapper.Points;
                        System.Diagnostics.Debug.WriteLine($"Saved {_currentTravel.RoutePoints.Count} points");
                    }

                    if (wrapper?.Strings != null && wrapper.Strings.Count > 0)
                    {
                        _currentTravel.TravelStrings = new ObservableCollection<TravelString>();

                        var indexToId = new Dictionary<int, int>();
                        for (int i = 0; i < _currentTravel.RoutePoints.Count; i++)
                        {
                            indexToId[i] = _currentTravel.RoutePoints[i].Id;
                            System.Diagnostics.Debug.WriteLine($"Mapping: Index={i} -> Point Id={_currentTravel.RoutePoints[i].Id}");
                        }

                        foreach (var travelString in wrapper.Strings)
                        {
                            if (indexToId.ContainsKey(travelString.From) && indexToId.ContainsKey(travelString.To))
                            {
                                _currentTravel.TravelStrings.Add(new TravelString
                                {
                                    From = indexToId[travelString.From],
                                    To = indexToId[travelString.To],
                                    Description = travelString.Description ?? "",
                                    Color = travelString.Color ?? "#ed8936",
                                    Width = travelString.Width > 0 ? travelString.Width : 2
                                });
                                System.Diagnostics.Debug.WriteLine($"Added string: Index {travelString.From}->{travelString.To} converted to Id {indexToId[travelString.From]}->{indexToId[travelString.To]}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Warning: Cannot save string from index {travelString.From} to {travelString.To}");
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"Saved {_currentTravel.TravelStrings.Count} strings");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No strings to save");
                        _currentTravel.TravelStrings = new ObservableCollection<TravelString>();
                    }

                    await _dbService.SaveTravelAsync(_currentTravel);

                    MessageBox.Show($"Сохранено {_currentTravel.RoutePoints.Count} точек и {_currentTravel.TravelStrings.Count} связей!",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON Error: {jsonEx.Message}");
                    MessageBox.Show($"Ошибка разбора данных: {jsonEx.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения точек: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbTravels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTravels.SelectedItem is Travel selectedTravel)
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

        private void BtnLoadPoints_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTravel != null)
            {
                LoadPointsToMap();
                MessageBox.Show("Точки загружены на карту!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

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
            public ObservableCollection<RoutePoint> Points { get; set; } = new();
            public List<TravelString> Strings { get; set; } = new();
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
                            _mapPage._currentTravel.RoutePoints = new ObservableCollection<RoutePoint>();

                        _mapPage._currentTravel.RoutePoints.Add(new RoutePoint
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