using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Shared
{
    [Serializable]
    public class Igrac
    {
        [NonSerialized]
        public Socket socket;
        public int id { get; }
        public string ime { get; set; }
        public int brojPromasaja { get; set; }
        public List<Podmornica> podmornice { get; set; } = new List<Podmornica>();
        public int[,] matrica { get; set; }
        public int[,] matricaGadjana { get; set; }
        public bool izgubio { get; set; }

        public Igrac()
        {
        }

        public Igrac(Socket socket, int id, int dimenzija)
        {
            this.socket = socket;
            this.id = id;
            brojPromasaja = 0;
            podmornice = new List<Podmornica>();
            matrica = new int[dimenzija, dimenzija];
            matricaGadjana = new int[dimenzija, dimenzija];
            this.ime = ime;
            this.izgubio = false;
        }

        public Igrac(Igrac original)
        {
            this.socket = null;
            this.id = original.id;
            this.ime = original.ime;
            this.brojPromasaja = original.brojPromasaja;
            this.podmornice = new List<Podmornica>(original.podmornice);

            int dimX = original.matrica.GetLength(0);
            int dimY = original.matrica.GetLength(1);

            this.matrica = new int[dimX, dimY];
            this.matricaGadjana = new int[dimX, dimY];

            for (int i = 0; i < dimX; i++)
            {
                for (int j = 0; j < dimY; j++)
                {
                    this.matrica[i, j] = original.matrica[i, j];
                    this.matricaGadjana[i, j] = original.matricaGadjana[i, j];
                }
            }
        }

        /// <summary>
        /// Dodaj podmornicu
        /// </summary>
        public bool DodajPodmornicu(Podmornica podmornica, out string poruka)
        {
            poruka = string.Empty;

            if (podmornica == null || podmornica.Pozicije.Count == 0)
            {
                poruka = "Podmornica mora imati pozicije!";
                return false;
            }

            foreach (var pozicija in podmornica.Pozicije)
            {
                if (DaLiPodmornicaNaPoziciji(pozicija))
                {
                    poruka = $"Na poziciji {pozicija} već postoji podmornica!";
                    return false;
                }
            }

            podmornice.Add(podmornica);

            // Ažuriramo matricu
            foreach (var pozicija in podmornica.Pozicije)
            {
                int i = (pozicija - 1) / matrica.GetLength(0);
                int j = (pozicija - 1) % matrica.GetLength(1);
                matrica[i, j] = 1;
            }

            return true;
        }

        public bool DaLiPodmornicaNaPoziciji(int pozicija)
        {
            return podmornice.Any(p => p.SadrziPoziciju(pozicija));
        }

        public Podmornica GetPodmornicaNaPoziciji(int pozicija)
        {
            return podmornice.FirstOrDefault(p => p.SadrziPoziciju(pozicija));
        }

        public int GetBrojPreostalihPodmornica()
        {
            return podmornice.Count(p => !p.Potopljena);
        }

        public bool DaLiSuSvePodmornicePotonjene()
        {
            return podmornice.All(p => p.Potopljena) && podmornice.Count == 10;
        }

        public void ResetujPodmornice()
        {
            podmornice.Clear();
        }

        public int AzurirajMatricu(int gadjanaPoz)
        {
            int i = (gadjanaPoz - 1) / matricaGadjana.GetLength(0);
            int j = (gadjanaPoz - 1) % matricaGadjana.GetLength(1);

            if (matricaGadjana[i, j] != 0)
            {
                return 0;  // Već je gadjano
            }

            Podmornica podmornica = GetPodmornicaNaPoziciji(gadjanaPoz);

            if (podmornica == null)
            {
                matricaGadjana[i, j] = 1;  // Promasaj
                return 1;
            }
            else
            {
                podmornica.DodajPogodak(gadjanaPoz);
                matricaGadjana[i, j] = 2;  // Pogodak

                if (podmornica.Potopljena)
                {
                    return 3;  // Potopljena
                }
                return 2;  // Pogodak
            }
        }

        public string PrikaziMatricuGadjana()
        {
            string s = "   ";

            for (int j = 0; j < matricaGadjana.GetLength(1); j++)
            {
                if (j == 9)
                    s = s + " ";
                s = s + string.Format("{0,2}", j + 1);
            }
            s = s + "\n";

            for (int i = 0; i < matricaGadjana.GetLength(0); i++)
            {
                s = s + string.Format("{0,2}", i + 1) + " ";

                for (int j = 0; j < matricaGadjana.GetLength(1); j++)
                {
                    if (matricaGadjana[i, j] == 0)
                        s = s + " -";
                    else if (matricaGadjana[i, j] == 1)
                        s = s + " +";
                    else
                        s = s + " x";
                }
                s = s + "\n";
            }
            return s;
        }
        public string PrikaziMatricu()
        {
            string s = "   ";

            for (int j = 0; j < matrica.GetLength(1); j++)
            {
                if (j == 9)
                    s = s + " ";
                s = s + string.Format("{0,2}", j + 1);
            }
            s = s + "\n";

            for (int i = 0; i < matrica.GetLength(0); i++)
            {
                s = s + string.Format("{0,2}", i + 1) + " ";

                for (int j = 0; j < matrica.GetLength(1); j++)
                {
                    int pozicija = i * matrica.GetLength(1) + j + 1;

                    // Pronađi podmornicu na ovoj poziciji
                    Podmornica podmornica = GetPodmornicaNaPoziciji(pozicija);

                    if (podmornica != null)
                    {
                        // Ako je podmornica potopljena i ova pozicija je pogođena
                        if (podmornica.Potopljena && podmornica.PogodjenePozicije.Contains(pozicija))
                        {
                            s = s + " x";  // Potopljena
                        }
                        else if(podmornica.Pozicije.Contains(pozicija) && podmornica.PogodjenePozicije.Contains(pozicija))
                        {
                            s = s + " x";  // Pogodak, ali nije potopljena
                        }
                        else
                        {
                            s = s + " -";  // Ostalo
                        }
                    }
                    else
                    {
                        s = s + " -";  // Prazno
                    }
                }
                s = s + "\n";
            }
            return s;
        }

        public override string ToString()
        {
            string s = $"----------\nIgrac ID={id} Ime={ime}\n----------\nBroj promasaja: {brojPromasaja}";
            s = s + $"\nTabla:\n{PrikaziMatricu()}\n\n";
            return s;
        }

        public string PretvoriUString()
        {
            string strMatrica = string.Join(";", Enumerable.Range(0, matrica.GetLength(0))
                    .Select(i => string.Join(",", Enumerable.Range(0, matrica.GetLength(1))
                    .Select(j => matrica[i, j]))));
            return strMatrica;
        }

        public static int[,] PretvoriStringUMatricu(string ulaz)
        {
            string[] redovi = ulaz.Split(';', (char)StringSplitOptions.RemoveEmptyEntries);
            int brRedova = redovi.Length;
            int brKolona = redovi[0].Split(',', (char)StringSplitOptions.RemoveEmptyEntries).Length;
            int[,] matrica = new int[brRedova, brKolona];

            for (int i = 0; i < brRedova; i++)
            {
                string[] kolone = redovi[i].Split(',', (char)StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < brKolona; j++)
                {
                    matrica[i, j] = int.Parse(kolone[j]);
                }
            }
            return matrica;
        }

        public void ResetujIgraca()
        {
            brojPromasaja = 0;
            ResetujPodmornice();
            for (int i = 0; i < matrica.GetLength(0); i++)
            {
                for (int j = 0; j < matrica.GetLength(1); j++)
                {
                    matrica[i, j] = 0;
                    matricaGadjana[i, j] = 0;
                }
            }
            izgubio = false;
        }
    }
}