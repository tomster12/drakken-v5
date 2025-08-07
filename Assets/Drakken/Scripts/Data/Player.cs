using Drakken.Tokens;
using System.Collections.Generic;

namespace Drakken.Data
{
    public class Player
    {
        public int PlayerIndex { get; set; }
        public List<TokenInstance> Hand { get; set; }
        public List<Dice> Dice { get; set; }
    }
}
