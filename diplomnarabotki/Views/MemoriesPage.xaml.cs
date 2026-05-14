using diplomnarabotki.Services;
using diplomnarabotki.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace diplomnarabotki.Views
{
    public partial class MemoriesPage : Page
    {
        private readonly TravelViewModel _currentTravel;
        private readonly PhotoService _photoService;
        private readonly Dictionary<int, FrameworkElement> _cardElements = new();
        private FrameworkElement? _draggedCard;
        private Point _dragStartPoint;
        private bool _isDragging;
        private Grid? _zoomOverlay;

        // Для отката удаления
        private Stack<DeletedMemory> _undoStack = new();

        // Для масштабирования
        private double _currentScale = 1.0;
        private const double MinScale = 0.3;
        private const double MaxScale = 2.5;

        // Для панорамирования (правой кнопкой)
        private bool _isPanning = false;
        private Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;

        public MemoriesPage(TravelViewModel travel)
        {
            InitializeComponent();
            _currentTravel = travel;
            _photoService = new PhotoService();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadPhotoCards();
                LoadPositions();
                DrawStrings();
                ApplyFilter();
                UpdateUndoButtonState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке страницы: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadPhotoCards()
        {
            var allPoints = _currentTravel.RoutePoints.ToList();

            if (!allPoints.Any())
            {
                ShowEmptyMessage();
                return;
            }

            foreach (var point in allPoints)
            {
                await CreatePhotoCard(point);
            }
        }

        private async void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.GoBack();
            }
        }

        private void ShowEmptyMessage()
        {
            var emptyMessage = new TextBlock
            {
                Text = "📸 Нет фотографий в этом путешествии\n\nДобавьте фото к точкам на карте, чтобы они появились здесь",
                FontSize = 16,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };

            Canvas.SetLeft(emptyMessage, 400);
            Canvas.SetTop(emptyMessage, 300);
            MemoriesCanvas.Children.Add(emptyMessage);
        }

        private async System.Threading.Tasks.Task CreatePhotoCard(RoutePointViewModel point)
        {
            var border = new Border
            {
                Style = (Style)FindResource("MemoryCardStyle"),
                Tag = point.Id,
                Cursor = Cursors.Hand
            };

            // Двойной клик для открытия фото
            // Двойной клик для открытия фото
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    ShowPhotoModal(point);
                    e.Handled = true;
                }
            };

            var mainPanel = new StackPanel();

            var image = new Image
            {
                Width = 220,
                Height = 180,
                Stretch = Stretch.UniformToFill,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var loadedImage = await LoadPointPhoto(point);
            bool hasPhoto = loadedImage != null;

            if (!hasPhoto)
            {
                loadedImage = LoadPlaceholderImage();
            }

            if (loadedImage != null)
            {
                image.Source = loadedImage;
                if (hasPhoto)
                {
                    image.MouseLeftButtonDown += (s, e) => ShowPhotoModal(point);
                }
                mainPanel.Children.Add(image);
            }
            else
            {
                var placeholder = new Border
                {
                    Width = 220,
                    Height = 180,
                    Background = Brushes.LightGray,
                    Margin = new Thickness(10, 10, 10, 5),
                    CornerRadius = new CornerRadius(8)
                };
                var placeholderText = new TextBlock
                {
                    Text = "📷\nНет фото",
                    FontSize = 24,
                    TextAlignment = TextAlignment.Center,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center
                };
                placeholder.Child = placeholderText;
                mainPanel.Children.Add(placeholder);
            }

            // Статус
            var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };

            string statusText = point.Status switch
            {
                "visited" => "✅ Посещено",
                "planned" => "📅 Запланировано",
                "wish" => "⭐ Хочу посетить",
                _ => "📍 Обычное"
            };

            Brush statusColor = point.Status switch
            {
                "visited" => Brushes.Green,
                "planned" => Brushes.Orange,
                "wish" => Brushes.Purple,
                _ => Brushes.Gray
            };

            var statusBlock = new TextBlock
            {
                Text = statusText,
                FontSize = 11,
                Foreground = statusColor,
                FontWeight = FontWeights.Bold
            };
            statusPanel.Children.Add(statusBlock);
            mainPanel.Children.Add(statusPanel);

            var title = new TextBlock
            {
                Text = point.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(10, 5, 10, 2),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 40,
                ToolTip = point.Title
            };
            mainPanel.Children.Add(title);

            if (!string.IsNullOrEmpty(point.VisitDate))
            {
                var datePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                var dateIcon = new TextBlock { Text = "📅 ", FontSize = 11, Foreground = Brushes.Gray };
                var dateText = new TextBlock { Text = point.VisitDate, FontSize = 11, Foreground = Brushes.Gray };
                datePanel.Children.Add(dateIcon);
                datePanel.Children.Add(dateText);
                mainPanel.Children.Add(datePanel);
            }

            if (!string.IsNullOrEmpty(point.Description))
            {
                var desc = new TextBlock
                {
                    Text = point.Description.Length > 80 ? point.Description.Substring(0, 80) + "..." : point.Description,
                    FontSize = 10,
                    Foreground = Brushes.DarkGray,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(10, 2, 10, 5),
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 40
                };
                mainPanel.Children.Add(desc);
            }

            border.Child = mainPanel;

            // Drag & Drop обработчики (левая кнопка)
            border.MouseLeftButtonDown += Card_MouseLeftButtonDown;
            border.MouseMove += Card_MouseMove;
            border.MouseLeftButtonUp += Card_MouseLeftButtonUp;

            // Правая кнопка - контекстное меню
            border.MouseRightButtonDown += (s, e) => ShowContextMenu(s as Border, point, e);

            MemoriesCanvas.Children.Add(border);
            _cardElements[point.Id] = border;
        }

        private void ShowPhotoModal(RoutePointViewModel point)
        {
            if (_zoomOverlay != null) return;

            _zoomOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _zoomOverlay.MouseLeftButtonDown += (s, e) => ClosePhotoModal();

            var container = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20),
                Width = 600,
                MaxHeight = 700,
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 5, Opacity = 0.5 }
            };

            var mainPanel = new StackPanel();
            var topPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var closeButton = new Button
            {
                Content = "✖",
                Width = 36,
                Height = 36,
                FontSize = 18,
                Background = Brushes.LightGray,
                Foreground = Brushes.Black,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand,
                ToolTip = "Закрыть (Esc)"
            };
            closeButton.Click += (s, e) => ClosePhotoModal();
            topPanel.Children.Add(closeButton);
            mainPanel.Children.Add(topPanel);

            var image = new Image
            {
                Width = 500,
                Height = 400,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 10, 0, 10)
            };

            _ = LoadPointPhoto(point).ContinueWith(t =>
            {
                if (t.Result != null)
                {
                    Dispatcher.Invoke(() => image.Source = t.Result);
                }
            });
            mainPanel.Children.Add(image);

            string statusText = point.Status switch
            {
                "visited" => "✅ Посещено",
                "planned" => "📅 Запланировано",
                "wish" => "⭐ Хочу посетить",
                _ => "📍 Обычное"
            };

            Brush statusColor = point.Status switch
            {
                "visited" => Brushes.Green,
                "planned" => Brushes.Orange,
                "wish" => Brushes.Purple,
                _ => Brushes.Gray
            };

            var statusBlock = new TextBlock
            {
                Text = statusText,
                FontSize = 14,
                Foreground = statusColor,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            mainPanel.Children.Add(statusBlock);

            var title = new TextBlock
            {
                Text = point.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };
            mainPanel.Children.Add(title);

            if (!string.IsNullOrEmpty(point.VisitDate))
            {
                var dateText = new TextBlock
                {
                    Text = $"📅 {point.VisitDate}",
                    FontSize = 14,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                mainPanel.Children.Add(dateText);
            }

            if (!string.IsNullOrEmpty(point.Description))
            {
                var scrollDescription = new ScrollViewer
                {
                    MaxHeight = 150,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var desc = new TextBlock
                {
                    Text = point.Description,
                    FontSize = 12,
                    Foreground = Brushes.DarkGray,
                    TextAlignment = TextAlignment.Left,
                    TextWrapping = TextWrapping.Wrap
                };
                scrollDescription.Content = desc;
                mainPanel.Children.Add(scrollDescription);
            }

            container.Child = mainPanel;

            _zoomOverlay.Children.Add(container);
            container.HorizontalAlignment = HorizontalAlignment.Center;
            container.VerticalAlignment = VerticalAlignment.Center;

            MemoriesCanvas.Children.Add(_zoomOverlay);
            Canvas.SetLeft(_zoomOverlay, 0);
            Canvas.SetTop(_zoomOverlay, 0);
            Canvas.SetZIndex(_zoomOverlay, 2000);
            _zoomOverlay.Width = MemoriesCanvas.ActualWidth;
            _zoomOverlay.Height = MemoriesCanvas.ActualHeight;

            MemoriesCanvas.SizeChanged += ZoomOverlay_SizeChanged;

            _zoomOverlay.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    ClosePhotoModal();
            };
            _zoomOverlay.Focus();
        }

        private void ZoomOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_zoomOverlay != null)
            {
                _zoomOverlay.Width = MemoriesCanvas.ActualWidth;
                _zoomOverlay.Height = MemoriesCanvas.ActualHeight;
            }
        }

        private void ClosePhotoModal()
        {
            if (_zoomOverlay == null) return;

            MemoriesCanvas.SizeChanged -= ZoomOverlay_SizeChanged;
            MemoriesCanvas.Children.Remove(_zoomOverlay);
            _zoomOverlay = null;
        }

        private void ShowContextMenu(Border? card, RoutePointViewModel point, MouseButtonEventArgs e)
        {
            if (card == null) return;

            var contextMenu = new ContextMenu();

            var resetItem = new MenuItem { Header = "📍 Вернуть в начальную точку" };
            resetItem.Click += (s, args) => ResetCardPosition(card);
            contextMenu.Items.Add(resetItem);

            var editItem = new MenuItem { Header = "✏️ Редактировать заметку" };
            editItem.Click += async (s, args) => await EditMemory(point);
            contextMenu.Items.Add(editItem);

            var deleteItem = new MenuItem { Header = "🗑️ Удалить заметку", Foreground = Brushes.Red };
            deleteItem.Click += (s, args) => DeleteMemory(card, point);
            contextMenu.Items.Add(deleteItem);

            contextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void ResetCardPosition(FrameworkElement card)
        {
            AutoArrangeCards();
            SavePositions();
            DrawStrings();
        }

        private async System.Threading.Tasks.Task EditMemory(RoutePointViewModel point)
        {
            var originalPhotoUrl = point.PhotoUrl;
            var originalStoredPhotoPath = point.StoredPhotoPath;

            var dialog = new Window
            {
                Title = $"Редактировать - {point.Title}",
                Width = 400,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.White,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            stackPanel.Children.Add(new TextBlock { Text = "Название:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            var titleBox = new TextBox { Text = point.Title, Margin = new Thickness(0, 0, 0, 15) };
            stackPanel.Children.Add(titleBox);

            stackPanel.Children.Add(new TextBlock { Text = "Дата посещения:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            var dateBox = new TextBox { Text = point.VisitDate, Margin = new Thickness(0, 0, 0, 15) };
            stackPanel.Children.Add(dateBox);

            stackPanel.Children.Add(new TextBlock { Text = "Описание:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            var descBox = new TextBox { Text = point.Description, Height = 100, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 15) };
            stackPanel.Children.Add(descBox);

            var warningText = new TextBlock
            {
                Text = "⚠️ Фото не редактируется через эту форму.\nДля изменения фото используйте карту.",
                FontSize = 10,
                Foreground = Brushes.Orange,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(warningText);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var saveBtn = new Button { Content = "Сохранить", Width = 80, Margin = new Thickness(5), Background = Brushes.Green, Foreground = Brushes.White };
            var cancelBtn = new Button { Content = "Отмена", Width = 80, Margin = new Thickness(5) };

            saveBtn.Click += (s, e) =>
            {
                point.Title = titleBox.Text;
                point.VisitDate = dateBox.Text;
                point.Description = descBox.Text;
                point.PhotoUrl = originalPhotoUrl;
                point.StoredPhotoPath = originalStoredPhotoPath;
                UpdateCardContentOnly(point.Id);
                dialog.Close();
            };

            cancelBtn.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = new ScrollViewer { Content = stackPanel };
            dialog.ShowDialog();
        }

        private void UpdateCardContentOnly(int pointId)
        {
            if (_cardElements.ContainsKey(pointId))
            {
                var oldCard = _cardElements[pointId];
                var point = _currentTravel.RoutePoints.FirstOrDefault(p => p.Id == pointId);
                if (point != null)
                {
                    UpdateCardContent(oldCard, point);
                }
            }
        }

        private void UpdateCardContent(FrameworkElement card, RoutePointViewModel point)
        {
            if (card is not Border border) return;

            var mainPanel = new StackPanel();

            var image = new Image
            {
                Width = 220,
                Height = 180,
                Stretch = Stretch.UniformToFill,
                Margin = new Thickness(10, 10, 10, 5)
            };

            var loadedImage = LoadPointPhoto(point).Result;
            bool hasPhoto = loadedImage != null;

            if (!hasPhoto)
            {
                loadedImage = LoadPlaceholderImage();
            }

            if (loadedImage != null)
            {
                image.Source = loadedImage;
                if (hasPhoto)
                {
                    image.MouseLeftButtonDown += (s, e) => ShowPhotoModal(point);
                }
                mainPanel.Children.Add(image);
            }
            else
            {
                var placeholder = new Border
                {
                    Width = 220,
                    Height = 180,
                    Background = Brushes.LightGray,
                    Margin = new Thickness(10, 10, 10, 5),
                    CornerRadius = new CornerRadius(8)
                };
                var placeholderText = new TextBlock
                {
                    Text = "📷\nНет фото",
                    FontSize = 24,
                    TextAlignment = TextAlignment.Center,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center
                };
                placeholder.Child = placeholderText;
                mainPanel.Children.Add(placeholder);
            }

            var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };

            string statusText = point.Status switch
            {
                "visited" => "✅ Посещено",
                "planned" => "📅 Запланировано",
                "wish" => "⭐ Хочу посетить",
                _ => "📍 Обычное"
            };

            Brush statusColor = point.Status switch
            {
                "visited" => Brushes.Green,
                "planned" => Brushes.Orange,
                "wish" => Brushes.Purple,
                _ => Brushes.Gray
            };

            var statusBlock = new TextBlock
            {
                Text = statusText,
                FontSize = 11,
                Foreground = statusColor,
                FontWeight = FontWeights.Bold
            };
            statusPanel.Children.Add(statusBlock);
            mainPanel.Children.Add(statusPanel);

            var title = new TextBlock
            {
                Text = point.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(10, 5, 10, 2),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 40,
                ToolTip = point.Title
            };
            mainPanel.Children.Add(title);

            if (!string.IsNullOrEmpty(point.VisitDate))
            {
                var datePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                var dateIcon = new TextBlock { Text = "📅 ", FontSize = 11, Foreground = Brushes.Gray };
                var dateText = new TextBlock { Text = point.VisitDate, FontSize = 11, Foreground = Brushes.Gray };
                datePanel.Children.Add(dateIcon);
                datePanel.Children.Add(dateText);
                mainPanel.Children.Add(datePanel);
            }

            if (!string.IsNullOrEmpty(point.Description))
            {
                var desc = new TextBlock
                {
                    Text = point.Description.Length > 80 ? point.Description.Substring(0, 80) + "..." : point.Description,
                    FontSize = 10,
                    Foreground = Brushes.DarkGray,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(10, 2, 10, 5),
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 40
                };
                mainPanel.Children.Add(desc);
            }

            border.Child = mainPanel;
        }

        private void RefreshCard(int pointId)
        {
            UpdateCardContentOnly(pointId);
        }

        private void DeleteMemory(Border card, RoutePointViewModel point)
        {
            var result = MessageBox.Show($"Удалить заметку \"{point.Title}\"?\n\nЭто действие можно будет отменить.",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var position = new PositionData
                {
                    X = Canvas.GetLeft(card),
                    Y = Canvas.GetTop(card)
                };

                _undoStack.Push(new DeletedMemory
                {
                    Point = point,
                    Card = card,
                    Position = position
                });

                MemoriesCanvas.Children.Remove(card);
                _cardElements.Remove(point.Id);

                UpdateUndoButtonState();
                DrawStrings();

                MessageBox.Show($"Заметка \"{point.Title}\" удалена. Используйте кнопку \"Откатить\" для восстановления.",
                    "Удалено", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count > 0)
            {
                var deleted = _undoStack.Pop();

                if (!_currentTravel.RoutePoints.Any(p => p.Id == deleted.Point.Id))
                {
                    _currentTravel.RoutePoints.Add(deleted.Point);
                }

                MemoriesCanvas.Children.Add(deleted.Card);
                Canvas.SetLeft(deleted.Card, deleted.Position.X);
                Canvas.SetTop(deleted.Card, deleted.Position.Y);
                _cardElements[deleted.Point.Id] = deleted.Card;

                UpdateUndoButtonState();
                DrawStrings();

                MessageBox.Show($"Заметка \"{deleted.Point.Title}\" восстановлена.",
                    "Откат выполнен", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdateUndoButtonState()
        {
            UndoButton.IsEnabled = _undoStack.Count > 0;
        }

        private async System.Threading.Tasks.Task<BitmapImage?> LoadPointPhoto(RoutePointViewModel point)
        {
            try
            {
                if (!string.IsNullOrEmpty(point.PhotoUrl) && point.PhotoUrl.StartsWith("data:image"))
                {
                    return LoadImageFromBase64(point.PhotoUrl);
                }

                string? basePath = null;

                if (!string.IsNullOrEmpty(point.StoredPhotoPath))
                {
                    var directory = System.IO.Path.GetDirectoryName(point.StoredPhotoPath);
                    var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(point.StoredPhotoPath);
                    basePath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        directory ?? "",
                        fileNameWithoutExt);
                }
                else if (!string.IsNullOrEmpty(point.PhotoUrl) && !point.PhotoUrl.StartsWith("data:image"))
                {
                    var directory = System.IO.Path.GetDirectoryName(point.PhotoUrl);
                    var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(point.PhotoUrl);
                    basePath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        directory ?? "",
                        fileNameWithoutExt);
                }

                if (basePath != null)
                {
                    var fullPath = FindImageFile(basePath);
                    if (fullPath != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Загружаем фото точки: {fullPath}");
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(fullPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки фото точки: {ex.Message}");
                return null;
            }
        }

        private BitmapImage? LoadPlaceholderImage()
        {
            try
            {
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                string[] possibleBasePaths = new string[]
                {
                    System.IO.Path.Combine(baseDirectory, "Resources", "Photos", "Заглушка"),
                    System.IO.Path.Combine(baseDirectory, "Photos", "Заглушка"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Photos", "Заглушка"),
                    System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Photos", "Заглушка"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Resources\Photos\Заглушка")
                };

                foreach (var basePath in possibleBasePaths)
                {
                    var fullPath = FindImageFile(System.IO.Path.GetFullPath(basePath));
                    if (fullPath != null)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(fullPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }

                return CreateDrawingPlaceholder();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки заглушки: {ex.Message}");
                return CreateDrawingPlaceholder();
            }
        }

        private string? FindImageFile(string basePathWithoutExtension)
        {
            string[] supportedExtensions = new string[]
            {
                ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico"
            };

            foreach (var ext in supportedExtensions)
            {
                string fullPath = basePathWithoutExtension + ext;
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        private BitmapImage? CreateDrawingPlaceholder()
        {
            try
            {
                var drawingVisual = new DrawingVisual();
                using (var context = drawingVisual.RenderOpen())
                {
                    context.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, 220, 180));

                    var formattedText = new FormattedText(
                        "📷",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI Emoji"),
                        48,
                        Brushes.Gray,
                        96);

                    context.DrawText(formattedText, new Point(85, 50));

                    var text = new FormattedText(
                        "Нет фото",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        12,
                        Brushes.DarkGray,
                        96);

                    context.DrawText(text, new Point(80, 120));
                }

                var renderBitmap = new RenderTargetBitmap(220, 180, 96, 96, PixelFormats.Pbgra32);
                renderBitmap.Render(drawingVisual);

                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                    encoder.Save(stream);

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private BitmapImage? LoadImageFromBase64(string base64Data)
        {
            if (string.IsNullOrEmpty(base64Data))
                return null;

            try
            {
                var base64 = base64Data;
                if (base64Data.Contains(","))
                {
                    base64 = base64Data.Substring(base64Data.IndexOf(",") + 1);
                }

                var bytes = Convert.FromBase64String(base64);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(bytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка конвертации base64: {ex.Message}");
                return null;
            }
        }

        // Drag & Drop логика (левая кнопка)
        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border card)
            {
                _draggedCard = card;
                _dragStartPoint = e.GetPosition(MemoriesCanvas);
                _isDragging = true;
                card.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Card_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedCard != null)
            {
                var currentPos = e.GetPosition(MemoriesCanvas);
                var deltaX = currentPos.X - _dragStartPoint.X;
                var deltaY = currentPos.Y - _dragStartPoint.Y;

                var left = Canvas.GetLeft(_draggedCard);
                var top = Canvas.GetTop(_draggedCard);

                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                Canvas.SetLeft(_draggedCard, left + deltaX);
                Canvas.SetTop(_draggedCard, top + deltaY);

                _dragStartPoint = currentPos;

                if (ShowConnectionsCheckbox.IsChecked == true)
                {
                    DrawStrings();
                }
            }
        }

        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedCard != null)
            {
                _draggedCard.ReleaseMouseCapture();
                _draggedCard = null;
                _isDragging = false;
                SavePositions();

                if (ShowConnectionsCheckbox.IsChecked == true)
                {
                    DrawStrings();
                }
            }
        }

        private void DrawStrings()
        {
            if (MemoriesCanvas == null) return;

            try
            {
                var linesToRemove = MemoriesCanvas.Children.OfType<Line>().ToList();
                foreach (var line in linesToRemove)
                {
                    MemoriesCanvas.Children.Remove(line);
                }

                var labelsToRemove = MemoriesCanvas.Children.OfType<TextBlock>().Where(t => t.Tag?.ToString() == "DistanceLabel").ToList();
                foreach (var label in labelsToRemove)
                {
                    MemoriesCanvas.Children.Remove(label);
                }

                if (ShowConnectionsCheckbox?.IsChecked != true)
                    return;

                foreach (var travelString in _currentTravel.TravelStrings)
                {
                    if (_cardElements.ContainsKey(travelString.From) &&
                        _cardElements.ContainsKey(travelString.To))
                    {
                        var fromCard = _cardElements[travelString.From];
                        var toCard = _cardElements[travelString.To];

                        if (fromCard.Visibility != Visibility.Visible || toCard.Visibility != Visibility.Visible)
                            continue;

                        var fromPos = GetCardCenter(fromCard);
                        var toPos = GetCardCenter(toCard);

                        if (fromPos.HasValue && toPos.HasValue)
                        {
                            var color = TryParseColor(travelString.Color) ?? Colors.Orange;

                            var line = new Line
                            {
                                X1 = fromPos.Value.X,
                                Y1 = fromPos.Value.Y,
                                X2 = toPos.Value.X,
                                Y2 = toPos.Value.Y,
                                Stroke = new SolidColorBrush(color),
                                StrokeThickness = travelString.Width > 0 ? travelString.Width : 2,
                                StrokeDashArray = new DoubleCollection { 8, 4 },
                                Opacity = 0.6
                            };

                            Panel.SetZIndex(line, -1);
                            MemoriesCanvas.Children.Add(line);

                            var fromPoint = _currentTravel.RoutePoints.FirstOrDefault(p => p.Id == travelString.From);
                            var toPoint = _currentTravel.RoutePoints.FirstOrDefault(p => p.Id == travelString.To);

                            if (fromPoint != null && toPoint != null)
                            {
                                double distance = CalculateDistance(
                                    fromPoint.Latitude, fromPoint.Longitude,
                                    toPoint.Latitude, toPoint.Longitude);

                                string distanceText;
                                if (distance < 1)
                                    distanceText = $"{distance * 1000:F0} м";
                                else if (distance < 100)
                                    distanceText = $"{distance:F1} км";
                                else
                                    distanceText = $"{distance:F0} км";

                                var midX = (fromPos.Value.X + toPos.Value.X) / 2;
                                var midY = (fromPos.Value.Y + toPos.Value.Y) / 2;

                                var distanceLabel = new TextBlock
                                {
                                    Text = $"📏 {distanceText}",
                                    FontSize = 10,
                                    Foreground = new SolidColorBrush(color),
                                    Background = Brushes.White,
                                    Padding = new Thickness(4, 2, 4, 2),
                                    Tag = "DistanceLabel",
                                    Opacity = 0.85
                                };

                                double offsetX = 0;
                                double offsetY = -15;

                                Canvas.SetLeft(distanceLabel, midX + offsetX);
                                Canvas.SetTop(distanceLabel, midY + offsetY);
                                Panel.SetZIndex(distanceLabel, 1);
                                MemoriesCanvas.Children.Add(distanceLabel);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в DrawStrings: {ex.Message}");
            }
        }

        private Point? GetCardCenter(FrameworkElement card)
        {
            if (card == null || MemoriesCanvas == null) return null;

            var left = Canvas.GetLeft(card);
            var top = Canvas.GetTop(card);

            if (double.IsNaN(left) || double.IsNaN(top))
                return null;

            return new Point(left + card.Width / 2, top + card.Height / 2);
        }

        private Color? TryParseColor(string colorHex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(colorHex);
            }
            catch
            {
                return null;
            }
        }

        private void AutoArrangeButton_Click(object sender, RoutedEventArgs e)
        {
            AutoArrangeCards();
            SavePositions();
            DrawStrings();
        }

        private void AutoArrangeCards()
        {
            if (MemoriesCanvas == null) return;

            int cols = 4;
            double cardWidth = 240;
            double cardHeight = 320;
            double margin = 40;

            int index = 0;
            var pointsWithCards = _currentTravel.RoutePoints
                .Where(p => _cardElements.ContainsKey(p.Id))
                .ToList();

            foreach (var point in pointsWithCards)
            {
                int row = index / cols;
                int col = index % cols;
                double x = margin + col * (cardWidth + margin);
                double y = margin + row * (cardHeight + margin);

                Canvas.SetLeft(_cardElements[point.Id], x);
                Canvas.SetTop(_cardElements[point.Id], y);

                index++;
            }
        }

        private void ResetPositionsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Сбросить все позиции карточек?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var filePath = GetPositionsFilePath();
                if (File.Exists(filePath))
                    File.Delete(filePath);

                AutoArrangeCards();
                DrawStrings();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var kvp in _cardElements)
            {
                MemoriesCanvas.Children.Remove(kvp.Value);
            }
            _cardElements.Clear();

            await LoadPhotoCards();
            LoadPositions();
            DrawStrings();
            ApplyFilter();
        }

        private async void ExportImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bounds = VisualTreeHelper.GetDescendantBounds(MemoriesCanvas);
                var renderBitmap = new RenderTargetBitmap(
                    (int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);

                renderBitmap.Render(MemoriesCanvas);

                var dialog = new SaveFileDialog
                {
                    Filter = "PNG Image|*.png",
                    FileName = $"memories_{_currentTravel.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                };

                if (dialog.ShowDialog() == true)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                    using (var stream = File.Create(dialog.FileName))
                    {
                        encoder.Save(stream);
                    }

                    MessageBox.Show($"Доска сохранена в:\n{dialog.FileName}", "Экспорт завершен",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (FilterAll != null)
            {
                ApplyFilter();
            }
        }

        private void ShowConnections_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowConnectionsCheckbox != null)
            {
                DrawStrings();
            }
        }

        private void ApplyFilter()
        {
            try
            {
                if (FilterAll == null || FilterVisited == null || FilterPlanned == null || FilterWish == null)
                    return;

                string? filterStatus = null;
                if (FilterVisited.IsChecked == true)
                    filterStatus = "visited";
                else if (FilterPlanned.IsChecked == true)
                    filterStatus = "planned";
                else if (FilterWish.IsChecked == true)
                    filterStatus = "wish";

                foreach (var kvp in _cardElements)
                {
                    var point = _currentTravel.RoutePoints.FirstOrDefault(p => p.Id == kvp.Key);
                    if (point != null)
                    {
                        bool shouldShow = filterStatus == null || point.Status == filterStatus;
                        kvp.Value.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                    }
                }

                DrawStrings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в ApplyFilter: {ex.Message}");
            }
        }

        private void LoadPositions()
        {
            if (MemoriesCanvas == null) return;

            var filePath = GetPositionsFilePath();
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var positions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, PositionData>>(json);

                    if (positions != null)
                    {
                        foreach (var kvp in positions)
                        {
                            if (_cardElements.ContainsKey(kvp.Key))
                            {
                                Canvas.SetLeft(_cardElements[kvp.Key], kvp.Value.X);
                                Canvas.SetTop(_cardElements[kvp.Key], kvp.Value.Y);
                            }
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки позиций: {ex.Message}");
                }
            }

            AutoArrangeCards();
        }

        private void SavePositions()
        {
            if (MemoriesCanvas == null) return;

            var positions = new Dictionary<int, PositionData>();

            foreach (var kvp in _cardElements)
            {
                var left = Canvas.GetLeft(kvp.Value);
                var top = Canvas.GetTop(kvp.Value);

                if (!double.IsNaN(left) && !double.IsNaN(top))
                {
                    positions[kvp.Key] = new PositionData { X = left, Y = top };
                }
            }

            if (positions.Any())
            {
                var json = System.Text.Json.JsonSerializer.Serialize(positions);
                File.WriteAllText(GetPositionsFilePath(), json);
            }
        }

        private string GetPositionsFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = System.IO.Path.Combine(appData, "TravelJournal");
            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            return System.IO.Path.Combine(appFolder, $"memories_{_currentTravel.Id}_positions.json");
        }

        private void MemoriesCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawStrings();
        }

        private void ArrangeByMapButton_Click(object sender, RoutedEventArgs e)
        {
            ArrangeCardsByMapCoordinates();
            SavePositions();
            DrawStrings();
        }

        private void ArrangeCardsByMapCoordinates()
        {
            if (MemoriesCanvas == null) return;

            var pointsWithCards = _currentTravel.RoutePoints
                .Where(p => _cardElements.ContainsKey(p.Id))
                .OrderBy(p => p.Order)
                .ToList();

            if (pointsWithCards.Count == 0) return;

            double cardWidth = 240;
            double cardHeight = 320;

            double canvasWidth = Math.Max(2000, MemoriesCanvas.ActualWidth);
            double canvasHeight = Math.Max(2000, MemoriesCanvas.ActualHeight);
            MemoriesCanvas.Width = canvasWidth;
            MemoriesCanvas.Height = canvasHeight;

            double centerX = canvasWidth / 2;
            double centerY = canvasHeight / 2;

            double fixedDistance = 320;

            var firstPoint = pointsWithCards[0];
            Canvas.SetLeft(_cardElements[firstPoint.Id], centerX - cardWidth / 2);
            Canvas.SetTop(_cardElements[firstPoint.Id], centerY - cardHeight / 2);

            for (int i = 1; i < pointsWithCards.Count; i++)
            {
                var currentPoint = pointsWithCards[i];
                var prevPoint = pointsWithCards[i - 1];

                if (!_cardElements.ContainsKey(currentPoint.Id) || !_cardElements.ContainsKey(prevPoint.Id))
                    continue;

                double deltaLat = currentPoint.Latitude - prevPoint.Latitude;
                double deltaLng = currentPoint.Longitude - prevPoint.Longitude;

                double length = Math.Sqrt(deltaLat * deltaLat + deltaLng * deltaLng);
                if (length == 0) length = 1;

                double dirX = deltaLng / length;
                double dirY = -deltaLat / length;

                double prevLeft = Canvas.GetLeft(_cardElements[prevPoint.Id]);
                double prevTop = Canvas.GetTop(_cardElements[prevPoint.Id]);

                if (double.IsNaN(prevLeft)) prevLeft = centerX - cardWidth / 2;
                if (double.IsNaN(prevTop)) prevTop = centerY - cardHeight / 2;

                double prevCenterX = prevLeft + cardWidth / 2;
                double prevCenterY = prevTop + cardHeight / 2;

                double newCenterX = prevCenterX + dirX * fixedDistance;
                double newCenterY = prevCenterY + dirY * fixedDistance;

                double margin = 50;
                double newLeft = newCenterX - cardWidth / 2;
                double newTop = newCenterY - cardHeight / 2;

                if (newLeft < margin)
                    newLeft = margin;
                if (newTop < margin)
                    newTop = margin;
                if (newLeft + cardWidth > canvasWidth - margin)
                    newLeft = canvasWidth - cardWidth - margin;
                if (newTop + cardHeight > canvasHeight - margin)
                    newTop = canvasHeight - cardHeight - margin;

                Canvas.SetLeft(_cardElements[currentPoint.Id], newLeft);
                Canvas.SetTop(_cardElements[currentPoint.Id], newTop);
            }

            var scrollViewer = FindParent<ScrollViewer>(MemoriesCanvas);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToHorizontalOffset((canvasWidth - scrollViewer.ViewportWidth) / 2);
                scrollViewer.ScrollToVerticalOffset((canvasHeight - scrollViewer.ViewportHeight) / 2);
            }
        }

        // Масштабирование колесиком мыши (только при Ctrl)
        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Запрещаем масштабирование, если открыто модальное окно с фото
            if (_zoomOverlay != null)
                return;

            // Масштабирование только при зажатом Ctrl
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            e.Handled = true;

            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            // Получаем позицию курсора относительно ScrollViewer
            Point mouseInScrollViewer = e.GetPosition(scrollViewer);

            // Позиция в координатах содержимого (учитываем текущий скролл)
            double contentX = scrollViewer.HorizontalOffset + mouseInScrollViewer.X;
            double contentY = scrollViewer.VerticalOffset + mouseInScrollViewer.Y;

            double delta = e.Delta > 0 ? 0.1 : -0.1;
            double newScale = _currentScale + delta;
            newScale = Math.Max(MinScale, Math.Min(MaxScale, newScale));

            if (Math.Abs(newScale - _currentScale) < 0.01) return;

            var scaleTransform = MemoriesCanvas.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform();
                MemoriesCanvas.RenderTransform = scaleTransform;
                MemoriesCanvas.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            double oldScale = _currentScale;
            double scaleRatio = newScale / oldScale;

            // Применяем новый масштаб
            scaleTransform.ScaleX = newScale;
            scaleTransform.ScaleY = newScale;
            _currentScale = newScale;

            // Корректируем скролл, чтобы точка под курсором осталась на месте
            double newHorizontalOffset = (contentX * scaleRatio) - mouseInScrollViewer.X;
            double newVerticalOffset = (contentY * scaleRatio) - mouseInScrollViewer.Y;

            scrollViewer.ScrollToHorizontalOffset(Math.Max(0, newHorizontalOffset));
            scrollViewer.ScrollToVerticalOffset(Math.Max(0, newVerticalOffset));

            DrawStrings();
        }

        // Панорамирование зажатой ПРАВОЙ кнопкой
        private void MainScrollViewer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Запрещаем панорамирование, если открыто модальное окно с фото
            if (_zoomOverlay != null)
                return;

            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            _isPanning = true;
            _panStartPoint = e.GetPosition(scrollViewer);
            _panStartHorizontalOffset = scrollViewer.HorizontalOffset;
            _panStartVerticalOffset = scrollViewer.VerticalOffset;

            scrollViewer.Cursor = Cursors.ScrollAll;
            scrollViewer.CaptureMouse();
            e.Handled = true;
        }

        private void MainScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;
            if (_zoomOverlay != null) return;

            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            Point currentPoint = e.GetPosition(scrollViewer);

            double deltaX = currentPoint.X - _panStartPoint.X;
            double deltaY = currentPoint.Y - _panStartPoint.Y;

            scrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - deltaX);
            scrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - deltaY);

            e.Handled = true;
        }

        private void MainScrollViewer_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            _isPanning = false;
            scrollViewer.Cursor = null;
            scrollViewer.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void MainScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var scrollViewer = sender as ScrollViewer;
                if (scrollViewer != null)
                {
                    _isPanning = false;
                    scrollViewer.Cursor = null;
                    scrollViewer.ReleaseMouseCapture();
                }
            }
        }

        private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            // Запрещаем сброс масштаба, если открыто модальное окно с фото
            if (_zoomOverlay != null)
                return;

            _currentScale = 1.0;
            var scaleTransform = MemoriesCanvas.RenderTransform as ScaleTransform;
            if (scaleTransform != null)
            {
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
            }

            var scrollViewer = FindParent<ScrollViewer>(MemoriesCanvas);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToHorizontalOffset((MemoriesCanvas.Width - scrollViewer.ViewportWidth) / 2);
                scrollViewer.ScrollToVerticalOffset((MemoriesCanvas.Height - scrollViewer.ViewportHeight) / 2);
            }

            DrawStrings();
        }

        // Вспомогательные методы
        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            double R = 6371;
            double dLat = ToRadians(lat2 - lat1);
            double dLng = ToRadians(lng2 - lng1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        private class DeletedMemory
        {
            public RoutePointViewModel Point { get; set; } = null!;
            public Border Card { get; set; } = null!;
            public PositionData Position { get; set; } = null!;
        }

        private class PositionData
        {
            public double X { get; set; }
            public double Y { get; set; }
        }
    }
}