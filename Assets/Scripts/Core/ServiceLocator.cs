using System;
using System.Collections.Generic;

namespace FGJ2026
{
    public sealed class ServiceLocator
    {
        private static ServiceLocator instance;

        public static ServiceLocator Instance => instance ??= new ServiceLocator();

        private readonly Dictionary<Type, IService> registeredServices = new Dictionary<Type, IService>();

        public event Action AllInitialized;

        private ServiceLocator()
        {
        }

        public void Add(IService service) => registeredServices[service.GetType()] = service;

        /// <summary>Returns the service, or null when it isn't registered.</summary>
        public T GetService<T>() where T : class, IService =>
            registeredServices.TryGetValue(typeof(T), out IService service) ? (T)service : null;

        public void InitializeAll()
        {
            foreach (IService service in registeredServices.Values)
            {
                if (!service.IsInitialized)
                    service.Initialize();
            }
            AllInitialized?.Invoke();
        }
    }
}
