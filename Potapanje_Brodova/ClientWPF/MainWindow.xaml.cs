using Shared;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;

namespace ClientWPF
{
    public partial class MainWindow : Window
    {
        private string _korisnickoIme;
        private Socket tcpSocket;

        public MainWindow(string ime)
        {
            InitializeComponent();
            this.Loaded += (s, e) => {
                Task.Run(() => PoveziSeNaServer(ime));
            };
        }

        private void PoveziSeNaServer(string ime)
        {
            tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Connect("192.168.56.1", 5001);

            byte[] buffer = new byte[4096];
            while (true)
            {
                int received = tcpSocket.Receive(buffer);
                if (received > 0)
                {
                    Poruka p = Poruka.DeserializujPoruku(buffer);

                    Dispatcher.Invoke(() => {
                        // Ovdje ispisuješ poruku u UI
                        // Npr: StatusTextBlock.Text = p.poruka;
                    });
                }
            }
        }
    }
}