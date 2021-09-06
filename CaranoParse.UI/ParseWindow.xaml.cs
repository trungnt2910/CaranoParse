using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
#if NETFRAMEWORK
using Mono.Web;
#endif

namespace CaranoParse.UI
{
    /// <summary>
    /// Interaction logic for ParseWindow.xaml
    /// </summary>
    public partial class ParseWindow : Window
    {
        public string ID { get; private set; }
        public string Password { get; private set; }

        public ParseWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Box_Url_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var uri = new Uri(Box_Url.Text);
                var col = HttpUtility.ParseQueryString(uri.Query);

                ID = col.Get("id");
                Password = col.Get("password");

                Block_Id.Text = ID;
                Block_Password.Text = Password;

                OkButton.IsEnabled = true;
            }
            catch
            {
                OkButton.IsEnabled = false;
            }
        }
    }
}
