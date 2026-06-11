using Lab3.Data;
using Lab3.Hubs;
using Lab3.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Pages;

[RequestSizeLimit(20 * 1024 * 1024)]
public class ChatModel : PageModel
{
    public const long MaxUploadSize = 20 * 1024 * 1024;
    public static readonly string[] AvailableEmojis = EmojiCatalog.MessageEmojis;
    public static readonly string[] ReactionEmojis = EmojiCatalog.ReactionEmojis;

    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatModel(
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        IHubContext<ChatHub> hubContext)
    {
        _dbContext = dbContext;
        _environment = environment;
        _hubContext = hubContext;
    }

    public string DisplayName { get; private set; } = string.Empty;
    public string RoomName { get; private set; } = string.Empty;
    public List<ChatMessageViewModel> Messages { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? displayName, string? roomName)
    {
        DisplayName = Clean(displayName, 80);
        RoomName = Clean(roomName, 100);

        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(RoomName))
        {
            return RedirectToPage("/Index");
        }

        var messages = await _dbContext.ChatMessages
            .AsNoTracking()
            .Include(message => message.Reactions)
            .Where(message => message.RoomName == RoomName)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync();

        Messages = messages.Select(ToViewModel).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? uploadFile, string? roomName, string? senderName)
    {
        roomName = Clean(roomName, 100);
        senderName = Clean(senderName, 80);

        if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(senderName))
        {
            return BadRequest(new { error = "Room and sender are required." });
        }

        if (uploadFile is null || uploadFile.Length == 0)
        {
            return BadRequest(new { error = "Please choose a file." });
        }

        if (uploadFile.Length > MaxUploadSize)
        {
            return BadRequest(new { error = "File size must be 20 MB or smaller." });
        }

        var originalFileName = Path.GetFileName(uploadFile.FileName);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return BadRequest(new { error = "File name is invalid." });
        }

        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var savedPath = Path.Combine(uploadsFolder, storedFileName);
        await using (var stream = new FileStream(savedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            // Stream the upload asynchronously instead of loading large files into memory.
            await uploadFile.CopyToAsync(stream);
        }

        var contentType = string.IsNullOrWhiteSpace(uploadFile.ContentType)
            ? "application/octet-stream"
            : uploadFile.ContentType;

        var message = new ChatMessage
        {
            RoomName = roomName,
            SenderName = senderName,
            MessageType = ImageContentTypes.Contains(contentType) ? ChatMessageType.Image : ChatMessageType.File,
            FileUrl = $"/uploads/{storedFileName}",
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSize = uploadFile.Length,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ChatMessages.AddAsync(message);
        await _dbContext.SaveChangesAsync();

        var dto = ToDto(message);
        await _hubContext.Clients.Group(roomName).SendAsync("ReceiveMessage", dto);

        return new JsonResult(new { success = true, message = dto });
    }

    public static string FormatFileSize(long? bytes)
    {
        if (bytes is null)
        {
            return string.Empty;
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.0} KB";
        }

        return $"{bytes / 1024d / 1024d:0.0} MB";
    }

    private static string Clean(string? value, int maxLength)
    {
        value = (value ?? string.Empty).Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static ChatMessageViewModel ToViewModel(ChatMessage message)
    {
        return new ChatMessageViewModel
        {
            Id = message.Id,
            RoomName = message.RoomName,
            SenderName = message.SenderName,
            Content = message.Content,
            MessageType = message.MessageType.ToString(),
            FileUrl = message.FileUrl,
            OriginalFileName = message.OriginalFileName,
            ContentType = message.ContentType,
            FileSize = message.FileSize,
            CreatedAt = message.CreatedAt,
            Reactions = message.Reactions
                .GroupBy(reaction => reaction.Emoji)
                .Select(group => new ReactionViewModel(group.Key, group.Count()))
                .ToList()
        };
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
            Reactions = Array.Empty<object>()
        };
    }
}

public class ChatMessageViewModel
{
    public int Id { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReactionViewModel> Reactions { get; set; } = [];
}

public record ReactionViewModel(string Emoji, int Count);
