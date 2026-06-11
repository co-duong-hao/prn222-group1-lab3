(() => {
    const shell = document.querySelector(".chat-shell");
    if (!shell) {
        return;
    }

    const roomName = shell.dataset.roomName;
    const senderName = shell.dataset.senderName;
    const messagesList = document.getElementById("messagesList");
    const messageForm = document.getElementById("messageForm");
    const messageInput = document.getElementById("messageInput");
    const emojiButton = document.getElementById("emojiButton");
    const emojiPicker = document.getElementById("emojiPicker");
    const fileInput = document.getElementById("fileInput");
    const uploadPanel = document.getElementById("uploadPanel");
    const uploadBar = document.getElementById("uploadBar");
    const uploadStatus = document.getElementById("uploadStatus");
    const token = document.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

    if (!window.signalR) {
        showUploadError("SignalR client script could not be loaded.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessage", (message) => {
        appendMessage(message);
        scrollToBottom();
    });

    connection.on("ReceiveReaction", (payload) => {
        const messageId = read(payload, "messageId");
        const reactions = read(payload, "reactions") ?? [];
        updateReactionSummary(messageId, reactions);
    });

    startConnection();
    scrollToBottom();

    messageForm.addEventListener("submit", async (event) => {
        event.preventDefault();

        const content = messageInput.value.trim();
        if (!content) {
            return;
        }

        try {
            await connection.invoke("SendMessage", roomName, senderName, content);
            messageInput.value = "";
            messageInput.focus();
        } catch (error) {
            alert(error.message ?? "Could not send message.");
        }
    });

    emojiButton.addEventListener("click", () => {
        emojiPicker.hidden = !emojiPicker.hidden;
    });

    emojiPicker.addEventListener("click", (event) => {
        const button = event.target.closest("button[data-emoji]");
        if (!button) {
            return;
        }

        messageInput.value += button.dataset.emoji;
        emojiPicker.hidden = true;
        messageInput.focus();
    });

    fileInput.addEventListener("change", () => {
        const file = fileInput.files[0];
        if (!file) {
            return;
        }

        uploadFile(file);
    });

    messagesList.addEventListener("click", async (event) => {
        const toggle = event.target.closest(".reaction-toggle");
        if (toggle) {
            const picker = toggle.parentElement.querySelector(".reaction-picker");
            picker.hidden = !picker.hidden;
            return;
        }

        const reactionButton = event.target.closest(".reaction-picker button[data-emoji]");
        if (!reactionButton) {
            return;
        }

        const row = reactionButton.closest(".message-row");
        const messageId = Number(row.dataset.messageId);
        const emoji = reactionButton.dataset.emoji;

        try {
            await connection.invoke("SendReaction", messageId, senderName, emoji);
            reactionButton.closest(".reaction-picker").hidden = true;
        } catch (error) {
            alert(error.message ?? "Could not send reaction.");
        }
    });

    document.addEventListener("click", (event) => {
        if (!event.target.closest(".emoji-picker") && !event.target.closest("#emojiButton")) {
            emojiPicker.hidden = true;
        }
    });

    async function startConnection() {
        try {
            await connection.start();
            await connection.invoke("JoinRoom", roomName);
        } catch {
            setTimeout(startConnection, 1500);
        }
    }

    function uploadFile(file) {
        const maxSize = 20 * 1024 * 1024;
        if (file.size > maxSize) {
            showUploadError("File size must be 20 MB or smaller.");
            fileInput.value = "";
            return;
        }

        const formData = new FormData();
        formData.append("uploadFile", file);
        formData.append("roomName", roomName);
        formData.append("senderName", senderName);
        formData.append("__RequestVerificationToken", token);

        // Files go through HTTP for progress events; the server broadcasts the saved message.
        const xhr = new XMLHttpRequest();
        xhr.open("POST", "/Chat?handler=Upload", true);

        xhr.upload.addEventListener("progress", (event) => {
            if (!event.lengthComputable) {
                return;
            }

            const percent = Math.round((event.loaded / event.total) * 100);
            setUploadProgress(percent, `Uploading ${file.name}`);
        });

        xhr.addEventListener("load", () => {
            fileInput.value = "";

            if (xhr.status >= 200 && xhr.status < 300) {
                setUploadProgress(100, "Upload completed.");
                setTimeout(() => {
                    uploadPanel.hidden = true;
                    setUploadProgress(0, "");
                }, 900);
                return;
            }

            let message = "Upload failed.";
            try {
                message = JSON.parse(xhr.responseText).error ?? message;
            } catch {
                message = xhr.responseText || message;
            }

            showUploadError(message);
        });

        xhr.addEventListener("error", () => {
            fileInput.value = "";
            showUploadError("Network error while uploading.");
        });

        uploadPanel.hidden = false;
        setUploadProgress(0, `Uploading ${file.name}`);
        xhr.send(formData);
    }

    function appendMessage(rawMessage) {
        const message = normalizeMessage(rawMessage);
        if (document.querySelector(`[data-message-id='${message.id}']`)) {
            return;
        }

        const row = document.createElement("article");
        row.className = `message-row ${equalsIgnoreCase(message.senderName, senderName) ? "mine" : "theirs"}`;
        row.dataset.messageId = message.id;

        const bubble = document.createElement("div");
        bubble.className = "message-bubble";

        const meta = document.createElement("div");
        meta.className = "message-meta";

        const sender = document.createElement("span");
        sender.textContent = message.senderName;

        const time = document.createElement("time");
        const createdAt = new Date(message.createdAt);
        time.dateTime = createdAt.toISOString();
        time.textContent = formatTime(createdAt);

        meta.append(sender, time);
        bubble.appendChild(meta);

        if (message.messageType === "Text") {
            const text = document.createElement("div");
            text.className = "message-text";
            text.textContent = message.content ?? "";
            bubble.appendChild(text);
        } else if (message.messageType === "Image") {
            const link = document.createElement("a");
            link.href = message.fileUrl;
            link.target = "_blank";
            link.rel = "noopener";

            const img = document.createElement("img");
            img.className = "chat-image";
            img.src = message.fileUrl;
            img.alt = message.originalFileName ?? "Uploaded image";

            const name = document.createElement("div");
            name.className = "file-name";
            name.textContent = message.originalFileName ?? "Image";

            link.appendChild(img);
            bubble.append(link, name);
        } else {
            const link = document.createElement("a");
            link.className = "file-link";
            link.href = message.fileUrl;
            link.download = message.originalFileName ?? "";
            link.textContent = message.originalFileName ?? "Download file";

            const size = document.createElement("div");
            size.className = "file-size";
            size.textContent = formatFileSize(message.fileSize);

            bubble.append(link, size);
        }

        bubble.appendChild(createReactionLine(message.reactions));
        row.appendChild(bubble);
        messagesList.appendChild(row);
    }

    function createReactionLine(reactions) {
        const line = document.createElement("div");
        line.className = "reaction-line";

        const toggle = document.createElement("button");
        toggle.className = "reaction-toggle";
        toggle.type = "button";
        toggle.setAttribute("aria-label", "React to message");
        toggle.textContent = "+";

        const picker = document.createElement("div");
        picker.className = "reaction-picker compact";
        picker.hidden = true;

        ["😀", "😂", "❤️", "👍", "😢", "😮"].forEach((emoji) => {
            const button = document.createElement("button");
            button.type = "button";
            button.dataset.emoji = emoji;
            button.textContent = emoji;
            picker.appendChild(button);
        });

        const summary = document.createElement("div");
        summary.className = "reaction-summary";
        fillReactionSummary(summary, reactions ?? []);

        line.append(toggle, picker, summary);
        return line;
    }

    function updateReactionSummary(messageId, reactions) {
        const row = document.querySelector(`[data-message-id='${messageId}']`);
        const summary = row?.querySelector(".reaction-summary");
        if (!summary) {
            return;
        }

        fillReactionSummary(summary, reactions);
    }

    function fillReactionSummary(summary, reactions) {
        summary.replaceChildren();

        reactions.forEach((reaction) => {
            const emoji = read(reaction, "emoji");
            const count = read(reaction, "count");
            const item = document.createElement("span");
            item.textContent = `${emoji} ${count}`;
            summary.appendChild(item);
        });
    }

    function normalizeMessage(message) {
        return {
            id: read(message, "id"),
            senderName: read(message, "senderName"),
            content: read(message, "content"),
            messageType: read(message, "messageType"),
            fileUrl: read(message, "fileUrl"),
            originalFileName: read(message, "originalFileName"),
            fileSize: read(message, "fileSize"),
            createdAt: read(message, "createdAt"),
            reactions: read(message, "reactions") ?? []
        };
    }

    function read(object, propertyName) {
        if (!object) {
            return undefined;
        }

        const pascalName = propertyName.charAt(0).toUpperCase() + propertyName.slice(1);
        return object[propertyName] ?? object[pascalName];
    }

    function setUploadProgress(percent, text) {
        uploadPanel.hidden = false;
        uploadBar.style.width = `${percent}%`;
        uploadBar.textContent = `${percent}%`;
        uploadStatus.textContent = text;
    }

    function showUploadError(message) {
        uploadPanel.hidden = false;
        uploadBar.style.width = "0%";
        uploadBar.textContent = "0%";
        uploadStatus.textContent = message;
    }

    function scrollToBottom() {
        messagesList.scrollTop = messagesList.scrollHeight;
    }

    function formatTime(date) {
        return date.toLocaleString([], {
            hour: "2-digit",
            minute: "2-digit",
            day: "2-digit",
            month: "2-digit",
            year: "numeric"
        });
    }

    function formatFileSize(bytes) {
        if (!bytes && bytes !== 0) {
            return "";
        }

        if (bytes < 1024) {
            return `${bytes} B`;
        }

        if (bytes < 1024 * 1024) {
            return `${(bytes / 1024).toFixed(1)} KB`;
        }

        return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    }

    function equalsIgnoreCase(left, right) {
        return (left ?? "").toLowerCase() === (right ?? "").toLowerCase();
    }
})();
