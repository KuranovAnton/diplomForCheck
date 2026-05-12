using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace diplomnarabotki.Data
{
    public class TravelDbContext : DbContext
    {
        public DbSet<TravelEntity> Travels { get; set; }
        public DbSet<NoteEntity> Notes { get; set; }
        public DbSet<PinnedNoteEntity> PinnedNotes { get; set; }
        public DbSet<RoutePointEntity> RoutePoints { get; set; }
        public DbSet<TravelStringEntity> TravelStrings { get; set; }
        public DbSet<ListItemEntity> ListItems { get; set; }
        public DbSet<ChecklistItemEntity> ChecklistItems { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = @"Server=DESKTOP-11PGGLI\SQLEXPRESS;Database=TravelJournalDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
            optionsBuilder.UseSqlServer(connectionString);
            optionsBuilder.EnableSensitiveDataLogging(true); // Включаем для отладки
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка сущности Travel
            modelBuilder.Entity<TravelEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Route).HasMaxLength(1000);
                entity.HasMany(e => e.Notes).WithOne(e => e.Travel).HasForeignKey(e => e.TravelId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.PinnedNotes).WithOne(e => e.Travel).HasForeignKey(e => e.TravelId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.RoutePoints).WithOne(e => e.Travel).HasForeignKey(e => e.TravelId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.TravelStrings).WithOne(e => e.Travel).HasForeignKey(e => e.TravelId).OnDelete(DeleteBehavior.Cascade);
            });

            // Настройка сущности Note
            modelBuilder.Entity<NoteEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.NoteType).HasMaxLength(50);
                entity.Property(e => e.Content).HasColumnType("nvarchar(max)");
                entity.Property(e => e.NotificationDataJson).HasColumnType("nvarchar(max)");

                entity.HasMany(e => e.ListItems).WithOne(e => e.Note).HasForeignKey(e => e.NoteId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.ChecklistItems).WithOne(e => e.Note).HasForeignKey(e => e.NoteId).OnDelete(DeleteBehavior.Cascade);
            });

            // Настройка ListItem
            modelBuilder.Entity<ListItemEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Text).IsRequired().HasMaxLength(500);
                entity.HasOne(e => e.Note).WithMany(e => e.ListItems).HasForeignKey(e => e.NoteId).OnDelete(DeleteBehavior.Cascade);
            });

            // Настройка ChecklistItem
            modelBuilder.Entity<ChecklistItemEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ItemName).IsRequired().HasMaxLength(500);
                entity.HasOne(e => e.Note).WithMany(e => e.ChecklistItems).HasForeignKey(e => e.NoteId).OnDelete(DeleteBehavior.Cascade);
            });

            // Настройка RoutePoint
            // Настройка RoutePoint
            modelBuilder.Entity<RoutePointEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.IconEmoji).HasMaxLength(10);
                entity.Property(e => e.IconType).HasMaxLength(50);
                entity.Property(e => e.IconColor).HasMaxLength(20);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.PhotoUrl).HasMaxLength(500);
                entity.Property(e => e.StoredPhotoPath).HasMaxLength(500);  // ✅ ДОБАВЬ ЭТУ СТРОКУ
                entity.Property(e => e.VisitDate).HasMaxLength(50);
                entity.HasOne(e => e.Travel).WithMany(e => e.RoutePoints).HasForeignKey(e => e.TravelId).OnDelete(DeleteBehavior.Cascade);
            });

            // ИСПРАВЛЕНИЕ: Настройка TravelString с правильным каскадным удалением
            modelBuilder.Entity<TravelStringEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Color).HasMaxLength(20);
                entity.HasOne(e => e.Travel).WithMany(e => e.TravelStrings).HasForeignKey(e => e.TravelId).OnDelete(DeleteBehavior.Cascade);

                // ИСПРАВЛЕНИЕ: Делаем связи с каскадным удалением
                entity.HasOne(e => e.FromPoint)
                    .WithMany()
                    .HasForeignKey(e => e.FromPointId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired(true);  // ИСПРАВЛЕНО: Делаем обязательным

                entity.HasOne(e => e.ToPoint)
                    .WithMany()
                    .HasForeignKey(e => e.ToPointId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired(true);  // ИСПРАВЛЕНО: Делаем обязательным
            });

            // Настройка PinnedNote
            modelBuilder.Entity<PinnedNoteEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Travel).WithMany(e => e.PinnedNotes).HasForeignKey(e => e.TravelId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Note).WithMany().HasForeignKey(e => e.NoteId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}