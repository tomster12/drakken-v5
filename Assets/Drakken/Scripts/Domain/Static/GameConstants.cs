using UnityEngine;

namespace Drakken.Domain.Static
{
    public static class GameConstants
    {
        public static int DraftingTokenCount = 6;
        public static int StandardTokenCount = 4;
        public static int StandardDiceCount = 4;
        public static int StandardDiceSideCount = 6;
        public static int MaxCountOfEachToken = 3;

        public static Vector3 DiceTrayCenterP1 = new(-2.5f, 0f, 0f);
        public static Vector3 DiceTrayCenterP2 = new(2.5f, 0f, 0f);
        public static Vector3 DiceTraySize = new(4f, 0f, 4f);
        public static float DiceTrayWallHeight = 2.0f;

        public static float DicePhysicsFixedTimestep = 1f / 30f;
        public static int DicePhysicsMaxTicksPerStep = 900;
        public static float DiceSettleLinearVelocityThreshold = 0.01f;
        public static float DiceSettleAngularVelocityThreshold = 0.01f;
        public static float DiceRequiredSettleDuration = 1.0f;
        public static float DiceThrowImpulseSpeed = 20f;
        public static float DiceThrowTorque = 20f;

        public static Vector3 DiceTrayCenter(int clientIndex) => clientIndex == 0 ? DiceTrayCenterP1 : DiceTrayCenterP2;
    }
}
