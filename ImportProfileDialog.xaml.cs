using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MsfsPhysicsCamera
{
    public partial class ImportProfileDialog : Window
    {
        private readonly HashSet<string> existingNames;
        private CameraProfile? decodedProfile;
        private bool nameManuallyEdited;

        public CameraProfile? ImportedProfile { get; private set; }
        public bool ApplyAfterImport => ApplyAfterImportCheck.IsChecked == true;

        public ImportProfileDialog(IEnumerable<string> existingProfileNames)
        {
            InitializeComponent();
            existingNames = new HashSet<string>(existingProfileNames, StringComparer.OrdinalIgnoreCase);
            Loaded += (_, _) => ShareInputBox.Focus();
        }

        private void ShareInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CameraProfile.TryFromShareString(ShareInputBox.Text, out var profile, out var error) && profile != null)
            {
                decodedProfile = profile;
                if (!nameManuallyEdited || string.IsNullOrWhiteSpace(ProfileNameBox.Text))
                {
                    ProfileNameBox.Text = profile.Name;
                    nameManuallyEdited = false;
                }
                PreviewBox.Text = profile.Settings.ToDisplaySummary();
            }
            else
            {
                decodedProfile = null;
                PreviewBox.Text = string.Empty;
                StatusText.Text = string.IsNullOrWhiteSpace(ShareInputBox.Text) ? string.Empty : (error ?? "Invalid share string.");
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }

            UpdateImportState();
        }

        private void ProfileNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ProfileNameBox.IsKeyboardFocusWithin)
            {
                nameManuallyEdited = true;
            }
            UpdateImportState();
        }

        private void UpdateImportState()
        {
            if (decodedProfile == null)
            {
                AddButton.IsEnabled = false;
                return;
            }

            string name = ProfileNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                StatusText.Text = "Please enter a name for the imported profile.";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                AddButton.IsEnabled = false;
                return;
            }

            if (name.Equals(CameraProfile.DefaultProfileName, StringComparison.OrdinalIgnoreCase)
                || name.Equals(CameraProfile.CustomProfileName, StringComparison.OrdinalIgnoreCase))
            {
                StatusText.Text = $"'{name}' is a reserved name. Please choose another.";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                AddButton.IsEnabled = false;
                return;
            }

            if (existingNames.Contains(name))
            {
                StatusText.Text = $"Profile '{name}' already exists and will be overwritten.";
                StatusText.Foreground = System.Windows.Media.Brushes.DarkOrange;
            }
            else
            {
                StatusText.Text = "Valid profile string. Review the settings below and click Add Profile.";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
            }

            AddButton.IsEnabled = true;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (decodedProfile == null) return;

            string name = ProfileNameBox.Text.Trim();
            if (existingNames.Contains(name))
            {
                var result = System.Windows.MessageBox.Show(
                    this,
                    $"Overwrite existing profile '{name}'?",
                    "Confirm Overwrite",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            ImportedProfile = new CameraProfile
            {
                Name = name,
                Settings = decodedProfile.Settings.Clone()
            };
            DialogResult = true;
        }
    }
}
