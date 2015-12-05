// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   The valid response kinds.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStreams.Server
{
    /// <summary>
    /// The valid response kinds.
    /// </summary>
    internal static class ResponseKind
    {
        /// <summary>
        /// The OnNext response kind.
        /// </summary>
        public const char Next = 'n';

        /// <summary>
        /// The OnError response kind.
        /// </summary>
        public const char Error = 'e';

        /// <summary>
        /// The OnCompleted response kind.
        /// </summary>
        public const char Completed = 'c';

        /// <summary>
        /// The Final response kind, which represents OnNext followed immediately by OnCompleted.
        /// </summary>
        public const char Final = 'f';
    }
}