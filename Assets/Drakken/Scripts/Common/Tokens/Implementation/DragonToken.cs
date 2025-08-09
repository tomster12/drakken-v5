using Drakken.Common.Data;

namespace Drakken.Common.Tokens.Implementation
{
    public class DragonTokenIntent
    {
        public int SelectedDiceIndex;
    }

    public class DragonTokenResponse
    {
        public int RolledValue;
    }

    public class DragonToken : Token<DragonTokenIntent, DragonTokenResponse>
    {
        protected override DragonTokenResponse Execute(State state, DragonTokenIntent intent)
        {
            return new DragonTokenResponse { RolledValue = 2 };
        }

        protected override void Animate(TokenVisualContext visual, DragonTokenResponse response)
        {
        }
    }
}
