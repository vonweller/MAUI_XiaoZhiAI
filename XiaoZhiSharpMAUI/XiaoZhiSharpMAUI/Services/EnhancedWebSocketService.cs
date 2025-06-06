using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using Microsoft.Maui.ApplicationModel;

namespace XiaoZhiSharpMAUI.Services
{
    public class EnhancedWebSocketService : IDisposable
    {
        private readonly ILogger<EnhancedWebSocketService> _logger;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly string _webSocketUrl;
        private readonly string _token;
        private readonly string _deviceId;
        private bool _disposed;
        private bool _isConnecting;
        private int _reconnectAttempts;
        private const int MaxReconnectAttempts = 5;
        private const int ReconnectDelayMs = 3000;

        // 事件
        public event EventHandler<string>? MessageReceived;
        public event EventHandler<byte[]>? BinaryDataReceived;
        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;

        // 属性
        public bool IsConnected => _webSocket?.State == WebSocketState.Open;
        public string? SessionId { get; private set; }

        public EnhancedWebSocketService(string webSocketUrl, string token, string deviceId, ILogger<EnhancedWebSocketService> logger)
        {
            _webSocketUrl = webSocketUrl ?? throw new ArgumentNullException(nameof(webSocketUrl));
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ConnectAsync()
        {
            if (_isConnecting || IsConnected)
            {
                _logger.LogWarning("WebSocket connection already in progress or established");
                return IsConnected;
            }

            try
            {
                _isConnecting = true;
                
                // 检查网络连接
                var networkAccess = Connectivity.NetworkAccess;
                if (networkAccess != NetworkAccess.Internet)
                {
                    _logger.LogError("No internet connection available");
                    ConnectionStatusChanged?.Invoke(this, "No Internet Connection");
                    return false;
                }

                await CloseAsync();

                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                // 配置WebSocket选项
                ConfigureWebSocketOptions();

                _logger.LogInformation($"Connecting to WebSocket: {_webSocketUrl}");
                ConnectionStatusChanged?.Invoke(this, "Connecting...");

                // 设置连接超时
                var connectTask = _webSocket.ConnectAsync(new Uri(_webSocketUrl), _cancellationTokenSource.Token);
                var timeoutTask = Task.Delay(10000, _cancellationTokenSource.Token); // 10秒超时

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _logger.LogError("WebSocket connection timeout");
                    ConnectionStatusChanged?.Invoke(this, "Connection Timeout");
                    return false;
                }

                await connectTask; // 确保连接任务完成

                if (IsConnected)
                {
                    _logger.LogInformation("WebSocket connected successfully");
                    ConnectionStatusChanged?.Invoke(this, "Connected");
                    _reconnectAttempts = 0;

                    // 启动消息接收
                    _ = Task.Run(ReceiveMessagesAsync);

                    // 启动连接监控
                    _ = Task.Run(MonitorConnectionAsync);

                    return true;
                }
                else
                {
                    _logger.LogError($"WebSocket connection failed. State: {_webSocket.State}");
                    ConnectionStatusChanged?.Invoke(this, $"Connection Failed: {_webSocket.State}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect WebSocket");
                ErrorOccurred?.Invoke(this, ex);
                ConnectionStatusChanged?.Invoke(this, $"Connection Error: {ex.Message}");
                return false;
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private void ConfigureWebSocketOptions()
        {
            if (_webSocket == null) return;

            try
            {
                // 设置请求头
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_token}");
                _webSocket.Options.SetRequestHeader("Protocol-Version", "1");
                _webSocket.Options.SetRequestHeader("Device-Id", _deviceId);
                _webSocket.Options.SetRequestHeader("Client-Id", Guid.NewGuid().ToString());
                _webSocket.Options.SetRequestHeader("User-Agent", "XiaoZhiMAUI/1.0.0 Android");

                // 配置缓冲区大小
                _webSocket.Options.SetBuffer(1024 * 16, 1024 * 16); // 16KB缓冲区

                // 配置Keep-Alive
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                _logger.LogDebug("WebSocket options configured");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure some WebSocket options");
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[1024 * 16]; // 16KB缓冲区

            try
            {
                while (IsConnected && !_cancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    var result = await _webSocket!.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        _cancellationTokenSource!.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket close message received");
                        break;
                    }

                    var messageBytes = new byte[result.Count];
                    Array.Copy(buffer, messageBytes, result.Count);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(messageBytes);
                        _logger.LogDebug($"Received text message: {message}");
                        
                        // 解析session_id
                        if (message.Contains("session_id"))
                        {
                            try
                            {
                                var json = System.Text.Json.JsonDocument.Parse(message);
                                if (json.RootElement.TryGetProperty("session_id", out var sessionIdProperty))
                                {
                                    SessionId = sessionIdProperty.GetString();
                                    _logger.LogInformation($"Session ID received: {SessionId}");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to parse session_id from message");
                            }
                        }

                        MessageReceived?.Invoke(this, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        _logger.LogDebug($"Received binary data: {messageBytes.Length} bytes");
                        BinaryDataReceived?.Invoke(this, messageBytes);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebSocket receive operation cancelled");
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, $"WebSocket error during receive: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex);
                _ = Task.Run(TryReconnectAsync);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during WebSocket receive");
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        private async Task MonitorConnectionAsync()
        {
            try
            {
                while (!_disposed && !_cancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    await Task.Delay(5000, _cancellationTokenSource!.Token); // 每5秒检查一次

                    if (!IsConnected && !_isConnecting)
                    {
                        _logger.LogWarning("WebSocket connection lost, attempting to reconnect");
                        _ = Task.Run(TryReconnectAsync);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Connection monitoring cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in connection monitoring");
            }
        }

        private async Task TryReconnectAsync()
        {
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                _logger.LogError($"Max reconnection attempts ({MaxReconnectAttempts}) reached");
                ConnectionStatusChanged?.Invoke(this, "Max Reconnection Attempts Reached");
                return;
            }

            _reconnectAttempts++;
            var delay = ReconnectDelayMs * _reconnectAttempts; // 递增延迟

            _logger.LogInformation($"Reconnection attempt {_reconnectAttempts}/{MaxReconnectAttempts} in {delay}ms");
            ConnectionStatusChanged?.Invoke(this, $"Reconnecting... ({_reconnectAttempts}/{MaxReconnectAttempts})");

            await Task.Delay(delay);

            if (!_disposed)
            {
                await ConnectAsync();
            }
        }

        public async Task SendTextAsync(string message)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send message: WebSocket not connected");
                throw new InvalidOperationException("WebSocket is not connected");
            }

            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await _webSocket!.SendAsync(
                    new ArraySegment<byte>(buffer), 
                    WebSocketMessageType.Text, 
                    true, 
                    _cancellationTokenSource!.Token);

                _logger.LogDebug($"Sent text message: {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send text message");
                throw;
            }
        }

        public async Task SendBinaryAsync(byte[] data)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send binary data: WebSocket not connected");
                throw new InvalidOperationException("WebSocket is not connected");
            }

            try
            {
                await _webSocket!.SendAsync(
                    new ArraySegment<byte>(data), 
                    WebSocketMessageType.Binary, 
                    true, 
                    _cancellationTokenSource!.Token);

                _logger.LogDebug($"Sent binary data: {data.Length} bytes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send binary data");
                throw;
            }
        }

        public async Task CloseAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Closing connection", 
                        CancellationToken.None);
                }

                _webSocket?.Dispose();
                _webSocket = null;

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                ConnectionStatusChanged?.Invoke(this, "Disconnected");
                _logger.LogInformation("WebSocket connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing WebSocket connection");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _ = CloseAsync();
        }
    }
} 