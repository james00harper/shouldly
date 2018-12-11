using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Shouldly
{
    [ShouldlyMethods]
    public static class ObjectGraphTestExtensions
    {
        public static void ShouldBeEquivalentTo(this object actual, object expected)
        {
            ShouldBeEquivalentTo(actual, expected, () => null);
        }

        public static void ShouldBeEquivalentTo(this object actual, object expected, string customMessage)
        {
            ShouldBeEquivalentTo(actual, expected, () => customMessage);
        }

        public static void ShouldBeEquivalentTo(this object actual, object expected, [InstantHandle] Func<string> customMessage)
        {
            CompareObjects(actual, expected, new List<string>(), customMessage);
        }

        private static void CompareObjects(object actual, object expected,
            IList<string> path,
            [InstantHandle] Func<string> customMessage, [CallerMemberName] string shouldlyMethod = null)
        {
            if (BothValuesAreNull(actual, expected, path, customMessage, shouldlyMethod))
                return;

            var type = GetTypeToCompare(actual, expected, path, customMessage, shouldlyMethod);

#if NewReflection
            if (type.GetTypeInfo().IsValueType)
#else
            if (type.IsValueType)
#endif
            {
                CompareValueTypes((ValueType)actual, (ValueType)expected, path, customMessage, shouldlyMethod);
            }
            else
            {
                CompareReferenceTypes(actual, expected, type, path, customMessage, shouldlyMethod);
            }
        }

        private static bool BothValuesAreNull(object actual, object expected, IEnumerable<string> path,
            [InstantHandle] Func<string> customMessage, [CallerMemberName] string shouldlyMethod = null)
        {
            if (expected == null)
            {
                if (actual == null)
                    return true;

                ThrowException(actual, null, path, customMessage, shouldlyMethod);
            }
            else if (actual == null)
            {
                ThrowException(null, expected, path, customMessage, shouldlyMethod);
            }

            return false;
        }

        private static Type GetTypeToCompare(object actual, object expected, IList<string> path,
            [InstantHandle] Func<string> customMessage, [CallerMemberName] string shouldlyMethod = null)
        {
            var expectedType = expected.GetType();
            var actualType = actual.GetType();

            if (actualType != expectedType && !expectedType.IsAssignableFrom(actualType) &&
                !(typeof(IEnumerable).IsAssignableFrom(expectedType) &&
                  typeof(IEnumerable).IsAssignableFrom(actualType)))
            {
                ThrowException(actualType, expectedType, path, customMessage, shouldlyMethod);
            }

            var typeName = $" [{expectedType.FullName}]";
            if (path.Count == 0)
                path.Add(typeName);
            else
                path[path.Count - 1] += typeName;

            return expectedType;
        }

        private static void CompareValueTypes(ValueType actual, ValueType expected, IEnumerable<string> path,
            [InstantHandle] Func<string> customMessage, [CallerMemberName] string shouldlyMethod = null)
        {
            if (!actual.Equals(expected))
                ThrowException(actual, expected, path, customMessage, shouldlyMethod);
        }

        private static void CompareReferenceTypes(object actual, object expected, Type type,
            IList<string> path, [InstantHandle] Func<string> customMessage,
            [CallerMemberName] string shouldlyMethod = null)
        {
            if (ReferenceEquals(actual, expected))
                return;

            if (type == typeof(string))
            {
                CompareStrings((string) actual, (string) expected, path, customMessage, shouldlyMethod);
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                CompareEnumerables((IEnumerable) actual, (IEnumerable) expected, path, customMessage, shouldlyMethod);
            }
            else
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                CompareProperties(actual, expected, properties, path, customMessage, shouldlyMethod);
            }
        }

        private static void CompareStrings(string actual, string expected, IEnumerable<string> path,
            [InstantHandle] Func<string> customMessage, [CallerMemberName] string shouldlyMethod = null)
        {
            if (!actual.Equals(expected, StringComparison.Ordinal))
                ThrowException(actual, expected, path, customMessage, shouldlyMethod);
        }

        private static void CompareEnumerables(IEnumerable actual, IEnumerable expected,
            IList<string> path, [InstantHandle] Func<string> customMessage,
            [CallerMemberName] string shouldlyMethod = null)
        {
            var expectedList = expected.Cast<object>().ToList();
            var actualList = actual.Cast<object>().ToList();

            if (actualList.Count != expectedList.Count)
            {
                var newPath = path.Concat(new[] {"Count"});
                ThrowException(actualList.Count, expectedList.Count, newPath, customMessage, shouldlyMethod);
            }

            var unmatchedIndexes = Enumerable.Range(0, actualList.Count).ToList();

            for (var i = 0; i < expectedList.Count; i++)
            {
                var newPath = path.Concat(new[] {$"Element [{i}]"}).ToList();

                var expectedItem = expectedList[i];

                PerformLooseMatch(unmatchedIndexes, actualList, expectedItem, newPath, customMessage, shouldlyMethod);
            }
        }

        private static void PerformLooseMatch(IList<int> unmatchedIndexes, IList<object> actual, object expectedItem,
            IList<string> path, 
            [InstantHandle] Func<string> customMessage, [CallerMemberName] string shouldlyMethod = null)
        {
            var indexToBeRemoved = -1;

            for (var i = 0; i < unmatchedIndexes.Count; i++)
            {
                try
                {
                    var index = unmatchedIndexes[i];
                    var subject = actual[index];

                    CompareObjects(subject, expectedItem, path, customMessage, shouldlyMethod);
                    indexToBeRemoved = i;
                    break;
                }
                catch (ShouldAssertException)
                {
                    if (i == unmatchedIndexes.Count - 1)
                    {
                        throw;
                    }
                }

            }

            if (indexToBeRemoved != -1)
            {
                unmatchedIndexes.RemoveAt(indexToBeRemoved);
            }
        }

        private static void CompareProperties(object actual, object expected, IEnumerable<PropertyInfo> properties,
            IList<string> path, 
            [InstantHandle] Func<string> customMessage, [CallerMemberName] string shouldlyMethod = null)
        {
            foreach (var property in properties)
            {
                var actualValue = property.GetValue(actual, new object[0]);
                var expectedValue = property.GetValue(expected, new object[0]);

                var newPath = path.Concat(new[] { property.Name });
                CompareObjects(actualValue, expectedValue, newPath.ToList(), customMessage, shouldlyMethod);
            }
        }

        private static void ThrowException(object actual, object expected, IEnumerable<string> path,
            [InstantHandle] Func<string> customMessage, [CallerMemberName] string shouldlyMethod = null)
        {
            throw new ShouldAssertException(
                new ExpectedEquvalenceShouldlyMessage(expected, actual, path, customMessage, shouldlyMethod).ToString());
        }

        private static bool Contains(this IDictionary<object, IList<object>> comparisons, object actual, object expected)
        {
            return comparisons.TryGetValue(actual, out var list)
                   && list.Contains(expected);
        }

        private static void Record(this IDictionary<object, IList<object>> comparisons, object actual,
            object expected)
        {
            if (comparisons.TryGetValue(actual, out var list))
                list.Add(expected);
            else
                comparisons.Add(actual, new List<object>(new[] { expected }));
        }
    }
}
