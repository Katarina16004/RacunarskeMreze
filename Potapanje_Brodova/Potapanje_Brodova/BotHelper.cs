using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class BotHelper
    {
        private static Random random = new Random();

        public static int IzaberiRandomPolje(int tableSize)
        {
            return random.Next(1, tableSize * tableSize + 1);
        }

        public static string IzaberiRandomIgraca(List<Igrac> igraci, Igrac trenutniIgrac)
        {
            List<Igrac> dostupni = igraci
                .Where(i => i.ime != trenutniIgrac.ime && !i.izgubio)
                .ToList();

            if (dostupni.Count == 0)
                return null;

            return dostupni[random.Next(dostupni.Count)].ime;
        }
    }
}