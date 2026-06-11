# PRN222 Lab3 - Razor Pages Realtime Group Chat

This project is a simple Zalo-style group chat web app for PRN222.

## Main Technology

- ASP.NET Core Razor Pages, C#
- Entity Framework Core
- SQLite: `Data Source=chatlab.db`
- SignalR realtime hub at `/chatHub`
- Bootstrap, HTML, CSS, JavaScript
- Async/await for database work, message history, SignalR, and file upload streams

## NuGet Packages

- `Microsoft.EntityFrameworkCore.Sqlite` 8.0.22
- `Microsoft.EntityFrameworkCore.Design` 8.0.22

SignalR server support comes from `Microsoft.AspNetCore.App`. The browser SignalR client is stored locally at:

- `wwwroot/lib/signalr/signalr.min.js`

## Files Added Or Changed

- `Program.cs`
- `appsettings.json`
- `Lab3.csproj`
- `Data/AppDbContext.cs`
- `Models/ChatMessage.cs`
- `Models/ChatMessageType.cs`
- `Models/MessageReaction.cs`
- `Hubs/ChatHub.cs`
- `Pages/Index.cshtml`
- `Pages/Index.cshtml.cs`
- `Pages/Chat.cshtml`
- `Pages/Chat.cshtml.cs`
- `Pages/Shared/_Layout.cshtml`
- `wwwroot/css/chat.css`
- `wwwroot/js/chat.js`
- `wwwroot/lib/signalr/signalr.min.js`
- `wwwroot/uploads/.gitkeep`

## Run In Visual Studio

1. Open `Lab3.csproj` in Visual Studio.
2. Restore NuGet packages.
3. Run the project.
4. The SQLite database file `chatlab.db` is created automatically on first startup.
5. Open two browser tabs.
6. In each tab, enter a different display name and the same room name, for example `PRN222-Lab`.
7. Send text, emoji, reactions, images, and files to test realtime chat.

## Optional EF Core Migration Commands

The project uses `EnsureCreatedAsync()` for easy lab demo startup, so migrations are not required.

If your teacher requires migrations, replace `EnsureCreatedAsync()` with `MigrateAsync()` in `Program.cs`, then run:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## How It Works

1. User enters display name and room on `Pages/Index.cshtml`.
2. User enters `Pages/Chat.cshtml`, which loads message history from SQLite with `ToListAsync()`.
3. Browser connects to `/chatHub` and calls `JoinRoom(roomName)`.
4. Text messages call `SendMessage` on `ChatHub`.
5. The hub saves the message to SQLite with `AddAsync()` and `SaveChangesAsync()`.
6. The hub broadcasts the saved message to `Clients.Group(roomName)`.
7. File and image uploads go through `OnPostUploadAsync`.
8. Server stores uploaded files in `wwwroot/uploads` and stores only metadata in SQLite.
9. Upload progress is shown by JavaScript using `XMLHttpRequest.upload`.
10. Reactions call `SendReaction`, save to SQLite, and broadcast updated emoji counts.
