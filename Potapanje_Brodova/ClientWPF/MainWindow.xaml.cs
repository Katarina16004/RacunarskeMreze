using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ClientWPF
{
    public partial class MainWindow : Window
    {
        private Socket tcpSocket;
        private int poslednjiNapadnutiIndeks = -1;
        private List<Podmornica> mojePodmornice = new List<Podmornica>();
        private HashSet<int> zauzetePozicije = new HashSet<int>();
        private Button poslednjeKliknutoDugme = null;
        private List<int> poslednjeGadjanihPolja = new List<int>();
        private string mojeIme;
        private string imeProtivnika = "";
        private int brojPromasaja = 0;
        private int brojBotPoteza = 3;
        private int botPolje;

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
                if (tcpSocket == null) break;
                int received = tcpSocket.Receive(buffer);
                if (received > 0)
                {
                    // dodajemo primljene bajtove u akumulator
                    for (int i = 0; i < received; i++) akumulator.Add(buffer[i]);

                    try
                    {
                        // deserializacija kompletnog akumulatora
                        Poruka p = Poruka.DeserializujPoruku(akumulator.ToArray());

                        akumulator.Clear();

                        Dispatcher.Invoke(() =>
                        {
                            if (p.tipPoruke == TipPoruke.Obavestenje && !unosZavrsen)
                            {
                                UnosPodmornicaPanel.Visibility = Visibility.Visible;
                                GenerisiTablu();
                                OsveziInfoZaUnos();

                                InicijalizujProtivnickuTablu();
                            }
                            ObradiPorukuSaServera(p);
                        });
                    }
                    catch (Exception ex)
                    {
                        if (ex is System.Security.Cryptography.CryptographicException ||
                            ex is System.Runtime.Serialization.SerializationException)
                        {
                            // Ovo znači da podaci nisu kompletni ili su pogrešni.
                            // Nastavljamo da akumuliramo dok ne dobijemo sve bajtove.
                            continue;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Kritična greška: " + ex.Message);
                            break;
                        }
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

                    if (p.poruka.Contains("BOT"))
                    {
                        StatusUnosa.Text = $"Vreme isteklo! {p.poruka}";
                        brojBotPoteza--;
                        TxtBrojBotPoteza.Text = $"Preostali broj BOT poteza: {brojBotPoteza}";
                        string[] delovi = p.poruka.Split(new string[] { "BOT je odigrao polje:" }, StringSplitOptions.None);
                        if (delovi.Length > 1)
                        {
                            string potencijalniBroj = delovi[1].Trim();

                            string broj = new string(potencijalniBroj.TakeWhile(char.IsDigit).ToArray());

                            if (!string.IsNullOrEmpty(broj))
                            {
                                botPolje = int.Parse(broj);
                                
                            }
                        }

                    }

                    if (p.poruka.Contains("Cekamo nove igrace"))
                    {
                        StatusUnosa.Text = p.poruka;
                        ProtivnickaTablaGrid.IsEnabled = false;
                        return;
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

                case TipPoruke.Promasaj:
                case TipPoruke.Pogodak:
                    bool jePogodak = (p.tipPoruke == TipPoruke.Pogodak);
                    bool jeSuperPotez = p.poruka.Contains("Detalji:"); 

                    Dispatcher.Invoke(() => {
                        if (jeSuperPotez)
                        {
                            string detalji = p.poruka.Split(new[] { "Detalji:" }, StringSplitOptions.None)[1];
                            string[] deloviPoruke = detalji.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                            string cistString = deloviPoruke[0];

                            string[] stavke = cistString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                            foreach (string stavka in stavke)
                            {
                                string ociscenaStavka = stavka.Trim().TrimEnd('.');

                                string[] delovi = ociscenaStavka.Split(':');
                                if (delovi.Length == 2)
                                {
                                    int poz;
                                    if (int.TryParse(delovi[0], out poz))
                                    {
                                        string status = delovi[1];
                                        var btn = ProtivnickaTablaGrid.Children.OfType<Button>()
                                                    .FirstOrDefault(b => (int)b.Tag == poz);

                                        if (btn != null)
                                        {
                                            btn.Background = (status == "X") ? Brushes.Red : Brushes.Gray;
                                            btn.IsEnabled = false;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        { 
                            //bot odigrao
                            if(poslednjeGadjanihPolja.Count==0)
                            {

                                var btn = ProtivnickaTablaGrid.Children.OfType<Button>().FirstOrDefault(b => (int)b.Tag == botPolje);
                                if (btn != null)
                                {
                                    btn.Background = jePogodak ? Brushes.Red : Brushes.Gray;
                                }
                            }

                            foreach (int poz in poslednjeGadjanihPolja)
                            {
                                var btn = ProtivnickaTablaGrid.Children.OfType<Button>().FirstOrDefault(b => (int)b.Tag == poz);
                                if (btn != null)
                                {
                                    btn.Background = jePogodak ? Brushes.Red : Brushes.Gray;
                                    btn.IsEnabled = false;
                                }
                            }
                        }
                        if (jePogodak)
                        {
                            brojPromasaja=0;
                        }
                        else
                        {

                            brojPromasaja++;
                        }

                        TxtBrojPromasaja.Text = $"Uzastopni promašaji: {brojPromasaja}";
                        if (p.poruka.Contains("Preostalo podmornica protivniku je:"))
                        {
                            string[] delovi = p.poruka.Split(new string[] { "je: " }, StringSplitOptions.None);
                            if (delovi.Length > 1)
                            {
                                string broj = delovi[1].Trim().Split(' ')[0];
                                TxtPreostalePodmornice.Text = $"{broj}";
                            }
                        }
                        MessageBox.Show(p.poruka);
                    });
                    break;
                case TipPoruke.Napadnut:
                    Dispatcher.Invoke(() =>
                    {
                        Console.WriteLine("Primljena poruka Napadnut: " + p.poruka);
                        bool pogodjen = false;
                        string[] delovi = p.poruka.Split(new string[] { "Vasa tabla sada izgleda ovako:\n" }, StringSplitOptions.None);
                        if (delovi.Length > 1)
                        {
                            string matricaString = delovi[1];
                            string[] redovi = matricaString.Split('\n');

                            for (int i = 0; i < 10; i++)
                            {
                                if (i + 1 >= redovi.Length) break;
                                string red = redovi[i + 1];
                                string[] elementi = red.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                for (int j = 0; j < 10; j++)
                                {
                                    if (j + 1 >= elementi.Length) break;
                                    string stanje = elementi[j + 1];

                                    int index = (i * 10) + j;
                                    Button btn = MojaTablaGrid.Children[index] as Button;

                                    if (btn != null)
                                    {
                                        if (stanje == "x")
                                        {
                                            pogodjen = true;
                                            btn.Background = Brushes.LightBlue; // pogodak moje podmornice
                                            btn.IsEnabled = false;        // Onemogući dalje interakcije na tom polju
                                        }
                                    }
                                }
                            }
                        }
                        if (p.poruka.Contains("Preostalo vam je:"))
                        {
                            delovi = p.poruka.Split(new string[] { "Preostalo vam je: " }, StringSplitOptions.None);
                            if (delovi.Length > 1)
                            {
                                string broj = delovi[1].Split(' ')[0]; 
                                TxtPreostalePodmorniceMoje.Text = $"{broj}";
                            }
                        }
                        string statusTekst = pogodjen ? "vas je pogodio! Igra ponovo.." : "vas je promašio!";
                        StatusUnosa.Text = $"{imeProtivnika} {statusTekst}";
                    });
                    break;
                case TipPoruke.GlasanjeNova:
                    Dispatcher.Invoke(() =>
                    {
                        string poruka = "";
                        if (p.poruka.Contains(mojeIme))
                            poruka = "Čestitam! Pobedio si!";
                        else
                            poruka = $"Nažalost, igrač {imeProtivnika} Vas je pobedio!";
                        var rezultat = MessageBox.Show(poruka + "\n\nDa li želite novu partiju?",
                                                        "Kraj partije",
                                                        MessageBoxButton.YesNo,
                                                        MessageBoxImage.Question);

                        string odgovor = (rezultat == MessageBoxResult.Yes) ? "1" : "2";

                        Poruka pOdgovor = new Poruka(null, null, TipPoruke.Ostalo, odgovor);
                        tcpSocket.Send(pOdgovor.Serializuj());

                        StatusUnosa.Text = (odgovor == "1") ? "Čekamo protivnika da se odluči..." : "Igra se završava.";
                    });
                    break;
                case TipPoruke.Kraj:
                    Dispatcher.Invoke(() =>
                    {
                        if (tcpSocket != null)
                        {
                            tcpSocket.Shutdown(SocketShutdown.Both);
                            tcpSocket.Close();
                            tcpSocket = null;
                        }

                        Application.Current.Shutdown();
                    });
                    break;
                case TipPoruke.Ostalo:
                    Dispatcher.Invoke(() =>
                    {
                        if (string.IsNullOrWhiteSpace(p.poruka))
                        {
                            ResetujIgru();
                            StatusUnosa.Text = "Partija resetovana. Postavite podmornice.";
                        }
                        
                    });
                    break;
            }
        }
        private void ResetujIgru()
        {
            Dispatcher.Invoke(() =>
            {
                mojePodmornice.Clear();
                zauzetePozicije.Clear();
                poslednjeGadjanihPolja.Clear();
                tempX = 0; tempY = 0;
                trenutnaGrupa = 0;
                postavljenihUGrupi = 0;
                unosZavrsen = false;
                brojPromasaja = 0;
                brojBotPoteza = 3;
                poslednjeKliknutoDugme = null;
                imeProtivnika = "";

                MojaTablaGrid.Children.Clear();
                ProtivnickaTablaGrid.Children.Clear();

                TxtImeProtivnika.Text = "Protivnik";
                TxtImeProtivnika.Visibility = Visibility.Collapsed;
                TxtBrojPromasaja.Text = "Uzastopni promašaji: 0";
                TxtBrojBotPoteza.Text = "Preostali broj BOT poteza: 3";
                TxtPreostalePodmornice.Text = "10";
                TxtPreostalePodmorniceMoje.Text = "10";
                StatusUnosa.Text = "Postavljanje brodova...";

                UnosPodmornicaPanel.Visibility = Visibility.Visible;
                ProtivnickaTablaGrid.Visibility = Visibility.Collapsed;
                ProtivnickaTablaGrid.IsEnabled = false;
                BtnPotvrdi.Visibility = Visibility.Visible;
                BtnPotvrdi.IsEnabled = false;

                CbSuperPotez.IsChecked = false;
                CbSuperPotez.Visibility = Visibility.Visible;
                

                GenerisiTablu();
            });
        }
        private void InicijalizujProtivnickuTablu()
        {
            TxtImeProtivnika.Visibility = Visibility.Visible;
            ProtivnickaTablaGrid.Visibility = Visibility.Visible;
            ProtivnickaTablaGrid.Children.Clear();

            for (int i = 1; i <= 100; i++)
            {
                Button btn = new Button { Tag = i, Background = Brushes.White };
                

                ControlTemplate template = new ControlTemplate(typeof(Button));
                FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border), "border");

                borderFactory.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
                borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Black);
                borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0.5));

                template.VisualTree = borderFactory;
                btn.Template = template;

                btn.Click += (s, e) => {
                    int poz = (int)((Button)s).Tag;
                    poslednjiNapadnutiIndeks = poz; 
                    poslednjeGadjanihPolja.Clear();

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
                                    poslednjeGadjanihPolja.Add((nx - 1) * 10 + ny);
                                }
                            }
                        }
                        string superPoruka = "SUPER|" + string.Join(",", polja);
                        Poruka napad = new Poruka(null, null, TipPoruke.Napad, superPoruka);
                        tcpSocket.Send(napad.Serializuj());

                        CbSuperPotez.IsChecked = false; // Resetujemo checkbox
                        CbSuperPotez.Visibility = Visibility.Hidden; // Sakrivamo jer se koristi samo jednom
                    }
                    else
                    {
                        // Običan napad
                        poslednjeGadjanihPolja.Clear();
                        poslednjeGadjanihPolja.Add(poz);
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

                ControlTemplate template = new ControlTemplate(typeof(Button));
                FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border), "border");
                borderFactory.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
                borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Black);
                borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0.5));
                template.VisualTree = borderFactory;
                btn.Template = template;

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