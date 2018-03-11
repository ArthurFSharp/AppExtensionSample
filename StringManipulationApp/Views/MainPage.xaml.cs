using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace StringManipulationApp.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public MainPage()
        {
            InitializeComponent();

            DataContext = AppData.ExtensionManager.Extensions; // Populate the extension buttons using the collection of loaded extensions
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

        private void Display_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            string data = Data.Text;
            Result.Text = data;
        }

        private async void InvokeExtension(object sender, RoutedEventArgs e)
        {
            // The contract that I've specified for math extensions is to expect arguments labeled arg1, arg2.
            ValueSet message = new ValueSet();
            message.Add("data", Data.Text);

            // Get the extension to call based on the button pressed.
            // The extension is associated with the button's data context. 
            // The Items control builds each button from the collection of installed extensions
            Button btn = sender as Button;
            Extension ext = btn.DataContext as Extension;

            // Invoke the extension with the arguments in the ValueSet
            string result = await ext.Invoke(message);
            Result.Text = result;
        }
    }
}
