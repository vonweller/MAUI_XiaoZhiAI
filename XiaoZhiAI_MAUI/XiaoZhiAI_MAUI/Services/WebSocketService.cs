using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiaoZhiAI_MAUI.Models;
using XiaoZhiAI_MAUI.Utils;
using Newtonsoft.Json;

namespace XiaoZhiAI_MAUI.Services;

public class WebSocketService : IWebSocketService
{
    private ClientWebSocket _client;
    private CancellationTokenSource _cts;
    private TaskCompletionSource<bool> _serverHelloTcs;
    private static readonly HttpClient _httpClient = new();

    private const string OtaUrl = "https://api.tenclass.net/xiaozhi/ota/";
    private const string WebSocketUrl = "wss://api.tenclass.net/xiaozhi/v1/";
    private const string AccessToken = "test-token"; // Assuming this is correct from original setup

    private string _sessionId;
    public string SessionId => _sessionId;

    private WebSocketStatus _status;
    public WebSocketStatus Status
    {
        get => _status;
        private set
        {
            _status = value;
            StatusChanged?.Invoke(this, _status);
        }
    }

    public event EventHandler<WebSocketStatus> StatusChanged;
    public event EventHandler<string> MessageReceived;

    private string _webSocketUrl = WebSocketUrl;
    private string _accessToken = AccessToken;
    private string _deviceId;
    private string _clientId;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_client?.State == WebSocketState.Open || Status == WebSocketStatus.Connecting)
        {
            return;
        }

        try
        {
            Status = WebSocketStatus.Connecting;

            // Step 1: Perform the OTA check and extract websocket url/token
            await PerformOtaCheckAsync(cancellationToken);

            _serverHelloTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _client = new ClientWebSocket();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Step 2: Set headers as per the protocol document, using OTA values
            _client.Options.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
            _client.Options.SetRequestHeader("Protocol-Version", "1");
            _client.Options.SetRequestHeader("Device-Id", _deviceId);
            _client.Options.SetRequestHeader("Client-Id", _clientId);

            Uri serverUri = new(_webSocketUrl);
            MessageReceived?.Invoke(this, $"Connecting to {serverUri} with headers...");
            await _client.ConnectAsync(serverUri, _cts.Token);
            MessageReceived?.Invoke(this, $"TCP Connection successful. State: {_client.State}. Starting receive loop...");

            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            // Step 3: Send HELLO message as per the protocol document
            MessageReceived?.Invoke(this, "Sending HELLO message...");
            if (!await SendHelloAsync(_cts.Token))
            {
                throw new InvalidOperationException("Failed to send HELLO message. Server may have closed the connection immediately.");
            }

            MessageReceived?.Invoke(this, "Waiting for server HELLO response...");
            var helloTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, helloTimeoutCts.Token);
            await _serverHelloTcs.Task.WaitAsync(linkedCts.Token);

            MessageReceived?.Invoke(this, "Handshake complete. Connection established.");
            Status = WebSocketStatus.Connected;
        }
        catch (Exception ex)
        {
            Status = WebSocketStatus.Error;
            MessageReceived?.Invoke(this, $"Connection failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                MessageReceived?.Invoke(this, $"Inner Exception: {ex.InnerException.Message}");
            }
            await DisconnectInternalAsync();
        }
    }

    public async Task DisconnectAsync()
    {
        Status = WebSocketStatus.Disconnecting;
        await DisconnectInternalAsync();
        Status = WebSocketStatus.Disconnected;
    }

    private async Task DisconnectInternalAsync()
    {
        if (_client != null)
        {
            if (_client.State == WebSocketState.Open || _client.State == WebSocketState.Connecting)
            {
                _cts?.Cancel();
                try
                {
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    // Ignore exceptions if the socket is already closed
                }
            }
            _client.Dispose();
            _client = null;
        }
        _cts?.Dispose();
        _cts = null;
    }

    public async Task<bool> SendTextAsync(string message, CancellationToken cancellationToken)
    {
        if (_client?.State != WebSocketState.Open)
        {
            var currentState = _client?.State.ToString() ?? "null";
            MessageReceived?.Invoke(this, $"Send failed. WebSocket client is not open. Current state: {currentState}");
            return false;
        }

        try
        {
            var messageBuffer = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            MessageReceived?.Invoke(this, $"Send exception: {ex.Message}");
            return false;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[8192]);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _client.ReceiveAsync(buffer, cancellationToken);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    MessageReceived?.Invoke(this, $"Server closed connection. Status: {result.CloseStatus}, Description: {result.CloseStatusDescription}");
                    break;
                }

                ms.Seek(0, SeekOrigin.Begin);
                var message = Encoding.UTF8.GetString(ms.ToArray());
                
                HandleIncomingMessage(message);
            }
            catch (OperationCanceledException)
            {
                break; // Disconnecting
            }
            catch (Exception ex)
            {
                Status = WebSocketStatus.Error;
                MessageReceived?.Invoke(this, $"Receive loop error: {ex.Message}");
                break;
            }
        }
        await DisconnectInternalAsync();
        if(Status != WebSocketStatus.Error)
            Status = WebSocketStatus.Disconnected;
    }

    private void HandleIncomingMessage(string message)
    {
        try
        {
            var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            // Capture SessionId if present, as per XiaoYiSharp logic.
            if (root.TryGetProperty("session_id", out var sessionIdElement))
            {
                _sessionId = sessionIdElement.GetString();
                MessageReceived?.Invoke(this, $"SessionId captured: {_sessionId}");
            }

            if (root.TryGetProperty("type", out var typeElement))
            {
                var messageType = typeElement.GetString();
                if (messageType == "hello")
                {
                    _serverHelloTcs.TrySetResult(true);
                    MessageReceived?.Invoke(this, "Server HELLO received.");
                    return;
                }
            }
        }
        catch (Newtonsoft.Json.JsonException)
        {
            // not a json message, or malformed
        }

        // For other messages, just raise the event
        MessageReceived?.Invoke(this, message);
    }

    private async Task PerformOtaCheckAsync(CancellationToken cancellationToken)
    {
        MessageReceived?.Invoke(this, $"Performing OTA check to {OtaUrl}...");
        try
        {
            _deviceId = DeviceInfoHelper.GetDeviceMacAddress().ToLower();
            _clientId = DeviceInfoHelper.GetClientId();
            var userAgent = "Unity-ID";
            var acceptLanguage = "zh-CN";
            var version = "1.6.1";
            var postData = new
            {
                version = 0,
                language = acceptLanguage,
                flash_size = 16777216,
                minimum_free_heap_size = 8457848,
                mac_address = _deviceId,
                uuid = _clientId,
                chip_model_name = "UnitySimulator",
                application = new
                {
                    name = "UnityApp",
                    version = version,
                    compile_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    idf_version = "6000.1.1f",
                    elf_sha256 = "1234567890abcdef1234567890abcdef1234567890abcdef"
                },
                partition_table = new[]
                {
                    new { label = "nvs", type = 1, subtype = 2, address = 36864, size = 16384 },
                    new { label = "otadata", type = 1, subtype = 0, address = 53248, size = 8192 },
                    new { label = "ota_0", type = 0, subtype = 16, address = 1048576, size = 6291456 }
                },
                ota = new { label = "ota_0" },
                board = new
                {
                    type = "UnityApp",
                    name = "UnityApp",
                    ssid = "UnityApp",
                    rssi = -55,
                    channel = 1,
                    ip = "192.168.1.1",
                    mac = _deviceId
                }
            };
            var jsonBody = JsonConvert.SerializeObject(postData);
            MessageReceived?.Invoke(this, $"OTA Request body: {jsonBody}");
            var request = new HttpRequestMessage(HttpMethod.Post, OtaUrl);
            request.Content = new System.Net.Http.StringContent(jsonBody, Encoding.UTF8, "application/json");
            request.Headers.Add("Device-Id", _deviceId);
            request.Headers.Add("Client-Id", _clientId);
            request.Headers.UserAgent.ParseAdd(userAgent);
            request.Headers.Add("Accept-Language", acceptLanguage);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                MessageReceived?.Invoke(this, $"OTA check successful. Response: {responseContent}");
                // 解析websocket.url和token
                try
                {
                    var json = Newtonsoft.Json.Linq.JObject.Parse(responseContent);
                    var wsObj = json["websocket"];
                    if (wsObj != null)
                    {
                        _webSocketUrl = wsObj["url"]?.ToString() ?? WebSocketUrl;
                        _accessToken = wsObj["token"]?.ToString() ?? AccessToken;
                        MessageReceived?.Invoke(this, $"Using WebSocketUrl: {_webSocketUrl}, token: {_accessToken}");
                    }
                }
                catch (Exception ex)
                {
                    MessageReceived?.Invoke(this, $"Failed to parse websocket url/token from OTA response: {ex.Message}");
                }
            }
            else
            {
                throw new HttpRequestException($"OTA check failed with status {response.StatusCode}. Response: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            MessageReceived?.Invoke(this, $"OTA check threw an exception: {ex.Message}");
            throw;
        }
    }

    private Task<bool> SendHelloAsync(CancellationToken cancellationToken)
    {
        var helloMessage = new
        {
            type = "hello",
            version = 1,
            transport = "websocket",
            audio_params = new
            {
                format = "opus",
                sample_rate = 16000,
                channels = 1,
                frame_duration = 60
            }
        };
        var message = JsonConvert.SerializeObject(helloMessage);
        MessageReceived?.Invoke(this, $"HELLO message: {message}");
        return SendTextAsync(message, cancellationToken);
    }
} 