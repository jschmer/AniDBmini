
#region Using Statements

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

#endregion Using Statements

namespace AniDBmini
{
    public partial class OptionsWindow : Window
    {

        #region Fields

        private bool isInitialized, dlgResult;

        #endregion Fields

        #region Constructor

        public OptionsWindow()
        {
            InitializeComponent();
            LoadOptions();

            applyButton.IsEnabled = false;
            isInitialized = true;
        }

        #endregion Constructor

        #region Private Methods

        private void LoadOptions()
        {
            adbmUsernameTextBox.Text = ConfigFile.Read("username").ToString();
            adbmPasswordPasswordBox.Password = ConfigFile.Read("password").ToString();
            adbmLocalPortTextBox.Text = ConfigFile.Read("localPort").ToString();
            adbmAutoLoginCheckBox.IsChecked = ConfigFile.Read("autoLogin").ToBoolean();
            adbmRememberUserCheckBox.IsChecked = ConfigFile.Read("rememberUser").ToBoolean();
        }

        private void SaveOptions()
        {
            ConfigFile.Write("username", adbmUsernameTextBox.Text);
            ConfigFile.Write("password", adbmPasswordPasswordBox.Password);
            ConfigFile.Write("localPort", adbmLocalPortTextBox.Text);
            ConfigFile.Write("autoLogin", adbmAutoLoginCheckBox.IsChecked.ToString());
            ConfigFile.Write("rememberUser", adbmRememberUserCheckBox.IsChecked.ToString());

            dlgResult = true;
            applyButton.IsEnabled = false;
            okButton.Focus();
        }

        #endregion Private Methods

        #region Events

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (isInitialized)
            {
                int oldIndex = int.Parse(((TreeViewItem)e.OldValue).Tag.ToString());
                OptionGirds.Children[oldIndex].Visibility = System.Windows.Visibility.Collapsed;

                int selectedIndex = int.Parse(((TreeViewItem)e.NewValue).Tag.ToString());
                OptionGirds.Children[selectedIndex].Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void enableApplyButton(object sender, EventArgs e)
        {
            if (isInitialized)
                applyButton.IsEnabled = true;
        }

        private void OKOnClick(object sender, RoutedEventArgs e)
        {
            if (applyButton.IsEnabled)
                SaveOptions();

            this.DialogResult = dlgResult;
        }

        private void CancelOnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ApplyOnClick(object sender, RoutedEventArgs e)
        {
            SaveOptions();            
        }

        #endregion Events

    }
}
