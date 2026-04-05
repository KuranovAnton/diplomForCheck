using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace diplomnarabotki
{
    public partial class MapPage : Page
    {
        private ObservableCollection<Travel> _travels;
        private Travel _currentTravel;
        private string _mapHtmlPath;
        private bool _isMapLoaded = false;

        public MapPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTravels();
            LoadMapHtml();
        }

        private void LoadTravels()
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
                MessageBox.Show($"Ошибка загрузки путешествий: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadMapHtml()
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

                // Читаем HTML файл
                string htmlContent = File.ReadAllText(mapHtmlPath);

                // Настраиваем взаимодействие с JavaScript
                WebBrowserMap.ObjectForScripting = new MapScriptInterface(this);

                // Загружаем HTML
                WebBrowserMap.NavigateToString(htmlContent);
                WebBrowserMap.LoadCompleted += (s, e) =>
                {
                    _isMapLoaded = true;
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

        private void WebBrowserMap_LoadCompleted(object sender, NavigationEventArgs e)
        {
            _isMapLoaded = true;

            // Загружаем сохраненные точки для текущего путешествия
            if (_currentTravel != null && _currentTravel.RoutePoints != null && _currentTravel.RoutePoints.Any())
            {
                LoadPointsToMap();
            }
        }

        private void LoadPointsToMap()
        {
            if (!_isMapLoaded || _currentTravel?.RoutePoints == null) return;

            try
            {
                var pointsJson = JsonSerializer.Serialize(_currentTravel.RoutePoints);
                WebBrowserMap.InvokeScript("loadPoints", new object[] { pointsJson });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки точек: {ex.Message}");
            }
        }

        private void SavePointsFromMap()
        {
            if (!_isMapLoaded || _currentTravel == null) return;

            try
            {
                var pointsJson = WebBrowserMap.InvokeScript("exportPoints")?.ToString();
                if (!string.IsNullOrEmpty(pointsJson))
                {
                    var points = JsonSerializer.Deserialize<ObservableCollection<RoutePoint>>(pointsJson);
                    _currentTravel.RoutePoints = points ?? new ObservableCollection<RoutePoint>();
                    SaveTravels();

                    MessageBox.Show($"Сохранено {_currentTravel.RoutePoints.Count} точек маршрута!",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения точек: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Добавьте этот метод в класс MapPage
        private void CheckCacheStatus()
        {
            try
            {
                var status = WebBrowserMap.InvokeScript("getCacheStatus");
                System.Diagnostics.Debug.WriteLine($"Cache status: {status}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache check error: {ex.Message}");
            }
        }

        private void SaveTravels()
        {
            try
            {
                string saveFilePath = "travels.json";
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    IncludeFields = false
                };
                string json = JsonSerializer.Serialize(_travels, options);
                File.WriteAllText(saveFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbTravels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTravels.SelectedItem is Travel selectedTravel)
            {
                _currentTravel = selectedTravel;

                if (_isMapLoaded)
                {
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

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            // Возвращаемся к планеру
            if (NavigationService != null)
            {
                NavigationService.GoBack();
            }
        }

        // Класс для взаимодействия с JavaScript
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
                // Вызываем в UI потоке
                Application.Current.Dispatcher.Invoke(() =>
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
                    }
                });
            }

            public void NotifyPointsCleared()
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_mapPage._currentTravel != null)
                    {
                        _mapPage._currentTravel.RoutePoints?.Clear();
                    }
                });
            }

            public void NotifyRouteShown(int pointCount)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"Построен маршрут из {pointCount} точек");
                });
            }
            public void NotifyRouteShown(int pointCount, double distanceKm)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"Построен маршрут из {pointCount} точек. Расстояние: {distanceKm} км");
                    // Можно показать сообщение пользователю
                    MessageBox.Show($"Маршрут построен!\nРасстояние: {distanceKm} км",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }

    }
}