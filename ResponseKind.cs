// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   The valid response kinds.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
{
    /// <summary>
    /// The valid response kinds.
    /// </summary>
    public static class ResponseKind
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
    }
}