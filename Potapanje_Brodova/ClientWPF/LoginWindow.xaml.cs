using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ClientWPF
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void BtnPrijava_Click(object sender, RoutedEventArgs e)
        {
            string ime = ImeTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(ime))
            {
                MessageBox.Show("Molimo unesite korisničko ime.", "Greška", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnPrijava.IsEnabled = false;
            BtnPrijava.Content = "Učitavanje...";

            Task.Run(() =>
            {
                UradiPrijavu(ime);
            });
        }

        private void UradiPrijavu(string ime)
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                IPEndPoint destination = new IPEndPoint(IPAddress.Parse("192.168.56.1"), 60002);
                byte[] buffer = Encoding.UTF8.GetBytes("PRIJAVA" + ime);

                try
                {
                    socket.SendTo(buffer, destination);

                    Dispatcher.Invoke(() => {
                        StatusTextBlock.Visibility = Visibility.Visible;
                        BtnPrijava.Content = "Prijava poslata...";
                    });

                    byte[] buffer2 = new byte[200];
                    EndPoint posiljaocEP = new IPEndPoint(IPAddress.Parse("192.168.56.1"), 0);
                    string odgovor = "";

                    do
                    {
                        int primljena = socket.ReceiveFrom(buffer2, ref posiljaocEP);
                        odgovor = Encoding.UTF8.GetString(buffer2, 0, primljena).TrimEnd(' ');

                    } while (!odgovor.Contains("SPREMAN") && !odgovor.Contains("Neuspesno"));

                    Dispatcher.Invoke(() =>
                    {
                        if (odgovor.Contains("SPREMAN"))
                        {
                            MainWindow mw = new MainWindow(ime);
                            mw.Show();
                            this.Close();
                        }
                        else
                        {
                            StatusTextBlock.Visibility = Visibility.Collapsed;
                            MessageBox.Show("Server javlja: " + odgovor);
                            BtnPrijava.IsEnabled = true;
                            BtnPrijava.Content = "PRIJAVI SE";
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Greška: " + ex.Message, "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                        BtnPrijava.IsEnabled = true;
                        BtnPrijava.Content = "PRIJAVI SE";
                    });
                }
            }
        }

    }
}