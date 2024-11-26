using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Serilog;
using System;
using Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchChatVotingProxy.ChaosPipe;
using TwitchLib.Client.Models;

namespace TwitchChatVotingProxy.VotingReceiver
{
    internal class YouTubeVotingReceiver : IVotingReceiver
    {
        private static string KEY_CLIENT_ID = "YouTubeClientId";
        private static string KEY_CLIENT_SECRET = "YouTubeClientSecret";
        private static readonly int RETRY_TIMEOUT = 5000;

        public event EventHandler<OnMessageArgs>? OnMessage = null;

        private readonly ILogger logger = Log.Logger.ForContext<YouTubeVotingReceiver>();
        private string? nextPageToken = null;
        private readonly string clientId;
        private readonly string clientSecret;
        private YouTubeService? cacheYouTubeService = null;
        private LiveBroadcast? cacheCurrentLiveBroadcast = null;
        private readonly ChaosPipeClient m_ChaosPipe;
        private bool isRunning = false;
        private Task? messagePollingTask = null;

        public YouTubeVotingReceiver(OptionsFile config, ChaosPipeClient chaosPipe)
        {
            clientId = config.ReadValue(KEY_CLIENT_ID);
            clientSecret = config.ReadValue(KEY_CLIENT_SECRET);
            m_ChaosPipe = chaosPipe;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new ArgumentException("YouTube Client ID and Secret must be configured in voting.ini");
            }
        }

        public async Task<bool> Init()
        {
            try
            {
                logger.Information("Initializing YouTube voting...");
                
                // Get YouTube service (this will trigger auth if needed)
                cacheYouTubeService = await GetYouTubeService();
                logger.Information("YouTube authentication successful");

                // Start message polling
                isRunning = true;
                messagePollingTask = Task.Run(async () =>
                {
                    while (isRunning)
                    {
                        try
                        {
                            await GetMessages();
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Error polling messages: {0}", ex.Message);
                            m_ChaosPipe.SendErrorMessage($"YouTube voting error: {ex.Message}");
                            await Task.Delay(RETRY_TIMEOUT);
                        }
                    }
                });

                logger.Information("YouTube voting initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("YouTube voting initialization failed: {0}", ex.Message);
                m_ChaosPipe.SendErrorMessage($"YouTube voting initialization failed: {ex.Message}");
                return false;
            }
        }

        private async Task<LiveBroadcast?> GetCurrentLiveBroadcast()
        {
            if (cacheCurrentLiveBroadcast != null)
            {
                return cacheCurrentLiveBroadcast;
            }

            while (isRunning)
            {
                try
                {
                    var broadcasts = await GetAllBroadCasts();
                    cacheCurrentLiveBroadcast = FindLiveBroadCast(broadcasts);

                    if (cacheCurrentLiveBroadcast != null)
                    {
                        logger.Information("Live broadcast found: {0}", cacheCurrentLiveBroadcast.Snippet.Title);
                        return cacheCurrentLiveBroadcast;
                    }

                    logger.Debug("No live broadcast found, retrying in {0}ms", RETRY_TIMEOUT);
                    await Task.Delay(RETRY_TIMEOUT);
                }
                catch (Exception ex)
                {
                    logger.Error("Error finding live broadcast: {0}", ex.Message);
                    m_ChaosPipe.SendErrorMessage($"Error finding YouTube live stream: {ex.Message}");
                    await Task.Delay(RETRY_TIMEOUT);
                }
            }
            return null;
        }

        private async Task<YouTubeService> GetYouTubeService()
        {
            if (cacheYouTubeService != null)
            {
                return cacheYouTubeService;
            }

            var secret = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };
            var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secret,
                new[] { YouTubeService.Scope.Youtube },
                "user",
                System.Threading.CancellationToken.None
            );

            cacheYouTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credentials,
                ApplicationName = typeof(YouTubeVotingReceiver).ToString(),
            });

            return cacheYouTubeService;
        }

        public async Task GetMessages()
        {
            var youtubeService = await GetYouTubeService();
            var currentBroadcast = await GetCurrentLiveBroadcast();
            var liveChatId = currentBroadcast.Snippet.LiveChatId;

            var req = youtubeService.LiveChatMessages.List(liveChatId, "snippet,authorDetails");
            req.PageToken = nextPageToken;

            var res = await req.ExecuteAsync();
            nextPageToken = res.NextPageToken;

            foreach (var item in res.Items)
            {
                DispatchOnMessageWith(item);
            }

            var delay = TimeSpan.FromMilliseconds(res.PollingIntervalMillis ?? 200);
            await Task.Delay(delay);
        }

        public Task SendMessage(string message)
        {
            throw new Exception("sending messages currently not implemented");
        }

        private static LiveBroadcast? FindLiveBroadCast(IList<LiveBroadcast> broadcasts)
        {
            foreach (var broadcast in broadcasts)
            {
                if (broadcast.Status.LifeCycleStatus == "live")
                {
                    return broadcast;
                }
            }
            return null;
        }

        private void DispatchOnMessageWith(LiveChatMessage item)
        {
            try
            {
                var message = item.Snippet.DisplayMessage?.Trim();
                if (string.IsNullOrEmpty(message) || message.Length != 1 || !"12345678".Contains(message))
                {
                    return;
                }

                var args = new OnMessageArgs
                {
                    Message = message,
                    ClientId = item.AuthorDetails.ChannelId,
                    Username = item.AuthorDetails.DisplayName?.ToLower() ?? ""
                };
                OnMessage?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing YouTube message: {0}", ex.Message);
            }
        }

        private async Task<IList<LiveBroadcast>> GetAllBroadCasts()
        {
            var youtubeService = await GetYouTubeService();
            var req = youtubeService.LiveBroadcasts.List("snippet,status");
            req.Mine = true;
            var res = await req.ExecuteAsync();
            return res.Items;
        }

        public void Stop()
        {
            isRunning = false;
            messagePollingTask?.Wait();
            cacheCurrentLiveBroadcast = null;
            nextPageToken = null;
        }
    }
}
