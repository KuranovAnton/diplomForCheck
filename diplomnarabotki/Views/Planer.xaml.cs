using diplomnarabotki.Models;
using diplomnarabotki.Models.Enums;
using diplomnarabotki.Services;
using diplomnarabotki.ViewModels;
using diplomnarabotki.ViewModels.NoteViewModels;
using diplomnarabotki.Views;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace diplomnarabotki.Views
{
    public partial class Planer : Page
    {
        private DatabaseService _dbService;
        private ObservableCollection<TravelViewModel> _travels;
        private TravelViewModel _currentTravel;
        private NoteBaseViewModel _editingNote;
        private string _saveFilePath = "travels.json";
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private DispatcherTimer _notificationTimer;
        private SoundPlayer _soundPlayer;

        // Новые поля для поиска и фильтрации
        private string _currentSearchText = string.Empty;
        private string _currentFilterType = "All";
        private List<NoteDisplayItem> _allNotes = new List<NoteDisplayItem>();

        public Planer()
        {
            InitializeComponent();
            _dbService = new DatabaseService();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadTravelsFromDb();
            InitializeData();

            await MigrateOldData();

            if (LvPinnedNotes != null)
                UpdatePinnedNotesDisplay();

            if (LbNotes != null)
                UpdateNotesDisplay();

            InitializeNotificationTimer();
        }

        private async void BtnReloadTravels_Click(object sender, RoutedEventArgs e)
        {
            await LoadTravelsFromDb();
            MessageBox.Show("Путешествия перезагружены из БД!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task LoadTravelsFromDb()
        {
            try
            {
                _travels = await _dbService.LoadAllTravelsAsync();
                CmbTravels.ItemsSource = _travels;
                CmbTravels.DisplayMemberPath = "Name";
                CmbTravels.SelectedIndex = -1; // Сброс выбора

                if (_travels.Count > 0)
                {
                    CmbTravels.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTravelsFromDb Error: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки из БД: {ex.Message}\n\nБудет использован JSON файл.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                LoadTravels();
            }
        }

        private async Task MigrateOldData()
        {
            string jsonPath = "travels.json";
            if (File.Exists(jsonPath) && _travels.Count == 0)
            {
                var result = MessageBox.Show("Обнаружены старые данные в JSON файле.\nИмпортировать их в базу данных?",
                    "Миграция данных", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _dbService.MigrateFromJsonAsync(jsonPath);
                    await LoadTravelsFromDb();

                    if (File.Exists(jsonPath + ".backup"))
                        File.Delete(jsonPath + ".backup");
                    File.Move(jsonPath, jsonPath + ".backup");

                    MessageBox.Show("Данные успешно импортированы в базу данных!",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void InitializeNotificationTimer()
        {
            _notificationTimer = new DispatcherTimer();
            _notificationTimer.Interval = TimeSpan.FromSeconds(30);
            _notificationTimer.Tick += NotificationTimer_Tick;
            _notificationTimer.Start();
        }

        private void NotificationTimer_Tick(object sender, EventArgs e)
        {
            if (_currentTravel == null) return;

            var now = DateTime.Now;

            foreach (var note in _currentTravel.Notes)
            {
                if (note.Notification?.IsEnabled == true)
                {
                    CheckAndNotify(note, now);
                }
            }

            foreach (var note in _currentTravel.PinnedNotes)
            {
                if (note.Notification?.IsEnabled == true)
                {
                    CheckAndNotify(note, now);
                }
            }
        }

        private async void CheckAndNotify(NoteBaseViewModel note, DateTime currentTime)
        {
            var notification = note.Notification;
            if (notification == null) return;

            bool shouldNotify = false;
            DateTime? nextNotifyTime = null;

            switch (notification.RepeatType)
            {
                case ReminderRepeatType.None:
                    if (notification.ReminderTime <= currentTime &&
                        (!notification.LastNotified.HasValue || notification.LastNotified.Value.Date != currentTime.Date))
                    {
                        shouldNotify = true;
                        nextNotifyTime = null;
                    }
                    break;

                case ReminderRepeatType.EveryMinute:
                    if (notification.ReminderTime <= currentTime)
                    {
                        shouldNotify = true;
                        nextNotifyTime = currentTime.AddMinutes(1);
                    }
                    break;

                case ReminderRepeatType.Every5Minutes:
                    if (notification.ReminderTime <= currentTime)
                    {
                        shouldNotify = true;
                        nextNotifyTime = currentTime.AddMinutes(5);
                    }
                    break;

                case ReminderRepeatType.Every10Minutes:
                    if (notification.ReminderTime <= currentTime)
                    {
                        shouldNotify = true;
                        nextNotifyTime = currentTime.AddMinutes(10);
                    }
                    break;

                case ReminderRepeatType.Every30Minutes:
                    if (notification.ReminderTime <= currentTime)
                    {
                        shouldNotify = true;
                        nextNotifyTime = currentTime.AddMinutes(30);
                    }
                    break;

                case ReminderRepeatType.EveryHour:
                    if (notification.ReminderTime <= currentTime)
                    {
                        shouldNotify = true;
                        nextNotifyTime = currentTime.AddHours(1);
                    }
                    break;

                case ReminderRepeatType.EveryDay:
                    if (notification.ReminderTime.Date <= currentTime.Date)
                    {
                        shouldNotify = true;
                        nextNotifyTime = currentTime.AddDays(1);
                    }
                    break;

                case ReminderRepeatType.EveryWeek:
                    if (notification.ReminderTime.Date <= currentTime.Date)
                    {
                        shouldNotify = true;
                        nextNotifyTime = currentTime.AddDays(7);
                    }
                    break;
            }

            if (shouldNotify)
            {
                ShowNotification(note);
                PlayNotificationSound(notification.Sound);

                notification.LastNotified = currentTime;

                if (nextNotifyTime.HasValue)
                {
                    notification.ReminderTime = nextNotifyTime.Value;
                }

                await _dbService.SaveTravelAsync(_currentTravel);
                UpdateNotesDisplay();
            }
        }

        private void ShowNotification(NoteBaseViewModel note)
        {
            string noteType = note.NoteType switch
            {
                NoteType.Text => "Текстовая заметка",
                NoteType.List => "Список",
                NoteType.Checklist => "Чек-лист",
                _ => "Заметка"
            };

            string message = $"{noteType}: {note.Title}\n\n";

            switch (note)
            {
                case TextNoteViewModel textNote:
                    message += textNote.Content?.Length > 100 ?
                        textNote.Content.Substring(0, 100) + "..." :
                        textNote.Content;
                    break;
                case ListNoteViewModel listNote:
                    message += $"Содержит {listNote.Items?.Count ?? 0} элементов";
                    break;
                case ChecklistNoteViewModel checklistNote:
                    int checkedCount = checklistNote.Items?.Count(x => x.IsChecked) ?? 0;
                    int totalCount = checklistNote.Items?.Count ?? 0;
                    message += $"Выполнено {checkedCount}/{totalCount} пунктов";
                    break;
            }

            MessageBox.Show(message, $"🔔 Напоминание: {note.Title}",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PlayNotificationSound(NotificationSound sound)
        {
            try
            {
                string soundFile = sound switch
                {
                    NotificationSound.Default => "C:\\Windows\\Media\\Windows Notify.wav",
                    NotificationSound.Bell => "C:\\Windows\\Media\\Windows Ringout.wav",
                    NotificationSound.Chime => "C:\\Windows\\Media\\chimes.wav",
                    NotificationSound.Alert => "C:\\Windows\\Media\\Windows Critical Stop.wav",
                    NotificationSound.Gentle => "C:\\Windows\\Media\\tada.wav",
                    _ => "C:\\Windows\\Media\\Windows Notify.wav"
                };

                if (File.Exists(soundFile))
                {
                    _soundPlayer = new SoundPlayer(soundFile);
                    _soundPlayer.Play();
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
            }
            catch
            {
                SystemSounds.Beep.Play();
            }
        }

        private void UpdateNotesDisplay()
        {
            if (_currentTravel == null) return;
            if (LbNotes == null) return;

            _allNotes = _currentTravel.Notes.Select(note => new NoteDisplayItem
            {
                Note = note,
                Title = note.Title,
                PreviewText = GetNotePreview(note),
                Icon = GetNoteIcon(note),
                CreatedDate = note.CreatedDate,
                NoteType = note.NoteType,
                HasNotification = note.Notification?.IsEnabled ?? false,
                NotificationInfo = GetNotificationInfo(note),
                HasSearchMatch = false,
                SearchHighlight = string.Empty
            }).ToList();

            ApplySearchAndFilter();
        }

        private void ApplySearchAndFilter()
        {
            if (_allNotes == null) return;
            if (LbNotes == null) return;

            var filteredNotes = _allNotes.AsEnumerable();
            filteredNotes = ApplyTypeFilter(filteredNotes);
            filteredNotes = ApplySearchFilter(filteredNotes);

            var resultList = filteredNotes.ToList();
            LbNotes.ItemsSource = resultList;
            UpdateSearchResultsInfo(resultList.Count, _allNotes.Count);
        }

        private IEnumerable<NoteDisplayItem> ApplyTypeFilter(IEnumerable<NoteDisplayItem> notes)
        {
            if (string.IsNullOrEmpty(_currentFilterType))
                _currentFilterType = "All";

            switch (_currentFilterType)
            {
                case "Text":
                    return notes.Where(n => n.NoteType == NoteType.Text);
                case "List":
                    return notes.Where(n => n.NoteType == NoteType.List);
                case "Checklist":
                    return notes.Where(n => n.NoteType == NoteType.Checklist);
                case "HasNotification":
                    return notes.Where(n => n.HasNotification);
                case "All":
                default:
                    return notes;
            }
        }

        private IEnumerable<NoteDisplayItem> ApplySearchFilter(IEnumerable<NoteDisplayItem> notes)
        {
            if (string.IsNullOrWhiteSpace(_currentSearchText))
            {
                foreach (var note in notes)
                {
                    note.HasSearchMatch = false;
                    note.SearchHighlight = string.Empty;
                }
                return notes;
            }

            var searchText = _currentSearchText.ToLower();
            var result = new List<NoteDisplayItem>();

            foreach (var note in notes)
            {
                bool hasMatch = false;
                var matchedFields = new List<string>();

                if (note.Title.ToLower().Contains(searchText))
                {
                    hasMatch = true;
                    matchedFields.Add($"заголовке: \"{HighlightText(note.Title, searchText)}\"");
                }

                string contentForSearch = GetNoteContentForSearch(note.Note);
                if (contentForSearch.ToLower().Contains(searchText))
                {
                    hasMatch = true;
                    var highlightedContent = HighlightText(contentForSearch, searchText);
                    matchedFields.Add($"содержимом: {highlightedContent}");
                }

                if (note.NotificationInfo.ToLower().Contains(searchText))
                {
                    hasMatch = true;
                    matchedFields.Add($"уведомлении: {HighlightText(note.NotificationInfo, searchText)}");
                }

                note.HasSearchMatch = hasMatch;
                note.SearchHighlight = hasMatch
                    ? $"🔍 Найдено в {string.Join(", ", matchedFields)}"
                    : string.Empty;

                if (hasMatch)
                {
                    result.Add(note);
                }
            }

            return result;
        }

        private string GetNoteContentForSearch(NoteBaseViewModel note)
        {
            switch (note)
            {
                case TextNoteViewModel textNote:
                    return textNote.Content ?? string.Empty;
                case ListNoteViewModel listNote:
                    return string.Join(" ", listNote.Items?.Select(i => i.Text) ?? new List<string>());
                case ChecklistNoteViewModel checklistNote:
                    return string.Join(" ", checklistNote.Items?.Select(i => i.ItemName) ?? new List<string>());
                default:
                    return string.Empty;
            }
        }

        private string HighlightText(string text, string searchText)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchText))
                return text;

            var lowerText = text.ToLower();
            var lowerSearch = searchText.ToLower();

            int index = lowerText.IndexOf(lowerSearch);
            if (index >= 0)
            {
                var before = text.Substring(0, index);
                var match = text.Substring(index, searchText.Length);
                var after = text.Substring(index + searchText.Length);

                if (before.Length > 30)
                {
                    before = "..." + before.Substring(before.Length - 30);
                }
                if (after.Length > 50)
                {
                    after = after.Substring(0, 50) + "...";
                }

                return $"{before}▶{match}◀{after}";
            }

            return text.Length > 100 ? text.Substring(0, 100) + "..." : text;
        }

        private void UpdateSearchResultsInfo(int filteredCount, int totalCount)
        {
            if (TxtSearchResults == null) return;

            if (!string.IsNullOrWhiteSpace(_currentSearchText) || _currentFilterType != "All")
            {
                TxtSearchResults.Visibility = Visibility.Visible;

                string filterInfo = "";
                if (_currentFilterType != "All" && CmbFilterType?.SelectedItem is ComboBoxItem selectedItem)
                {
                    string filterName = selectedItem.Content?.ToString() ?? "Неизвестный фильтр";
                    filterInfo = $" | Фильтр: {filterName}";
                }

                if (!string.IsNullOrWhiteSpace(_currentSearchText))
                {
                    TxtSearchResults.Text = $"🔍 Найдено {filteredCount} из {totalCount} заметок по запросу \"{_currentSearchText}\"{filterInfo}";
                }
                else
                {
                    TxtSearchResults.Text = $"📋 Показано {filteredCount} из {totalCount} заметок{filterInfo}";
                }

                if (filteredCount == 0)
                {
                    TxtSearchResults.Text += "\n⚠️ Ничего не найдено. Попробуйте изменить поисковый запрос или фильтр.";
                    TxtSearchResults.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    TxtSearchResults.Foreground = System.Windows.Media.Brushes.Green;
                }
            }
            else
            {
                TxtSearchResults.Visibility = Visibility.Collapsed;
            }
        }

        private string GetNotificationInfo(NoteBaseViewModel note)
        {
            if (note.Notification?.IsEnabled != true) return string.Empty;

            var notification = note.Notification;
            string repeatText = notification.RepeatType switch
            {
                ReminderRepeatType.None => "Однократное",
                ReminderRepeatType.EveryMinute => "Каждую минуту",
                ReminderRepeatType.Every5Minutes => "Каждые 5 минут",
                ReminderRepeatType.Every10Minutes => "Каждые 10 минут",
                ReminderRepeatType.Every30Minutes => "Каждые 30 минут",
                ReminderRepeatType.EveryHour => "Каждый час",
                ReminderRepeatType.EveryDay => "Каждый день",
                ReminderRepeatType.EveryWeek => "Каждую неделю",
                _ => ""
            };

            return $"🔔 Уведомление: {notification.ReminderTime:dd.MM.yyyy HH:mm} ({repeatText})";
        }

        private void InitializeData()
        {
            if (_travels == null)
                _travels = new ObservableCollection<TravelViewModel>();

            CmbTravels.ItemsSource = _travels;
            CmbTravels.DisplayMemberPath = "Name";

            if (LvListItems != null)
                LvListItems.ItemsSource = new ObservableCollection<ListItemModel>();

            if (LvChecklistItems != null)
                LvChecklistItems.ItemsSource = new ObservableCollection<ChecklistItemModel>();
        }

        private void LoadTravels()
        {
            try
            {
                if (File.Exists(_saveFilePath))
                {
                    string json = File.ReadAllText(_saveFilePath);
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

                        if (_travels.Count > 0)
                        {
                            CmbTravels.SelectedIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveTravels()
        {
            try
            {
                if (_currentTravel != null)
                {
                    _currentTravel.Name = TxtTravelName.Text;
                    _currentTravel.Route = TxtRoute.Text;

                    System.Diagnostics.Debug.WriteLine($"=== Saving Travel ===");
                    System.Diagnostics.Debug.WriteLine($"Id: {_currentTravel.Id}");
                    System.Diagnostics.Debug.WriteLine($"Name: '{_currentTravel.Name}'");
                    System.Diagnostics.Debug.WriteLine($"Route: '{_currentTravel.Route}'");

                    await _dbService.SaveTravelAsync(_currentTravel);

                    System.Diagnostics.Debug.WriteLine("Save successful");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save error: {ex.Message}");
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCurrentTravel()
        {
            if (_currentTravel != null)
            {
                _currentTravel.Name = TxtTravelName.Text;
                _currentTravel.Route = TxtRoute.Text;
            }
        }

        private class NoteDisplayItem : INotifyPropertyChanged
        {
            private NoteBaseViewModel _note;
            private string _title;
            private string _previewText;
            private string _icon;
            private DateTime _createdDate;
            private NoteType _noteType;
            private bool _hasNotification;
            private string _notificationInfo;
            private bool _hasSearchMatch;
            private string _searchHighlight;

            public NoteBaseViewModel Note
            {
                get => _note;
                set { _note = value; OnPropertyChanged(); }
            }

            public string Title
            {
                get => _title;
                set { _title = value; OnPropertyChanged(); }
            }

            public string PreviewText
            {
                get => _previewText;
                set { _previewText = value; OnPropertyChanged(); }
            }

            public string Icon
            {
                get => _icon;
                set { _icon = value; OnPropertyChanged(); }
            }

            public DateTime CreatedDate
            {
                get => _createdDate;
                set { _createdDate = value; OnPropertyChanged(); }
            }

            public NoteType NoteType
            {
                get => _noteType;
                set { _noteType = value; OnPropertyChanged(); }
            }

            public bool HasNotification
            {
                get => _hasNotification;
                set { _hasNotification = value; OnPropertyChanged(); }
            }

            public string NotificationInfo
            {
                get => _notificationInfo;
                set { _notificationInfo = value; OnPropertyChanged(); }
            }

            public bool HasSearchMatch
            {
                get => _hasSearchMatch;
                set { _hasSearchMatch = value; OnPropertyChanged(); }
            }

            public string SearchHighlight
            {
                get => _searchHighlight;
                set { _searchHighlight = value; OnPropertyChanged(); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private string GetNotePreview(NoteBaseViewModel note)
        {
            switch (note)
            {
                case TextNoteViewModel textNote:
                    string content = textNote.Content ?? "";
                    return content.Length > 50 ? content.Substring(0, 50) + "..." : content;
                case ListNoteViewModel listNote:
                    return $"Список ({listNote.Items?.Count ?? 0} элементов)";
                case ChecklistNoteViewModel checklistNote:
                    int checkedCount = checklistNote.Items?.Count(x => x.IsChecked) ?? 0;
                    int totalCount = checklistNote.Items?.Count ?? 0;
                    return $"Чек-лист ({checkedCount}/{totalCount} выполнено)";
                default:
                    return "";
            }
        }

        private string GetNoteIcon(NoteBaseViewModel note)
        {
            switch (note)
            {
                case TextNoteViewModel:
                    return "📄";
                case ListNoteViewModel:
                    return "📋";
                case ChecklistNoteViewModel:
                    return "✅";
                default:
                    return "📝";
            }
        }

        private void UpdatePinnedNotesDisplay()
        {
            if (_currentTravel?.PinnedNotes == null)
            {
                if (LvPinnedNotes != null)
                    LvPinnedNotes.ItemsSource = null;
                return;
            }

            if (LvPinnedNotes == null) return;

            var pinnedItems = _currentTravel.PinnedNotes.Select(note => new
            {
                Note = note,
                Title = note.Title,
                Icon = GetNoteIcon(note),
                CreatedDate = note.CreatedDate
            }).ToList();

            LvPinnedNotes.ItemsSource = pinnedItems;

            if (TxtEmptyPinned != null)
            {
                TxtEmptyPinned.Visibility = _currentTravel.PinnedNotes.Count == 0 ?
                    Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void RemovePinnedNote_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var note = button?.Tag as NoteBaseViewModel;

            if (note != null && _currentTravel != null && _currentTravel.PinnedNotes.Contains(note))
            {
                if (MessageBox.Show($"Удалить заметку \"{note.Title}\" из закрепленных?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _currentTravel.PinnedNotes.Remove(note);
                    UpdatePinnedNotesDisplay();
                    await _dbService.SaveTravelAsync(_currentTravel);

                    MessageBox.Show("Заметка удалена из закрепленных!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void LbNotes_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;

            var item = ItemsControl.ContainerFromElement(LbNotes, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                item.IsSelected = true;
            }
        }

        private void LbNotes_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                var point = e.GetPosition(null);
                var diff = _dragStartPoint - point;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (LbNotes.SelectedItem is NoteDisplayItem selectedDisplayItem)
                    {
                        _isDragging = true;

                        try
                        {
                            var dataObject = new DataObject();
                            dataObject.SetData("NoteTitle", selectedDisplayItem.Title);
                            dataObject.SetData("NoteCreatedDate", selectedDisplayItem.CreatedDate);
                            dataObject.SetData("NoteType", selectedDisplayItem.NoteType);

                            DragDrop.DoDragDrop(LbNotes, dataObject, DragDropEffects.Move);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Drag drop error: {ex.Message}");
                        }
                        finally
                        {
                            _isDragging = false;
                        }
                    }
                }
            }
        }

        private void PinnedNotesArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("NoteTitle") && e.Data.GetDataPresent("NoteCreatedDate"))
            {
                e.Effects = DragDropEffects.Move;
                PinnedNotesArea.Background = System.Windows.Media.Brushes.LightGoldenrodYellow;
                PinnedNotesArea.BorderBrush = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void PinnedNotesArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("NoteTitle") && e.Data.GetDataPresent("NoteCreatedDate"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void PinnedNotesArea_DragLeave(object sender, DragEventArgs e)
        {
            PinnedNotesArea.Background = System.Windows.Media.Brushes.LightYellow;
            PinnedNotesArea.BorderBrush = System.Windows.Media.Brushes.Goldenrod;
            e.Handled = true;
        }

        private async void PinnedNotesArea_Drop(object sender, DragEventArgs e)
        {
            try
            {
                PinnedNotesArea.Background = System.Windows.Media.Brushes.LightYellow;
                PinnedNotesArea.BorderBrush = System.Windows.Media.Brushes.Goldenrod;

                if (_currentTravel != null &&
                    e.Data.GetDataPresent("NoteTitle") &&
                    e.Data.GetDataPresent("NoteCreatedDate"))
                {
                    string noteTitle = e.Data.GetData("NoteTitle") as string;
                    DateTime noteCreatedDate = (DateTime)e.Data.GetData("NoteCreatedDate");

                    var droppedNote = _currentTravel.Notes.FirstOrDefault(n =>
                        n.Title == noteTitle && n.CreatedDate == noteCreatedDate);

                    if (droppedNote != null)
                    {
                        if (!_currentTravel.PinnedNotes.Contains(droppedNote))
                        {
                            _currentTravel.PinnedNotes.Add(droppedNote);
                            UpdatePinnedNotesDisplay();

                            try
                            {
                                await _dbService.SaveTravelAsync(_currentTravel);
                                MessageBox.Show($"Заметка \"{droppedNote.Title}\" добавлена в закрепленные!",
                                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            catch (Exception saveEx)
                            {
                                _currentTravel.PinnedNotes.Remove(droppedNote);
                                UpdatePinnedNotesDisplay();
                                MessageBox.Show($"Ошибка сохранения: {saveEx.Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Эта заметка уже закреплена!", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при закреплении заметки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        private async void BtnNewTravel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateTravelDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IsCreated)
            {
                try
                {
                    var newTravel = new TravelViewModel
                    {
                        Name = dialog.TravelName,
                        Route = dialog.TravelRoute
                    };

                    await _dbService.SaveTravelAsync(newTravel);
                    await LoadTravelsFromDb();

                    var createdTravel = _travels.FirstOrDefault(t => t.Name == dialog.TravelName);
                    if (createdTravel != null)
                    {
                        CmbTravels.SelectedItem = createdTravel;
                        MessageBox.Show($"Путешествие \"{dialog.TravelName}\" успешно создано!",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при создании путешествия: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnSaveTravel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTravel != null)
            {
                _currentTravel.Name = TxtTravelName.Text;
                _currentTravel.Route = TxtRoute.Text;

                try
                {
                    await _dbService.SaveTravelAsync(_currentTravel);
                    MessageBox.Show("Путешествие сохранено в базу данных!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnForceSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTravel != null)
            {
                _currentTravel.Name = TxtTravelName.Text;
                _currentTravel.Route = TxtRoute.Text;

                using var connection = new Microsoft.Data.SqlClient.SqlConnection(
                    "Server=DESKTOP-11PGGLI\\SQLEXPRESS;Database=TravelJournalDb;Trusted_Connection=True;TrustServerCertificate=True");

                await connection.OpenAsync();

                var command = new Microsoft.Data.SqlClient.SqlCommand(
                    "UPDATE Travels SET Name = @Name, Route = @Route WHERE Id = @Id", connection);
                command.Parameters.AddWithValue("@Id", _currentTravel.Id);
                command.Parameters.AddWithValue("@Name", _currentTravel.Name);
                command.Parameters.AddWithValue("@Route", _currentTravel.Route ?? "");

                int rows = await command.ExecuteNonQueryAsync();

                if (rows > 0)
                {
                    MessageBox.Show($"Принудительно сохранено!\nName: {_currentTravel.Name}\nRoute: {_currentTravel.Route}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Ничего не обновлено!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnLoadTravel_Click(object sender, RoutedEventArgs e)
        {
            LoadTravels();
            MessageBox.Show("Данные загружены из JSON!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ГЛАВНОЕ ИСПРАВЛЕНИЕ - SelectionChanged
        private void CmbTravels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTravels.SelectedItem is TravelViewModel selectedTravel)
            {
                _currentTravel = selectedTravel;

                System.Diagnostics.Debug.WriteLine("=== Travel Selected ===");
                System.Diagnostics.Debug.WriteLine($"Id: {_currentTravel.Id}");
                System.Diagnostics.Debug.WriteLine($"Name: '{_currentTravel.Name}'");
                System.Diagnostics.Debug.WriteLine($"Route: '{_currentTravel.Route}'");

                // ВАЖНО: Отключаем обработчики событий временно
                TxtTravelName.TextChanged -= TravelInfo_Changed;
                TxtRoute.TextChanged -= TravelInfo_Changed;

                // Устанавливаем значения
                TxtTravelName.Text = _currentTravel.Name ?? "";
                TxtRoute.Text = _currentTravel.Route ?? "";

                // Включаем обработчики обратно
                TxtTravelName.TextChanged += TravelInfo_Changed;
                TxtRoute.TextChanged += TravelInfo_Changed;

                System.Diagnostics.Debug.WriteLine($"UI Route set to: '{TxtRoute.Text}'");

                // Обновляем заметки
                _currentSearchText = string.Empty;
                _currentFilterType = "All";

                if (TxtSearchNotes != null)
                    TxtSearchNotes.Text = string.Empty;

                if (CmbFilterType != null && CmbFilterType.SelectedIndex != 0)
                    CmbFilterType.SelectedIndex = 0;

                UpdateNotesDisplay();
                UpdatePinnedNotesDisplay();
            }
        }

        private void TravelInfo_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateCurrentTravel();
        }

        private void TxtRoute_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_currentTravel != null)
            {
                _currentTravel.Route = TxtRoute.Text;
                System.Diagnostics.Debug.WriteLine($"Route saved on LostFocus: '{_currentTravel.Route}'");
                SaveTravels();
            }
        }

        private void TxtTravelName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_currentTravel != null)
            {
                _currentTravel.Name = TxtTravelName.Text;
                System.Diagnostics.Debug.WriteLine($"Name saved on LostFocus: '{_currentTravel.Name}'");
                SaveTravels();
            }
        }

        private void BtnDebugRoute_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTravel != null)
            {
                MessageBox.Show($"Текущее путешествие:\n" +
                                $"Id: {_currentTravel.Id}\n" +
                                $"Name: {_currentTravel.Name}\n" +
                                $"Route: {_currentTravel.Route}\n" +
                                $"Текст из UI Name: {TxtTravelName.Text}\n" +
                                $"Текст из UI Route: {TxtRoute.Text}");
            }
            else
            {
                MessageBox.Show("_currentTravel = null");
            }
        }

        private void TxtSearchNotes_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSearchNotes == null) return;

            _currentSearchText = TxtSearchNotes.Text;
            ApplySearchAndFilter();
        }

        private void CmbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFilterType == null || CmbFilterType.SelectedItem == null) return;

            if (CmbFilterType.SelectedItem is ComboBoxItem selectedItem)
            {
                string content = selectedItem.Content?.ToString() ?? string.Empty;

                if (content.Contains("Все"))
                    _currentFilterType = "All";
                else if (content.Contains("Текстовые"))
                    _currentFilterType = "Text";
                else if (content.Contains("Списки") && !content.Contains("Чек-листы"))
                    _currentFilterType = "List";
                else if (content.Contains("Чек-листы"))
                    _currentFilterType = "Checklist";
                else if (content.Contains("уведомлениями"))
                    _currentFilterType = "HasNotification";
                else
                    _currentFilterType = "All";

                ApplySearchAndFilter();
            }
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (TxtSearchNotes == null || CmbFilterType == null) return;

            TxtSearchNotes.Text = string.Empty;
            _currentSearchText = string.Empty;

            if (CmbFilterType.Items.Count > 0)
            {
                for (int i = 0; i < CmbFilterType.Items.Count; i++)
                {
                    if (CmbFilterType.Items[i] is ComboBoxItem item)
                    {
                        string content = item.Content?.ToString() ?? string.Empty;
                        if (content.Contains("Все"))
                        {
                            CmbFilterType.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (CmbFilterType.SelectedIndex == -1 && CmbFilterType.Items.Count > 0)
                {
                    CmbFilterType.SelectedIndex = 0;
                }
            }
            else
            {
                ApplySearchAndFilter();
            }
        }

        private void BtnAddNote_Click(object sender, RoutedEventArgs e)
        {
            _editingNote = null;
            ClearNoteDialog();
            NoteDialog.Visibility = Visibility.Visible;
        }

        private void ClearNoteDialog()
        {
            TxtNoteTitle.Text = string.Empty;
            CmbNoteType.SelectedIndex = 0;

            if (LvListItems?.ItemsSource is ObservableCollection<ListItemModel> listItems)
                listItems.Clear();

            if (LvChecklistItems?.ItemsSource is ObservableCollection<ChecklistItemModel> checklistItems)
                checklistItems.Clear();

            TxtNoteContent.Text = string.Empty;

            if (TextNotePanel != null)
                TextNotePanel.Visibility = Visibility.Visible;

            if (ListNotePanel != null)
                ListNotePanel.Visibility = Visibility.Collapsed;

            if (ChecklistNotePanel != null)
                ChecklistNotePanel.Visibility = Visibility.Collapsed;

            if (ChkEnableNotification != null)
                ChkEnableNotification.IsChecked = false;

            if (DatePickerReminder != null)
                DatePickerReminder.SelectedDate = DateTime.Now;

            if (TimePickerReminder != null)
                TimePickerReminder.Text = "12:00";

            if (CmbRepeatType != null)
                CmbRepeatType.SelectedIndex = 0;

            if (CmbSound != null)
                CmbSound.SelectedIndex = 0;
        }

        private void ChkEnableNotification_Checked(object sender, RoutedEventArgs e)
        {
            if (NotificationPanel != null)
                NotificationPanel.IsEnabled = true;
        }

        private void ChkEnableNotification_Unchecked(object sender, RoutedEventArgs e)
        {
            if (NotificationPanel != null)
                NotificationPanel.IsEnabled = false;
        }

        private void BtnTestSound_Click(object sender, RoutedEventArgs e)
        {
            if (CmbSound.SelectedItem is ComboBoxItem selectedItem)
            {
                NotificationSound sound = selectedItem.Tag?.ToString() switch
                {
                    "Default" => NotificationSound.Default,
                    "Bell" => NotificationSound.Bell,
                    "Chime" => NotificationSound.Chime,
                    "Alert" => NotificationSound.Alert,
                    "Gentle" => NotificationSound.Gentle,
                    _ => NotificationSound.Default
                };
                PlayNotificationSound(sound);
            }
        }

        private void CmbNoteType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbNoteType.SelectedItem is ComboBoxItem selectedItem &&
                TextNotePanel != null && ListNotePanel != null && ChecklistNotePanel != null)
            {
                string type = selectedItem.Tag?.ToString() ?? "Text";

                TextNotePanel.Visibility = Visibility.Collapsed;
                ListNotePanel.Visibility = Visibility.Collapsed;
                ChecklistNotePanel.Visibility = Visibility.Collapsed;

                switch (type)
                {
                    case "Text":
                        TextNotePanel.Visibility = Visibility.Visible;
                        break;
                    case "List":
                        ListNotePanel.Visibility = Visibility.Visible;
                        break;
                    case "Checklist":
                        ChecklistNotePanel.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void BtnAddListItem_Click(object sender, RoutedEventArgs e)
        {
            if (LvListItems?.ItemsSource is ObservableCollection<ListItemModel> items)
            {
                items.Add(new ListItemModel { Text = "Новый элемент" });
            }
        }

        private void BtnRemoveListItem_Click(object sender, RoutedEventArgs e)
        {
            if (LvListItems?.SelectedItem is ListItemModel selectedItem &&
                LvListItems.ItemsSource is ObservableCollection<ListItemModel> items)
            {
                items.Remove(selectedItem);
            }
            else
            {
                MessageBox.Show("Выберите элемент для удаления", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnAddChecklistItemNote_Click(object sender, RoutedEventArgs e)
        {
            if (LvChecklistItems?.ItemsSource is ObservableCollection<ChecklistItemModel> items)
            {
                items.Add(new ChecklistItemModel { ItemName = "Новый элемент", IsChecked = false });
            }
        }

        private void BtnRemoveChecklistItemNote_Click(object sender, RoutedEventArgs e)
        {
            if (LvChecklistItems?.SelectedItem is ChecklistItemModel selectedItem &&
                LvChecklistItems.ItemsSource is ObservableCollection<ChecklistItemModel> items)
            {
                items.Remove(selectedItem);
            }
            else
            {
                MessageBox.Show("Выберите элемент для удаления", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNoteTitle.Text))
            {
                MessageBox.Show("Введите заголовок заметки", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentTravel != null && CmbNoteType.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedType = selectedItem.Tag?.ToString() ?? "Text";
                NoteBaseViewModel newNote = null;

                switch (selectedType)
                {
                    case "Text":
                        if (string.IsNullOrWhiteSpace(TxtNoteContent.Text))
                        {
                            MessageBox.Show("Введите содержание заметки", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        newNote = new TextNoteViewModel
                        {
                            Title = TxtNoteTitle.Text,
                            Content = TxtNoteContent.Text
                        };
                        break;

                    case "List":
                        if (LvListItems?.ItemsSource is ObservableCollection<ListItemModel> listItems)
                        {
                            if (listItems.Count == 0)
                            {
                                MessageBox.Show("Добавьте хотя бы один элемент в список", "Внимание",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }

                            foreach (var item in listItems)
                            {
                                if (string.IsNullOrWhiteSpace(item.Text))
                                {
                                    MessageBox.Show("Названия элементов списка не могут быть пустыми!",
                                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    return;
                                }
                                if (item.Text.Length > 500)
                                {
                                    item.Text = item.Text.Substring(0, 500);
                                }
                            }

                            newNote = new ListNoteViewModel
                            {
                                Title = TxtNoteTitle.Text,
                                Items = new ObservableCollection<ListItemModel>(listItems)
                            };
                        }
                        break;

                    case "Checklist":
                        if (LvChecklistItems?.ItemsSource is ObservableCollection<ChecklistItemModel> checklistItems)
                        {
                            if (checklistItems.Count == 0)
                            {
                                MessageBox.Show("Добавьте хотя бы один элемент в чек-лист", "Внимание",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }

                            foreach (var item in checklistItems)
                            {
                                if (string.IsNullOrWhiteSpace(item.ItemName))
                                {
                                    MessageBox.Show("Названия элементов чек-листа не могут быть пустыми!",
                                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    return;
                                }
                                if (item.ItemName.Length > 500)
                                {
                                    item.ItemName = item.ItemName.Substring(0, 500);
                                }
                            }

                            newNote = new ChecklistNoteViewModel
                            {
                                Title = TxtNoteTitle.Text,
                                Items = new ObservableCollection<ChecklistItemModel>(checklistItems)
                            };
                        }
                        break;
                }

                if (newNote != null)
                {
                    if (ChkEnableNotification.IsChecked == true)
                    {
                        var reminderTime = DatePickerReminder.SelectedDate ?? DateTime.Now;
                        TimeSpan timeOfDay;
                        if (!TimeSpan.TryParse(TimePickerReminder.Text, out timeOfDay))
                        {
                            timeOfDay = new TimeSpan(12, 0, 0);
                        }
                        var fullDateTime = reminderTime.Date + timeOfDay;

                        newNote.Notification = new NotificationViewModel
                        {
                            IsEnabled = true,
                            ReminderTime = fullDateTime,
                            RepeatType = CmbRepeatType.SelectedItem is ComboBoxItem repeatItem && repeatItem.Tag != null
                                ? Enum.Parse<ReminderRepeatType>(repeatItem.Tag.ToString())
                                : ReminderRepeatType.None,
                            Sound = CmbSound.SelectedItem is ComboBoxItem soundItem && soundItem.Tag != null
                                ? Enum.Parse<NotificationSound>(soundItem.Tag.ToString())
                                : NotificationSound.Default
                        };
                    }

                    try
                    {
                        if (_editingNote == null)
                        {
                            _currentTravel.Notes.Add(newNote);
                        }
                        else
                        {
                            int index = _currentTravel.Notes.IndexOf(_editingNote);
                            _currentTravel.Notes[index] = newNote;

                            var pinnedIndex = _currentTravel.PinnedNotes.IndexOf(_editingNote);
                            if (pinnedIndex >= 0)
                            {
                                _currentTravel.PinnedNotes[pinnedIndex] = newNote;
                            }
                        }

                        await _dbService.SaveTravelAsync(_currentTravel);
                        NoteDialog.Visibility = Visibility.Collapsed;
                        UpdateNotesDisplay();

                        MessageBox.Show("Заметка успешно сохранена!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        if (_editingNote == null && _currentTravel.Notes.Contains(newNote))
                        {
                            _currentTravel.Notes.Remove(newNote);
                        }

                        MessageBox.Show($"Ошибка при сохранении заметки: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            NoteDialog.Visibility = Visibility.Collapsed;
        }

        private void RefreshNotesDisplay()
        {
            if (LbNotes != null)
            {
                UpdateNotesDisplay();
            }
        }

        private void BtnCancelNote_Click(object sender, RoutedEventArgs e)
        {
            NoteDialog.Visibility = Visibility.Collapsed;
        }

        private async void BtnDeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (LbNotes.SelectedItem is NoteDisplayItem selectedDisplayItem && _currentTravel != null)
            {
                var selectedNote = selectedDisplayItem.Note;
                _currentTravel.Notes.Remove(selectedNote);

                if (_currentTravel.PinnedNotes.Contains(selectedNote))
                {
                    _currentTravel.PinnedNotes.Remove(selectedNote);
                    UpdatePinnedNotesDisplay();
                }

                await _dbService.SaveTravelAsync(_currentTravel);
                UpdateNotesDisplay();
            }
            else
            {
                MessageBox.Show("Выберите заметку для удаления", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LbNotes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void LbNotes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LbNotes.SelectedItem is NoteDisplayItem selectedDisplayItem)
            {
                var selectedNote = selectedDisplayItem.Note;
                _editingNote = selectedNote;
                TxtNoteTitle.Text = selectedNote.Title;

                switch (selectedNote)
                {
                    case TextNoteViewModel textNote:
                        CmbNoteType.SelectedIndex = 0;
                        TxtNoteContent.Text = textNote.Content;
                        break;

                    case ListNoteViewModel listNote:
                        CmbNoteType.SelectedIndex = 1;
                        var listItems = new ObservableCollection<ListItemModel>();
                        if (listNote.Items != null)
                        {
                            foreach (var item in listNote.Items)
                            {
                                listItems.Add(new ListItemModel { Text = item.Text });
                            }
                        }
                        if (LvListItems != null)
                            LvListItems.ItemsSource = listItems;
                        break;

                    case ChecklistNoteViewModel checklistNote:
                        CmbNoteType.SelectedIndex = 2;
                        var checklistItems = new ObservableCollection<ChecklistItemModel>();
                        if (checklistNote.Items != null)
                        {
                            foreach (var item in checklistNote.Items)
                            {
                                checklistItems.Add(new ChecklistItemModel
                                {
                                    ItemName = item.ItemName,
                                    IsChecked = item.IsChecked
                                });
                            }
                        }
                        if (LvChecklistItems != null)
                            LvChecklistItems.ItemsSource = checklistItems;
                        break;
                }

                if (selectedNote.Notification != null && selectedNote.Notification.IsEnabled)
                {
                    ChkEnableNotification.IsChecked = true;
                    DatePickerReminder.SelectedDate = selectedNote.Notification.ReminderTime.Date;
                    TimePickerReminder.Text = selectedNote.Notification.ReminderTime.ToString("HH:mm");

                    if (CmbRepeatType.Items.Count > 0)
                    {
                        foreach (ComboBoxItem item in CmbRepeatType.Items)
                        {
                            if (item.Tag?.ToString() == selectedNote.Notification.RepeatType.ToString())
                            {
                                CmbRepeatType.SelectedItem = item;
                                break;
                            }
                        }
                    }

                    if (CmbSound.Items.Count > 0)
                    {
                        foreach (ComboBoxItem item in CmbSound.Items)
                        {
                            if (item.Tag?.ToString() == selectedNote.Notification.Sound.ToString())
                            {
                                CmbSound.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    ChkEnableNotification.IsChecked = false;
                }

                NoteDialog.Visibility = Visibility.Visible;
            }
        }

        private async void LvPinnedNotes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvPinnedNotes.SelectedItem != null && _currentTravel != null)
            {
                var selectedPinned = LvPinnedNotes.SelectedItem;
                var noteProperty = selectedPinned.GetType().GetProperty("Note");
                if (noteProperty != null)
                {
                    var note = noteProperty.GetValue(selectedPinned) as NoteBaseViewModel;
                    if (note != null && _currentTravel.PinnedNotes.Contains(note))
                    {
                        if (MessageBox.Show($"Удалить заметку \"{note.Title}\" из закрепленных?",
                            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            _currentTravel.PinnedNotes.Remove(note);
                            UpdatePinnedNotesDisplay();
                            await _dbService.SaveTravelAsync(_currentTravel);

                            MessageBox.Show("Заметка удалена из закрепленных!", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
        }

        private async void BtnOpenMap_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTravel != null)
            {
                _currentTravel.Name = TxtTravelName.Text;
                _currentTravel.Route = TxtRoute.Text;
                await _dbService.SaveTravelAsync(_currentTravel);
            }

            NavigationService?.Navigate(new MapPage());
        }
    }
}