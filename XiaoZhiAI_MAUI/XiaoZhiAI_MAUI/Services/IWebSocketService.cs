namespace XiaoZhiAI_MAUI.Services;

public interface IWebSocketService
{
    /// <summary>
    /// Gets the current status of the WebSocket connection.
    /// </summary>
    WebSocketStatus Status { get; }

    /// <summary>
    /// Event triggered when the connection status changes.
    /// </summary>
    event EventHandler<WebSocketStatus> StatusChanged;

    /// <summary>
    /// Event triggered when a text message is received.
    /// </summary>
    event EventHandler<string> MessageReceived;

    /// <summary>
    /// Event triggered when a binary message is received.
    /// </summary>
    event EventHandler<byte[]> BinaryMessageReceived;

    /// <summary>
    /// Connects to the WebSocket server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Disconnects from the WebSocket server.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAsync();

    /// <summary>
    /// Sends a text message to the server.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a boolean indicating success.</returns>
    Task<bool> SendTextAsync(string message, CancellationToken cancellationToken);

    /// <summary>
    /// Sends binary data to the server.
    /// </summary>
    /// <param name="data">The binary data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a boolean indicating success.</returns>
    Task<bool> SendBinaryAsync(byte[] data, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a detect message with the correct format for text input.
    /// </summary>
    /// <param name="text">The text content to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a boolean indicating success.</returns>
    Task<bool> SendDetectMessageAsync(string text, CancellationToken cancellationToken);
}

/// <summary>
/// Represents the status of the WebSocket connection.
/// </summary>
public enum WebSocketStatus
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Error
} 