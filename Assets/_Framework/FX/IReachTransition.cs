using System.Threading.Tasks;
using Reach.Framework.Core;

namespace Reach.Framework.FX
{
    /// <summary>
    /// A transition effect that runs around a perspective switch.
    /// The implementation handles its own visuals/audio; the only contract
    /// is "play the transition AROUND a switch and return when done".
    /// </summary>
    public interface IReachTransition
    {
        bool IsTransitioning { get; }

        /// <summary>
        /// Run the transition: visuals lead in → call switch internally → visuals lead out.
        /// Returns true if the switch succeeded.
        /// </summary>
        Task<bool> PlayAndSwitchAsync(PossessableCharacter target);
    }
}