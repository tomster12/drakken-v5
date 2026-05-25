using Drakken.Domain;
using System;
using System.Threading.Tasks;

namespace Drakken.Domain.Tokens
{
    public interface ITokenExecutor<TIntent, TResponse>
    {
        TResponse Execute(GameState state, TIntent intent);
    }

    public interface ITokenAnimator<TResponse>
    {
        Task Animate(TokenVisualContext visual, TResponse response);
    }

    // public object ExecuteGeneric(GameState state, object intent)
    // {
    //     if (intent is TIntent typedIntent) return Execute(state, typedIntent);
    //     throw new ArgumentException($"Invalid intent type '{intent.GetType().Name}' for token '{GetType().Name}'. Expected '{typeof(TIntent).Name}'.");
    // }

    // public void AnimateGeneric(TokenVisualContext visual, object response)
    // {
    //     if (response is TResponse typedResponse) Animate(visual, typedResponse);
    //     else throw new ArgumentException($"Invalid response type '{response.GetType().Name}' for token '{GetType().Name}'. Expected '{typeof(TResponse).Name}'.");
    // }
}
