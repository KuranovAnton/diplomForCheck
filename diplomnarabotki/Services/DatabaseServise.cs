using diplomnarabotki.Data;
using diplomnarabotki.Models;
using diplomnarabotki.Models.Enums;
using diplomnarabotki.ViewModels;
using diplomnarabotki.ViewModels.NoteViewModels;
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
        private readonly PhotoService _photoService;

        public DatabaseService()
        {
            _context = new TravelDbContext();
            _context.Database.EnsureCreated();
            _photoService = new PhotoService();
        }

        public async Task<ObservableCollection<TravelViewModel>> LoadAllTravelsAsync()
        {
            var travels = new ObservableCollection<TravelViewModel>();

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
                var travelViewModel = await ConvertToTravelViewModelAsync(entity);
                travels.Add(travelViewModel);
            }

            return travels;
        }

        public async Task SaveTravelAsync(TravelViewModel travel)
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
                    var newEntity = await ConvertToEntityAsync(travel);
                    _context.Travels.Add(newEntity);
                    await _context.SaveChangesAsync();

                    // Обновляем ID у ViewModel после сохранения
                    travel.Id = newEntity.Id;
                    foreach (var point in travel.RoutePoints)
                    {
                        var savedPoint = newEntity.RoutePoints.FirstOrDefault(p =>
                            p.Latitude == point.Latitude &&
                            p.Longitude == point.Longitude &&
                            p.Order == point.Order);
                        if (savedPoint != null)
                        {
                            point.Id = savedPoint.Id;
                            // Сохраняем путь к фото, а не base64
                            if (!string.IsNullOrEmpty(savedPoint.PhotoUrl))
                            {
                                point.PhotoUrl = savedPoint.PhotoUrl;
                            }
                        }
                    }
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
            var travel = await _context.Travels
                .Include(t => t.RoutePoints)
                .FirstOrDefaultAsync(t => t.Id == travelId);

            if (travel != null)
            {
                // Удаляем все фото, связанные с этим путешествием
                foreach (var point in travel.RoutePoints)
                {
                    if (!string.IsNullOrEmpty(point.PhotoUrl) && point.PhotoUrl.StartsWith("Photos/"))
                    {
                        _photoService.DeletePhoto(point.PhotoUrl);
                    }
                }

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
                var oldTravels = JsonSerializer.Deserialize<ObservableCollection<TravelViewModel>>(json, options);

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

        private string? SerializeNotification(NotificationViewModel? notification)
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

        private NotificationViewModel? DeserializeNotification(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                var data = JsonSerializer.Deserialize<NotificationData>(json);
                if (data == null || !data.IsEnabled)
                    return null;

                return new NotificationViewModel
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

        private async Task<TravelViewModel> ConvertToTravelViewModelAsync(TravelEntity entity)
        {
            var travel = new TravelViewModel
            {
                Id = entity.Id,
                Name = entity.Name ?? "",
                Route = entity.Route ?? ""
            };

            var notesDict = new Dictionary<int, NoteBaseViewModel>();

            foreach (var noteEntity in entity.Notes.OrderBy(n => n.Id))
            {
                NoteBaseViewModel note = noteEntity.NoteType switch
                {
                    "Text" => new TextNoteViewModel
                    {
                        Title = noteEntity.Title,
                        Content = noteEntity.Content ?? "",
                        CreatedDate = noteEntity.CreatedDate
                    },
                    "List" => new ListNoteViewModel
                    {
                        Title = noteEntity.Title,
                        CreatedDate = noteEntity.CreatedDate,
                        Items = new ObservableCollection<ListItemModel>(
                            noteEntity.ListItems.OrderBy(i => i.Order).Select(i => new ListItemModel { Text = i.Text }))
                    },
                    "Checklist" => new ChecklistNoteViewModel
                    {
                        Title = noteEntity.Title,
                        CreatedDate = noteEntity.CreatedDate,
                        Items = new ObservableCollection<ChecklistItemModel>(
                            noteEntity.ChecklistItems.OrderBy(i => i.Order).Select(i => new ChecklistItemModel
                            {
                                ItemName = i.ItemName,
                                IsChecked = i.IsChecked
                            }))
                    },
                    _ => new TextNoteViewModel
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

            // Загружаем точки маршрута с фото
            foreach (var pointEntity in entity.RoutePoints.OrderBy(p => p.Order))
            {
                string photoBase64 = "";

                // Загружаем фото из файла, если есть путь к файлу
                if (!string.IsNullOrEmpty(pointEntity.PhotoUrl) && pointEntity.PhotoUrl.StartsWith("Photos/"))
                {
                    photoBase64 = await _photoService.LoadPhotoAsBase64Async(pointEntity.PhotoUrl);
                }
                else if (!string.IsNullOrEmpty(pointEntity.PhotoUrl) && pointEntity.PhotoUrl.StartsWith("data:image"))
                {
                    // Если это уже base64 (для обратной совместимости)
                    photoBase64 = pointEntity.PhotoUrl;
                }

                var routePoint = new RoutePointViewModel
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
                    PhotoUrl = photoBase64, // Загруженное фото в base64 для отображения
                    StoredPhotoPath = pointEntity.PhotoUrl, // Сохраняем путь для дальнейшего использования
                    VisitDate = pointEntity.VisitDate
                };
                travel.RoutePoints.Add(routePoint);
            }

            // Загружаем связи используя ID точек
            foreach (var stringEntity in entity.TravelStrings)
            {
                travel.TravelStrings.Add(new TravelStringViewModel
                {
                    Id = stringEntity.Id,
                    From = stringEntity.FromPointId,
                    To = stringEntity.ToPointId,
                    Description = stringEntity.Description ?? "",
                    Color = stringEntity.Color ?? "#ed8936",
                    Width = stringEntity.Width > 0 ? stringEntity.Width : 2
                });
            }

            return travel;
        }

        private async Task<TravelEntity> ConvertToEntityAsync(TravelViewModel travel)
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

                if (note is TextNoteViewModel textNote)
                {
                    noteEntity.Content = textNote.Content;
                }
                else if (note is ListNoteViewModel listNote)
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
                else if (note is ChecklistNoteViewModel checklistNote)
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
                string photoPath = point.StoredPhotoPath ?? "";

                // Если есть новое фото в base64, сохраняем его в файл
                if (!string.IsNullOrEmpty(point.PhotoUrl) && point.PhotoUrl.StartsWith("data:image"))
                {
                    // Удаляем старое фото, если оно было
                    if (!string.IsNullOrEmpty(point.StoredPhotoPath) && point.StoredPhotoPath.StartsWith("Photos/"))
                    {
                        _photoService.DeletePhoto(point.StoredPhotoPath);
                    }

                    // Сохраняем новое фото
                    photoPath = await _photoService.SavePhotoAsync(
                        point.PhotoUrl,
                        travel.Id.ToString(),
                        pointOrder.ToString()
                    );
                }
                else if (string.IsNullOrEmpty(point.PhotoUrl) && !string.IsNullOrEmpty(point.StoredPhotoPath))
                {
                    // Фото было удалено, удаляем файл
                    if (point.StoredPhotoPath.StartsWith("Photos/"))
                    {
                        _photoService.DeletePhoto(point.StoredPhotoPath);
                    }
                    photoPath = "";
                }

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
                    PhotoUrl = photoPath, // Сохраняем путь к файлу, а не base64
                    VisitDate = point.VisitDate
                });
            }

            return entity;
        }

        private async Task UpdateTravelEntityAsync(TravelEntity existing, TravelViewModel updated)
        {
            existing.Name = updated.Name;
            existing.Route = updated.Route;
            existing.UpdatedAt = DateTime.Now;

            // Очищаем старые связи между точками
            var oldStrings = existing.TravelStrings.ToList();
            _context.TravelStrings.RemoveRange(oldStrings);

            // Очищаем старые точки
            var oldPoints = existing.RoutePoints.ToList();
            _context.RoutePoints.RemoveRange(oldPoints);

            // Очищаем старые заметки и закрепления
            var oldPinnedNotes = existing.PinnedNotes.ToList();
            _context.PinnedNotes.RemoveRange(oldPinnedNotes);

            var oldNotes = existing.Notes.ToList();
            _context.Notes.RemoveRange(oldNotes);

            await _context.SaveChangesAsync();

            // Создаём новые заметки
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

                if (note is TextNoteViewModel textNote)
                {
                    noteEntity.Content = textNote.Content;
                }
                else if (note is ListNoteViewModel listNote)
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
                else if (note is ChecklistNoteViewModel checklistNote)
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

            // Сохраняем заметки
            await _context.SaveChangesAsync();

            // Создаём новые закрепленные заметки
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

            // Создаём новые точки маршрута с сохранением фото
            var oldToNewIdMap = new Dictionary<int, int>();
            int pointOrder = 0;

            foreach (var point in updated.RoutePoints)
            {
                string photoPath = point.StoredPhotoPath ?? "";

                // Если есть новое фото в base64, сохраняем его в файл
                if (!string.IsNullOrEmpty(point.PhotoUrl) && point.PhotoUrl.StartsWith("data:image"))
                {
                    // Удаляем старое фото, если оно было
                    if (!string.IsNullOrEmpty(point.StoredPhotoPath) && point.StoredPhotoPath.StartsWith("Photos/"))
                    {
                        _photoService.DeletePhoto(point.StoredPhotoPath);
                    }

                    // Сохраняем новое фото
                    photoPath = await _photoService.SavePhotoAsync(
                        point.PhotoUrl,
                        existing.Id.ToString(),
                        pointOrder.ToString()
                    );
                }
                else if (string.IsNullOrEmpty(point.PhotoUrl) && !string.IsNullOrEmpty(point.StoredPhotoPath))
                {
                    // Фото было удалено, удаляем файл
                    if (point.StoredPhotoPath.StartsWith("Photos/"))
                    {
                        _photoService.DeletePhoto(point.StoredPhotoPath);
                    }
                    photoPath = "";
                }

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
                    PhotoUrl = photoPath, // Сохраняем путь к файлу
                    VisitDate = point.VisitDate
                };
                existing.RoutePoints.Add(pointEntity);
            }

            // Сохраняем точки, чтобы получить новые ID
            await _context.SaveChangesAsync();

            // Заполняем карту старых ID на новые
            var newPoints = existing.RoutePoints.OrderBy(p => p.Order).ToList();
            var oldPointsList = updated.RoutePoints.OrderBy(p => p.Order).ToList();

            for (int i = 0; i < newPoints.Count && i < oldPointsList.Count; i++)
            {
                oldToNewIdMap[oldPointsList[i].Id] = newPoints[i].Id;
                // Обновляем ID и путь к фото в ViewModel
                oldPointsList[i].Id = newPoints[i].Id;
                oldPointsList[i].StoredPhotoPath = newPoints[i].PhotoUrl;
                // Очищаем base64 фото после сохранения
                if (oldPointsList[i].PhotoUrl != null && oldPointsList[i].PhotoUrl.StartsWith("data:image"))
                {
                    oldPointsList[i].PhotoUrl = "";
                }
            }

            System.Diagnostics.Debug.WriteLine($"=== Saving strings to DB ===");
            System.Diagnostics.Debug.WriteLine($"Old to New ID map: {string.Join(", ", oldToNewIdMap.Select(kvp => $"{kvp.Key}->{kvp.Value}"))}");

            // Создаём новые связи используя новые ID точек
            foreach (var travelString in updated.TravelStrings)
            {
                System.Diagnostics.Debug.WriteLine($"Processing string: From={travelString.From}, To={travelString.To}");

                // Конвертируем старые ID в новые
                int newFromId = oldToNewIdMap.ContainsKey(travelString.From) ? oldToNewIdMap[travelString.From] : travelString.From;
                int newToId = oldToNewIdMap.ContainsKey(travelString.To) ? oldToNewIdMap[travelString.To] : travelString.To;

                System.Diagnostics.Debug.WriteLine($"Converted to new IDs: From={newFromId}, To={newToId}");

                // Проверяем существование точек с новыми ID
                var fromPointExists = existing.RoutePoints.Any(p => p.Id == newFromId);
                var toPointExists = existing.RoutePoints.Any(p => p.Id == newToId);

                if (fromPointExists && toPointExists)
                {
                    var stringEntity = new TravelStringEntity
                    {
                        TravelId = existing.Id,
                        FromPointId = newFromId,
                        ToPointId = newToId,
                        Description = travelString.Description ?? "",
                        Color = travelString.Color ?? "#ed8936",
                        Width = travelString.Width > 0 ? travelString.Width : 2
                    };

                    existing.TravelStrings.Add(stringEntity);
                    System.Diagnostics.Debug.WriteLine($"Saved string with IDs: {newFromId} -> {newToId}");

                    // Обновляем ID в ViewModel
                    travelString.From = newFromId;
                    travelString.To = newToId;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Points not found. FromPointExists={fromPointExists}, ToPointExists={toPointExists}");
                }
            }

            // Финальное сохранение
            await _context.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"Total strings saved: {existing.TravelStrings.Count}");
        }
    }
}