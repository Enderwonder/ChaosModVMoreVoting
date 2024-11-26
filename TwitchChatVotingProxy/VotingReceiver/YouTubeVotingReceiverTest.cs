using Google.Apis.YouTube.v3.Data;
using Serilog;
using Shared;
using System;
using System.Threading.Tasks;
using TwitchChatVotingProxy.ChaosPipe;
using TwitchLib.Client.Models;

namespace TwitchChatVotingProxy.VotingReceiver
{
    internal class YouTubeVotingReceiverTest : IVotingReceiver
    {
        public event EventHandler<OnMessageArgs>? OnMessage = null;

        private ILogger logger = Log.Logger.ForContext<YouTubeVotingReceiverTest>();
        private readonly ChaosPipeClient m_ChaosPipe;
        private System.Timers.Timer simulatedVoteTimer;
        private Random random = new Random();
        private string[] testUsers = new[] { "TestUser1", "TestUser2", "TestUser3" };

        public YouTubeVotingReceiverTest(OptionsFile config, ChaosPipeClient chaosPipe)
        {
            m_ChaosPipe = chaosPipe;
            simulatedVoteTimer = new System.Timers.Timer(2000); // Vote every 2 seconds
            simulatedVoteTimer.Elapsed += SimulateVote;
        }

        public Task<bool> Init()
        {
            logger.Information("Test YouTube receiver initialized");
            simulatedVoteTimer.Start();
            return Task.FromResult(true);
        }

        public Task GetMessages()
        {
            // Messages are simulated via timer
            return Task.CompletedTask;
        }

        public Task SendMessage(string message)
        {
            logger.Information($"Test message sent: {message}");
            return Task.CompletedTask;
        }

        private void SimulateVote(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var testUser = testUsers[random.Next(testUsers.Length)];
            var voteNumber = random.Next(1, 5).ToString(); // Simulate voting 1-4 to match TikTok test implementation

            var args = new OnMessageArgs
            {
                Message = voteNumber,
                ClientId = $"test_channel_{testUser.ToLower()}",
                Username = testUser
            };

            OnMessage?.Invoke(this, args);
            logger.Information($"Simulated vote: {testUser} voted {voteNumber}");
        }
    }
}
