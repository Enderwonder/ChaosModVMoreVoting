using Serilog;
using Shared;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using TwitchChatVotingProxy.ChaosPipe;

namespace TwitchChatVotingProxy.VotingReceiver
{
    /// <summary>
    /// TikTok voting receiver using TikFinity WebSocket API
    /// </summary>
    internal class TikTokVotingReceiver : IVotingReceiver
    {
        public event EventHandler<OnMessageArgs>? OnMessage = null;
        public static readonly int RECONNECT_INTERVAL = 5000;

        private readonly ILogger logger = Log.Logger.ForContext<TikTokVotingReceiver>();
        private readonly ChaosPipeClient m_ChaosPipe;
        private ClientWebSocket? webSocket;
        private CancellationTokenSource? cancellationTokenSource;
        private readonly string tikfinityWebsocketUrl = "";  // Initialize with empty string
        private static string KEY_TIKFINITY_WEBSOCKET = "TikFinity_WebSocket";
        private bool isConnected = false;
        private bool shouldReconnect = true;

        public TikTokVotingReceiver(OptionsFile config, ChaosPipeClient chaosPipe)
        {
            tikfinityWebsocketUrl = config.ReadValue(KEY_TIKFINITY_WEBSOCKET, "ws://localhost:21213");
            m_ChaosPipe = chaosPipe;
        }
        public async Task<bool> Init()
        {
            try
            {
                logger.Information("Initializing TikTok voting with WebSocket URL: {0}", tikfinityWebsocketUrl);
                shouldReconnect = true;
                webSocket = new ClientWebSocket();
                cancellationTokenSource = new CancellationTokenSource();

                logger.Information("Attempting to connect to TikFinity WebSocket...");
                var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
                using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, timeoutToken);
        
                try {
                    await webSocket.ConnectAsync(new Uri(tikfinityWebsocketUrl), linkedToken.Token);
                    isConnected = true;
                    logger.Information("Connected to TikFinity WebSocket successful.");
                    //Start Listening for messages
                    _ = StartListening(cancellationTokenSource.Token);
                    return true;
                }
                catch (OperationCanceledException) {
                    logger.Error("Connection to TikFinity timed out after 10 seconds");
                    try {
                        m_ChaosPipe.SendErrorMessage("TikFinity connection timed out. Please check if TikFinity is running and configured correctly.");
                    }
                    catch (ObjectDisposedException) {
                        // Ignore pipe errors
                    }
                    return false;
                }
            }
            catch (UriFormatException ex)
            {
                logger.Error("Invalid TikFinity WebSocket URL: {0}. Error: {1}", tikfinityWebsocketUrl, ex.Message);
                try {
                    m_ChaosPipe.SendErrorMessage($"TikTok voting initialization failed: Invalid WebSocket URL '{tikfinityWebsocketUrl}'");
                }
                catch (ObjectDisposedException) {
                    // Ignore pipe errors
                }
                return false;
            }
            catch (WebSocketException ex)
            {
                logger.Error("Failed to connect to TikFinity WebSocket at {0}. Error: {1}", tikfinityWebsocketUrl, ex.Message);
                try {
                    m_ChaosPipe.SendErrorMessage($"TikTok voting initialization failed: Could not connect to TikFinity. Make sure TikFinity is running and listening on {tikfinityWebsocketUrl}");
                }
                catch (ObjectDisposedException) {
                    // Ignore pipe errors
                }
                isConnected = false;
                _ = TryReconnect();
                return false;
            }
            catch (Exception ex)
            {
                logger.Error("TikTok voting initialization failed: {0}", ex.Message);
                try {
                    m_ChaosPipe.SendErrorMessage($"TikTok voting initialization failed: {ex.Message}");
                }
                catch (ObjectDisposedException) {
                    // Ignore pipe errors
                }
                isConnected = false;
                _ = TryReconnect();
                return false;
            }
        }

        private async Task StartListening(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 4];
            try
            {
                logger.Information("Starting TikTok WebSocket listener. Initial state: {0}", webSocket?.State);
        
                while (!cancellationToken.IsCancellationRequested && webSocket?.State == WebSocketState.Open)
                {
                    try
                    {
                        logger.Debug("Waiting for next message...");
                        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        logger.Debug("Received message type: {0}, Count: {1}, EndOfMessage: {2}", 
                            result.MessageType, result.Count, result.EndOfMessage);
            
                        if(result.MessageType == WebSocketMessageType.Text)
                        {
                            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            ProcessMessage(message);
                        }
                        else if(result.MessageType == WebSocketMessageType.Close)
                        {
                            logger.Warning("Received WebSocket close message");
                            isConnected = false;
                            _ = TryReconnect();
                            break;
                        }
                        else
                        {
                            logger.Warning("Received unexpected message type: {0}", result.MessageType);
                        }
                    }
                    catch (WebSocketException ex)
                    {
                        logger.Error($"WebSocket error: {ex.Message}");
                        isConnected = false;
                        _ = TryReconnect();
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Error receiving message: {ex.Message}");
                        continue;
                    }
                }
        
                if (webSocket?.State != WebSocketState.Open)
                {
                    logger.Warning("WebSocket state changed to: {0}", webSocket?.State);
                    isConnected = false;
                    _ = TryReconnect();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Listening error: {0}. WebSocket state: {1}", ex.Message, webSocket?.State);
                isConnected = false;
                _ = TryReconnect();
            }
        }

        private async Task TryReconnect()
        {
            int attempts = 0;
            while (shouldReconnect && !isConnected)
            {
                try
                {
                    attempts++;
                    logger.Information("Attempting to reconnect to TikFinity WebSocket (attempt {0})...", attempts);
                    webSocket = new ClientWebSocket();
                    await webSocket.ConnectAsync(new Uri(tikfinityWebsocketUrl), cancellationTokenSource.Token);
                    isConnected = true;
                    _ = StartListening(cancellationTokenSource.Token);
                    logger.Information("Reconnected to TikFinity WebSocket successful.");
                }
                catch (Exception ex)
                {
                    logger.Error("Reconnection attempt {0} failed: {1}", attempts, ex.Message);
                    if (attempts % 5 == 0) // Only show message every 5 attempts to avoid spam
                    {
                        try {
                            m_ChaosPipe.SendErrorMessage($"Still trying to reconnect to TikFinity (attempt {attempts}). Make sure TikFinity is running.");
                        }
                        catch (ObjectDisposedException) {
                            // Ignore pipe errors
                        }
                    }
                    await Task.Delay(RECONNECT_INTERVAL);
                }
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                logger.Debug("Received TikTok message: {0}", message);
                var msg = JsonSerializer.Deserialize<TikTokMessage>(message);
                logger.Debug("Message event type: {0}", msg?.@event);
                
                if (msg?.@event == "chat" && msg.data?.comment != null && msg.data?.nickname != null && msg.data?.uniqueId != null)
                {
                    var vote = msg.data.comment.Trim();
                    logger.Debug("Potential vote from {0} ({1}): {2}", msg.data.nickname, msg.data.uniqueId, vote);
                    
                    if (vote.Length == 1 && "12345678".Contains(vote))
                    {
                        var args = new OnMessageArgs
                        {
                            Message = vote,
                            ClientId = msg.data.uniqueId,
                            Username = msg.data.uniqueId.ToLower()  // Using uniqueId as username since it's the TikTok handle
                        };
                        OnMessage?.Invoke(this, args);
                        logger.Information("Valid vote received from {0}: {1}", msg.data.nickname, vote);
                    }
                    else
                    {
                        logger.Debug("Invalid vote format: {0}", vote);
                    }
                }
                else
                {
                    logger.Debug("Message did not meet voting criteria. Event: {0}, Has Comment: {1}, Has Nickname: {2}, Has UniqueId: {3}",
                        msg?.@event,
                        msg?.data?.comment != null,
                        msg?.data?.nickname != null,
                        msg?.data?.uniqueId != null);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing message: {0}. Raw message: {1}", ex.Message, message);
            }
        }

        public Task SendMessage(string message)
        {
            throw new Exception("sending messages currently not implemented");
        }

        public void Stop()
        {
            shouldReconnect = false;
            cancellationTokenSource?.Cancel();
            webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait();
            isConnected = false;
        }

        private class TikTokMessage
        {
            public string? @event { get; set; }  // Changed from eventType to event
            public MessageData? data { get; set; }

            public class MessageData
            {
                public string? comment { get; set; }
                public string? uniqueId { get; set; }  // This is the username
                public string? nickname { get; set; }
            }
        }
    }
}
