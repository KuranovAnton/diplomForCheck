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

            System.Diagnostics.Debug.WriteLine("=== LoadAllTravelsAsync ===");
            foreach (var entity in travelEntities)
            {
                System.Diagnostics.Debug.WriteLine($"Entity: Id={entity.Id}, Name={entity.Name}, Route={entity.Route}");
            }

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
            System.Diagnostics.Debug.WriteLine($"=== ConvertToTravel ===");
            System.Diagnostics.Debug.WriteLine($"Entity Id: {entity.Id}");
            System.Diagnostics.Debug.WriteLine($"Entity Name: '{entity.Name}'");
            System.Diagnostics.Debug.WriteLine($"Entity Route: '{entity.Route}'");

            var travel = new Travel
            {
                Id = entity.Id,
                Name = entity.Name ?? "",
                Route = entity.Route ?? ""
            };

            System.Diagnostics.Debug.WriteLine($"Created Travel Route: '{travel.Route}'");
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

            // ВАЖНО: Сохраняем Id точек при загрузке
            foreach (var pointEntity in entity.RoutePoints.OrderBy(p => p.Order))
            {
                var routePoint = new RoutePoint
                {
                    Id = pointEntity.Id,  // Сохраняем Id из БД
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

            // Загрузка связей - здесь From и To - это Id точек из БД
            foreach (var stringEntity in entity.TravelStrings)
            {
                travel.TravelStrings.Add(new TravelString
                {
                    From = stringEntity.FromPointId,
                    To = stringEntity.ToPointId,
                    Description = stringEntity.Description ?? "",
                    Color = stringEntity.Color ?? "#ed8936",
                    Width = stringEntity.Width > 0 ? stringEntity.Width : 2
                });
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

            foreach (var travelString in travel.TravelStrings)
            {
                entity.TravelStrings.Add(new TravelStringEntity
                {
                    TravelId = entity.Id,
                    FromPointId = travelString.From,
                    ToPointId = travelString.To,
                    Description = travelString.Description ?? "",
                    Color = travelString.Color ?? "#ed8936",
                    Width = travelString.Width > 0 ? travelString.Width : 2
                });
            }

            return entity;
        }

        private async Task UpdateTravelEntityAsync(TravelEntity existing, Travel updated)
        {
            existing.Name = updated.Name;
            existing.Route = updated.Route;
            existing.UpdatedAt = DateTime.Now;

            // 1. Удаляем старые закрепленные заметки (они зависят от Notes)
            var oldPinnedNotes = existing.PinnedNotes.ToList();
            _context.PinnedNotes.RemoveRange(oldPinnedNotes);
            await _context.SaveChangesAsync();
            existing.PinnedNotes.Clear();

            // 2. Удаляем старые заметки
            var oldNotes = existing.Notes.ToList();
            _context.Notes.RemoveRange(oldNotes);
            await _context.SaveChangesAsync();
            existing.Notes.Clear();

            // 3. Удаляем старые точки маршрута и связи
            var oldPoints = existing.RoutePoints.ToList();
            _context.RoutePoints.RemoveRange(oldPoints);
            await _context.SaveChangesAsync();
            existing.RoutePoints.Clear();

            var oldStrings = existing.TravelStrings.ToList();
            _context.TravelStrings.RemoveRange(oldStrings);
            await _context.SaveChangesAsync();
            existing.TravelStrings.Clear();

            // 4. Создаем новые заметки и сразу сохраняем их, чтобы получить Id
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

            // ВАЖНО: Сохраняем заметки, чтобы получить их Id
            await _context.SaveChangesAsync();

            // 5. Создаем новые закрепленные заметки (теперь NoteId существует)
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

            // 6. Создаем новые точки маршрута
            int pointOrder = 0;
            foreach (var point in updated.RoutePoints)
            {
                existing.RoutePoints.Add(new RoutePointEntity
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
                });
            }

            // Сохраняем точки, чтобы получить их Id
            await _context.SaveChangesAsync();

            // 7. Создаем словарь для связи индексов с реальными Id
            var pointsDict = new Dictionary<int, int>();
            var pointsList = existing.RoutePoints.OrderBy(p => p.Order).ToList();
            for (int i = 0; i < pointsList.Count; i++)
            {
                pointsDict[i] = pointsList[i].Id;
            }

            // 8. Создаем новые связи
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