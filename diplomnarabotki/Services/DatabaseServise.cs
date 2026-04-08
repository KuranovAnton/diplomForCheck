using diplomnarabotki.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace diplomnarabotki.Services
{
    public class DatabaseService
    {
        private readonly TravelDbContext _context;

        public DatabaseService()
        {
            _context = new TravelDbContext();
            _context.Database.EnsureCreated();
        }

        public async Task<ObservableCollection<Travel>> LoadAllTravelsAsync()
        {
            var travels = new ObservableCollection<Travel>();

            var travelEntities = await _context.Travels
                .Include(t => t.Notes)
                    .ThenInclude(n => n.ListItems)
                .Include(t => t.Notes)
                    .ThenInclude(n => n.ChecklistItems)
                .Include(t => t.PinnedNotes)
                    .ThenInclude(pn => pn.Note)
                .Include(t => t.RoutePoints)
                .Include(t => t.TravelStrings)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();

            foreach (var entity in travelEntities)
            {
                travels.Add(ConvertToTravel(entity));
            }

            return travels;
        }

        public async Task SaveTravelAsync(Travel travel)
        {
            try
            {
                var existingEntity = await _context.Travels
                    .Include(t => t.Notes)
                        .ThenInclude(n => n.ListItems)
                    .Include(t => t.Notes)
                        .ThenInclude(n => n.ChecklistItems)
                    .Include(t => t.PinnedNotes)
                    .Include(t => t.RoutePoints)
                    .Include(t => t.TravelStrings)
                    .FirstOrDefaultAsync(t => t.Id == travel.Id);

                if (existingEntity != null && existingEntity.Id > 0)
                {
                    await UpdateTravelEntityAsync(existingEntity, travel);
                }
                else
                {
                    travel.Id = 0;
                    var newEntity = ConvertToEntity(travel);
                    _context.Travels.Add(newEntity);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                throw new Exception($"Ошибка сохранения в БД: {innerMessage}", ex);
            }
        }

        public async Task DeleteTravelAsync(int travelId)
        {
            var travel = await _context.Travels.FindAsync(travelId);
            if (travel != null)
            {
                _context.Travels.Remove(travel);
                await _context.SaveChangesAsync();
            }
        }

        public async Task MigrateFromJsonAsync(string jsonFilePath)
        {
            if (File.Exists(jsonFilePath))
            {
                var json = await File.ReadAllTextAsync(jsonFilePath);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var oldTravels = JsonSerializer.Deserialize<ObservableCollection<Travel>>(json, options);

                if (oldTravels != null)
                {
                    foreach (var oldTravel in oldTravels)
                    {
                        oldTravel.Id = 0;
                        await SaveTravelAsync(oldTravel);
                    }
                }
            }
        }

        private string? SerializeNotification(Notification? notification)
        {
            if (notification == null || !notification.IsEnabled)
                return null;

            var data = new NotificationData
            {
                IsEnabled = notification.IsEnabled,
                ReminderTime = notification.ReminderTime,
                RepeatType = notification.RepeatType.ToString(),
                Sound = notification.Sound.ToString(),
                LastNotified = notification.LastNotified
            };

            return JsonSerializer.Serialize(data);
        }

        private Notification? DeserializeNotification(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                var data = JsonSerializer.Deserialize<NotificationData>(json);
                if (data == null || !data.IsEnabled)
                    return null;

                return new Notification
                {
                    IsEnabled = data.IsEnabled,
                    ReminderTime = data.ReminderTime,
                    RepeatType = Enum.Parse<ReminderRepeatType>(data.RepeatType),
                    Sound = Enum.Parse<NotificationSound>(data.Sound),
                    LastNotified = data.LastNotified
                };
            }
            catch
            {
                return null;
            }
        }

        private Travel ConvertToTravel(TravelEntity entity)
        {
            var travel = new Travel
            {
                Id = entity.Id,
                Name = entity.Name ?? "",
                Route = entity.Route ?? ""
            };

            var notesDict = new Dictionary<int, NoteBase>();

            foreach (var noteEntity in entity.Notes.OrderBy(n => n.Id))
            {
                NoteBase note = noteEntity.NoteType switch
                {
                    "Text" => new TextNote
                    {
                        Title = noteEntity.Title,
                        Content = noteEntity.Content ?? "",
                        CreatedDate = noteEntity.CreatedDate
                    },
                    "List" => new ListNote
                    {
                        Title = noteEntity.Title,
                        CreatedDate = noteEntity.CreatedDate,
                        Items = new ObservableCollection<ListItem>(
                            noteEntity.ListItems.OrderBy(i => i.Order).Select(i => new ListItem { Text = i.Text }))
                    },
                    "Checklist" => new ChecklistNote
                    {
                        Title = noteEntity.Title,
                        CreatedDate = noteEntity.CreatedDate,
                        Items = new ObservableCollection<ChecklistItem>(
                            noteEntity.ChecklistItems.OrderBy(i => i.Order).Select(i => new ChecklistItem
                            {
                                ItemName = i.ItemName,
                                IsChecked = i.IsChecked
                            }))
                    },
                    _ => new TextNote
                    {
                        Title = noteEntity.Title,
                        Content = noteEntity.Content ?? "",
                        CreatedDate = noteEntity.CreatedDate
                    }
                };

                var notification = DeserializeNotification(noteEntity.NotificationDataJson);
                if (notification != null)
                {
                    note.Notification = notification;
                }

                travel.Notes.Add(note);
                notesDict[noteEntity.Id] = note;
            }

            foreach (var pinned in entity.PinnedNotes.OrderBy(p => p.Order))
            {
                if (pinned.Note != null && notesDict.ContainsKey(pinned.Note.Id))
                {
                    travel.PinnedNotes.Add(notesDict[pinned.Note.Id]);
                }
            }

            // Загружаем точки маршрута
            foreach (var pointEntity in entity.RoutePoints.OrderBy(p => p.Order))
            {
                var routePoint = new RoutePoint
                {
                    Id = pointEntity.Id,
                    Latitude = pointEntity.Latitude,
                    Longitude = pointEntity.Longitude,
                    Title = pointEntity.Title,
                    Order = pointEntity.Order,
                    IconEmoji = pointEntity.IconEmoji,
                    IconType = pointEntity.IconType,
                    Description = pointEntity.Description,
                    IconColor = pointEntity.IconColor,
                    IconSize = pointEntity.IconSize,
                    Status = pointEntity.Status,
                    PhotoUrl = pointEntity.PhotoUrl,
                    VisitDate = pointEntity.VisitDate
                };
                travel.RoutePoints.Add(routePoint);
            }

            // Загружаем связи (используем Order для связи, а не Id)
            var pointsByOrder = travel.RoutePoints.ToDictionary(p => p.Order, p => p.Order);

            foreach (var stringEntity in entity.TravelStrings)
            {
                // Находим точки по их Id из БД и получаем их Order
                var fromPoint = travel.RoutePoints.FirstOrDefault(p => p.Id == stringEntity.FromPointId);
                var toPoint = travel.RoutePoints.FirstOrDefault(p => p.Id == stringEntity.ToPointId);

                if (fromPoint != null && toPoint != null)
                {
                    travel.TravelStrings.Add(new TravelString
                    {
                        From = fromPoint.Order,
                        To = toPoint.Order,
                        Description = stringEntity.Description ?? "",
                        Color = stringEntity.Color ?? "#ed8936",
                        Width = stringEntity.Width > 0 ? stringEntity.Width : 2
                    });
                }
            }

            return travel;
        }

        private TravelEntity ConvertToEntity(Travel travel)
        {
            var entity = new TravelEntity
            {
                Id = travel.Id,
                Name = travel.Name,
                Route = travel.Route ?? "",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            foreach (var note in travel.Notes)
            {
                var noteEntity = new NoteEntity
                {
                    TravelId = entity.Id,
                    Title = note.Title,
                    NoteType = note.NoteType.ToString(),
                    CreatedDate = note.CreatedDate,
                    NotificationDataJson = SerializeNotification(note.Notification)
                };

                if (note is TextNote textNote)
                {
                    noteEntity.Content = textNote.Content;
                }
                else if (note is ListNote listNote)
                {
                    int itemOrder = 0;
                    foreach (var item in listNote.Items)
                    {
                        noteEntity.ListItems.Add(new ListItemEntity
                        {
                            Text = item.Text,
                            Order = itemOrder++
                        });
                    }
                }
                else if (note is ChecklistNote checklistNote)
                {
                    int itemOrder = 0;
                    foreach (var item in checklistNote.Items)
                    {
                        noteEntity.ChecklistItems.Add(new ChecklistItemEntity
                        {
                            ItemName = item.ItemName,
                            IsChecked = item.IsChecked,
                            Order = itemOrder++
                        });
                    }
                }

                entity.Notes.Add(noteEntity);
            }

            int pointOrder = 0;
            foreach (var point in travel.RoutePoints)
            {
                entity.RoutePoints.Add(new RoutePointEntity
                {
                    Id = point.Id > 0 ? point.Id : 0,
                    TravelId = entity.Id,
                    Latitude = point.Latitude,
                    Longitude = point.Longitude,
                    Title = point.Title,
                    Order = pointOrder++,
                    IconEmoji = point.IconEmoji,
                    IconType = point.IconType,
                    Description = point.Description,
                    IconColor = point.IconColor,
                    IconSize = point.IconSize,
                    Status = point.Status,
                    PhotoUrl = point.PhotoUrl?.Length > 450 ? point.PhotoUrl.Substring(0, 450) : point.PhotoUrl ?? "",
                    VisitDate = point.VisitDate
                });
            }

            return entity;
        }

        private async Task UpdateTravelEntityAsync(TravelEntity existing, Travel updated)
        {
            existing.Name = updated.Name;
            existing.Route = updated.Route;
            existing.UpdatedAt = DateTime.Now;

            // ВАЖНО: Очищаем все связи перед обновлением
            var oldStrings = existing.TravelStrings.ToList();
            foreach (var oldString in oldStrings)
            {
                // Отвязываем точки перед удалением
                oldString.FromPoint = null;
                oldString.ToPoint = null;
                _context.Entry(oldString).State = EntityState.Detached;
            }
            _context.TravelStrings.RemoveRange(oldStrings);

            var oldPinnedNotes = existing.PinnedNotes.ToList();
            _context.PinnedNotes.RemoveRange(oldPinnedNotes);

            var oldNotes = existing.Notes.ToList();
            _context.Notes.RemoveRange(oldNotes);

            var oldPoints = existing.RoutePoints.ToList();
            foreach (var oldPoint in oldPoints)
            {
                // Очищаем связи
                _context.Entry(oldPoint).State = EntityState.Detached;
            }
            _context.RoutePoints.RemoveRange(oldPoints);

            // Очищаем коллекции
            existing.TravelStrings.Clear();
            existing.PinnedNotes.Clear();
            existing.Notes.Clear();
            existing.RoutePoints.Clear();

            await _context.SaveChangesAsync();

            // Создаем новые заметки
            var newNotes = new List<NoteEntity>();
            foreach (var note in updated.Notes)
            {
                var noteEntity = new NoteEntity
                {
                    TravelId = existing.Id,
                    Title = note.Title,
                    NoteType = note.NoteType.ToString(),
                    CreatedDate = note.CreatedDate,
                    NotificationDataJson = SerializeNotification(note.Notification)
                };

                if (note is TextNote textNote)
                {
                    noteEntity.Content = textNote.Content;
                }
                else if (note is ListNote listNote)
                {
                    int itemOrder = 0;
                    foreach (var item in listNote.Items)
                    {
                        noteEntity.ListItems.Add(new ListItemEntity
                        {
                            Text = item.Text,
                            Order = itemOrder++
                        });
                    }
                }
                else if (note is ChecklistNote checklistNote)
                {
                    int itemOrder = 0;
                    foreach (var item in checklistNote.Items)
                    {
                        noteEntity.ChecklistItems.Add(new ChecklistItemEntity
                        {
                            ItemName = item.ItemName,
                            IsChecked = item.IsChecked,
                            Order = itemOrder++
                        });
                    }
                }

                existing.Notes.Add(noteEntity);
                newNotes.Add(noteEntity);
            }

            // Сохраняем заметки, чтобы получить их Id
            await _context.SaveChangesAsync();

            // Создаем новые закрепленные заметки
            int pinnedOrder = 0;
            foreach (var pinnedNote in updated.PinnedNotes)
            {
                var matchingNote = newNotes.FirstOrDefault(n =>
                    n.Title == pinnedNote.Title && n.CreatedDate == pinnedNote.CreatedDate);

                if (matchingNote != null)
                {
                    existing.PinnedNotes.Add(new PinnedNoteEntity
                    {
                        TravelId = existing.Id,
                        NoteId = matchingNote.Id,
                        Order = pinnedOrder++
                    });
                }
            }

            // Создаем новые точки маршрута
            int pointOrder = 0;
            var pointsList = new List<RoutePointEntity>();
            foreach (var point in updated.RoutePoints)
            {
                var pointEntity = new RoutePointEntity
                {
                    TravelId = existing.Id,
                    Latitude = point.Latitude,
                    Longitude = point.Longitude,
                    Title = point.Title,
                    Order = pointOrder++,
                    IconEmoji = point.IconEmoji,
                    IconType = point.IconType,
                    Description = point.Description,
                    IconColor = point.IconColor,
                    IconSize = point.IconSize,
                    Status = point.Status,
                    PhotoUrl = point.PhotoUrl?.Length > 450 ? point.PhotoUrl.Substring(0, 450) : point.PhotoUrl ?? "",
                    VisitDate = point.VisitDate
                };
                existing.RoutePoints.Add(pointEntity);
                pointsList.Add(pointEntity);
            }

            // Сохраняем точки, чтобы получить их Id
            await _context.SaveChangesAsync();

            // Создаем словарь для связи Order -> Id
            var pointsDict = pointsList.ToDictionary(p => p.Order, p => p.Id);

            // Создаем новые связи
            foreach (var travelString in updated.TravelStrings)
            {
                if (pointsDict.ContainsKey(travelString.From) && pointsDict.ContainsKey(travelString.To))
                {
                    existing.TravelStrings.Add(new TravelStringEntity
                    {
                        TravelId = existing.Id,
                        FromPointId = pointsDict[travelString.From],
                        ToPointId = pointsDict[travelString.To],
                        Description = travelString.Description ?? "",
                        Color = travelString.Color ?? "#ed8936",
                        Width = travelString.Width > 0 ? travelString.Width : 2
                    });
                }
            }

            // Финальное сохранение
            await _context.SaveChangesAsync();
        }
    }
}