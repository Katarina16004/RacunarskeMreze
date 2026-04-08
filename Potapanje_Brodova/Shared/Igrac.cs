using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Shared
{
    [Serializable]
    public class Igrac
    {

        [NonSerialized] public Socket socket;
        public int id { get; }
        public string ime { get; set; }
        public int brojPromasaja { get; set; }
        public List<int> pozicije { get; set; } //korisnik salje pozicije (1-dim)
        public int[,] matrica { get; set; }

        public int[,] matricaGadjana { get; set; } //matrica koja pamti poteze gadjanja


        public bool izgubio { get; set; }

        public Igrac()
        {

        }

        public Igrac(Socket socket, int id, int dimenzija)
        {
            this.socket = socket;
            this.id = id;
            brojPromasaja = 0;
            pozicije = new List<int>();
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
            this.pozicije = new List<int>(original.pozicije);

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

        public void DodajPodmornice(List<int> pozicije, string ime)
        {
            this.pozicije = pozicije;
            this.ime = ime;
            inicijalizujBrodove();
        }

        private void inicijalizujBrodove()
        {
            foreach (var pozicija in pozicije)
            {
                int i = (pozicija - 1) / matrica.GetLength(0);
                int j = (pozicija - 1) % matrica.GetLength(1);
                matrica[i, j] = 1;
            }

        }

        public int AzurirajMatricu(int gadjanaPoz) //salje se pozicija (1-dim) koju protivnik gadja
        {

            int i = (gadjanaPoz - 1) / matricaGadjana.GetLength(0);
            int j = (gadjanaPoz - 1) % matricaGadjana.GetLength(1);
            if (matricaGadjana[i, j] == 0)
            {
                if (pozicije.Contains(gadjanaPoz))
                {
                    matricaGadjana[i, j] = 2; // ako se tu nalazila podmornica, znaci da je sada pogodjena
                    pozicije.Remove(gadjanaPoz);
                    matrica[i, j] = 0; //brisemo brod sa prave matrice
                    return 2; //pogodjeno
                }
                else
                {
                    matricaGadjana[i, j] = 1; // ako se tu ne nalazi podmornica, znaci da je promasena
                    return 1; //promaseno
                }
            }
            else
            {
                return 0; //vec gadjano polje
            }
            //server ce na osnovu povratne vrednosti da ispise poruku protivniku
        }
        public string PrikaziMatricuGadjana()
        {
            string s = "   ";  // Razmak za ugao

            // Ispis zaglavlja (brojevi kolona)
            for (int j = 0; j < matricaGadjana.GetLength(1); j++)
            {
                if (j == 9)
                    s = s + " ";
                s = s + string.Format("{0,2}", j + 1);
            }
            s = s + "\n";

            // Ispis redova sa indeksima
            for (int i = 0; i < matricaGadjana.GetLength(0); i++)
            {
                s = s + string.Format("{0,2}", i + 1) + " ";  // Redni broj

                for (int j = 0; j < matricaGadjana.GetLength(1); j++)
                {
                    if (matricaGadjana[i, j] == 0)
                        s = s + " -";  // Negazdjano
                    else if (matricaGadjana[i, j] == 1)
                        s = s + " +";  // Promasaj
                    else
                        s = s + " x";  // Pogodak
                }
                s = s + "\n";
            }
            return s;
        }

        public string PrikaziMatricu()
        {
            string s = "   ";  // Razmak za ugao

            // Ispis zaglavlja (brojevi kolona)
            for (int j = 0; j < matrica.GetLength(1); j++)
            {
                if (j == 9)
                    s = s + " ";
                s = s + string.Format("{0,2}", j + 1);
            }
            s = s + "\n";

            // Ispis redova sa indeksima
            for (int i = 0; i < matrica.GetLength(0); i++)
            {
                s = s + string.Format("{0,2}", i + 1) + " ";  // Redni broj

                for (int j = 0; j < matrica.GetLength(1); j++)
                {
                    if (matrica[i, j] == 1)
                        s = s + " O";  // Brod
                    else
                        s = s + " -";  // Prazno
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
            pozicije.Clear();
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
