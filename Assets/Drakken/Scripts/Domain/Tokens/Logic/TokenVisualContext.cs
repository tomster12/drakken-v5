using Drakken.Client.World;

namespace Drakken.Domain.Tokens.Logic
{
    public class TokenVisualContext
    {
        public TokenView TokenView { get; }
        public SceneLayout SceneLayout { get; }
        public SceneObjects SceneObjects { get; }

        public TokenVisualContext(
            TokenView TokenView,
            SceneLayout SceneLayout,
            SceneObjects SceneObjects)
        {
            this.TokenView = TokenView;
            this.SceneLayout = SceneLayout;
            this.SceneObjects = SceneObjects;
        }
    }
}
