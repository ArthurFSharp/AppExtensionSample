using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace StringManipulationApp.Views
{
    public sealed partial class ExtensionsViewPage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<Extension> Items = null;

        public ExtensionsViewPage()
        {
            InitializeComponent();

            Items = AppData.ExtensionManager.Extensions; // The AppData object holds global state such as the collection of extensions
            this.DataContext = Items;
        }

        /// <summary>
        /// Enable an extension
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Extension ext = cb.DataContext as Extension;
            if (!ext.Enabled)
            {
                await ext.Enable();
            }
        }

        /// <summary>
        /// Disable an extension
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Extension ext = cb.DataContext as Extension;
            if (ext.Enabled)
            {
                ext.Disable();
            }
        }

        /// <summary>
        /// Remove an extension from the system if the user OKs it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // remove the package
            Button btn = sender as Button;
            Extension ext = btn.DataContext as Extension;
            AppData.ExtensionManager.RemoveExtension(ext);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
