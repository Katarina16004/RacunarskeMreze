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
        private static int VelicinaTable = 10;
        private static int MaxUzastopnihGresaka = 0;
        private static bool NovaIgra = true;
        private static bool krajPartije = false;
        private static int rezultatGadjanja;

        static void Main(string[] args)
        {
            Console.WriteLine("Dobrodosli na server!");
            do
            {
                Console.WriteLine("Unesite broj igraca koji ce da igraju:");
                int.TryParse(Console.ReadLine(), out MaxBrojIgraca);
            }
            while (MaxBrojIgraca < 1);

            Console.WriteLine("Cekam prijave Igraca...");

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
                Console.WriteLine("Unesite maksimalan broj uzastopnih gresaka:");
                int.TryParse((string)Console.ReadLine(), out MaxUzastopnihGresaka);
            } while (MaxUzastopnihGresaka > VelicinaTable * VelicinaTable - 1);
        }

        private static void UspostaviTCPKonekciju()
        {
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
            int brPodmornica = 10;
            string info = $"\nVelicina table: {VelicinaTable}\nBroj podmornica: {brPodmornica}\nMaksimalan broj uzastopnih gresaka: {MaxUzastopnihGresaka}";

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
                    byte[] buffer = new byte[4096];
                    try
                    {
                        int messLength = s.Receive(buffer);

                        if (messLength > 0)
                        {
                            Poruka p = Poruka.DeserializujPoruku(buffer);
                            string[] delovi = p.poruka.Split('|');
                            string ime = delovi[0];
                            string podmornicaString = delovi[1];
                            Console.WriteLine($"Podmornice od {ime}: {podmornicaString}");

                            string[] podmornicaGrupe = podmornicaString.Split(';');
                            List<Podmornica> podmornice = new List<Podmornica>();

                            foreach (var grupa in podmornicaGrupe)
                            {
                                if (string.IsNullOrWhiteSpace(grupa))
                                    continue;

                                string[] detalji = grupa.Split(',');
                                if (detalji.Length >= 3)
                                {
                                    int tip = int.Parse(detalji[0]);
                                    bool horizontalna = detalji[1].Trim() == "H";

                                    List<int> pozicije = new List<int>();
                                    for (int i = 2; i < detalji.Length; i++)
                                    {
                                        pozicije.Add(int.Parse(detalji[i]));
                                    }

                                    Podmornica podmornica = new Podmornica((TipPodmornice)tip, pozicije, horizontalna);
                                    podmornice.Add(podmornica);
                                }
                            }

                            foreach (Igrac igrac in Igraci)
                            {
                                if (igrac.socket == s)
                                {
                                    foreach (var podmornica in podmornice)
                                    {
                                        igrac.DodajPodmornicu(podmornica, out string greska);
                                    }
                                    igrac.ime = ime;
                                    Console.WriteLine($"Podmornice od {ime} prihvaćene ({podmornice.Count}/10)");
                                    brojPrimljenihPoruka++;
                                    break;
                                }
                            }
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
                if (i.socket != null && i.socket.Connected)
                    i.socket.Close();
            }
            if (serverSocket != null)
                serverSocket.Close();
        }

        private static void PosaljiKlijentimaTable()
        {
            foreach (Igrac igrac in Igraci)
            {
                try
                {
                    Poruka p = new Poruka(null, null, TipPoruke.Obavestenje, igrac.PretvoriUString());
                    igrac.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata klijentu: {igrac.ime} - tabla");
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
                    ObavestiIgraceONapadacu(igracNaPotezu);

                    if (krajPartije)
                    {
                        GlasanjeNovaIgra();
                        return;
                    }

                    string imeProtivnika;
                    do
                    {
                        imeProtivnika = CekajNaPotez(igracNaPotezu);
                        if (string.IsNullOrEmpty(imeProtivnika))
                            continue;

                        Igrac protivnik = Igraci.FirstOrDefault(i => i.ime == imeProtivnika);

                        if (protivnik == null || protivnik.izgubio)
                        {
                            Console.WriteLine("Igrac ne postoji ili je vec eliminisan. Pokusaj ponovo");
                            imeProtivnika = null;
                        }
                        else
                        {
                            break;
                        }
                    } while (imeProtivnika == null);

                    Igrac napadnuti = Igraci.First(i => i.ime == imeProtivnika);
                    int polje = -1;
                    string poljeProtivnika;

                    bool krajPoteza;
                    do
                    {
                        PosaljiTabluGadjanja(igracNaPotezu, napadnuti);
                        do
                        {
                            do
                            {
                                poljeProtivnika = CekajNaPotez(igracNaPotezu);
                            } while (string.IsNullOrEmpty(poljeProtivnika));

                            polje = int.Parse(poljeProtivnika);
                            krajPoteza = NapadniProtivnika(igracNaPotezu, imeProtivnika, polje);
                        } while (rezultatGadjanja == 0);

                        PosaljiTabluGadjanja(igracNaPotezu, napadnuti);
                        Console.WriteLine("Poslata tablica gadjanja: " + napadnuti.PrikaziMatricuGadjana());

                    } while (!krajPoteza);
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
                        return null;
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
                    poruka = "Izaberi koga zelis da napadnes";
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
                    poruka = $"{igracNaPotezu.ime} je na potezu. Sacekajte..";
                }

                try
                {
                    Poruka p = new Poruka(null, null, i == igracNaPotezu ? TipPoruke.Napad : TipPoruke.Obavestenje, poruka);
                    i.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata igracu {i.ime}");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                }
            }
        }

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
                        Console.WriteLine($"Greska u prijemu odgovora od {s.RemoteEndPoint}: {ex.Message}");
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
                    Console.WriteLine($"Poruka poslata igracu {i.ime}");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                }
            }
            if (NovaIgra == false) Environment.Exit(0);
        }

        private static void ObavestiOstaleONapadu(Igrac trenutniIgrac, Igrac protivnik, string ishod)
        {
            Thread.Sleep(300);
            Poruka p = new Poruka();

            foreach (Igrac i in Igraci)
            {
                if (i != trenutniIgrac)
                {
                    if (i == protivnik)
                    {
                        try
                        {
                            if (protivnik.DaLiSuSvePodmornicePotonjene())
                            {
                                protivnik.izgubio = true;
                            }
                            p.tipPoruke = TipPoruke.Napadnut;

                            string porukaSaTabelom = trenutniIgrac.ime + " je gadjao vas i odigrao: " + ishod +
                                                   "\n\nVasa tabla sada izgleda ovako:\n" + protivnik.PrikaziMatricu();

                            int preostalo = protivnik.GetBrojPreostalihPodmornica();
                            porukaSaTabelom += $"\n\nPreostalo vam je: {preostalo} brodova!";

                            p.poruka = porukaSaTabelom;
                            p.Napadnut = protivnik;
                            i.socket.Send(p.Serializuj());
                            Console.WriteLine($"Poruka poslata igracu {i.ime}: Napadnut");
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
                            string porukaSaDetaljem = $"{trenutniIgrac.ime} je gadjao {protivnik.ime}\n" +
                                                    $"Rezultat: {ishod}\n" +
                                                    $"Preostalo podmornica: {protivnik.GetBrojPreostalihPodmornica()}/10";

                            p.poruka = porukaSaDetaljem;
                            i.socket.Send(p.Serializuj());
                            Console.WriteLine($"Poruka poslata igracu {i.ime}: Obavestenje");
                        }
                        catch (SocketException ex)
                        {
                            Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                        }
                    }
                }
            }
        }

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
                case 1:
                    poruka = "Promasaj!";
                    trenutniIgrac.brojPromasaja++;
                    info = $"\nBroj uzastopnih gresaka do sad je {trenutniIgrac.brojPromasaja}, maksimalan broj je: {MaxUzastopnihGresaka}\n";
                    p.tipPoruke = TipPoruke.Promasaj;
                    if (trenutniIgrac.brojPromasaja == MaxUzastopnihGresaka)
                    {
                        p.tipPoruke = TipPoruke.Izgubio;
                        trenutniIgrac.izgubio = true;
                    }
                    krajPoteza = true;
                    break;

                case 2:
                    trenutniIgrac.brojPromasaja = 0;
                    poruka = "Pogodak!";
                    info = $"\nPreostalo podmornica protivniku je: {Protivnik.GetBrojPreostalihPodmornica()}\n";
                    p.tipPoruke = TipPoruke.Pogodak;
                    krajPoteza = Protivnik.DaLiSuSvePodmornicePotonjene() ? true : false;
                    break;

                case 3:
                    trenutniIgrac.brojPromasaja = 0;
                    Podmornica potopljena = Protivnik.GetPodmornicaNaPoziciji(polje);
                    string imePodmornice = potopljena != null ? $"({potopljena.GetDuzina()}x1)" : "";
                    poruka = $"Potopljena podmornica! {imePodmornice}";
                    info = $"\nPreostalo podmornica protivniku je: {Protivnik.GetBrojPreostalihPodmornica()}\n";
                    p.tipPoruke = TipPoruke.Pogodak;
                    krajPoteza = Protivnik.DaLiSuSvePodmornicePotonjene() ? true : false;
                    break;

                default:
                    poruka = "Greska!";
                    break;
            }

            p.poruka = poruka + info;
            p.NaPotezu = new Igrac(trenutniIgrac);
            p.Napadnut = new Igrac(Protivnik);

            try
            {
                trenutniIgrac.socket.Send(p.Serializuj());
                Console.WriteLine($"Poruka poslata igracu {trenutniIgrac.ime}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greska pri slanju poruke igracu {trenutniIgrac.ime}: {ex.Message}");
            }

            ObavestiOstaleONapadu(trenutniIgrac, Protivnik, poruka);

            if (!krajPoteza)
            {
                Thread.Sleep(500);
                PosaljiTabluGadjanja(trenutniIgrac, Protivnik);

                Thread.Sleep(300);
                int sledeci = (Igraci.IndexOf(trenutniIgrac) + 1) % Igraci.Count;
                Igrac sledreciIgrac = Igraci[sledeci];

                string obavestenjeNaPotezu = $"{sledreciIgrac.ime} je na potezu. Sacekajte..";
                Poruka obavestenje = new Poruka();
                obavestenje.tipPoruke = TipPoruke.Obavestenje;
                obavestenje.poruka = obavestenjeNaPotezu;

                foreach (Igrac igrac in Igraci)
                {
                    try
                    {
                        igrac.socket.Send(obavestenje.Serializuj());
                        Console.WriteLine($"Obavestenje poslato igracu {igrac.ime}: {obavestenjeNaPotezu}");
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"Greska pri slanju obavestenja igracu {igrac.ime}: {ex.Message}");
                    }
                }
            }

            return krajPoteza;
        }
    }
}