using Drakken.Common.Utility;
using UnityEngine;

namespace Drakken.Domain.Tokens
{
    public class TokenVisualContext
    {
        public int LocalClientIndex { get; }
        public Vector3 BoardCenter { get; }

        public TokenVisualContext(int localClientIndex, Vector3 boardCenter)
        {
            LocalClientIndex = localClientIndex;
            BoardCenter = boardCenter;
        }

        public DiceViewPlaceholder GetDiceView(int clientIndex, int diceId)
        {
            Log.Info("TokenVisualContext", $"GetDiceView clientIndex={clientIndex} diceId={diceId}");
            return null;
        }
    }

    public class DiceViewPlaceholder
    {
        public void PlayShatterAnimation() { }
        public void PlayLandAnimation(int value) { }
        public void AttachEffect(string effectId) { }
    }
}
