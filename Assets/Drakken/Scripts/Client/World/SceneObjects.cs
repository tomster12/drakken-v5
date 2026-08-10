using System;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain.Animation;
using Drakken.Domain;
using Drakken.Domain.Dice;
using Drakken.Domain.Tokens;
using Drakken.Utility;
using UnityEngine;

namespace Drakken.Client.World
{
    [Serializable]
    public class SceneObjects
    {
        public ScenePlayerObjects P1 { get; } = new();
        public ScenePlayerObjects P2 { get; } = new();

        public ScenePlayerObjects Player(int clientIndex) => clientIndex == 0 ? P1 : P2;

        public void Init(SceneLayout sceneLayout, AssetDatabase assets)
        {
            P1.Init(sceneLayout, assets, clientIndex: 0);
            P2.Init(sceneLayout, assets, clientIndex: 1);
        }

        public void OnDisconnect()
        {
            P1.OnDisconnect();
            P2.OnDisconnect();
        }
    }

    public class ScenePlayerObjects
    {
        public TokenView[] TokenViews { get; set; } = new TokenView[0];
        public DiceView[] DiceViews { get; set; } = new DiceView[0];
        public DiceSimulationReplayer DiceSimReplayer { get; } = new();

        private SceneLayout sceneLayout;
        private AssetDatabase assets;
        private int clientIndex;

        private GamePlayerLayout GameLayout => sceneLayout.Game.Player(clientIndex);
        private DraftingPlayerLayout DraftingLayout => sceneLayout.Drafting.Player(clientIndex);
        public Vector3 BagPosition => GameLayout.Bag.transform.position;

        public void Init(SceneLayout sceneLayout, AssetDatabase assets, int clientIndex)
        {
            this.sceneLayout = sceneLayout;
            this.assets = assets;
            this.clientIndex = clientIndex;
        }

        public void OnDisconnect()
        {
            DestroyAllTokens();
            DestroyAllDice();
        }

        // ------------------------------ Dice

        public DiceView SpawnDiceAt(DiceInstance instance, int diceIndex, Vector3 position, Quaternion rotation)
        {
            var diceView = DiceView.Create(assets, instance);
            diceView.transform.SetPositionAndRotation(position, rotation);
            DiceViews[diceIndex] = diceView;
            return diceView;
        }

        public void DestroyDiceAtIndex(int diceIndex)
        {
            GameObject.Destroy(DiceViews[diceIndex].gameObject);
            DiceViews[diceIndex] = null;
        }

        public DiceView FindDiceView(int diceInstanceId)
        {
            foreach (var diceView in DiceViews)
            {
                if (diceView != null && diceView.DiceInstance.InstanceId == diceInstanceId)
                    return diceView;
            }
            return null;
        }

        public void DestroyAllDice()
        {
            foreach (var diceView in DiceViews)
            {
                GameObject.Destroy(diceView.gameObject);
            }
            DiceViews = new DiceView[0];
        }

        // ------------------------------ Tokens

        public Vector3 GetDraftTokenRowIndexPosition(int tokenIndex, int tokenCount)
        {
            var draftTokenRow = DraftingLayout.DraftTokenRow;
            float offset = (tokenIndex - (tokenCount - 1) / 2f) * sceneLayout.TokenSpacing;
            return draftTokenRow.position + draftTokenRow.right * offset;
        }

        public Vector3 GetTokenRowIndexPosition(int tokenIndex)
        {
            var tokenRow = GameLayout.TokenRow;
            float offset = (tokenIndex - (TokenViews.Length - 1) / 2f) * sceneLayout.TokenSpacing;
            return tokenRow.position + tokenRow.right * offset;
        }

        public TokenView SpawnTokenAtIndex(TokenInstance instance, int tokenIndex, bool hidden = false)
        {
            var tokenView = TokenView.Create(assets, GameEntrypoint.Singleton.TokenRegistry, instance, hidden);
            TokenViews[tokenIndex] = tokenView;
            return tokenView;
        }

        public void DestroyTokenAtIndex(int tokenIndex)
        {
            GameObject.Destroy(TokenViews[tokenIndex].gameObject);
        }

        public void DestroyAllTokens()
        {
            foreach (var tokenView in TokenViews)
            {
                GameObject.Destroy(tokenView.gameObject);
            }
            TokenViews = new TokenView[0];
        }
    }
}