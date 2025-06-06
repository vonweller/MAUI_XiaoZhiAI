using XiaoZhiAI_MAUI.Services;

namespace XiaoZhiAI_MAUI.Pages;

public partial class MainPage : ContentPage
{
	private readonly IWebSocketService _webSocketService;
	private CancellationTokenSource _cts;

	public MainPage()
	{
		InitializeComponent();
		// This is a simple way to get the service.
		// In a real MVVM app, you would use dependency injection in the view model.
		_webSocketService = IPlatformApplication.Current.Services.GetService<IWebSocketService>();
		_webSocketService.StatusChanged += OnWebSocketStatusChanged;
		_webSocketService.MessageReceived += OnWebSocketMessageReceived;
	}

	private void OnWebSocketMessageReceived(object sender, string message)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			MessageLogLabel.Text += $"\n- {message}";
		});
	}

	private void OnWebSocketStatusChanged(object sender, WebSocketStatus status)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			StatusLabel.Text = $"Status: {status}";
			ConnectButton.Text = status == WebSocketStatus.Connected ? "Disconnect" : "Connect";
			ConnectButton.IsEnabled = status == WebSocketStatus.Connected || status == WebSocketStatus.Disconnected || status == WebSocketStatus.Error;
		});
	}

	private async void OnConnectButtonClicked(object sender, EventArgs e)
	{
		ConnectButton.IsEnabled = false;

		if (_webSocketService.Status == Services.WebSocketStatus.Connected)
		{
			await _webSocketService.DisconnectAsync();
		}
		else
		{
			_cts = new CancellationTokenSource();
			await _webSocketService.ConnectAsync(_cts.Token);
		}
	}
}