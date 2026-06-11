using System.ComponentModel.DataAnnotations;

namespace Lab3.Models;

public class MessageReaction
{
    public int Id { get; set; }

    public int ChatMessageId { get; set; }

    public ChatMessage? ChatMessage { get; set; }

    [MaxLength(80)]
    public string SenderName { get; set; } = string.Empty;

    [MaxLength(16)]
    public string Emoji { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
