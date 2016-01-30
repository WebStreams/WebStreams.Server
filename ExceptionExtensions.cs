using System;
using System.Linq;
using System.Text;

namespace WebStreams.Server
{
    /// <summary>
    ///     The exception extensions.
    /// </summary>
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Returns a detailed string representation of this instance.
        /// </summary>
        /// <param name="exception">
        /// The exception.
        /// </param>
        /// <returns>
        /// A detailed string representation of this instance.
        /// </returns>
        /// <remarks>
        /// Returns <see cref="string.Empty"/> if <paramref name="exception"/> is null.
        /// </remarks>
        public static string ToDetailedString(this Exception exception)
        {
            if (exception == null)
            {
                return string.Empty;
            }

            var result = new StringBuilder();
            result.AppendFormat("{0} - {1}\n", exception.GetType(), exception.Message);

            if (exception.StackTrace != null)
            {
                result.AppendFormat("Stack: {0}\n", exception.StackTrace);
            }

            if (!string.IsNullOrWhiteSpace(exception.Source))
            {
                result.AppendFormat("Source: {0}\n", exception.Source);
            }

            var ag = exception as AggregateException;
            if (ag != null)
            {
                result.AppendLine(string.Join("\n\n", ag.Flatten().InnerExceptions.Select(ToDetailedString)));
            }
            else if (exception.InnerException != null)
            {
                result.AppendLine(exception.InnerException.ToDetailedString());
            }

            return result.ToString();
        }
    }
}
