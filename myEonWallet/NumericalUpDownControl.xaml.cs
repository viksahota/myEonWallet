using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

namespace myEonWallet
{
    public partial class NumericalUpDownControl : UserControl
    {
        private int minvalue;
        private int maxvalue;
        private int value;
        private int increment;
        private string propertyToUpdate;

        public event EventHandler<string> ConfigChangeEvent;

        public NumericalUpDownControl()
        {
            InitializeComponent();
            //NUDTextBox.Text = value.ToString();

            //minvalue = 5;
            //maxvalue = 60;
            //value = 10;
            //increment = 1;
/*            this.Resources.Add("increment", increment);
            this.Resources.Add("maxvalue", maxvalue);
            this.Resources.Add("minvalue", minvalue);
            this.Resources.Add("startvalue", startvalue);*/
        }

        public void Setup(int CurrentValue, int MinValue, int MaxValue, int Increment, string PropertyToUpdate )
        {   
            minvalue = MinValue;
            maxvalue = MaxValue;
            increment = Increment;
            value = CurrentValue;

            propertyToUpdate = PropertyToUpdate;

            NUDTextBox.Text = Convert.ToString(value);
        }

        private void NUDButtonUP_Click(object sender, RoutedEventArgs e)
        {
            int number;
            if (NUDTextBox.Text != "") number = Convert.ToInt32(NUDTextBox.Text);
            else number = 0;
            if (number < maxvalue)
            {
                NUDTextBox.Text = Convert.ToString(number + increment);
            }
        }

        private void NUDButtonDown_Click(object sender, RoutedEventArgs e)
        {
            int number;
            if (NUDTextBox.Text != "") number = Convert.ToInt32(NUDTextBox.Text);
            else number = 0;
            if (number > minvalue)
            {
                NUDTextBox.Text = Convert.ToString(number - increment);
            }
        }

        private void NUDTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Up)
            {
                NUDButtonUP.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonUP, new object[] { true });
            }


            if (e.Key == Key.Down)
            {
                NUDButtonDown.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonDown, new object[] { true });
            }
        }

        private void NUDTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonUP, new object[] { false });

            if (e.Key == Key.Down)
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonDown, new object[] { false });
        }

        private void NUDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int number = 0;
            if (NUDTextBox.Text != "")
                if (!int.TryParse(NUDTextBox.Text, out number))
                {
                    NUDTextBox.Text = value.ToString();                   
                }
            if (number > maxvalue) NUDTextBox.Text = maxvalue.ToString();
            if (number < minvalue) NUDTextBox.Text = minvalue.ToString();
            NUDTextBox.SelectionStart = NUDTextBox.Text.Length;

            if (propertyToUpdate == "BalanceSyncPeriod")
            {
                Properties.Settings.Default.BalanceSyncPeriod = number;                
            }
            else if (propertyToUpdate == "TXHistoryMax")
            {
                Properties.Settings.Default.TransactionHistoryMax = number;
            }
            else if (propertyToUpdate == "RecentConfirmedMax")
            {
                //Properties.Settings.Default.RecentConfirmedMax = number;
            }

            Properties.Settings.Default.Save();
            ConfigChangeEvent?.Invoke(this, propertyToUpdate);


        }

    }
}
