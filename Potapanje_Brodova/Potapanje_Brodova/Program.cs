using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ConstrainedExecution;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

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
        private static int sekundeCekanjaNaUnos = 14;

        static void Main(string[] args)
        {
            Console.WriteLine("Dobrodosli na server!");
            do
            {
                Console.WriteLine("Unesite broj igraca koji ce da igraju (min 2):");
                int.TryParse(Console.ReadLine(), out MaxBrojIgraca);
            }
            while (MaxBrojIgraca <= 1);

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
            } while (MaxUzastopnihGresaka > VelicinaTable * VelicinaTable - 1 || MaxUzastopnihGresaka < 1);
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
                    clientSocket.Blocking = true;
                    clientSockets.Add(clientSocket);
                    Console.WriteLine($"Novi klijent povezan: {clientSocket.RemoteEndPoint}");
                    Igraci.Add(new Igrac(clientSocket, Igraci.Count, VelicinaTable));
                }
            }
        }

        private static void IncijalizujTable()
        {
            int brPodmornica = 10;
            Logger.ZapocetaNovaIgra();
            string info = $"\nVelicina table: {VelicinaTable}\nBroj podmornica: {brPodmornica}\nMaksimalan broj uzastopnih gresaka: {MaxUzastopnihGresaka}";

            foreach (Socket clientSocket in clientSockets)
            {
                Igrac i = new Igrac(Igraci.Find(igrac => igrac.socket == clientSocket));
                Poruka p = new Poruka(i, null, TipPoruke.Obavestenje, info);
                try
                {
                    clientSocket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata klijentu: {i.ime} Tip poruke: {p.tipPoruke}");
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
                                    Logger.LogPodmornice(ime, $"Postavljeno je {podmornice.Count} podmornica");
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
                        Thread.Sleep(2000);
                        GlasanjeNovaIgra();
                        return;
                    }

                    // ODABIR PROTIVNIKA SA TIMEROM
                    string imeProtivnika = null;
                    bool protivnikOdabran = false;
                    bool igracIzabraoProtivnika = false;
                    bool botOdigraoPolje = false;

                    do
                    {
                        try
                        {
                            imeProtivnika = CekajNaPotezSaTimeoutom(igracNaPotezu, sekundeCekanjaNaUnos);

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
                                protivnikOdabran = true;
                                igracIzabraoProtivnika = true;
                            }
                        }
                        catch (TimeoutException)
                        {
                            igracNaPotezu.botTimeoutCount++;
                            Console.WriteLine($"\nTAJMER ISTEKAO\nPoruka poslata klijentu: {igracNaPotezu.ime}");

                            if (igracNaPotezu.botTimeoutCount == 1)
                            {
                                // Prvo BOT igranje - upozorenje
                                string upozorenje = $"UPOZORENJE: {igracNaPotezu.ime}!\nBOT je odigrao potez, imaš još 2 šanse!";
                                Logger.LogIgrac(igracNaPotezu.ime, "BOT OBAVEŠTENJE", "Tajmer istekao - BOT je odigrao prvi put");

                                try
                                {
                                    Poruka upozorenjePoruka = new Poruka(null, null, TipPoruke.Obavestenje, upozorenje);
                                    igracNaPotezu.socket.Send(upozorenjePoruka.Serializuj());
                                    Console.WriteLine("Prvo upozorenje na biranju protivnika: \n" + upozorenje);
                                }
                                catch { }

                                imeProtivnika = BotHelper.IzaberiRandomIgraca(Igraci, igracNaPotezu);
                                if (imeProtivnika != null)
                                {
                                    Console.WriteLine($"BOT je odabrao protivnika: {imeProtivnika}");
                                    Logger.LogIgrac(igracNaPotezu.ime, "BOT ODABIR PROTIVNIKA", $"Protivnik: {imeProtivnika}");
                                    protivnikOdabran = true;
                                }
                            }
                            else if (igracNaPotezu.botTimeoutCount == 2)
                            {
                                // Drugo BOT igranje - novo upozorenje
                                string upozorenje = $"UPOZORENJE: {igracNaPotezu.ime}!\nNa sledećem isteku vremena biće eliminisan/a!";
                                Logger.LogIgrac(igracNaPotezu.ime, "BOT UPOZORENJE", "Tajmer istekao drugi put - sledeće je kraj!");

                                try
                                {
                                    Poruka upozorenjePoruka = new Poruka(null, null, TipPoruke.Obavestenje, upozorenje);
                                    igracNaPotezu.socket.Send(upozorenjePoruka.Serializuj());
                                    Console.WriteLine("Drugo upozorenje na biranju protivnika: \n" + upozorenje);
                                }
                                catch { }

                                imeProtivnika = BotHelper.IzaberiRandomIgraca(Igraci, igracNaPotezu);
                                if (imeProtivnika != null)
                                {
                                    Console.WriteLine($"BOT je odabrao protivnika: {imeProtivnika}");
                                    Logger.LogIgrac(igracNaPotezu.ime, "BOT ODABIR PROTIVNIKA", $"Novi protivnik: {imeProtivnika}");
                                    protivnikOdabran = true;
                                }
                            }
                            else if (igracNaPotezu.botTimeoutCount >= 3)
                            {
                                // Treci BOT timeout - igrac je eliminisan
                                igracNaPotezu.izgubio = true;
                                Console.WriteLine($"Eliminisan igrac {igracNaPotezu.ime}");
                                Logger.LogIgrac(igracNaPotezu.ime, "ELIMINISAN/A", "Tri puta isteklo vreme!");

                                protivnikOdabran = true;
                            }
                        }

                    } while (!protivnikOdabran);

                    // Ako je igrac eliminisan na izboru protivnika, preskoči
                    if (igracNaPotezu.izgubio)
                    {
                        ObavestiOstaleOElim(igracNaPotezu);
                        trenutniIgrac = (trenutniIgrac + 1) % Igraci.Count;
                        continue;
                    }

                    // Provjeri da li je protivnik validan
                    if (string.IsNullOrEmpty(imeProtivnika))
                    {
                        Console.WriteLine("GRESKA: Nije odabran protivnik!");
                        trenutniIgrac = (trenutniIgrac + 1) % Igraci.Count;
                        continue;
                    }

                    Igrac napadnuti = Igraci.FirstOrDefault(i => i.ime == imeProtivnika);

                    if (napadnuti == null || napadnuti.izgubio)
                    {
                        Console.WriteLine($"GRESKA: Igrac '{imeProtivnika}' nije dostupan!");
                        trenutniIgrac = (trenutniIgrac + 1) % Igraci.Count;
                        continue;
                    }

                    // Ako je BOT odabrao protivnika, BOT mora odmah odigrati i polje
                    if (igracIzabraoProtivnika == false)
                    {
                        int randomPolje = BotHelper.IzaberiRandomPolje(VelicinaTable);
                        Console.WriteLine($"BOT je odigrao polje: {randomPolje}\n");
                        Logger.LogIgrac(igracNaPotezu.ime, "BOT POTEZ POLJE", $"Polje: {randomPolje}");

                        NapadniProtivnika(igracNaPotezu, imeProtivnika, randomPolje);
                        
                        trenutniIgrac = (trenutniIgrac + 1) % Igraci.Count;
                        Thread.Sleep(1000);
                        continue;
                    }


                    // ODABIR POLJA SA TAJMEROM - IGRAC JE SAM IZABRAO PROTIVNIKA
                    int polje = -1;
                    string poljeProtivnika = "";
                    bool krajPoteza = false;

                    do
                    {
                        PosaljiTabluGadjanja(igracNaPotezu, napadnuti);
                        do
                        {
                            do
                            {
                                try
                                {
                                    poljeProtivnika = CekajNaPotezSaTimeoutom(igracNaPotezu, sekundeCekanjaNaUnos);
                                }
                                catch (TimeoutException)
                                {
                                    igracNaPotezu.botTimeoutCount++;
                                    Console.WriteLine($"\nTAJMER ISTEKAO\nPoruka poslata klijentu: {igracNaPotezu.ime}");
                                    botOdigraoPolje = true;
                                    if (igracNaPotezu.botTimeoutCount == 1)
                                    {
                                        // Prvo BOT igranje - upozorenje
                                        string upozorenje = $"UPOZORENJE: {igracNaPotezu.ime}!\nBOT je odigrao potez, imaš još 2 šanse!";
                                        Logger.LogIgrac(igracNaPotezu.ime, "BOT UPOZORENJE", "Tajmer istekao pri izboru polja");

                                        try
                                        {
                                            Poruka upozorenjePoruka = new Poruka(null, null, TipPoruke.Obavestenje, upozorenje);
                                            igracNaPotezu.socket.Send(upozorenjePoruka.Serializuj());
                                            Console.WriteLine("Prvo upozorenje na odabiru polja: \n" + upozorenje);
                                        }
                                        catch { }

                                        poljeProtivnika = BotHelper.IzaberiRandomPolje(VelicinaTable).ToString();
                                        Console.WriteLine($"BOT je odigrao polje: {poljeProtivnika}\n");
                                        Logger.LogIgrac(igracNaPotezu.ime, "BOT POTEZ", $"Polje: {poljeProtivnika}");
                                        break;
                                    }
                                    else if (igracNaPotezu.botTimeoutCount == 2)
                                    {
                                        // Drugo BOT igranje - novo upozorenje
                                        string upozorenje = $"UPOZORENJE: {igracNaPotezu.ime}!\nNa sledećem isteku vremena biće eliminisan/a!";
                                        Logger.LogIgrac(igracNaPotezu.ime, "BOT UPOZORENJE", "Tajmer istekao drugi put pri izboru polja!");

                                        try
                                        {
                                            Poruka upozorenjePoruka = new Poruka(null, null, TipPoruke.Obavestenje, upozorenje);
                                            igracNaPotezu.socket.Send(upozorenjePoruka.Serializuj());
                                            Console.WriteLine("Drugo upozorenje na odabiru polja: \n" + upozorenje);
                                        }
                                        catch { }

                                        poljeProtivnika = BotHelper.IzaberiRandomPolje(VelicinaTable).ToString();
                                        Console.WriteLine($"BOT je odigrao polje: {poljeProtivnika}\n");
                                        Logger.LogIgrac(igracNaPotezu.ime, "BOT POTEZ", $"Novo polje: {poljeProtivnika}");
                                        break;
                                    }
                                    else if (igracNaPotezu.botTimeoutCount >= 3)
                                    {
                                        igracNaPotezu.izgubio = true;
                                        Console.WriteLine($"Eliminisan igrac {igracNaPotezu.ime}");
                                        Logger.LogIgrac(igracNaPotezu.ime, "ELIMINISAN/A", "Tri puta isteklo vrijeme pri izboru polja!");

                                        poljeProtivnika = null;
                                        break;
                                    }
                                }

                            } while (string.IsNullOrEmpty(poljeProtivnika));

                            // Ako je igrac eliminisan pri izboru polja
                            if (igracNaPotezu.izgubio)
                            {
                                krajPoteza = true;
                                break;
                            }
                            
                            //IGRAC IGRA CEO POTEZ
                            if (poljeProtivnika != null && poljeProtivnika.StartsWith("SUPER|"))
                            {
                                krajPoteza = NapadniProtivnika(igracNaPotezu, imeProtivnika, poljeProtivnika);
                            }
                            else if (poljeProtivnika != null)
                            {
                                if (!int.TryParse(poljeProtivnika.Trim(), out polje))
                                {
                                    poljeProtivnika = "";
                                    continue;
                                }
                                krajPoteza = NapadniProtivnika(igracNaPotezu, imeProtivnika, polje);
                            }
                            else
                            {
                                krajPoteza = true;
                            }

                            if(botOdigraoPolje)
                            {
                                krajPoteza = true;
                            }
                        } while (rezultatGadjanja == 0);

                        if (!krajPoteza && !igracNaPotezu.izgubio)
                        {
                            PosaljiTabluGadjanja(igracNaPotezu, napadnuti);
                            Console.WriteLine("Poslata tablica gadjanja: \n" + napadnuti.PrikaziMatricuGadjana());
                        }

                    } while (!krajPoteza);

                    if (igracNaPotezu.izgubio)
                    {
                        ObavestiOstaleOElim(igracNaPotezu);
                    }

                    Thread.Sleep(1000);
                }

                trenutniIgrac = (trenutniIgrac + 1) % Igraci.Count;

            } while (!krajPartije);
        }

        private static void ObavestiOstaleOElim(Igrac eliminisan)
        {
            foreach (Igrac i in Igraci)
            {
                if (i != eliminisan && !i.izgubio)
                {
                    if (eliminisan.botTimeoutCount >= 3)
                    {
                        try
                        {
                            Poruka p = new Poruka(null, null, TipPoruke.Obavestenje,
                                $" {eliminisan.ime} je eliminisan/a zbog isteka vremena!");
                            i.socket.Send(p.Serializuj());
                            Console.WriteLine($"Poruka poslata igracu {i.ime}: {p.poruka}");
                        }
                        catch { }
                    }
                    
                }
            }
        }

        private static string CekajNaPotezSaTimeoutom(Igrac igracNaPotezu, int seconds)
        {
            Socket socket = igracNaPotezu.socket;
            byte[] buffer = new byte[30000];

            int timeoutMicroSeconds = seconds * 1000000;
            List<Socket> readySockets = new List<Socket> { socket };

            Socket.Select(readySockets, null, null, timeoutMicroSeconds);

            if (readySockets.Count > 0)
            {
                try
                {
                    int bytesReceived = socket.Receive(buffer);
                    if (bytesReceived > 0)
                    {
                        Poruka p = Poruka.DeserializujPoruku(buffer);
                        Console.WriteLine($"Primljen odgovor od {igracNaPotezu.ime}: {p.poruka}");
                        return p.poruka;
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri prijemu podataka: {ex.Message}");
                    return null;
                }
            }

            // Tajmaut istekao
            throw new TimeoutException($"Tajmer istekao za {igracNaPotezu.ime}");
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

        private static void ObjaviKrajPartije(Igrac pobednik)
        {
            krajPartije = true;
            Logger.LogKrajPartije(pobednik.ime);
            string poruka = "Kraj partije! Igrac " + pobednik.ime + " je pobedio!";
            foreach (Igrac i in Igraci)
            {
                try
                {
                    Poruka p = new Poruka();
                    p.poruka = poruka;
                    p.tipPoruke = TipPoruke.GlasanjeNova;
                    i.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata igracu {i.ime}: {poruka} Tip poruke: {p.tipPoruke}");
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
                    if (i.izgubio == false)
                        poruka = $"{igracNaPotezu.ime} je na potezu. Sacekajte..";
                }

                try
                {
                    if (i == igracNaPotezu || !i.izgubio)
                    {
                        Poruka p = new Poruka(
                            null,
                            null,
                            i == igracNaPotezu ? TipPoruke.Napad : TipPoruke.Obavestenje,
                            poruka);

                        i.socket.Send(p.Serializuj());
                        Console.WriteLine($"Poruka poslata igracu {i.ime}: {p.poruka} Tip poruke: {p.tipPoruke}");
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greska pri slanju poruke igracu {i.ime}: {ex.Message}");
                }
            }
        }

        private static void GlasanjeNovaIgra()
        {
            Poruka p = new Poruka();
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
                Thread.Sleep(2000);
                p.tipPoruke = TipPoruke.Ostalo;
                Console.WriteLine("Pokrecemo novu partiju");
            }

            foreach (Igrac i in Igraci)
            {
                i.ResetujIgraca();
                try
                {
                    i.socket.Send(p.Serializuj());
                    Console.WriteLine($"Poruka poslata igracu {i.ime} Tip poruke: {p.tipPoruke}");
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

        private static bool NapadniProtivnika(Igrac trenutniIgrac, string imeProtivnika, dynamic polje)
        {
            Poruka p = new Poruka();
            Igrac Protivnik = Igraci.Find(igrac => igrac.ime == imeProtivnika);
            bool krajPoteza = false;
            string poruka;

            bool jeSuperPotez = polje is string && polje.ToString().StartsWith("SUPER|");
            string poljeString = polje.ToString();

            if (!jeSuperPotez)
            {
                int poljeInt = (int)(object)polje;
                rezultatGadjanja = Protivnik.AzurirajMatricu(poljeInt);
            }
            else
            {
                // super potez
                rezultatGadjanja = 0;
                int brojPogodaka = 0;
                int brojPotopljenih = 0;

                string[] poljaSplit = poljeString.Split('|');
                if (poljaSplit.Length == 2)
                {
                    string[] poljaBrojevi = poljaSplit[1].Split(',');
                    foreach (string p_str in poljaBrojevi)
                    {
                        if (int.TryParse(p_str, out int trenutnoPolje))
                        {
                            int rezultat = Protivnik.AzurirajMatricu(trenutnoPolje);
                            if (rezultat == 2) brojPogodaka++;
                            if (rezultat == 3) brojPotopljenih++;
                        }
                    }
                }

                if (brojPotopljenih > 0)
                    rezultatGadjanja = 3;
                else if (brojPogodaka > 0)
                    rezultatGadjanja = 2;
                else
                    rezultatGadjanja = 1;
            }

            string info = "";

            switch (rezultatGadjanja)
            {
                case 0:
                    poruka = "Vec napadnuto polje!";
                    info = $"\nPolje {polje} je vec gadjano. Izaberite drugo:";
                    p.tipPoruke = TipPoruke.Ponovi;
                    Logger.LogPotez(trenutniIgrac.ime, imeProtivnika, polje.ToString(), poruka);
                    break;
                case 1:
                    poruka = jeSuperPotez ? "Super promasaj!" : "Promasaj!";
                    trenutniIgrac.brojPromasaja++;
                    info = $"\nBroj uzastopnih gresaka do sad je {trenutniIgrac.brojPromasaja}, maksimalan broj je: {MaxUzastopnihGresaka}\n";
                    p.tipPoruke = TipPoruke.Promasaj;
                    Logger.LogPotez(trenutniIgrac.ime, imeProtivnika, polje.ToString(), poruka);
                    if (trenutniIgrac.brojPromasaja == MaxUzastopnihGresaka)
                    {
                        p.tipPoruke = TipPoruke.Izgubio;
                        trenutniIgrac.izgubio = true;
                        Logger.LogIgrac(trenutniIgrac.ime, "IZGUBIO", $"Maksimalan broj gresaka ({MaxUzastopnihGresaka})");
                    }
                    krajPoteza = true;
                    break;

                case 2:
                    trenutniIgrac.brojPromasaja = 0;
                    poruka = jeSuperPotez ? "Super pogodak!" : "Pogodak!";
                    info = $"\nPreostalo podmornica protivniku je: {Protivnik.GetBrojPreostalihPodmornica()}\n";
                    p.tipPoruke = TipPoruke.Pogodak;
                    Logger.LogPotez(trenutniIgrac.ime, imeProtivnika, polje.ToString(), poruka);
                    krajPoteza = Protivnik.DaLiSuSvePodmornicePotonjene() ? true : false;
                    break;

                case 3:
                    trenutniIgrac.brojPromasaja = 0;

                    string imePodmornice = "";
                    if (!jeSuperPotez)
                    {
                        int poljeInt = (int)(object)polje;
                        Podmornica potopljena = Protivnik.GetPodmornicaNaPoziciji(poljeInt);
                        imePodmornice = potopljena != null ? $"({potopljena.GetDuzina()}x1)" : "";
                    }

                    poruka = jeSuperPotez ? $"Super pogodak! Potopljene podmornice!" : $"Potopljena podmornica! {imePodmornice}";
                    info = $"\nPreostalo podmornica protivniku je: {Protivnik.GetBrojPreostalihPodmornica()}\n";
                    p.tipPoruke = TipPoruke.Pogodak;
                    Logger.LogPotez(trenutniIgrac.ime, imeProtivnika, polje.ToString(), poruka);

                    if (Protivnik.DaLiSuSvePodmornicePotonjene())
                    {
                        Protivnik.izgubio = true;
                        krajPoteza = true;
                        Logger.LogIgrac(imeProtivnika, "POTOPLJEN", $"Sve podmornice su potopljene");
                    }

                    break;

                default:
                    poruka = "Greska!";
                    Logger.LogGreska("NapadniProtivnika", new Exception($"Nepoznat rezultat: {rezultatGadjanja}"));
                    break;
            }

            p.poruka = poruka + info;
            p.NaPotezu = new Igrac(trenutniIgrac);
            p.Napadnut = new Igrac(Protivnik);

            try
            {
                trenutniIgrac.socket.Send(p.Serializuj());
                Console.WriteLine($"Poruka poslata igracu {trenutniIgrac.ime}. Tip poruke: {p.tipPoruke}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greska pri slanju poruke igracu {trenutniIgrac.ime}: {ex.Message}");
            }

            ObavestiOstaleONapadu(trenutniIgrac, Protivnik, poruka);

            return krajPoteza;
        }
    }
}