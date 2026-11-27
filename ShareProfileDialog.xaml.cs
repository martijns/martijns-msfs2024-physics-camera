using System.Windows;

namespace MsfsPhysicsCamera
{
    public partial class ShareProfileDialog : Window
    {
        public ShareProfileDialog(string profileName, string shareString)
        {
            InitializeComponent();
            HeaderText.Text = $"Share this code for '{profileName}' with other users:";
            ShareStringBox.Text = shareString;

            Loaded += (_, _) =>
            {
                ShareStringBox.Focus();
                ShareStringBox.SelectAll();
            };
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText(ShareStringBox.Text);
            CopiedStatusText.Visibility = Visibility.Visible;
        }
    }
}
