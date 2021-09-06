using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace CaranoParse.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Excel Documents|*.xlsx;*.xls";
            if (dialog.ShowDialog(this) == true)
            {
                var button = sender as Button;
                var row = Grid.GetRow(button);
                var grid = button.Parent as Grid;
                var childTextBox = grid.Children
                    .Cast<UIElement>()
                    .FirstOrDefault(child =>
                {
                    return Grid.GetColumn(child) == 1 && Grid.GetRow(child) == row;
                }) as TextBox;

                if (childTextBox != null)
                {
                    childTextBox.Text = dialog.FileName;
                }
            }
        }

        private async void PushButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Program.PushToServer(
                    baseApi: Block_Server.Text.EndsWith("/") ? Block_Server.Text : Block_Server.Text + "/",
                    id: Block_Id.Text,
                    password: Block_Password.Text,
                    updatePassword: Block_UpdatePassword.Text,
                    teachersFile: Block_TeachersFilePath.Text,
                    timespanFile: Block_TimespanFilePath.Text,
                    timetableFile: Block_TimetableFilePath.Text,
                    displayName: Block_DisplayName.Text,
                    name: Block_ClassId.Text
                    );
                MessageBox.Show(this, "Upload success. Reload your TimetableApp!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Failed to upload timetable", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ParseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ParseWindow();
            if (dialog.ShowDialog() == true)
            {
                Block_Id.Text = dialog.ID;
                Block_Password.Text = dialog.Password;
            }
        }
    }
}
