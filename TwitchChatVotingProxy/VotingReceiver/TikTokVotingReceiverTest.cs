using Serilog;
using Shared;
using System;
using System.Threading.Tasks;
using TwitchChatVotingProxy.ChaosPipe;

namespace TwitchChatVotingProxy.VotingReceiver
{
    internal class TikTokVotingReceiverTest : IVotingReceiver
    {
        public event EventHandler<OnMessageArgs>? OnMessage = null;

        private readonly ILogger logger = Log.Logger.ForContext<TikTokVotingReceiverTest>();
        private readonly ChaosPipeClient m_ChaosPipe;
        private System.Timers.Timer simulatedVoteTimer;
        private Random random = new Random();
        private string[] testUsers = new[] { "TikTokUser1", "TikTokUser2", "TikTokUser3" };

        public TikTokVotingReceiverTest(OptionsFile config, ChaosPipeClient chaosPipe)
        {
            m_ChaosPipe = chaosPipe;
            simulatedVoteTimer = new System.Timers.Timer(2000); // Vote every 2 seconds
            simulatedVoteTimer.Elapsed += SimulateVote;
        }

        public Task<bool> Init()
        {
            logger.Information("Test TikTok receiver initialized");
            simulatedVoteTimer.Start();
            return Task.FromResult(true);
        }

        private void SimulateVote(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var testUser = testUsers[random.Next(testUsers.Length)];
            var voteNumber = random.Next(1, 5).ToString(); // Simulate voting 1-4

            OnMessage?.Invoke(this, new OnMessageArgs
            {
                Username = testUser,
                Message = voteNumber
            });

            logger.Information($"Simulated vote from {testUser}: {voteNumber}");
        }

        public void Stop()
        {
            simulatedVoteTimer.Stop();
        }

        public Task SendMessage(string message)
        {
            logger.Information($"Test receiver would send message: {message}");
            return Task.CompletedTask;
        }
    }
}
