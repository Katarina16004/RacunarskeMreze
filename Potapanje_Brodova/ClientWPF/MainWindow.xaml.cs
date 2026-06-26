using Shared;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClientWPF
{
    public partial class MainWindow : Window
    {
        private Socket tcpSocket;
        private List<Podmornica> mojePodmornice = new List<Podmornica>();
        private HashSet<int> zauzetePozicije = new HashSet<int>();
        private Button poslednjeKliknutoDugme = null;
        private string mojeIme;

        private int[][] zadatak = new int[][] { new int[] { 4, 1 }, new int[] { 3, 2 }, new int[] { 2, 3 }, new int[] { 1, 4 } };
        private int trenutnaGrupa = 0;
        private int postavljenihUGrupi = 0;

        private int tempX, tempY;
        private bool cekamSmer = false;
        private int ukupnoPodmornica = 10;
        private bool unosZavrsen = false;

        public MainWindow(string ime)
        {
            InitializeComponent();
            this.mojeIme = ime;
            this.Loaded += (s, e) => Task.Run(() => PoveziSeNaServer(ime));
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
                        if (p.tipPoruke == TipPoruke.Obavestenje)
                        {
                            UnosPodmornicaPanel.Visibility = Visibility.Visible;
                            GenerisiTablu();
                            OsveziInfoZaUnos();
                        }
                    });
                }
            }
        }

        private void GenerisiTablu()
        {
            MojaTablaGrid.Children.Clear();
            for (int i = 1; i <= 100; i++)
            {
                Button btn = new Button { Tag = i, Background = Brushes.White };
                btn.Click += Polje_Click;
                MojaTablaGrid.Children.Add(btn);
            }
        }

        private void Polje_Click(object sender, RoutedEventArgs e)
        {
            if (unosZavrsen) return;
            Button trenutniBtn = sender as Button;
            if (trenutniBtn == null || trenutniBtn.Background==Brushes.Blue) return;

            if (poslednjeKliknutoDugme != null)
            {
                poslednjeKliknutoDugme.Background = Brushes.White;
            }

            poslednjeKliknutoDugme = trenutniBtn;
            poslednjeKliknutoDugme.Background = Brushes.Yellow;

            int poz = (int)((Button)sender).Tag;
            tempX = ((poz - 1) / 10) + 1;
            tempY = ((poz - 1) % 10) + 1;

            if (trenutnaGrupa >= zadatak.Length) return;
            int duzina = zadatak[trenutnaGrupa][1];

            if (duzina == 1) ProveriIPostavi(true);
            else { cekamSmer = true; StatusUnosa.Text = "Izaberite smer (H ili V)"; }
        }

        private void BtnSmer_Click(object sender, RoutedEventArgs e)
        {
            if (unosZavrsen || !cekamSmer) return;
            bool horizontalna = ((Button)sender).Tag.ToString() == "H";
            ProveriIPostavi(horizontalna);
        }

        private void ProveriIPostavi(bool horizontalna)
        {
            if (unosZavrsen || trenutnaGrupa >= zadatak.Length) return;

            int duzina = zadatak[trenutnaGrupa][1];
            var pozicije = GenerisiPozicijePodmornice(tempX, tempY, duzina, horizontalna, 10, zauzetePozicije);

            if (pozicije != null)
            {
                mojePodmornice.Add(new Podmornica((TipPodmornice)duzina, pozicije, horizontalna));
                foreach (var p in pozicije) { zauzetePozicije.Add(p); ObojiPolje(p); }
                if (poslednjeKliknutoDugme != null)
                {
                    poslednjeKliknutoDugme.Background = Brushes.Blue; 
                    poslednjeKliknutoDugme = null;
                }

                postavljenihUGrupi++;
                if (postavljenihUGrupi >= zadatak[trenutnaGrupa][0])
                {
                    trenutnaGrupa++;
                    postavljenihUGrupi = 0;
                }

                if (mojePodmornice.Count >= ukupnoPodmornica) ZavrsiUnos();
                else OsveziInfoZaUnos();

                cekamSmer = false;
            }
            else 
            {

                if (poslednjeKliknutoDugme != null && poslednjeKliknutoDugme.Background!=Brushes.Blue)
                {
                    poslednjeKliknutoDugme.Background = Brushes.White;
                }
                MessageBox.Show("Nevažeća pozicija!"); cekamSmer = false; 
            }
        }

        private void ZavrsiUnos()
        {
            unosZavrsen = true;
            UnosPodmornicaPanel.Visibility = Visibility.Collapsed;

            foreach (var child in MojaTablaGrid.Children)
            {
                if (child is Button btn) btn.Click -= Polje_Click;
            }

            StatusUnosa.Text = "Sve postavljeno! Pritisnite Potvrdi.";
            BtnPotvrdi.IsEnabled = true;
        }

        private void OsveziInfoZaUnos()
        {
            if (trenutnaGrupa < zadatak.Length)
            {
                StatusUnosa.Text = $"Postavljeno: {mojePodmornice.Count}/{ukupnoPodmornica}. " +
                                   $"Sledeća: {zadatak[trenutnaGrupa][1]}x1";
            }
        }

        private void ObojiPolje(int poz)
        {
            var btn = MojaTablaGrid.Children[poz - 1] as Button;
            if (btn != null) btn.Background = Brushes.Blue;
        }

        private void BtnPotvrdi_Click(object sender, RoutedEventArgs e)
        {
            string podaci = string.Join(";", mojePodmornice.Select(pp =>
                $"{(int)pp.Tip},{(pp.Horizontalna ? "H" : "V")},{string.Join(",", pp.Pozicije)}"));

            Poruka p = new Poruka(null, null, TipPoruke.PozicijeBrodova, $"{mojeIme}|{podaci}");
            tcpSocket.Send(p.Serializuj());

            MessageBox.Show("Podmornice poslate serveru!");
            BtnPotvrdi.Visibility= Visibility.Collapsed;
            UnosPodmornicaPanel.Visibility = Visibility.Collapsed;
        }

        private List<int> GenerisiPozicijePodmornice(int x, int y, int duzina, bool horizontalna, int velTable, HashSet<int> zauzetePozicije)
        {
            List<int> potencijalnePozicije = new List<int>();

            for (int i = 0; i < duzina; i++)
            {
                int nx = horizontalna ? x : x + i;
                int ny = horizontalna ? y + i : y;

                if (nx < 1 || nx > velTable || ny < 1 || ny > velTable) return null; // Izvan table

                int poz = (nx - 1) * velTable + ny;

                // PROVERA PREKLAPANJA: Ako je ovo polje već zauzeto, ne može se tu postaviti brod
                if (zauzetePozicije.Contains(poz)) return null;

                potencijalnePozicije.Add(poz);
            }

            // da se ne dodiruju
            foreach (int p in potencijalnePozicije)
            {
                int px = ((p - 1) / velTable) + 1;
                int py = ((p - 1) % velTable) + 1;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int susedX = px + dx;
                        int susedY = py + dy;

                        if (susedX >= 1 && susedX <= velTable && susedY >= 1 && susedY <= velTable)
                        {
                            int susedPoz = (susedX - 1) * velTable + susedY;

                            // Ako je sused zauzet, a nije deo podmornice koju upravo postavljamo -> NE MOŽE
                            if (zauzetePozicije.Contains(susedPoz) && !potencijalnePozicije.Contains(susedPoz))
                            {
                                return null;
                            }
                        }
                    }
                }
            }

            return potencijalnePozicije;
        }
    }
}