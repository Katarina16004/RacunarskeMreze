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
        private static List<Klijent> Klijenti = new List<Klijent>();
        private static List<Igrac> Igraci = new List<Igrac>();
        private static List<Socket> readySockets = null;
        Poruka p = new Poruka();

        private static List<Socket> clientSockets = null;

        public static Socket serverSocket = null;
        private static int MaxBrojIgraca = 0;
        private static int VelicinaTable = 0;
        private static int MaxUzastopnihGresaka = 0;
        private static bool NovaIgra = true;
        private static bool krajPartije = false;
        private static int rezultatGadjanja;
        private static bool PrvaPartija = true;

        static void Main(string[] args)
        {

            Console.WriteLine("Dobrodosli na server!");
            do
            {
                Console.WriteLine("Unesite broj igraca koji ce da igraju:");
                int.TryParse(Console.ReadLine(), out MaxBrojIgraca);
            }
            while (MaxBrojIgraca < 1);

            Console.WriteLine("Cekam prijave Igraca:");

            UcitajIgrace();
            UspostaviTCPKonekciju();


            while (NovaIgra)
            {
                IncijalizujTable();
                PosaljiKlijentimaTable();
                ZapocniIgru();
            }

            ZatvoriUticnice();


            Console.ReadKey();
        }

        //Ucitava igrace, proverava da li neko sa tim imenom vec prijavljen, ako nije ubacuje,
        //ako jeste odbija prijavu i salje poruku nazad
        static void UcitajIgrace()
        {

            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 60002);
            serverSocket.Bind(serverEP);
            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] binarnaPoruka;
            byte[] prijemniBafer = new byte[128];
            do
            {
                try
                {
                    int brBajta = serverSocket.ReceiveFrom(prijemniBafer, ref posiljaocEP);
                    string poruka = Encoding.UTF8.GetString(prijemniBafer, 0, brBajta);
                    Console.WriteLine($"Pokusaj prijave od {posiljaocEP}");
                    string ime = poruka.Substring(7);
                    string errorMessage = null;
                    bool postojiKlijent = false;

                    if (ime.Length == 0)
                    {
                        Console.WriteLine("Ime je prazno!");
                        errorMessage = "Ime je prazno!";
                        return;
                    }

                    Klijent klijent = new Klijent(ime, posiljaocEP);

                    foreach (Klijent k in Klijenti)
                    {
                        if (k.Ime == ime)
                        {
                            Console.WriteLine("Vec postoji client sa datim imenom");
                            errorMessage = "Vec postoji client sa datim imenom";
                            postojiKlijent = true;
                            break;
                        }
                    }

                    if (!postojiKlijent)
                    {
                        Klijenti.Add(klijent);
                        Console.WriteLine("Ubacen klijent!");
                    }

                    Console.WriteLine("Do sada su ubaceni:");
                    foreach (Klijent k in Klijenti)
                    {
                        Console.WriteLine(k);
                    }


                    if (errorMessage == null)
                    {
                        binarnaPoruka = Encoding.UTF8.GetBytes("Uspesno ubacen na server");
                    }
                    else
                    {
                        binarnaPoruka = Encoding.UTF8.GetBytes("Neuspesno ubacen na server. razlog: \n" + errorMessage + '\0');
                    }
                    brBajta = serverSocket.SendTo(binarnaPoruka, 0, binarnaPoruka.Length, SocketFlags.None, posiljaocEP);

                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Doslo je do greske tokom prijema poruke: \n{ex}");
                }


            } while (Klijenti.Count() < MaxBrojIgraca);


            UnesiParametreIgre();

            //PosaljiSignalSpreman
            foreach (Klijent k in Klijenti)
            {
                binarnaPoruka = Encoding.UTF8.GetBytes("SPREMAN" + '\0');
                serverSocket.SendTo(binarnaPoruka, 0, binarnaPoruka.Length, SocketFlags.None, k.IPAdresa);
            }


            serverSocket.Close();
        }

        private static void UnesiParametreIgre()
        {
            Console.WriteLine("Svi igraci su spremni za igru!");
            do
            {
                Console.WriteLine("Unesite dimenziju table:");
                int.TryParse((string)Console.ReadLine(), out VelicinaTable);
            }
            while (VelicinaTable < 0);
            do
            {
                Console.WriteLine("Unesite maksimalan broj uzastopnih gresaka:");
                int.TryParse((string)Console.ReadLine(), out MaxUzastopnihGresaka);
            } while (MaxUzastopnihGresaka > VelicinaTable * VelicinaTable - 1);
        }

        //Uspostavljanje TCP konekcije sa svim igracima, slanje informacija o igri,
        private static void UspostaviTCPKonekciju()
        {
            // uspostavljanje TCP konekcije
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 5001);
            serverSocket.Bind(serverEP);
            serverSocket.Listen(MaxBrojIgraca);
            serverSocket.Blocking = false;

            clientSockets = new List<Socket>();
            readySockets = new List<Socket>();

            while (clientSockets.Count != MaxBrojIgraca)
            {
                readySockets.Clear();
                readySockets.Add(serverSocket);

                Socket.Select(readySockets, null, null, 1000);

                if (readySockets.Count > 0)
                {
                    Socket clientSocket = serverSocket.Accept();
                    clientSocket.Blocking = false;
                    clientSockets.Add(clientSocket);
                    Console.WriteLine($"Novi klijent povezan: {clientSocket.RemoteEndPoint}");
                    Igraci.Add(new Igrac(clientSocket, Igraci.Count, VelicinaTable));
                }
            }

        }

        private static void IncijalizujTable()
        {
            // slanje informacija o igri klijentima
            int brPodmornica = VelicinaTable * VelicinaTable - MaxUzastopnihGresaka;
            string info = $"Velicina table: {VelicinaTable}, maksimalan broj uzastopnih gresaka: {MaxUzastopnihGresaka} broj podmornica: {brPodmornica}";



            foreach (Socket clientSocket in clientSockets)
            {

                Igrac i = new Igrac(Igraci.Find(igrac => igrac.socket == clientSocket));
                Poruka p = new Poruka(i, null, TipPoruke.Obavestenje, info);
                try
                {
                    clientSocket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata klijentu: {i.ime}");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke klijentu {clientSocket.RemoteEndPoint}: {ex.Message}");
                }
            }
            // obrada podmornica od klijenata
            int brojPrimljenihPoruka = 0;

            while (brojPrimljenihPoruka < clientSockets.Count)
            {
                readySockets.Clear();
                foreach (Socket clientSocket in clientSockets)
                {
                    readySockets.Add(clientSocket);
                }

                Socket.Select(readySockets, null, null, 1000);

                foreach (Socket s in readySockets)
                {
                    byte[] buffer = new byte[1024];
                    try
                    {
                        int messLength = s.Receive(buffer);

                        if (messLength > 0)
                        {

                            Poruka p = new Poruka();
                            p = Poruka.DeserializujPoruku(buffer);
                            string[] delovi = p.poruka.Split('|');
                            string ime = delovi[0];
                            string poruka = delovi[1];
                            Console.WriteLine($"Podmornice od {ime}: {poruka}");
                            string[] pozS = poruka.Split(',');
                            List<int> listaPoz = new List<int>();
                            for (int i = 0; i < pozS.Length; i++)
                                listaPoz.Add(int.Parse(pozS[i]));

                            //Svaki socket je vezan za specificnog igraca i moramo potraziti koji je igrac u pitanju
                            foreach (Igrac i in Igraci)
                            {
                                if (i.socket == s)
                                {
                                    i.DodajPodmornice(listaPoz, ime);
                                    break;
                                }
                            }
                            Console.WriteLine("Primljena poruka od:" + ime);
                            brojPrimljenihPoruka++;
                        }
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"Greska u prijemu podmornica od {s.RemoteEndPoint}: {ex.Message}");
                    }
                }
            }
        }

        private static void ZatvoriUticnice()
        {
            foreach (Igrac i in Igraci)
            {
                i.socket.Close();
            }
            serverSocket.Close();
        }

        private static void PosaljiKlijentimaTable()
        {
            foreach (Igrac igrac in Igraci)
            {
                try
                {

                    //Pretvaranje matrice u string

                    Poruka p = new Poruka(null, null, TipPoruke.Obavestenje, igrac.PretvoriUString());
                    igrac.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata klijentu: {igrac.ime} u {igrac.socket.RemoteEndPoint}");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke klijentu {igrac.ime}: {ex.Message}");
                }
            }
        }

        private static void ZapocniIgru()
        {
            int trenutniIgrac = 0;
            do
            {
                Igrac igracNaPotezu = Igraci[trenutniIgrac];
                if (!igracNaPotezu.izgubio)
                {
                    Igrac protivnik = null;
                    ObavestiIgraceONapadacu(igracNaPotezu);


                    if (krajPartije)
                    {
                        GlasanjeNovaIgra();
                        return;
                    }

                    //potez trenutnog igraca
                    string imeProtivnika;
                    do
                    {
                        imeProtivnika = CekajNaPotez(igracNaPotezu);
                        protivnik = Igraci.First(i => i.ime == imeProtivnika);

                        if (protivnik == null || protivnik.izgubio)
                        {
                            Console.WriteLine("Igrac ne postoji ili je vec eliminisan. Pokusaj ponovo");
                            imeProtivnika = null;
                        }
                    } while (imeProtivnika == null);

                    int polje = -1;
                    string poljeProtivnika;

                    bool krajPoteza;
                    do
                    {
                        PosaljiTabluGadjanja(igracNaPotezu, protivnik);
                        do
                        {
                            do
                                poljeProtivnika = CekajNaPotez(igracNaPotezu);
                            while (poljeProtivnika == null);
                            polje = int.Parse(poljeProtivnika);
                            krajPoteza = NapadniProtivnika(igracNaPotezu, imeProtivnika, polje);
                        } while (rezultatGadjanja == 0);


                        PosaljiTabluGadjanja(igracNaPotezu, protivnik);
                        Console.WriteLine("Poslata tablica gadjanja: " + protivnik.PrikaziMatricuGadjana());


                    } while (!krajPoteza); //dok se pogadja polje igra isti igrac
                }
                trenutniIgrac = (trenutniIgrac + 1) % Igraci.Count;
            } while (!krajPartije);
        }
        private static void PosaljiTabluGadjanja(Igrac igrac, Igrac protivnik)
        {
            string tablaGadjanja = protivnik.PrikaziMatricuGadjana();
            Poruka p = new Poruka();
            p.tipPoruke = TipPoruke.Ostalo;
            p.poruka = tablaGadjanja;
            try
            {
                igrac.socket.Send(p.Serializuj());
                //Console.WriteLine("Uspesno poslata tabla gadjanja protivnika");
            }
            catch
            {
                Console.WriteLine("Greska pri slanju table gadjanja protivnika");
            }
        }

        private static string CekajNaPotez(Igrac igracNaPotezu)
        {
            Socket socket = igracNaPotezu.socket;
            Poruka p = new Poruka();
            byte[] buffer = new byte[30000];
            int bytesReceived = 0;

            while (string.IsNullOrEmpty(p.poruka))
            {
                List<Socket> readySockets = new List<Socket> { socket };
                Socket.Select(readySockets, null, null, 1000);

                if (readySockets.Count > 0)
                {
                    try
                    {
                        bytesReceived = socket.Receive(buffer);
                        if (bytesReceived > 0)
                        {
                            p = Poruka.DeserializujPoruku(buffer);
                            Console.WriteLine($"Primljen odgovor od {igracNaPotezu.ime}: {p.poruka}");
                        }
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"Greska pri prijemu podataka: {ex.Message}");
                    }
                }
            }
            return p.poruka;
        }

        private static void ObjaviKrajPartije(Igrac pobednik)
        {
            krajPartije = true;
            string poruka = "Kraj partije! Igrac " + pobednik.ime + " je pobedio!";
            foreach (Igrac i in Igraci)
            {
                try
                {
                    Poruka p = new Poruka();
                    p.poruka = poruka;
                    p.tipPoruke = TipPoruke.GlasanjeNova;
                    i.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslana igracu {i.ime}: {poruka}");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                }
            }

        }
        private static void ObavestiIgraceONapadacu(Igrac igracNaPotezu)
        {
            foreach (Igrac i in Igraci)
            {
                int dostupnihIgraca = 0;
                string poruka = "";

                if (i == igracNaPotezu)
                {
                    poruka = "\nIzaberi koga zelis da napadnes";
                    foreach (Igrac ig in Igraci)
                    {
                        if (ig.ime != i.ime && ig.izgubio == false)
                        {
                            poruka = poruka + "\n\t->" + ig.ime;
                            dostupnihIgraca++;
                        }
                    }
                    if (dostupnihIgraca == 0)
                    {
                        ObjaviKrajPartije(i);
                        return;
                    }

                }
                else
                {
                    poruka = $"\n{igracNaPotezu.ime} je na potezu. Sacekajte..";
                }

                try
                {
                    Poruka p = new Poruka(null, null, i == igracNaPotezu ? TipPoruke.Napad : TipPoruke.Obavestenje, poruka);
                    i.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata igracu {i.ime}: {p.poruka}");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                }
            }
        }

        //Salje svima poruku da li zele novu igru, ukoliko zele ide ispocetka, ukoliko ne kraj
        private static void GlasanjeNovaIgra()
        {
            string poruka = "Unesite 1 ukoliko zelite novu partiju, 2 ukoliko ne zelite:";
            Poruka p = new Poruka();
            p.poruka = poruka;
            p.tipPoruke = TipPoruke.Obavestenje;
            foreach (Igrac i in Igraci)
            {
                try
                {
                    i.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata igracu {i.ime}: {p.poruka}");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                }
            }

            int brojPrimljenihPoruka = 0;

            while (brojPrimljenihPoruka < clientSockets.Count)
            {
                readySockets.Clear();
                foreach (Socket clientSocket in clientSockets)
                {
                    readySockets.Add(clientSocket);
                }

                Socket.Select(readySockets, null, null, 1000);

                foreach (Socket s in readySockets)
                {
                    byte[] buffer = new byte[1024];
                    try
                    {
                        int messLength = s.Receive(buffer);

                        if (messLength > 0)
                        {
                            Poruka odgovor = new Poruka();
                            odgovor = Poruka.DeserializujPoruku(buffer);
                            if (odgovor.poruka.Contains("2"))
                            {
                                NovaIgra = false;
                            }
                            Console.WriteLine("Primljena poruka od:" + s.RemoteEndPoint);
                            brojPrimljenihPoruka++;
                        }
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"Greska u prijemu podmornica od {s.RemoteEndPoint}: {ex.Message}");
                    }
                }
            }
             if (NovaIgra == false)
            {

                p.tipPoruke = TipPoruke.Kraj;

                Console.WriteLine("Program se zavrsava sa radom, pritisnite bilo koje dugme da ga ugasite!");

            }
            else
            {
                krajPartije = false;
                Thread.Sleep(1000);
                p.tipPoruke = TipPoruke.Ostalo;
                Console.WriteLine("Pokrecemo novu partiju");
            }

            foreach (Igrac i in Igraci)
            {
                i.ResetujIgraca();
                try
                {
                    i.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata igracu {i.ime}: Kraj");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                }
            }
            if (NovaIgra == false) Environment.Exit(0);
        }

        //Igrac napada dok ne napravi Maksimalan broj uzastopnih gresaka ili dok ne pobedi
        // 1 Bira koga ce napasti
        // 2 Napada 
        // 3 Server obavestava kako je prosao napad
        // 4 ukoliko ima jos napada ide na korak 2
        private static bool NapadniProtivnika(Igrac trenutniIgrac, string imeProtivnika, int polje)
        {

            Poruka p = new Poruka();
            Igrac Protivnik = Igraci.Find(igrac => igrac.ime == imeProtivnika);
            bool krajPoteza = false;
            string poruka;

            rezultatGadjanja = Protivnik.AzurirajMatricu(polje);
            string info = "";
            switch (rezultatGadjanja)
            {
                case 0:
                    poruka = "Vec napadnuto polje!";
                    info = $"\nPolje {polje} je vec gadjano. Izaberite drugo:";
                    p.tipPoruke = TipPoruke.Ponovi;
                    break;
                case 1: //kada napadac promasi, sledeci igrac dobija potez. Ukoliko je napadac stigao do max broja gresaka, za njega je partija
                        //zavrsena i ceka rezultat. On do kraja partije ne moze da gadja nikoga, niti iko njega (zadatak 8)
                    poruka = "Promasaj!";
                    trenutniIgrac.brojPromasaja++;
                    info = $"\nBroj uzastopnih gresaka do sad je " +
                            $"{trenutniIgrac.brojPromasaja}, maksimalan broj je: {MaxUzastopnihGresaka}\n";
                    p.tipPoruke = TipPoruke.Promasaj;
                    if (trenutniIgrac.brojPromasaja == MaxUzastopnihGresaka)
                    {
                        p.tipPoruke = TipPoruke.Izgubio;
                        trenutniIgrac.izgubio = true;
                    }
                    krajPoteza = true;

                    break;
                case 2: //kada napadac potopi podmornicu protivniku, dobija sansu da gadja opet. Ukoliko je potopio sve podmornice, dobija sledeci potez
                        //da gadja nekog preostalog. (ili tu moze da se zavrsi njegov potez zbog jednostavnosti)
                    trenutniIgrac.brojPromasaja = 0;
                    poruka = "Pogodak!";
                    info = $"\nPreostalo brodova protivniku je: {Protivnik.pozicije.Count}\n";
                    p.tipPoruke = TipPoruke.Pogodak;
                    krajPoteza = Protivnik.pozicije.Count == 0 ? true : false;
                    break;
                default:
                    poruka = "Greska!";
                    break;
            }

            p.poruka = poruka + info;
            p.NaPotezu = new Igrac(trenutniIgrac);
            p.Napadnut = new Igrac(Protivnik);

            //Posalji poruku Igracu kako je prosao potez
            try
            {
                trenutniIgrac.socket.Send(p.Serializuj());
                Console.WriteLine($"Poruka poslata igracu {trenutniIgrac.ime}: {p.poruka}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greska pri slanju poruke igracu {trenutniIgrac.ime}: {ex.Message}");
            }

            ObavestiOstaleONapadu(trenutniIgrac, Protivnik, poruka);

            return krajPoteza;
        }

        private static void ObavestiOstaleONapadu(Igrac trenutniIgrac, Igrac protivnik, string ishod)
        {
            Thread.Sleep(300);
            string poruka = $"Igrac {trenutniIgrac.ime} -> {protivnik.ime}: {ishod}";
            Console.WriteLine(poruka);//ispis i na serveru
            Poruka p = new Poruka();
            foreach (Igrac i in Igraci)
            {
                if (i != trenutniIgrac)
                {
                    if (i == protivnik)
                    {
                        try
                        {
                            if (protivnik.pozicije.Count == 0)
                            {
                                protivnik.izgubio = true;
                            }
                            p.tipPoruke = TipPoruke.Napadnut;
                            p.poruka = trenutniIgrac.ime + " je gadjao vas i odigrao: " + ishod + "\nVasa tabla sada izgleda ovako:\n" + protivnik.PrikaziMatricu();
                            p.Napadnut = protivnik;
                            i.socket.Send(p.Serializuj());
                            Console.WriteLine($"Poruka poslata igracu {i.ime}: {p.poruka}");
                        }
                        catch
                        {
                            Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}");
                        }
                    }
                    else
                    {
                        try
                        {
                            p.tipPoruke = TipPoruke.Obavestenje;
                            p.poruka = poruka;
                            i.socket.Send(p.Serializuj());
                            Console.WriteLine($"Poruka poslata igracu {i.ime}: {p.poruka}");
                        }
                        catch (SocketException ex)
                        {
                            Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}
