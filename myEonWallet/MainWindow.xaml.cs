using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;

namespace myEonWallet
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


        //debug message handler
        private void DebugMsg(string line)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                if (debugTB.Text.Length > 10000)
                {
                    debugTB.Text = debugTB.Text.Remove(0, 500);
                }

                debugTB.Text += (DateTime.Now.ToLongTimeString() + ": " + line + "\r\n");
                debugTB.Focus();
                debugTB.CaretIndex = debugTB.Text.Length;
                debugTB.ScrollToEnd();
            }));


        }

        //redirect errors to debug for now
        private void ErrorMsg(string line)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                DebugMsg(" - ERROR - " + line + "\r\n");
            }));
        }
    }
}
