﻿using System;
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
using System.Windows.Shapes;

namespace myEonWallet
{
    /// <summary>
    /// Interaction logic for MsgBoxYesNo.xaml
    /// </summary>
    public partial class MSMTXDialog : Window
    {
        public MSMTXDialog(string message, string msmTransaction)
        {
            InitializeComponent();
            YesButton.Visibility = Visibility.Visible;
            txtMessage.Text = message;
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

    }
}
