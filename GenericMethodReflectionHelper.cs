// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Reflection helper for generic methods.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStreams.Server
{
    using System;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Reflection helper for generic methods.
    /// </summary>
    internal static class GenericMethodReflectionHelper
    {
        /// <summary>
        /// Returns a value indicating whether or not deserialization should use a static TryParse method on the
        /// provided <paramref name="type"/>.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>
        /// A value indicating whether or not deserialization should use a static TryParse method on the provided
        /// <paramref name="type"/>.
        /// </returns>
        public static bool ShouldUseStaticTryParseMthod(this Type type)
        {
            if (type.IsNumericType())
            {
                return true;
            }

            return type == typeof(bool) || type == typeof(Guid);
        }

        /// <summary>
        /// Returns a value indicating whether or not this is a numeric type.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <see cref="type"/> is numeric, <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsNumericType(this Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the generic method on <paramref name="type"/> matching the provided parameters.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="name">
        /// The name of the method.
        /// </param>
        /// <param name="parameters">
        /// The parameters.
        /// </param>
        /// <param name="typeParameters">
        /// The type parameters.
        /// </param>
        /// <returns>
        /// The generic method on <paramref name="type"/> matching the provided parameters.
        /// </returns>
        public static MethodInfo GetGenericMethod(this Type type, string name, Type[] parameters, Type[] typeParameters)
        {
            return type.GetMethods().Where(
                method =>
                {
                    if (method.Name != name)
                    {
                        return false;
                    }

                    var methodParameters = method.GetParameters();
                    if (methodParameters.Length != parameters.Length)
                    {
                        return false;
                    }

                    if (!method.IsGenericMethod)
                    {
                        return false;
                    }

                    return GenericMethodParametersMatch(method, parameters, typeParameters);
                }).Select(method => method.MakeGenericMethod(typeParameters)).FirstOrDefault();
        }

        /// <summary>
        /// Returns <see langword="true"/> if the provided <see cref="parameters"/> match the parameters for
        /// <paramref name="method"/>; otherwise returns <see langword="false"/>.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <param name="parameters">
        /// The parameters.
        /// </param>
        /// <param name="typeParameters">
        /// The type parameters linking <paramref name="method"/> and <see cref="parameters"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the provided <see cref="parameters"/> match the parameters for
        /// <paramref name="method"/>; otherwise <see langword="false"/>
        /// </returns>
        public static bool GenericMethodParametersMatch(this MethodInfo method, Type[] parameters, Type[] typeParameters)
        {
            var generic = method.MakeGenericMethod(typeParameters);
            var methodParameters = generic.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                var mp = methodParameters[i].ParameterType;
                var p = parameters[i];
                if (p != mp)
                {
                    return false;
                }
            }

            return true;
        }
    }
}