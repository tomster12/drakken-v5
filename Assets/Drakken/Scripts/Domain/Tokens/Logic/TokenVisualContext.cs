using Drakken.Client.World;

namespace Drakken.Domain.Tokens.Logic
{
    public class TokenVisualContext
    {
        public SceneLayout SceneLayout { get; }
        public SceneObjects SceneObjects { get; }

        public TokenVisualContext(
            SceneLayout SceneLayout,
            SceneObjects SceneObjects)
        {
            this.SceneLayout = SceneLayout;
            this.SceneObjects = SceneObjects;
        }
    }
}
