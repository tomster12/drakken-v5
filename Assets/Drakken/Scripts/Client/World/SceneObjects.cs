using System;
using System.Collections.Generic;
using Drakken.Domain;
using Drakken.Domain.Dice;
using Drakken.Domain.Tokens;
using UnityEngine;

namespace Drakken.Client.World
{
    [Serializable]
    public class SceneObjects
    {
        public ScenePlayerObjects P1 { get; } = new();
        public ScenePlayerObjects P2 { get; } = new();

        public ScenePlayerObjects Player(int clientIndex) => clientIndex == 0 ? P1 : P2;

        public void Init(SceneLayout sceneLayout, AssetDatabase assets, TokenRegistry tokenRegistry, GameClient gameClient)
        {
            P1.Init(sceneLayout, assets, tokenRegistry, gameClient, clientIndex: 0);
            P2.Init(sceneLayout, assets, tokenRegistry, gameClient, clientIndex: 1);
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
        public Dictionary<int, DiceView> DiceViews { get; set; } = new();
        public DiceSimulationReplayer DiceSimReplayer { get; } = new();

        private SceneLayout sceneLayout;
        private AssetDatabase assets;
        private TokenRegistry tokenRegistry;
        private GameClient gameClient;
        private int clientIndex;

        private GamePlayerLayout GameLayout => sceneLayout.Game.Player(clientIndex);
        private DraftingPlayerLayout DraftingLayout => sceneLayout.Drafting.Player(clientIndex);
        public Vector3 BagPosition => GameLayout.Bag.transform.position;

        public void Init(
            SceneLayout sceneLayout, AssetDatabase assets, TokenRegistry tokenRegistry,
            GameClient gameClient, int clientIndex)
        {
            this.sceneLayout = sceneLayout;
            this.assets = assets;
            this.tokenRegistry = tokenRegistry;
            this.gameClient = gameClient;
            this.clientIndex = clientIndex;

            DiceSimReplayer.Init(assets, gameClient);
        }

        public void OnDisconnect()
        {
            DestroyAllTokens();
            DestroyAllDice();
            DiceSimReplayer.Cleanup();
        }

        // ------------------------------ Dice

        public DiceView SpawnDiceAt(DiceInstance instance, Vector3 position, Quaternion rotation)
        {
            var diceView = DiceView.Create(assets, instance, gameClient);
            diceView.transform.SetPositionAndRotation(position, rotation);
            DiceViews[instance.InstanceId] = diceView;
            return diceView;
        }

        public void DestroyDice(int diceInstanceId)
        {
            GameObject.Destroy(DiceViews[diceInstanceId].gameObject);
            DiceViews.Remove(diceInstanceId);
        }

        public DiceView FindDiceView(int diceInstanceId)
        {
            DiceViews.TryGetValue(diceInstanceId, out var diceView);
            return diceView;
        }

        public void DestroyAllDice()
        {
            foreach (var diceView in DiceViews.Values)
            {
                GameObject.Destroy(diceView.gameObject);
            }
            DiceViews = new();
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
            var tokenView = TokenView.Create(assets, tokenRegistry, instance, hidden);
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