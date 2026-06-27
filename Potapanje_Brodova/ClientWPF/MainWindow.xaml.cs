using Shared;
using System;
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
        private string imeProtivnika = "";

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
            TxtImeIgraca.Text = ime;
            this.Loaded += (s, e) => Task.Run(() => PoveziSeNaServer(ime));
            ProtivnickaTablaGrid.IsEnabled = false;
        }

        private void PoveziSeNaServer(string ime)
        {
            tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Connect("192.168.56.1", 5001);

            byte[] buffer = new byte[4096];
            List<byte> akumulator = new List<byte>();

            while (true)
            {
                int received = tcpSocket.Receive(buffer);
                if (received > 0)
                {
                    // Dodaj primljene bajtove u akumulator
                    for (int i = 0; i < received; i++) akumulator.Add(buffer[i]);

                    try
                    {
                        // Pokušaj deserializaciju kompletnog akumulatora
                        Poruka p = Poruka.DeserializujPoruku(akumulator.ToArray());
                        // Ako je uspelo, očisti akumulator za sledeću poruku
                        akumulator.Clear();

                        Dispatcher.Invoke(() =>
                        {
                            if (p.tipPoruke == TipPoruke.Obavestenje && !unosZavrsen)
                            {
                                UnosPodmornicaPanel.Visibility = Visibility.Visible;
                                GenerisiTablu();
                                OsveziInfoZaUnos();
                            }
                            InicijalizujProtivnickuTablu();
                            ObradiPorukuSaServera(p);
                        });
                    }
                    catch (System.Runtime.Serialization.SerializationException)
                    {
                        continue;
                    }
                }
            }
        }

        private void ObradiPorukuSaServera(Poruka p)
        {
            string protivnik = "";

            switch (p.tipPoruke)
                {
                    case TipPoruke.Obavestenje:
                        // tabla
                        if (p.poruka.Contains("0,0,0") || p.poruka.Contains("1,1,1"))
                        {
                            System.Diagnostics.Debug.WriteLine("Primljeni tehnički podaci: " + p.poruka);
                        }
                        else
                        {
                            StatusUnosa.Text = p.poruka;
                            if (p.poruka.Contains("Sacekajte"))
                            {
                                protivnik = p.poruka.Split(new string[] { " " }, System.StringSplitOptions.None)[0].Trim();
                                this.imeProtivnika = protivnik;
                                TxtImeProtivnika.Text = imeProtivnika;
                                ProtivnickaTablaGrid.IsEnabled = false;
                            }
                        }

                        break;

                    case TipPoruke.Napad:
                        protivnik = p.poruka.Split(new string[] { "->" }, System.StringSplitOptions.None)[1].Trim();
                        this.imeProtivnika = protivnik;
                        TxtImeProtivnika.Text = imeProtivnika;

                        StatusUnosa.Text = "VAŠ POTEZ: Izaberite polje na protivničkoj tabli.";
                        ProtivnickaTablaGrid.IsEnabled = true;

                        // Automatsko slanje imena protivnika serveru (uvek ce igrati dvoje na wpfu)
                        Poruka izbor = new Poruka(null, null, TipPoruke.Ostalo, imeProtivnika);
                        tcpSocket.Send(izbor.Serializuj());
                        break;

                    case TipPoruke.Pogodak:
                    case TipPoruke.Promasaj:
                        MessageBox.Show(p.poruka);
                        break;

                    case TipPoruke.Napadnut:
                        StatusUnosa.Text = "NAPADNUTI STE!";
                        break;

                    case TipPoruke.Kraj:
                        MessageBox.Show("Igra je završena!");
                        ProtivnickaTablaGrid.IsEnabled = false;
                        break;
                }
        }

        private void InicijalizujProtivnickuTablu()
        {
            TxtImeProtivnika.Visibility = Visibility.Visible;
            ProtivnickaTablaGrid.Visibility = Visibility.Visible;
            ProtivnickaTablaGrid.Children.Clear();

            for (int i = 1; i <= 100; i++)
            {
                Button btn = new Button { Tag = i, Background = Brushes.LightGray };
                
                btn.Click += (s, e) => {
                    int poz = (int)((Button)s).Tag;

                    if (CbSuperPotez.IsChecked == true)
                    {
                        int x = ((poz - 1) / 10) + 1;
                        int y = ((poz - 1) % 10) + 1;
                        List<int> polja = new List<int>();

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 1 && nx <= 10 && ny >= 1 && ny <= 10)
                                {
                                    polja.Add((nx - 1) * 10 + ny);
                                }
                            }
                        }
                        string superPoruka = "SUPER|" + string.Join(",", polja);
                        Poruka napad = new Poruka(null, null, TipPoruke.Napad, superPoruka);
                        tcpSocket.Send(napad.Serializuj());

                        CbSuperPotez.Visibility = Visibility.Hidden; // Sakrivamo jer se koristi samo jednom
                    }
                    else
                    {
                        // Običan napad
                        Poruka napad = new Poruka(null, null, TipPoruke.Napad, poz.ToString());
                        tcpSocket.Send(napad.Serializuj());
                    }
                };
                ProtivnickaTablaGrid.Children.Add(btn);
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
            if (trenutniBtn == null || trenutniBtn.Background == Brushes.Blue) return;

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
                if (poslednjeKliknutoDugme != null && poslednjeKliknutoDugme.Background != Brushes.Blue)
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
            StatusUnosa.Text= "Sacekajte protivnika da zavrsi unos.";
            BtnPotvrdi.Visibility = Visibility.Collapsed;
            UnosPodmornicaPanel.Visibility = Visibility.Collapsed;
        }

        private List<int> GenerisiPozicijePodmornice(int x, int y, int duzina, bool horizontalna, int velTable, HashSet<int> zauzetePozicije)
        {
            List<int> potencijalnePozicije = new List<int>();

            for (int i = 0; i < duzina; i++)
            {
                int nx = horizontalna ? x : x + i;
                int ny = horizontalna ? y + i : y;

                if (nx < 1 || nx > velTable || ny < 1 || ny > velTable) return null;

                int poz = (nx - 1) * velTable + ny;
                if (zauzetePozicije.Contains(poz)) return null;

                potencijalnePozicije.Add(poz);
            }

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