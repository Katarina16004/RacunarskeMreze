using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
    internal class Program
    {
        public static string ime = null;
        public static Socket clientSocket = null;
        private static int brojPodmornica = 10;
        private static int velTable = 10;
        public static bool PrvaPartija = true;
        public static int MaxUzastopnihGresaka = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("Pozdrav od Clienta");
            PrikaziMeni();

            UspostaviTCPKonekciju();
            ZatvoriTCPKonenciju();

            Console.ReadKey();
        }

        static void PrikaziMeni()
        {
            int x;
            ime = null;

            bool unos = false;
            do
            {
                Console.WriteLine("Dobrodosli u potapanje brodova! \n Pritisnite sledece opcije:" +
                           "\n 1) Nova igra \n 2) Izlaz");
                int.TryParse(Console.ReadLine(), out x);
                switch (x)
                {
                    case 1:
                        UnesiIme();
                        unos = true;
                        Prijava();
                        break;
                    case 2:
                        Console.WriteLine("Dovidjenja!");
                        Thread.Sleep(1000);
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Greska!");
                        break;
                }
            } while (!unos);
        }

        static void UnesiIme()
        {
            do
            {
                Console.WriteLine("Unesite svoje ime:");
                ime = Console.ReadLine();
            } while (ime == string.Empty);

            Console.WriteLine("Ucitavanje...");
            Thread.Sleep(1000);
        }

        static void Prijava()
        {
            if (ime == null)
                return;

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint destination = new IPEndPoint(IPAddress.Parse("192.168.56.1"), 60002);
            byte[] buffer = new byte[200];
            buffer = Encoding.UTF8.GetBytes("PRIJAVA" + ime);
            byte[] buffer2 = new byte[200];

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Parse("192.168.56.1"), 0);

            try
            {
                string poruka;
                int brBajta = socket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, destination);
                do
                {
                    int primljena = socket.ReceiveFrom(buffer2, ref posiljaocEP);
                    poruka = Encoding.UTF8.GetString(buffer2);
                    Console.WriteLine(poruka.TrimEnd(' '));

                } while (!poruka.Contains("SPREMAN") && !poruka.Contains("Neuspesno"));

                if (poruka.Contains("Neuspesno"))
                {
                    PrikaziMeni();
                }

                Console.WriteLine("Cekamo na prijavu ostalih igraca!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Desila se greska prilikom slanja poruke! \n " + ex.ToString());
            }
            finally
            {
                socket.Close();
            }
        }

        private static void UspostaviTCPKonekciju()
        {
            if (PrvaPartija == true)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ServerEP = new IPEndPoint(IPAddress.Parse("192.168.56.1"), 5001);
                Random random = new Random();
                int brPokusaja = 0;

                while (true)
                {
                    try
                    {
                        clientSocket.Connect(ServerEP);
                        Console.WriteLine("Connected to server.");
                        break;
                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine($"SocketException: {e.Message}");
                        Console.WriteLine("Pokusavam da se povezem na server...");
                        Thread.Sleep(random.Next(10, 100));
                        if (++brPokusaja == 10)
                        {
                            Console.WriteLine("Neuspeno povezivanje na server");
                            ZatvoriTCPKonenciju();
                            break;
                        }
                    }
                }
            }

            Poruka p = new Poruka();

            try
            {
                p = PrimiPoruku();
                Console.WriteLine("Primljena poruka: ");

                string[] redovi = p.poruka.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string red in redovi)
                {
                    Console.WriteLine("\t" + red);

                    if (red.Contains("Velicina table"))
                    {
                        string[] delovi = red.Split(':');
                        velTable = int.Parse(delovi[1].Trim());
                    }
                    else if (red.Contains("Broj podmornica"))
                    {
                        string[] delovi = red.Split(':');
                        brojPodmornica = int.Parse(delovi[1].Trim());
                    }
                    else if (red.Contains("Maksimalan broj uzastopnih gresaka"))
                    {
                        string[] delovi = red.Split(':');
                        MaxUzastopnihGresaka = int.Parse(delovi[1].Trim());
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Greska u konekciji! {e}");
                ZatvoriTCPKonenciju();
            }

            List<Podmornica> podmornice = UnosPodmornica();

            string PodmornicaZaSlanje = ime + "|" + string.Join(";", podmornice.Select(podm =>
                $"{(int)podm.Tip},{(podm.Horizontalna ? "H" : "V")},{string.Join(",", podm.Pozicije)}"));

            Igrac prazan = new Igrac();
            PosaljiPoruku(prazan, prazan, TipPoruke.PozicijeBrodova, PodmornicaZaSlanje);

            p = PrimiPoruku();
            int[,] tabla = Igrac.PretvoriStringUMatricu(p.poruka);
            PrikaziTablu(tabla);
            Thread.Sleep(2000);

            IgrajPartiju();
        }

        private static void IgrajPartiju()
        {
            try
            {
                while (true)
                {
                    Poruka p = PrimiPoruku();

                    if (p == null || p.poruka == null)
                        continue;

                    if (p.tipPoruke == TipPoruke.GlasanjeNova)
                    {
                        Console.Clear();
                        Console.WriteLine("═══════════════════════════════════════");
                        Console.WriteLine("              GLASANJE");
                        Console.WriteLine("═══════════════════════════════════════\n");
                        Console.WriteLine(p.poruka);
                        GlasajNovaPartija();
                    }
                    else if (p.tipPoruke == TipPoruke.Napad)
                    {
                        string[] linije = p.poruka.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> dostupniIgraci = new List<string>();

                        foreach (string linija in linije)
                        {
                            if (linija.StartsWith("\t->"))
                            {
                                string imeIgraca = linija.Substring(3).Trim();
                                dostupniIgraci.Add(imeIgraca);
                            }
                        }

                        if (dostupniIgraci.Count == 0)
                        {
                            Console.Clear();
                            Console.WriteLine("═══════════════════════════════════════");
                            Console.WriteLine("               POBEDA! ");
                            Console.WriteLine("═══════════════════════════════════════\n");
                            Console.WriteLine("Vi ste pobednik!");
                            Thread.Sleep(3000);
                            break;
                        }

                        string napadnuti = "";
                        while (true)
                        {
                            Console.Clear();
                            Console.WriteLine("═══════════════════════════════════════");
                            Console.WriteLine("           ODABIR PROTIVNIKA ");
                            Console.WriteLine("═══════════════════════════════════════\n");
                            Console.WriteLine(linije[0]);
                            for (int i = 0; i < dostupniIgraci.Count; i++)
                            {
                                Console.WriteLine($"  {i + 1}. {dostupniIgraci[i]}");
                            }
                            Console.Write("\nUnesite ime protivnika: ");
                            napadnuti = Console.ReadLine();
                            if (dostupniIgraci.Contains(napadnuti))
                                break;
                            else
                                Console.WriteLine("Nepostojece ime. Pokusajte ponovo.");
                        }

                        PosaljiPoruku(null, null, TipPoruke.Ostalo, napadnuti);

                        bool pogodio = true;
                        while (pogodio)
                        {
                            pogodio = Napadaj();
                        }
                    }
                    else if (p.tipPoruke == TipPoruke.Napadnut)
                    {
                        Odbrana(p.Napadnut, p.poruka);
                    }
                    else if (p.tipPoruke == TipPoruke.Obavestenje)
                    {
                        Console.Clear();
                        Console.WriteLine("═══════════════════════════════════════");
                        Console.WriteLine("             OBAVESTENJE");
                        Console.WriteLine("═══════════════════════════════════════\n");
                        Console.WriteLine(p.poruka);
                        Thread.Sleep(3000);
                    }
                    else
                    {
                        ZatvoriTCPKonenciju();
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Greska u konekciji! {e}");
                ZatvoriTCPKonenciju();
            }
        }

        private static void Odbrana(Igrac i, string poruka)
        {
            Console.Clear();
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("         NAPAD NA VAŠU TABLU ");
            Console.WriteLine("═══════════════════════════════════════\n");
            Console.WriteLine(poruka);

            int preostaloBrodova = i.podmornice.Count;
            if (preostaloBrodova == 0)
            {
                Console.WriteLine("\nIzgubio si partiju!");
                Console.WriteLine("Sacekaj da ostali igraci zavrse, nakon toga bice glasanje za novu partiju!");
            }
            else
            {
                Console.WriteLine($"\nPreostalo ti je: {preostaloBrodova} brodova!");
            }

            Thread.Sleep(3000);
        }

        private static void GlasajNovaPartija()
        {
            Console.WriteLine("Stigli smo do glasanja za novu partiju!");
            Poruka p = PrimiPoruku();
            Console.WriteLine(p.poruka);
            int x;
            do
            {
                int.TryParse(Console.ReadLine(), out x);
            } while (x != 1 && x != 2);

            PosaljiPoruku(null, null, TipPoruke.Obavestenje, x.ToString());
            p = PrimiPoruku();
            if (p.tipPoruke == TipPoruke.Kraj)
            {
                Console.WriteLine("Neko od igraca je odbio da nastavi, program se zavrsava s radom!");
                ZatvoriTCPKonenciju();
                Environment.Exit(0);
            }
            else
            {
                PrvaPartija = false;
                Console.Clear();
                Console.WriteLine("═══════════════════════════════════════");
                Console.WriteLine("           NOVA PARTIJA ");
                Console.WriteLine("═══════════════════════════════════════\n");
                Console.WriteLine("Pokrecemo novu partiju...");
                Thread.Sleep(2000);
                UspostaviTCPKonekciju();
            }
        }

        private static bool Napadaj()
        {
            Poruka p = PrimiPoruku();

            if (p == null || p.poruka == null)
                return false;

            if (p.tipPoruke == TipPoruke.GlasanjeNova)
            {
                GlasajNovaPartija();
                return false;
            }
            else if (p.tipPoruke == TipPoruke.Ostalo)
            {
                Console.Clear();
                Console.WriteLine("═══════════════════════════════════════");
                Console.WriteLine("           GADJANJA PROTIVNIKA ");
                Console.WriteLine("═══════════════════════════════════════\n");
                Console.WriteLine(p.poruka);

                int polje = -1;
                bool validanUnos = false;

                do
                {
                    Console.Write($"\nGadjaj - Unesite X,Y (1-{velTable}): ");
                    string unos = Console.ReadLine();

                    string[] delovi = unos.Split(',');

                    if (delovi.Length != 2)
                    {
                        Console.WriteLine("Pogresan format! Unesite: X,Y (npr: 1,1)");
                        continue;
                    }

                    if (!int.TryParse(delovi[0].Trim(), out int x) || !int.TryParse(delovi[1].Trim(), out int y))
                    {
                        Console.WriteLine("Unesite brojeve!");
                        continue;
                    }

                    if (x < 1 || x > velTable || y < 1 || y > velTable)
                    {
                        Console.WriteLine($"X i Y moraju biti od 1 do {velTable}");
                        continue;
                    }

                    polje = (x - 1) * velTable + y;
                    validanUnos = true;

                } while (!validanUnos);

                PosaljiPoruku(null, null, TipPoruke.Napad, polje.ToString());

                p = PrimiPoruku();

                if (p.tipPoruke == TipPoruke.Ponovi)
                {
                    Console.WriteLine("Polje je već gadjano. Pokušajte ponovo.");

                    do
                    {
                        Console.Write($"Gadjaj - Unesite X,Y (1-{velTable}): ");
                        string unos = Console.ReadLine();
                        string[] delovi = unos.Split(',');

                        if (delovi.Length == 2 && int.TryParse(delovi[0].Trim(), out int x) &&
                            int.TryParse(delovi[1].Trim(), out int y) &&
                            x >= 1 && x <= velTable && y >= 1 && y <= velTable)
                        {
                            polje = (x - 1) * velTable + y;
                            break;
                        }
                        else
                        {
                            Console.WriteLine("Pogresan unos!");
                        }
                    } while (true);

                    PosaljiPoruku(null, null, TipPoruke.Napad, polje.ToString());
                    p = PrimiPoruku();
                }

                if (p.tipPoruke == TipPoruke.Pogodak)
                {
                    Console.Clear();
                    Console.WriteLine("═══════════════════════════════════════");
                    Console.WriteLine("             POGODAK! ");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Thread.Sleep(1500);

                    return true;
                }
                else if (p.tipPoruke == TipPoruke.Promasaj)
                {
                    Console.Clear();
                    Console.WriteLine("═══════════════════════════════════════");
                    Console.WriteLine("             PROMASAJ!");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Thread.Sleep(2000);

                    PratiPoteze();

                    return false;
                }
                else if (p.tipPoruke == TipPoruke.Izgubio)
                {
                    Console.Clear();
                    Console.WriteLine("═══════════════════════════════════════");
                    Console.WriteLine("             IZGUBIO SI! ");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Thread.Sleep(2000);

                    GlasajNovaPartija();
                    return false;
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        private static void PratiPoteze()
        {
            while (true)
            {
                Poruka p = PrimiPoruku();

                if (p == null || p.poruka == null)
                    continue;

                if (p.tipPoruke == TipPoruke.Napad)
                {
                    return;
                }
                else if (p.tipPoruke == TipPoruke.Obavestenje)
                {
                    Console.Clear();
                    Console.WriteLine("═══════════════════════════════════════");
                    Console.WriteLine("            PRATNJA POTEZA ");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Thread.Sleep(2500);
                }
                else if (p.tipPoruke == TipPoruke.Napadnut)
                {
                    Odbrana(p.Napadnut, p.poruka);
                }
                else
                {
                    break;
                }
            }
        }

        private static void PosaljiPoruku(Igrac NaPotezu, Igrac Napadnut, TipPoruke tip, string poruka)
        {
            Poruka p = new Poruka(NaPotezu, Napadnut, tip, poruka);
            try
            {
                clientSocket.Send(p.Serializuj());
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Greska prilikom slanja poruke serveru: {e.Message}");
            }
        }

        private static Poruka PrimiPoruku()
        {
            Poruka p = new Poruka();
            try
            {
                byte[] dataBuffer = new byte[40806];
                int bytesRead = clientSocket.Receive(dataBuffer);
                if (bytesRead > 0)
                {
                    p = Poruka.DeserializujPoruku(dataBuffer);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Greska u konekciji! {e}");
                ZatvoriTCPKonenciju();
            }
            return p;
        }

        private static void PrikaziTablu(int[,] matrica)
        {
            Console.WriteLine("\nStanje vase table: ");

            Console.Write("   ");
            for (int j = 0; j < matrica.GetLength(1); j++)
            {
                if (j == 9)
                    Console.Write(" ");
                Console.Write(string.Format("{0,2}", j + 1));
            }
            Console.WriteLine();

            for (int i = 0; i < matrica.GetLength(0); i++)
            {
                Console.Write(string.Format("{0,2}", i + 1) + " ");

                for (int j = 0; j < matrica.GetLength(1); j++)
                {
                    if (matrica[i, j] == 1)
                        Console.Write(" O");
                    else
                        Console.Write(" -");
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        private static List<Podmornica> UnosPodmornica()
        {
            List<Podmornica> podmornice = new List<Podmornica>();
            HashSet<int> zauzetePozicije = new HashSet<int>();

            int[][] zadatak = new int[][]
            {
                new int[] { 4, 1 },
                new int[] { 3, 2 },
                new int[] { 2, 3 },
                new int[] { 1, 4 }
            };

            foreach (var grupa in zadatak)
            {
                int broj = grupa[0];
                int duzina = grupa[1];
                TipPodmornice tip = (TipPodmornice)duzina;

                for (int i = 0; i < broj; i++)
                {
                    bool validnaPodmornica = false;

                    do
                    {
                        Console.Clear();
                        PrikaziTabeluSaPodmornicama(podmornice, velTable);

                        Console.WriteLine($"\n--- Unos podmornice {i + 1}/{broj} ({duzina}x1) ---");

                        Console.Write($"Unesite početnu poziciju (X,Y): ");
                        string unosPos = Console.ReadLine();
                        string[] deloviPos = unosPos.Split(',');

                        if (deloviPos.Length != 2 || !int.TryParse(deloviPos[0].Trim(), out int x) ||
                            !int.TryParse(deloviPos[1].Trim(), out int y))
                        {
                            Console.WriteLine("Pogrešan format! Unesite X,Y (npr: 1,1)");
                            Thread.Sleep(1500);
                            continue;
                        }

                        if (x < 1 || x > velTable || y < 1 || y > velTable)
                        {
                            Console.WriteLine($"X i Y moraju biti od 1 do {velTable}");
                            Thread.Sleep(1500);
                            continue;
                        }
                        bool horizontalna = true;
                        if (duzina != 1)
                        {
                            Console.Write("Unesite smer - H (horizontalno) ili V (vertikalno): ");
                            string smerInput = Console.ReadLine()?.ToUpper() ?? "";
                            horizontalna = smerInput == "H";
                            if (smerInput != "H" && smerInput != "V")
                            {
                                Console.WriteLine("Nevažeći smer! Pokušajte ponovo.");
                                Thread.Sleep(1500);
                                continue;
                            }
                        }

                        List<int> pozicije = GenerisiPozicijePodmornice(x, y, duzina, horizontalna, velTable, zauzetePozicije);

                        if (pozicije == null || pozicije.Count == 0)
                        {
                            Console.WriteLine("Podmornica izlazi van granica table ili se preklapa! Pokušajte ponovo.");
                            Thread.Sleep(1500);
                            continue;
                        }

                        Podmornica novaPodmornica = new Podmornica(tip, pozicije, horizontalna);
                        podmornice.Add(novaPodmornica);

                        foreach (int poz in pozicije)
                            zauzetePozicije.Add(poz);

                        Console.WriteLine($"Podmornica uspešno postavljena!");
                        validnaPodmornica = true;
                        Thread.Sleep(1000);

                    } while (!validnaPodmornica);
                }
            }

            Console.Clear();
            Console.WriteLine("Sve podmornice su uspešno postavljene!\n");
            PrikaziTabeluSaPodmornicama(podmornice, velTable);

            return podmornice;
        }

        private static List<int> GenerisiPozicijePodmornice(int x, int y, int duzina, bool horizontalna, int velTable, HashSet<int> zauzetePozicije)
        {
            List<int> pozicije = new List<int>();

            if (horizontalna)
            {
                if (y + duzina - 1 > velTable)
                {
                    Console.WriteLine($"Podmornica izlazi van desne granice!");
                    return null;
                }

                for (int i = 0; i < duzina; i++)
                {
                    int pozicija = (x - 1) * velTable + (y + i);

                    if (zauzetePozicije.Contains(pozicija))
                    {
                        Console.WriteLine($"Pozicija X={x}, Y={y + i} je već zauzeta!");
                        return null;
                    }

                    pozicije.Add(pozicija);
                }
            }
            else
            {
                if (x + duzina - 1 > velTable)
                {
                    Console.WriteLine($"Podmornica izlazi van donje granice!");
                    return null;
                }

                for (int i = 0; i < duzina; i++)
                {
                    int pozicija = (x + i - 1) * velTable + y;

                    if (zauzetePozicije.Contains(pozicija))
                    {
                        Console.WriteLine($"Pozicija X={x + i}, Y={y} je već zauzeta!");
                        return null;
                    }

                    pozicije.Add(pozicija);
                }
            }

            return pozicije;
        }

        private static void PrikaziTabeluSaPodmornicama(List<Podmornica> podmornice, int velTable)
        {
            Console.WriteLine("\nTabla sa postavljenim podmornicama:");
            Console.Write("   ");
            for (int j = 0; j < velTable; j++)
            {
                if (j == 9)
                    Console.Write(" ");
                Console.Write(string.Format("{0,2}", j + 1));
            }
            Console.WriteLine();

            for (int i = 0; i < velTable; i++)
            {
                Console.Write(string.Format("{0,2}", i + 1) + " ");
                for (int j = 0; j < velTable; j++)
                {
                    int pozicija = i * velTable + j + 1;
                    bool imaPodmornicu = podmornice.Any(p => p.SadrziPoziciju(pozicija));
                    Console.Write(imaPodmornicu ? " ■" : " .");
                }
                Console.WriteLine();
            }
        }

        private static void ZatvoriTCPKonenciju()
        {
            Console.ReadKey();
            Console.WriteLine("Klijent zavrsava sa radom");
            if (clientSocket != null && clientSocket.Connected)
                clientSocket.Close();
        }
    }
}