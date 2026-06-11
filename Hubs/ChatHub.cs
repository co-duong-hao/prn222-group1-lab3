using Lab3.Data;
using Lab3.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Hubs;

public class ChatHub : Hub
{
    private static readonly HashSet<string> AllowedReactions = new(EmojiCatalog.ReactionEmojis);

    private readonly AppDbContext _dbContext;

    public ChatHub(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task JoinRoom(string roomName)
    {
        roomName = Clean(roomName, 100);

        if (string.IsNullOrWhiteSpace(roomName))
        {
            throw new HubException("Room name is required.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
    }

    public async Task SendMessage(string roomName, string senderName, string content)
    {
        roomName = Clean(roomName, 100);
        senderName = Clean(senderName, 80);
        content = Clean(content, 4000);

        if (string.IsNullOrWhiteSpace(roomName) ||
            string.IsNullOrWhiteSpace(senderName) ||
            string.IsNullOrWhiteSpace(content))
        {
            throw new HubException("Room, sender and message content are required.");
        }

        var message = new ChatMessage
        {
            RoomName = roomName,
            SenderName = senderName,
            Content = content,
            MessageType = ChatMessageType.Text,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ChatMessages.AddAsync(message);
        await _dbContext.SaveChangesAsync();

        // Persist first, then broadcast so every client receives a database-backed message id.
        await Clients.Group(roomName).SendAsync("ReceiveMessage", ToDto(message));
    }

    public async Task SendReaction(int messageId, string senderName, string emoji)
    {
        senderName = Clean(senderName, 80);
        emoji = Clean(emoji, 16);

        if (messageId <= 0 || string.IsNullOrWhiteSpace(senderName) || !AllowedReactions.Contains(emoji))
        {
            throw new HubException("Reaction data is invalid.");
        }

        var message = await _dbContext.ChatMessages
            .FirstOrDefaultAsync(item => item.Id == messageId);

        if (message is null)
        {
            throw new HubException("Message was not found.");
        }

        var existingReaction = await _dbContext.MessageReactions
            .FirstOrDefaultAsync(reaction =>
                reaction.ChatMessageId == messageId &&
                reaction.SenderName == senderName &&
                reaction.Emoji == emoji);

        if (existingReaction is null)
        {
            // One user can add each emoji once per message; the UI displays grouped counts.
            await _dbContext.MessageReactions.AddAsync(new MessageReaction
            {
                ChatMessageId = messageId,
                SenderName = senderName,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        var reactions = await GetReactionCountsAsync(messageId);

        await Clients.Group(message.RoomName).SendAsync("ReceiveReaction", new
        {
            MessageId = messageId,
            Reactions = reactions
        });
    }

    private static string Clean(string? value, int maxLength)
    {
        value = (value ?? string.Empty).Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private async Task<List<object>> GetReactionCountsAsync(int messageId)
    {
        return await _dbContext.MessageReactions
            .Where(reaction => reaction.ChatMessageId == messageId)
            .GroupBy(reaction => reaction.Emoji)
            .Select(group => new
            {
                Emoji = group.Key,
                Count = group.Count()
            })
            .Cast<object>()
            .ToListAsync();
    }

    private static object ToDto(ChatMessage message)
    {
        return new
        {
            message.Id,
            message.RoomName,
            message.SenderName,
            message.Content,
            MessageType = message.MessageType.ToString(),
            message.FileUrl,
            message.OriginalFileName,
            message.ContentType,
            message.FileSize,
            message.CreatedAt,
            Reactions = message.Reactions
                .GroupBy(reaction => reaction.Emoji)
                .Select(group => new
                {
                    Emoji = group.Key,
                    Count = group.Count()
                })
                .ToList()
        };
    }
}
