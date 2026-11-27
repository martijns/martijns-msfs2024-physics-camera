using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MsfsPhysicsCamera
{
    public partial class SaveProfileDialog : Window
    {
        private readonly HashSet<string> existingNames;

        public string ProfileName { get; private set; } = string.Empty;

        public SaveProfileDialog(IEnumerable<string> existingProfileNames, string? suggestedName = null)
        {
            InitializeComponent();
            existingNames = new HashSet<string>(existingProfileNames, StringComparer.OrdinalIgnoreCase);

            foreach (var name in existingProfileNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                ProfileNameInput.Items.Add(name);
            }

            if (!string.IsNullOrWhiteSpace(suggestedName)
                && !suggestedName.Equals(CameraProfile.DefaultProfileName, StringComparison.OrdinalIgnoreCase)
                && !suggestedName.Equals(CameraProfile.CustomProfileName, StringComparison.OrdinalIgnoreCase))
            {
                ProfileNameInput.Text = suggestedName;
            }

            Loaded += (_, _) =>
            {
                ProfileNameInput.Focus();
                UpdateValidation();
            };
        }

        private void ProfileNameInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateValidation();
        }

        private void UpdateValidation()
        {
            string name = ProfileNameInput.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ValidationText.Text = "Enter a profile name.";
                ValidationText.Foreground = System.Windows.Media.Brushes.Gray;
                SaveButton.IsEnabled = false;
                return;
            }

            if (name.Equals(CameraProfile.DefaultProfileName, StringComparison.OrdinalIgnoreCase)
                || name.Equals(CameraProfile.CustomProfileName, StringComparison.OrdinalIgnoreCase))
            {
                ValidationText.Text = $"'{name}' is a reserved name. Please choose another.";
                ValidationText.Foreground = System.Windows.Media.Brushes.Red;
                SaveButton.IsEnabled = false;
                return;
            }

            if (existingNames.Contains(name))
            {
                ValidationText.Text = $"A profile named '{name}' already exists and will be overwritten.";
                ValidationText.Foreground = System.Windows.Media.Brushes.DarkOrange;
            }
            else
            {
                ValidationText.Text = string.Empty;
            }

            SaveButton.IsEnabled = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = ProfileNameInput.Text.Trim();
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

            ProfileName = name;
            DialogResult = true;
        }
    }
}
