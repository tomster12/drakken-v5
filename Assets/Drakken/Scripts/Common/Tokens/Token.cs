using Drakken.Common.Data;
using System;

namespace Drakken.Common.Tokens
{
    public interface IToken
    {
        object ExecuteGeneric(State state, object intent);

        void AnimateGeneric(TokenVisualContext visual, object response);
    }

    public abstract class Token<TIntent, TResponse> : IToken
        where TIntent : class
        where TResponse : class
    {
        protected abstract TResponse Execute(State state, TIntent intent);

        protected abstract void Animate(TokenVisualContext visual, TResponse response);

        public object ExecuteGeneric(State state, object intent)
        {
            if (intent is TIntent typedIntent) return Execute(state, typedIntent);
            throw new ArgumentException($"Invalid intent type '{intent.GetType().Name}' for token '{GetType().Name}'. Expected '{typeof(TIntent).Name}'.");
        }

        public void AnimateGeneric(TokenVisualContext visual, object response)
        {
            if (response is TResponse typedResponse) Animate(visual, typedResponse);
            else throw new ArgumentException($"Invalid response type '{response.GetType().Name}' for token '{GetType().Name}'. Expected '{typeof(TResponse).Name}'.");
        }
    }
}
