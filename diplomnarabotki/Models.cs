using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace diplomnarabotki
{
    // Типы заметок
    public enum NoteType
    {
        Text,
        List,
        Checklist
    }

    // Тип повторения уведомления
    public enum ReminderRepeatType
    {
        None,
        EveryMinute,
        Every5Minutes,
        Every10Minutes,
        Every30Minutes,
        EveryHour,
        EveryDay,
        EveryWeek
    }

    // Звуки уведомлений
    public enum NotificationSound
    {
        Default,
        Bell,
        Chime,
        Alert,
        Gentle
    }

    // Модель уведомления
    public class Notification : INotifyPropertyChanged
    {
        private DateTime _reminderTime;
        private ReminderRepeatType _repeatType;
        private NotificationSound _sound;
        private bool _isEnabled;
        private DateTime? _lastNotified;

        public DateTime ReminderTime
        {
            get => _reminderTime;
            set { _reminderTime = value; OnPropertyChanged(); }
        }

        public ReminderRepeatType RepeatType
        {
            get => _repeatType;
            set { _repeatType = value; OnPropertyChanged(); }
        }

        public NotificationSound Sound
        {
            get => _sound;
            set { _sound = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public DateTime? LastNotified
        {
            get => _lastNotified;
            set { _lastNotified = value; OnPropertyChanged(); }
        }

        public Notification()
        {
            ReminderTime = DateTime.Now.AddHours(1);
            RepeatType = ReminderRepeatType.None;
            Sound = NotificationSound.Default;
            IsEnabled = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Базовая модель заметки
    public abstract class NoteBase : INotifyPropertyChanged
    {
        private string _title;
        private DateTime _createdDate;
        private NoteType _noteType;
        private Notification _notification;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
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

        public Notification Notification
        {
            get => _notification;
            set { _notification = value; OnPropertyChanged(); }
        }

        public NoteBase()
        {
            CreatedDate = DateTime.Now;
            Notification = new Notification();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Текстовая заметка
    public class TextNote : NoteBase
    {
        private string _content;

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public TextNote()
        {
            NoteType = NoteType.Text;
        }
    }

    // Элемент списка
    public class ListItem : INotifyPropertyChanged
    {
        private string _text;

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Элемент чек-листа
    public class ChecklistItem : INotifyPropertyChanged
    {
        private string _itemName;
        private bool _isChecked;

        public string ItemName
        {
            get => _itemName;
            set { _itemName = value; OnPropertyChanged(); }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Список заметка
    public class ListNote : NoteBase
    {
        private ObservableCollection<ListItem> _items;

        public ObservableCollection<ListItem> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(); }
        }

        public ListNote()
        {
            NoteType = NoteType.List;
            Items = new ObservableCollection<ListItem>();
        }
    }

    // Чек-лист заметка
    public class ChecklistNote : NoteBase
    {
        private ObservableCollection<ChecklistItem> _items;

        public ObservableCollection<ChecklistItem> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(); }
        }

        public ChecklistNote()
        {
            NoteType = NoteType.Checklist;
            Items = new ObservableCollection<ChecklistItem>();
        }
    }

    // Модель путешествия
    public class Travel : INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private string _route;
        private ObservableCollection<NoteBase> _pinnedNotes;
        private ObservableCollection<RoutePoint> _routePoints;
        private ObservableCollection<TravelString> _travelStrings;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Route
        {
            get => _route;
            set { _route = value; OnPropertyChanged(); }
        }

        public ObservableCollection<NoteBase> Notes { get; set; }

        public ObservableCollection<NoteBase> PinnedNotes
        {
            get => _pinnedNotes;
            set { _pinnedNotes = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RoutePoint> RoutePoints
        {
            get => _routePoints;
            set { _routePoints = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TravelString> TravelStrings
        {
            get => _travelStrings;
            set { _travelStrings = value; OnPropertyChanged(); }
        }

        public Travel()
        {
            Notes = new ObservableCollection<NoteBase>();
            PinnedNotes = new ObservableCollection<NoteBase>();
            RoutePoints = new ObservableCollection<RoutePoint>();
            TravelStrings = new ObservableCollection<TravelString>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Точка маршрута
    public class RoutePoint : INotifyPropertyChanged
    {
        private int _id;
        private double _latitude;
        private double _longitude;
        private string _title;
        private int _order;
        private string _iconEmoji = "📍";
        private string _iconType = "default";
        private string _description = "";
        private string _iconColor = "#e2e8f0";
        private int _iconSize = 36;
        private string _status = "planned";
        private string _photoUrl = "";
        private string _visitDate = "";

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public double Latitude
        {
            get => _latitude;
            set { _latitude = value; OnPropertyChanged(); }
        }

        public double Longitude
        {
            get => _longitude;
            set { _longitude = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(); }
        }

        public string IconEmoji
        {
            get => _iconEmoji;
            set { _iconEmoji = value; OnPropertyChanged(); }
        }

        public string IconType
        {
            get => _iconType;
            set { _iconType = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string IconColor
        {
            get => _iconColor;
            set { _iconColor = value; OnPropertyChanged(); }
        }

        public int IconSize
        {
            get => _iconSize;
            set { _iconSize = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string PhotoUrl
        {
            get => _photoUrl;
            set { _photoUrl = value; OnPropertyChanged(); }
        }

        public string VisitDate
        {
            get => _visitDate;
            set { _visitDate = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Модель соединения
    public class TravelString : INotifyPropertyChanged
    {
        private int _id;
        private int _from;
        private int _to;
        private string _description = "";
        private string _color = "#ed8936";
        private double _width = 2;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public int From
        {
            get => _from;
            set { _from = value; OnPropertyChanged(); }
        }

        public int To
        {
            get => _to;
            set { _to = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}