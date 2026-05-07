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
        private static bool superPotezIskoriscen = false;
        private static int sekundeCekanjaNaUnos = 14;
        private static int timeoutMs => sekundeCekanjaNaUnos * 1000;

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
            
            Thread.Sleep(2000);

            IgrajPartiju();
        }

        private static void IgrajPartiju()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    Poruka p = PrimiPoruku();

                    if (p == null || p.poruka == null)
                        continue;

                    if (p.tipPoruke == TipPoruke.GlasanjeNova)
                    {
                        Console.WriteLine("═══════════════════════════════════════");
                        Console.WriteLine("             KRAJ PARTIJE");
                        Console.WriteLine("═══════════════════════════════════════\n");
                        if (p.poruka.Contains(ime))
                            Console.WriteLine("\tTI SI POBEDNIK!\n");
                        else
                            Console.WriteLine(p.poruka);
                        Console.WriteLine("═══════════════════════════════════════");
                        Console.WriteLine("              GLASANJE");
                        Console.WriteLine("═══════════════════════════════════════\n");
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

                        string napadnuti = OdaberiProtivnikaSaTimeoutom(linije, dostupniIgraci);
                        if (string.IsNullOrEmpty(napadnuti))
                        {
                            // Timeout - BOT je odigrao
                            p = PrimiPoruku();
                            /*if (p.tipPoruke == TipPoruke.Izgubio)
                            {
                                Console.WriteLine("\nIzgubio si partiju eliminacijom!");
                                p= PrimiPoruku(); //za slanje poruke o glasanju nakon eliminacije
                            }*/
                            if (p.tipPoruke == TipPoruke.GlasanjeNova)
                            {
                                Console.WriteLine("\nIzgubio si partiju eliminacijom!");
                                Console.WriteLine("═══════════════════════════════════════");
                                Console.WriteLine("             KRAJ PARTIJE");
                                Console.WriteLine("═══════════════════════════════════════\n");
                                Console.WriteLine(p.poruka);
                                Console.WriteLine("═══════════════════════════════════════");
                                Console.WriteLine("              GLASANJE");
                                Console.WriteLine("═══════════════════════════════════════\n");
                                GlasajNovaPartija();
                            }
                            Console.WriteLine($"Poruka nakon BOT poteza: {p.poruka} , Tip: {p.tipPoruke}");
                            continue;
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
                        Console.WriteLine("═══════════════════════════════════════");
                        Console.WriteLine("             OBAVESTENJE");
                        Console.WriteLine("═══════════════════════════════════════\n");
                        Console.WriteLine(p.poruka);
                        Thread.Sleep(2000);
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

        private static string OdaberiProtivnikaSaTimeoutom(string[] linije, List<string> dostupniIgraci)
        {
            DateTime startTime = DateTime.Now;
            int timeout = timeoutMs;
            string napadnuti = "";

            while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
            {
                // Provera da li je stigla poruka od servera (BOT je odigrao)
                if (DaLiJePristiglaPortuka())
                {
                    Console.WriteLine("\n[BOT JE ODIGRAO - TIMEOUT!]");

                    return "";
                }

                Console.WriteLine("═══════════════════════════════════════");
                Console.WriteLine("           ODABIR PROTIVNIKA ");
                Console.WriteLine("═══════════════════════════════════════\n");
                Console.WriteLine(linije[0]);
                for (int i = 0; i < dostupniIgraci.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {dostupniIgraci[i]}");
                }

                long preostaloVremena = timeout - (long)(DateTime.Now - startTime).TotalMilliseconds;
                Console.Write("Unesite ime protivnika: ");

                napadnuti = UcitajSaTimeoutomAlt((int)(preostaloVremena / 1000));

                if (string.IsNullOrEmpty(napadnuti))
                {
                    Console.WriteLine("\n[TIMEOUT - BOT ĆE IGRATI!]");
                    return "";
                }

                if (dostupniIgraci.Contains(napadnuti))
                {
                    return napadnuti;
                }
                else
                {
                    Console.WriteLine("Nepostojece ime. Pokusajte ponovo.");
                    Console.Clear();
                }
            }

            return "";
        }

        private static bool DaLiJePristiglaPortuka()
        {
            clientSocket.Blocking = false;
            byte[] buffer = new byte[40806];

            try
            {
                int bytesRead = clientSocket.Receive(buffer, 0, buffer.Length, SocketFlags.Peek);
                return bytesRead > 0;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                clientSocket.Blocking = true;
            }
        }

        private static string UcitajSaTimeoutomAlt(int sekundi)
        {
            DateTime startTime = DateTime.Now;
            string input = "";

            while ((DateTime.Now - startTime).TotalSeconds < sekundi)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        return input;
                    }
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (input.Length > 0)
                        {
                            input = input.Substring(0, input.Length - 1);
                            Console.Write("\b \b");
                        }
                    }
                    else if (!char.IsControl(key.KeyChar))
                    {
                        input += key.KeyChar;
                        Console.Write(key.KeyChar);
                    }
                }
                Thread.Sleep(50);
            }

            Console.WriteLine();
            return "";
        }

        private static void Odbrana(Igrac i, string poruka)
        {
            Console.Clear();
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("         NAPAD NA VAŠU TABLU ");
            Console.WriteLine("═══════════════════════════════════════\n");
            Console.WriteLine(poruka);

            int preostaloBrodova = i.GetBrojPreostalihPodmornica();
            if (preostaloBrodova == 0)
            {
                Console.WriteLine("\nIzgubio si partiju!");
                Console.WriteLine("Sacekaj da ostali igraci zavrse, nakon toga bice glasanje za novu partiju!");
            }

            Thread.Sleep(2000);
        }

        private static void GlasajNovaPartija()
        {
            Console.WriteLine("Stigli smo do glasanja za novu partiju!");
           
            Console.WriteLine("Unesite 1 ukoliko zelite novu partiju, 2 ukoliko ne zelite:");

            int x;
            do
            {
                int.TryParse(Console.ReadLine(), out x);
            } while (x != 1 && x != 2);

            PosaljiPoruku(null, null, TipPoruke.Obavestenje, x.ToString());
            Poruka p = PrimiPoruku();
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

            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("           GADJANJA PROTIVNIKA ");
            Console.WriteLine("═══════════════════════════════════════\n");
            Console.WriteLine(p.poruka);

            bool koristiSuperPotez = false;
            int izbor = 1;

            if (!superPotezIskoriscen)
            {
                Console.WriteLine("\nIzaberite potez:");
                Console.WriteLine("1 - Obicno gadjanje");
                Console.WriteLine("2 - Super potez (gadja 3x3 oko izabranog polja, samo jednom po partiji)");

                do
                {
                    Console.Write("Vas izbor (1 ili 2): ");
                    string unosIzbor = UcitajSaTimeoutomAlt(sekundeCekanjaNaUnos);
                    if (string.IsNullOrEmpty(unosIzbor))
                    {
                        Console.WriteLine("\n[TIMEOUT - BOT ĆE IGRATI!]");
                        return false; // BOT je odigrao - sledeci igrac dobija potez
                    }
                    int.TryParse(unosIzbor, out izbor);
                } while (izbor != 1 && izbor != 2);

                if (izbor == 2)
                {
                    koristiSuperPotez = true;
                    superPotezIskoriscen = true;
                }
            }

            int x, y;

        UNOSPOLJA:
            while (true)
            {
                // Provera da li je stigla poruka od servera (BOT je odigrao)
                if (DaLiJePristiglaPortuka())
                {
                    Console.WriteLine("\n[BOT JE ODIGRAO - TIMEOUT!]");
                    return false; // BOT je odigrao - sledeci igrac dobija potez
                }

                Console.Write($"\nUnesite X,Y (1-{velTable}): ");
                string unos = UcitajSaTimeoutomAlt(sekundeCekanjaNaUnos);

                if (string.IsNullOrEmpty(unos))
                {
                    Console.WriteLine("\n[TIMEOUT - BOT ĆE IGRATI!]");
                    return false; // BOT je odigrao - sledeci igrac dobija potez
                }

                string[] delovi = unos.Split(',');

                if (delovi.Length != 2)
                {
                    Console.WriteLine("Pogresan format! Unesite X,Y (npr: 5,5)");
                    continue;
                }

                if (!int.TryParse(delovi[0].Trim(), out x) || !int.TryParse(delovi[1].Trim(), out y))
                {
                    Console.WriteLine("Unesite brojeve!");
                    continue;
                }

                if (x < 1 || x > velTable || y < 1 || y > velTable)
                {
                    Console.WriteLine($"X i Y moraju biti od 1 do {velTable}");
                    continue;
                }

                break;
            }


            if (!koristiSuperPotez) //obican potez, salje se samo jedno polje
            {
                int polje = (x - 1) * velTable + y;
                PosaljiPoruku(null, null, TipPoruke.Napad, polje.ToString());

                p = PrimiPoruku();

                if (p.tipPoruke == TipPoruke.Ponovi)
                {
                    Console.WriteLine("Polje je već gadjano. Pokušajte ponovo.");
                    goto UNOSPOLJA;
                }

                if (p.tipPoruke == TipPoruke.Pogodak)
                {
                    Console.WriteLine("\n═══════════════════════════════════════");
                    Console.WriteLine("             POGODAK! ");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Thread.Sleep(2000);

                    if (p.poruka.Contains("0 brodova") || p.Napadnut.GetBrojPreostalihPodmornica() == 0)
                        return false;

                    return true; // Korisnik je pogodio - nastavlja sa sledecim potezom
                }
                else if (p.tipPoruke == TipPoruke.Promasaj)
                {
                    Console.WriteLine("\n═══════════════════════════════════════");
                    Console.WriteLine("             PROMASAJ!");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Thread.Sleep(2000);

                    return false; // Korisnik je promasio - sledeci igrac dobija potez
                }
                else if (p.tipPoruke == TipPoruke.Izgubio)
                {
                    Console.WriteLine("\n═══════════════════════════════════════");
                    Console.WriteLine("             IZGUBIO SI! ");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Console.WriteLine("\nSacekaj da ostali igraci zavrse partiju...");
                    Thread.Sleep(2000);
                    return false; // Eliminisan - sledeci igrac dobija potez
                }
            }
            else  //super potez, salje se 3x3 oko izabranog polja
            {
                List<int> polja = new List<int>();

                for (int dx = -1; dx <= 1; dx++) //red iznad, isti red, red ispod
                {
                    for (int dy = -1; dy <= 1; dy++) //kolona levo, ista kolona, kolona desno
                    {
                        //koordinate svakog polja u okolini
                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx >= 1 && nx <= velTable && ny >= 1 && ny <= velTable)
                        {
                            int polje = (nx - 1) * velTable + ny;
                            polja.Add(polje);
                        }
                    }
                }

                string superPoruka = "SUPER|" + string.Join(",", polja);
                PosaljiPoruku(null, null, TipPoruke.Napad, superPoruka);

                p = PrimiPoruku();

                if (p.tipPoruke == TipPoruke.Pogodak)
                {
                    Console.WriteLine("\n═══════════════════════════════════════");
                    Console.WriteLine("          SUPER POGODAK! ");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Thread.Sleep(2000);

                    if (p.poruka.Contains("0 brodova") || p.Napadnut.GetBrojPreostalihPodmornica() == 0)
                        return false;

                    return true; // Korisnik je pogodio - nastavlja sa sledecim potezom
                }
                else if (p.tipPoruke == TipPoruke.Promasaj)
                {
                    Console.WriteLine("\n═══════════════════════════════════════");
                    Console.WriteLine("          SUPER PROMASAJ!");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Thread.Sleep(2000);
                    return false; // Korisnik je promasio - sledeci igrac dobija potez
                }
                else if (p.tipPoruke == TipPoruke.Izgubio)
                {
                    Console.WriteLine("\n═══════════════════════════════════════");
                    Console.WriteLine("             IZGUBIO SI! ");
                    Console.WriteLine("═══════════════════════════════════════\n");
                    Console.WriteLine(p.poruka);
                    Console.WriteLine("\nSacekaj da ostali igraci zavrse partiju...");
                    Thread.Sleep(2000);
                    return false; // Eliminisan - sledeci igrac dobija potez
                }
            }

            return false;
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

        private static List<Podmornica> UnosPodmornica()
        {
            List<Podmornica> podmornice = new List<Podmornica>();
            HashSet<int> zauzetePozicije = new HashSet<int>();
            superPotezIskoriscen = false;

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