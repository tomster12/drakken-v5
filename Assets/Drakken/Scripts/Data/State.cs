namespace Drakken.Data
{
    public class State
    {
        public Player[] Players { get; set; }
        public int CurrentTurnPlayerIndex { get; set; }
        public int CurrentRound { get; set; }

        public Player CurrentPlayer => Players[CurrentTurnPlayerIndex];
        public Player NextPlayer => Players[1 - CurrentTurnPlayerIndex];
    }
}
