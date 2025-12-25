using FeelShare.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using FeelShare.Web.Services;

namespace FeelShare.Web.Data
{
    
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Таблицы проекта 
        public DbSet<Emotion> Emotions => Set<Emotion>();
        public DbSet<Quote> Quotes => Set<Quote>();
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

        public DbSet<PublicStory> PublicStories => Set<PublicStory>();

        
        public DbSet<StoryLike> StoryLikes => Set<StoryLike>();

        // Реакции (сердце/обнять и т.д.)
        public DbSet<StoryReaction> StoryReactions => Set<StoryReaction>();

        // Комментарии к историям
        public DbSet<StoryComment> StoryComments => Set<StoryComment>();

        // Замеры настроения (опросы + элементы)
        public DbSet<MoodSurvey> MoodSurveys { get; set; } = default!;
        public DbSet<MoodSurveyItem> MoodSurveyItems { get; set; } = default!;

        // Советы по категориям bad/neutral/good
        public DbSet<MoodAdvice> MoodAdvices => Set<MoodAdvice>();

        // Жалобы на истории
        public DbSet<StoryReport> StoryReports => Set<StoryReport>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            //  Названия таблиц 
            b.Entity<Emotion>().ToTable("Emotion");
            b.Entity<Quote>().ToTable("Quote");
            b.Entity<JournalEntry>().ToTable("JournalEntry");
            b.Entity<PublicStory>().ToTable("PublicStory");
            b.Entity<StoryLike>().ToTable("StoryLike");
            b.Entity<StoryReport>().ToTable("StoryReport");

            //  StoryReport: связи + ограничения
            b.Entity<StoryReport>()
                .HasOne(r => r.Story)
                .WithMany(s => s.Reports)
                .HasForeignKey(r => r.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // один человек/гость может пожаловаться на историю только 1 раз
            b.Entity<StoryReport>()
                .HasIndex(x => new { x.StoryId, x.ReportKey })
                .IsUnique();

            // быстрый фильтр по количеству жалоб и статусу
            b.Entity<PublicStory>()
                .HasIndex(x => new { x.ModerationStatus, x.ReportsCount, x.CreatedAtUtc, x.Id });

            // Slug эмоции 
            b.Entity<Emotion>().HasIndex(e => e.Slug).IsUnique();

            //  MoodSurvey + MoodSurveyItem 
            b.Entity<MoodSurvey>().ToTable("MoodSurvey");
            b.Entity<MoodSurveyItem>().ToTable("MoodSurveyItem");

            //  MoodAdvice 
            b.Entity<MoodAdvice>().ToTable("MoodAdvice");
            b.Entity<MoodAdvice>().HasIndex(a => new { a.Category, a.IsActive });

            // лёгкий сид советов (стартовый набор)
            b.Entity<MoodAdvice>().HasData(
                new MoodAdvice { Id = 1, Category = "bad", Text = "Сегодня тяжело — это нормально. Дайте себе немного заботы и отдыха.", IsActive = true },
                new MoodAdvice { Id = 2, Category = "neutral", Text = "Неплохо! Маленькие радости дня усиливают устойчивость :)", IsActive = true },
                new MoodAdvice { Id = 3, Category = "good", Text = "Отличное настроение! Поделитесь теплом с близкими 💜", IsActive = true }
            );

            // 1 survey -> много items, при удалении survey удаляются items
            b.Entity<MoodSurvey>()
                .HasMany(s => s.Items)
                .WithOne(i => i.Survey)
                .HasForeignKey(i => i.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);

            // быстрый фильтр  для пользователя
            b.Entity<MoodSurvey>()
                .HasIndex(s => new { s.UserId, s.CreatedAtUtc });

            //  PublicStory: связи с эмоцией и пользователем 
            b.Entity<PublicStory>()
                .HasOne(s => s.Emotion)
                .WithMany()  
                .HasForeignKey(s => s.EmotionId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<PublicStory>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            
            // сортировка новые сверху
            b.Entity<PublicStory>()
                .HasIndex(x => new { x.CreatedAtUtc, x.Id });

            // фильтр по эмоции + сортировка
            b.Entity<PublicStory>()
                .HasIndex(x => new { x.EmotionId, x.CreatedAtUtc, x.Id });

            //  Ограничения лайков/реакций/комментов 
            // один лайк на историю для одного LikeKey
            b.Entity<StoryLike>()
                .HasIndex(x => new { x.StoryId, x.LikeKey })
                .IsUnique();

            // реакции: связи
            b.Entity<StoryReaction>()
                .HasOne(r => r.Story)
                .WithMany(s => s.Reactions)
                .HasForeignKey(r => r.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // комментарии: связи
            b.Entity<StoryComment>()
                .HasOne(c => c.Story)
                .WithMany(s => s.Comments)
                .HasForeignKey(c => c.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<StoryComment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // уникальность: одна реакция типа X от одного reactKey на одну историю
            b.Entity<StoryReaction>()
                .HasIndex(x => new { x.StoryId, x.Reaction, x.ReactKey })
                .IsUnique();

            // индекс для сортировки комментариев внутри истории
            b.Entity<StoryComment>()
                .HasIndex(x => new { x.StoryId, x.CreatedAtUtc });

            //  Сид эмоций (стартовые карточки на главной) 
            b.Entity<Emotion>().HasData(
                new Emotion { Id = 1, Slug = "sad", Name = "Мне грустно", Icon = "😢" },
                new Emotion { Id = 2, Slug = "happy", Name = "Радостно", Icon = "😊" },
                new Emotion { Id = 3, Slug = "anxious", Name = "Тревожно", Icon = "😟" },
                new Emotion { Id = 4, Slug = "angry", Name = "Злюсь", Icon = "😠" },
                new Emotion { Id = 5, Slug = "lonely", Name = "Одиноко", Icon = "🥺" },
                new Emotion { Id = 6, Slug = "grateful", Name = "Благодарен", Icon = "🙏" },
                new Emotion { Id = 7, Slug = "stuck", Name = "Нет вдохновения", Icon = "🪫" },
                new Emotion { Id = 8, Slug = "speak", Name = "Хочу высказаться", Icon = "🗣️" }
            );
        }
    }
}
