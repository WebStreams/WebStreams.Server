// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   The invalid attribute usage exception.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStreams.Server
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// The invalid attribute usage exception.
    /// </summary>
    public class InvalidAttributeUsageException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidAttributeUsageException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="innerException">
        /// The inner exception.
        /// </param>
        public InvalidAttributeUsageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidAttributeUsageException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        public InvalidAttributeUsageException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidAttributeUsageException"/> class.
        /// </summary>
        public InvalidAttributeUsageException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidAttributeUsageException"/> class.
        /// </summary>
        /// <param name="info">
        /// The info.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        protected InvalidAttributeUsageException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
