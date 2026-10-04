using System;

namespace Rebellion.Util.DependencyInjection
{
    /// <summary>
    /// Resolves services belonging to one runtime scope.
    /// </summary>
    public interface IServiceLocator
    {
        /// <summary>
        /// Resolves a registered service.
        /// </summary>
        /// <typeparam name="T">The requested service type.</typeparam>
        /// <returns>The registered service instance.</returns>
        T GetService<T>();

        /// <summary>
        /// Resolves a registered service by its runtime type.
        /// </summary>
        /// <param name="serviceType">The requested service type.</param>
        /// <returns>The registered service instance.</returns>
        object GetService(Type serviceType);
    }
}
