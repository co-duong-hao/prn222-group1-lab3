using Lab3.Models;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ChatMessage>()
            .Property(message => message.MessageType)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<ChatMessage>()
            .HasMany(message => message.Reactions)
            .WithOne(reaction => reaction.ChatMessage)
            .HasForeignKey(reaction => reaction.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(message => new { message.RoomName, message.CreatedAt });

        modelBuilder.Entity<MessageReaction>()
            .HasIndex(reaction => new { reaction.ChatMessageId, reaction.SenderName, reaction.Emoji })
            .IsUnique();
    }
}
