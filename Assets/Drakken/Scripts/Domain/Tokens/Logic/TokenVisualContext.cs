using Drakken.Client;
using Drakken.Client.World;

namespace Drakken.Domain.Tokens.Logic
{
    public class TokenVisualContext
    {
        public AssetDatabase Assets { get; set; }
        public TokenView TokenView { get; set; }
        public SceneLayout SceneLayout { get; set; }
        public SceneObjects SceneObjects { get; set; }
        public ClientUI ClientUI { get; set; }
    }
}