using System;
using System.IO;
using System.Text;

namespace Server
{
    public class Logger
    {
        private static readonly string LogDir = "Logs";
        private static string CurrentLogFile;
        private static readonly object lockObject = new object();

        public static void ZapocetaNovaIgra()
        {
            lock (lockObject)
            {
                if (!Directory.Exists(LogDir))
                {
                    Directory.CreateDirectory(LogDir);
                }

                // novi fajl za svaku novu igru
                CurrentLogFile = Path.Combine(LogDir, $"game_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss_fff}.txt");

                try
                {
                    using (StreamWriter writer = new StreamWriter(CurrentLogFile, true, Encoding.UTF8))
                    {
                        writer.WriteLine("═══════════════════════════════════════════════════════════════");
                        writer.WriteLine($"Početa partija: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine("═══════════════════════════════════════════════════════════════\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška pri kreiranju log fajla: {ex.Message}");
                }
            }
        }

        public static void LogPotez(string igrac, string protivnik, string potez, string rezultat)
        {
            lock (lockObject)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(CurrentLogFile, true, Encoding.UTF8))
                    {
                        writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {igrac} -> {protivnik}: {potez}");
                        writer.WriteLine($"  Rezultat: {rezultat}");
                        writer.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška pri pisanju u log fajl: {ex.Message}");
                }
            }
        }

        public static void LogIgrac(string igrac, string akcija, string detalji = "")
        {
            lock (lockObject)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(CurrentLogFile, true, Encoding.UTF8))
                    {
                        string poruka = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {igrac}: {akcija}";
                        if (!string.IsNullOrEmpty(detalji))
                            poruka += $" - {detalji}";
                        writer.WriteLine(poruka);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška pri pisanju u log fajl: {ex.Message}");
                }
            }
        }
        public static void LogKrajPartije(string pobednik)
        {
            lock (lockObject)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(CurrentLogFile, true, Encoding.UTF8))
                    {
                        writer.WriteLine("\n═══════════════════════════════════════════════════════════════");
                        writer.WriteLine($"Kraj partije: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"Pobednik: {pobednik}");
                        writer.WriteLine("═══════════════════════════════════════════════════════════════\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška pri pisanju u log fajl: {ex.Message}");
                }
            }
        }

        public static void LogGreska(string akcija, Exception ex)
        {
            lock (lockObject)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(CurrentLogFile, true, Encoding.UTF8))
                    {
                        writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ❌ GREŠKA: {akcija}");
                        writer.WriteLine($"  Poruka: {ex.Message}");
                        writer.WriteLine($"  Stack trace: {ex.StackTrace}");
                        writer.WriteLine();
                    }
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"Greška pri pisanju greške u log fajl: {logEx.Message}");
                }
            }
        }

        public static void LogPodmornice(string igrac, string podmornice)
        {
            lock (lockObject)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(CurrentLogFile, true, Encoding.UTF8))
                    {
                        writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Podmornice postavljene - {igrac}:");
                        writer.WriteLine(podmornice);
                        writer.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška pri pisanju u log fajl: {ex.Message}");
                }
            }
        }
    }
}