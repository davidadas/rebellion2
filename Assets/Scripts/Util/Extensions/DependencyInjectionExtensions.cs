using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DependencyInjectionExtensions
{
    /// <summary>
    /// 
    /// </summary>
    public interface IServiceLocator
    {
        T GetService<T>();
    }

    /// <summary>
    /// 
    /// </summary>
    internal class ServiceLocator : IServiceLocator
    {
        private readonly Dictionary<Type, object> singletonInstances;
        private readonly Dictionary<Type, Func<IServiceLocator, object>> transientServices;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="singletonInstances"></param>
        /// <param name="transientServices"></param>
        public ServiceLocator(Dictionary<Type, object> singletonInstances, Dictionary<Type, Func<IServiceLocator, object>> transientServices)
        {
            this.singletonInstances = singletonInstances;
            this.transientServices = transientServices;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public T GetService<T>()
        {
            var serviceType = typeof(T);

            // Handle singleton services.
            if (singletonInstances.ContainsKey(serviceType))
            {
                // Lazy creation of singleton instance.
                if (singletonInstances[serviceType] == null)
                {
                    var factory = transientServices[serviceType];
                    singletonInstances[serviceType] = factory(this);
                }
                return (T)singletonInstances[serviceType];
            }

            // Handle transient services.
            if (transientServices.ContainsKey(serviceType))
            {
                var factory = transientServices[serviceType];
                return (T)factory(this);
            }

            throw new Exception($"Service of type {serviceType.Name} is not registered.");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ServiceContainer
    {
        private Dictionary<Type, Func<IServiceLocator, object>> transientServices = new Dictionary<Type, Func<IServiceLocator, object>>();
        private Dictionary<Type, object> singletonInstances = new Dictionary<Type, object>();

        /// <summary>
        /// Adds a singleton service and its implementation.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        public void AddSingleton<TService, TImplementation>() where TImplementation : TService
        {
            singletonInstances[typeof(TService)] = null;  // Lazy initialization of singleton
            transientServices[typeof(TService)] = (locator) => CreateInstance(typeof(TImplementation), locator);
        }

        /// <summary>
        /// Adds a transient service and its implementation.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        public void AddTransient<TService, TImplementation>() where TImplementation : TService
        {
            transientServices[typeof(TService)] = (locator) => CreateInstance(typeof(TImplementation), locator);
        }

        /// <summary>
        /// Adds an already created singleton instance to the container.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="instance"></param>
        public void AddSingletonInstance<TService>(TService instance)
        {
            singletonInstances[typeof(TService)] = instance;
        }

        /// <summary>
        /// Builds the service locator based on the registered services.
        /// </summary>
        /// <returns></returns>
        public IServiceLocator BuildServiceLocator()
        {
            return new ServiceLocator(singletonInstances, transientServices);
        }

        /// <summary>
        /// Helper method to create an instance of the implementation with constructor injection.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="locator"></param>
        /// <returns></returns>
        private object CreateInstance(Type implementationType, IServiceLocator locator)
        {
            // Get the first constructor of the implementation type.
            ConstructorInfo constructor = implementationType.GetConstructors().First();

            // Resolve constructor parameters.
            ParameterInfo[] parameters = constructor.GetParameters();
            object[] parameterInstances = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                // Get the parameter type.
                Type parameterType = parameters[i].ParameterType;

                // Use reflection to dynamically invoke the generic GetService<T>() method.
                MethodInfo genericGetService = typeof(IServiceLocator)
                    .GetMethod("GetService")
                    .MakeGenericMethod(parameterType);
                parameterInstances[i] = genericGetService.Invoke(locator, null);
            }

            // Create the instance with resolved parameters.
            return Activator.CreateInstance(implementationType, parameterInstances);
        }
    }
}
