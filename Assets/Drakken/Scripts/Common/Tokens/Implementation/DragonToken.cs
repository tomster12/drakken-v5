using Drakken.Common.Data;

namespace Drakken.Common.Tokens.Implementation
{
    internal class DragonTokenIntent
    {
        public int SelectedDiceIndex;
    }

    internal class DragonTokenResponse
    {
        public int RolledValue;
    }

    internal class DragonToken : TokenImplementation<DragonTokenIntent, DragonTokenResponse>
    {
        protected override DragonTokenResponse Execute(GameState state, DragonTokenIntent intent)
        {
            return new DragonTokenResponse { RolledValue = 2 };
        }

        protected override void Animate(TokenVisualContext visual, DragonTokenResponse response)
        {
        }
    }
}
