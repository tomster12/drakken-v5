using System;
using Drakken.Client;
using Drakken.Client.World;
using UnityEngine;

namespace Drakken.Domain.Tokens.Logic
{
    public class TokenVisualContext
    {
        public AssetDatabase Assets { get; set; }
        public TokenView TokenView { get; set; }
        public SceneLayout SceneLayout { get; set; }
        public SceneObjects SceneObjects { get; set; }
        public ClientUI ClientUI { get; set; }
        public Func<int, int, Vector3> GetDiceRowIndexPosition { get; set; }
    }
}
