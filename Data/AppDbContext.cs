using FeelShare.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FeelShare.Web.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Emotion> Emotions => Set<Emotion>();
        public DbSet<Quote> Quotes => Set<Quote>();
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
        public DbSet<PublicStory> PublicStories => Set<PublicStory>();
        public DbSet<StoryLike> StoryLikes => Set<StoryLike>();
        public DbSet<StoryReaction> StoryReactions => Set<StoryReaction>();
        public DbSet<StoryComment> StoryComments => Set<StoryComment>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<Emotion>().ToTable("Emotion");           
            b.Entity<Quote>().ToTable("Quote");
            b.Entity<JournalEntry>().ToTable("JournalEntry");
            b.Entity<PublicStory>().ToTable("PublicStory");
            b.Entity<StoryLike>().ToTable("StoryLike");
            b.Entity<Emotion>().HasIndex(e => e.Slug).IsUnique();
            b.Entity<PublicStory>()
       .HasOne(s => s.Emotion)
       .WithMany() // истории не висят в навигации у Emotion
       .HasForeignKey(s => s.EmotionId)
       .OnDelete(DeleteBehavior.Restrict);

            b.Entity<PublicStory>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Один лайк на историю на один LikeKey
            b.Entity<StoryLike>()
                .HasIndex(x => new { x.StoryId, x.LikeKey })
                .IsUnique();
            // связи
            b.Entity<StoryReaction>()
                .HasOne(r => r.Story)
                .WithMany(s => s.Reactions)
                .HasForeignKey(r => r.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // уникальность: одна реакция типа X от одного пользователя/гостя на одну историю
            b.Entity<StoryReaction>()
                .HasIndex(x => new { x.StoryId, x.Reaction, x.ReactKey })
                .IsUnique();

            b.Entity<StoryComment>()
                .HasIndex(x => new { x.StoryId, x.CreatedAtUtc });
        
      
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