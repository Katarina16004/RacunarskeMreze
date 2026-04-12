using System;
using System.Collections.Generic;

namespace Shared
{
    [Serializable]
    public class Podmornica
    {
        public TipPodmornice Tip { get; set; }
        public List<int> Pozicije { get; set; } = new List<int>();
        public List<int> PogodjenePozicije { get; set; } = new List<int>();
        public bool Potopljena { get; set; }
        public bool Horizontalna { get; set; }

        public Podmornica() { }

        public Podmornica(TipPodmornice tip, List<int> pozicije, bool horizontalna)
        {
            Tip = tip;
            Pozicije = new List<int>(pozicije);
            Horizontalna = horizontalna;
            Potopljena = false;
            PogodjenePozicije = new List<int>();
        }
        public int GetDuzina()
        {
            return (int)Tip;
        }

        // Proverava da li je potopljena (sve njene pozicije pogodjene)
        public bool DaLiJePotopljena()
        {
            if (PogodjenePozicije.Count == Pozicije.Count && Pozicije.Count > 0)
            {
                Potopljena = true;
                return true;
            }
            return false;
        }
        // Dodaj pogodjenu poziciju podmornice
        public bool DodajPogodak(int pozicija)
        {
            if (Pozicije.Contains(pozicija) && !PogodjenePozicije.Contains(pozicija))
            {
                PogodjenePozicije.Add(pozicija);
                DaLiJePotopljena();
                return true;
            }
            return false;
        }
        public bool SadrziPoziciju(int pozicija)
        {
            return Pozicije.Contains(pozicija);
        }

        public override string ToString()
        {
            return $"Brod {Tip} ({GetDuzina()}x1) - Pozicije: [{string.Join(",", Pozicije)}] - " +
                    $"Pogođeno: {PogodjenePozicije.Count}/{Pozicije.Count}";
        }
    }
}

