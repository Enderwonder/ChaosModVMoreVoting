using System.Windows;
using System.Windows.Controls;

namespace ConfigApp.Tabs.Voting
{
    public class YouTubeTab : Tab
    {
        private CheckBox? m_EnableYouTubeVoting = null;

        private TextBox? m_ClientId = null;
        private PasswordBox? m_ClientSecret = null;

        private void SetElementsEnabled(bool state)
        {
            if (m_ClientId is not null)
            {
                m_ClientId.IsEnabled = state;
            }

            if (m_ClientSecret is not null)
            {
                m_ClientSecret.IsEnabled = state;
            }
        }

        protected override void InitContent()
        {
            PushNewColumn(new GridLength(340f));
            PushNewColumn(new GridLength(10f));
            PushNewColumn(new GridLength(150f));
            PushNewColumn(new GridLength(250f));
            PushNewColumn(new GridLength(10f));
            PushNewColumn(new GridLength());

            PushRowEmpty();
            PushRowEmpty();
            PushRowEmpty();
            m_EnableYouTubeVoting = new CheckBox()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Content = "Enable YouTube Voting"
            };
            m_EnableYouTubeVoting.Click += (sender, eventArgs) =>
            {
                SetElementsEnabled(m_EnableYouTubeVoting.IsChecked.GetValueOrDefault());
            };
            PushRowElement(m_EnableYouTubeVoting);
            PopRow();

            PushRowSpacedPair("Client ID", m_ClientId = new TextBox()
            {
                Width = 120f,
                Height = 20f
            });
            PopRow();

            PushRowSpacedPair("Client Secret", m_ClientSecret = new PasswordBox()
            {
                Width = 120f,
                Height = 20f
            });

            SetElementsEnabled(false);
        }

        public override void OnLoadValues()
        {
            if (m_EnableYouTubeVoting is not null)
            {
                m_EnableYouTubeVoting.IsChecked = OptionsManager.TwitchFile.ReadValueBool("EnableVotingYouTube", false);
                SetElementsEnabled(m_EnableYouTubeVoting.IsChecked.GetValueOrDefault());
            }

            if (m_ClientId is not null)
            {
                m_ClientId.Text = OptionsManager.TwitchFile.ReadValue("YouTubeClientId");
            }

            if (m_ClientSecret is not null)
            {
                m_ClientSecret.Password = OptionsManager.TwitchFile.ReadValue("YouTubeClientSecret");
            }
        }

        public override void OnSaveValues()
        {
            OptionsManager.TwitchFile.WriteValue("EnableVotingYouTube", m_EnableYouTubeVoting?.IsChecked);

            OptionsManager.TwitchFile.WriteValue("YouTubeClientId", m_ClientId?.Text);

            OptionsManager.TwitchFile.WriteValue("YouTubeClientSecret", m_ClientSecret?.Password);
        }
    }
}
