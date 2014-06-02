namespace WebStream
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

                    return DoGenericMethodParametersMatch(method, parameters, typeParameters);
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
        public static bool DoGenericMethodParametersMatch(this MethodInfo method, Type[] parameters, Type[] typeParameters)
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