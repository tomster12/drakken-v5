namespace Drakken.Common.Data
{
    public class Dice
    {
        public int Uid { get; set; }
        public int Value { get; set; }
        public int Sides { get; set; }

        public Dice(int sides)
        {
            Sides = sides;
        }
    }
}
