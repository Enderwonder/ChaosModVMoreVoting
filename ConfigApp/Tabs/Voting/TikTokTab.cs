using System.Windows;
using System.Windows.Controls;

namespace ConfigApp.Tabs.Voting
{
    public class TikTokTab : Tab
    {
        private CheckBox? m_EnableTikTokVoting = null;

        private TextBox? m_tikfinitywebsocket = null;

        private void SetElementsEnabled(bool state)
        {
            if (m_tikfinitywebsocket is not null)
            {
                m_tikfinitywebsocket.IsEnabled = state;
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
            m_EnableTikTokVoting = new CheckBox()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Content = "Enable TikTok Voting"
            };
            m_EnableTikTokVoting.Click += (sender, eventArgs) =>
            {
                SetElementsEnabled(m_EnableTikTokVoting.IsChecked.GetValueOrDefault());
            };
            PushRowElement(m_EnableTikTokVoting);
            PopRow();

            PushRowSpacedPair("TikFinity WebSocket", m_tikfinitywebsocket = new TextBox()
            {
                Width = 120f,
                Height = 20f
            });

            SetElementsEnabled(false);
        }

        public override void OnLoadValues()
        {
            if (m_EnableTikTokVoting is not null)
            {
                m_EnableTikTokVoting.IsChecked = OptionsManager.TwitchFile.ReadValueBool("EnableVotingTikTok", false);
                SetElementsEnabled(m_EnableTikTokVoting.IsChecked.GetValueOrDefault());
            }

            if (m_tikfinitywebsocket is not null)
            {
                m_tikfinitywebsocket.Text = OptionsManager.TwitchFile.ReadValue("TikFinity_WebSocket");
            }
        }

        public override void OnSaveValues()
        {
            OptionsManager.TwitchFile.WriteValue("EnableVotingTikTok", m_EnableTikTokVoting?.IsChecked);

            OptionsManager.TwitchFile.WriteValue("TikFinity_WebSocket", m_tikfinitywebsocket?.Text);
        }
    }
}