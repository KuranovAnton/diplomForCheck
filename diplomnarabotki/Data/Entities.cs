using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace diplomnarabotki.Data
{
    // DTO для уведомлений (сериализация в JSON)
    public class NotificationData
    {
        public DateTime ReminderTime { get; set; }
        public string RepeatType { get; set; } = "None";
        public string Sound { get; set; } = "Default";
        public bool IsEnabled { get; set; }
        public DateTime? LastNotified { get; set; }
    }

    // Сущность путешествия
    public class TravelEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Навигационные свойства
        public virtual List<NoteEntity> Notes { get; set; } = new();
        public virtual List<PinnedNoteEntity> PinnedNotes { get; set; } = new();
        public virtual List<RoutePointEntity> RoutePoints { get; set; } = new();
        public virtual List<TravelStringEntity> TravelStrings { get; set; } = new();
    }

    // Сущность заметки
    public class NoteEntity
    {
        public int Id { get; set; }
        public int TravelId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string NoteType { get; set; } = "Text"; // Text, List, Checklist
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Для TextNote
        public string? Content { get; set; }

        // Данные уведомления (хранятся как JSON строка)
        public string? NotificationDataJson { get; set; }

        // Навигационные свойства
        [JsonIgnore]
        public virtual TravelEntity? Travel { get; set; }
        public virtual List<ListItemEntity> ListItems { get; set; } = new();
        public virtual List<ChecklistItemEntity> ChecklistItems { get; set; } = new();
    }

    // Сущность закрепленной заметки
    public class PinnedNoteEntity
    {
        public int Id { get; set; }
        public int TravelId { get; set; }
        public int NoteId { get; set; }
        public int Order { get; set; }

        [JsonIgnore]
        public virtual TravelEntity? Travel { get; set; }
        public virtual NoteEntity? Note { get; set; }
    }

    // Элемент списка
    public class ListItemEntity
    {
        public int Id { get; set; }
        public int NoteId { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Order { get; set; }

        [JsonIgnore]
        public virtual NoteEntity? Note { get; set; }
    }

    // Элемент чек-листа
    public class ChecklistItemEntity
    {
        public int Id { get; set; }
        public int NoteId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public int Order { get; set; }

        [JsonIgnore]
        public virtual NoteEntity? Note { get; set; }
    }

    // Точка маршрута
    // Точка маршрута
    public class RoutePointEntity
    {
        public int Id { get; set; }
        public int TravelId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; }
        public string IconEmoji { get; set; } = "📍";
        public string IconType { get; set; } = "default";
        public string Description { get; set; } = "";
        public string IconColor { get; set; } = "#e2e8f0";
        public int IconSize { get; set; } = 36;
        public string Status { get; set; } = "planned";
        public string PhotoUrl { get; set; } = "";
        public string StoredPhotoPath { get; set; } = "";  // ✅ ДОБАВЬ ЭТУ СТРОКУ
        public string VisitDate { get; set; } = "";

        [JsonIgnore]
        public virtual TravelEntity? Travel { get; set; }
    }

    // Связь между точками
    public class TravelStringEntity
    {
        public int Id { get; set; }
        public int TravelId { get; set; }
        public int FromPointId { get; set; }
        public int ToPointId { get; set; }
        public string Description { get; set; } = "";
        public string Color { get; set; } = "#ed8936";
        public double Width { get; set; } = 2;

        [JsonIgnore]
        public virtual TravelEntity? Travel { get; set; }
        public virtual RoutePointEntity? FromPoint { get; set; }
        public virtual RoutePointEntity? ToPoint { get; set; }
    }
}
