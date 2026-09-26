using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public sealed class GameConfigTests
    {
        /// <summary>
        /// Verifies that AI configuration types do not provide gameplay tuning outside content.
        /// </summary>
        [Test]
        public void AIConfiguration_Constructors_ContainNoGameplayDefaults()
        {
            Queue<object> pending = new Queue<object>();
            HashSet<object> inspected = new HashSet<object>();
            pending.Enqueue(new GameConfig.AIConfig());

            while (pending.Count > 0)
            {
                object config = pending.Dequeue();
                if (!inspected.Add(config))
                    continue;

                Type configType = config.GetType();
                foreach (PropertyInfo property in configType.GetProperties())
                {
                    Type propertyType = property.PropertyType;
                    object value = property.GetValue(config);
                    if (IsGameplayValue(propertyType))
                    {
                        Assert.That(
                            value,
                            Is.EqualTo(Activator.CreateInstance(propertyType)),
                            $"{configType.Name}.{property.Name} defines gameplay tuning in code."
                        );
                        continue;
                    }

                    if (value is ICollection collection)
                    {
                        Assert.That(
                            collection.Count,
                            Is.Zero,
                            $"{configType.Name}.{property.Name} defines gameplay tuning in code."
                        );
                    }

                    if (IsAIConfigType(propertyType))
                        pending.Enqueue(value ?? Activator.CreateInstance(propertyType));
                }
            }
        }

        /// <summary>
        /// Returns whether a type represents a scalar gameplay configuration value.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True when the type is a scalar gameplay value.</returns>
        private static bool IsGameplayValue(Type type) =>
            type.IsValueType && Nullable.GetUnderlyingType(type) == null;

        /// <summary>
        /// Returns whether a type is a nested AI configuration object.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True when the type belongs to the AI configuration graph.</returns>
        private static bool IsAIConfigType(Type type) =>
            type.IsClass && type.DeclaringType == typeof(GameConfig);
    }
}
