using System.ComponentModel.DataAnnotations;

namespace Lab3.Models;

public class ChatMessage
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string RoomName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string SenderName { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Content { get; set; }

    public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;

    [MaxLength(500)]
    public string? FileUrl { get; set; }

    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    [MaxLength(120)]
    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
}
