﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FluentAssertions.Json
{
    internal static class JTokenDifferentiator
    {
        public static bool treatArrayAsSet = false;
        public static Difference FindFirstDifference(JToken actual, JToken expected)
        {
            var path = new JPath();
            
            if (actual == expected)
            {
                return null;
            }

            if (actual == null)
            {
                return new Difference(DifferenceKind.ActualIsNull, path);
            }

            if (expected == null)
            {
                return new Difference(DifferenceKind.ExpectedIsNull, path);
            }
            
            return FindFirstDifference(actual, expected, path);
        }

        private static Difference FindFirstDifference(JToken actual, JToken expected, JPath path)
        {
            switch (actual)
            {
                case JArray actualArray:
                    return FindJArrayDifference(actualArray, expected, path);
                case JObject actualObbject:
                    return FindJObjectDifference(actualObbject, expected, path);
                case JProperty actualProperty:
                    return FindJPropertyDifference(actualProperty, expected, path);
                case JValue actualValue:
                    return FindValueDifference(actualValue, expected, path);
                default: 
                    throw new NotSupportedException();
            }
        }

        private static Difference FindJArrayDifference(JArray actualArray, JToken expected, JPath path)
        {
            if (!(expected is JArray expectedArray))
            {
                return new Difference(DifferenceKind.OtherType, path);
            }
            
            return CompareItems(actualArray, expectedArray, path);
        }

        private static Difference CompareItems(JArray actual, JArray expected, JPath path)
        {
            if (treatArrayAsSet)
            {
                JEnumerable<JToken> actualChildren = actual.Children();
                JEnumerable<JToken> expectedChildren = expected.Children();
                for (int t = 0; t < 2; t++) 
                {
                    for (int i = 0; i < actualChildren.Count(); i++)
                    {
                        bool exist = false;
                        for (int j = 0; j < expectedChildren.Count(); j++)
                        {
                            Difference firstDifference = FindFirstDifference(actualChildren.ElementAt(i), expectedChildren.ElementAt(j), 
                            path.AddIndex(i));
                            if (firstDifference == null)
                            {
                                exist = true;
                                break;
                            }
                        }
                        if (!exist)
                            return new Difference(DifferenceKind.OtherSet, path);
                    }
                    // swap and compare sets in another direction
                    actualChildren = expected.Children();
                    expectedChildren = actual.Children();
                }

                return null;
            } else {
                JEnumerable<JToken> actualChildren = actual.Children();
                JEnumerable<JToken> expectedChildren = expected.Children();

                if (actualChildren.Count() != expectedChildren.Count())
                {
                    return new Difference(DifferenceKind.DifferentLength, path);
                }
                
                for (int i = 0; i < actualChildren.Count(); i++)
                {
                    Difference firstDifference = FindFirstDifference(actualChildren.ElementAt(i), expectedChildren.ElementAt(i), 
                        path.AddIndex(i));

                    if (firstDifference != null)
                    {
                        return firstDifference;
                    }
                }
                return null;
            }
        }

        private static Difference FindJObjectDifference(JObject actual, JToken expected, JPath path)
        {
            if (!(expected is JObject expectedObject))
            {
                return new Difference(DifferenceKind.OtherType, path);
            }

            return CompareProperties(actual?.Properties(), expectedObject.Properties(), path);
        }

        private static Difference CompareProperties(IEnumerable<JProperty> actual, IEnumerable<JProperty> expected, JPath path)
        {
            var actualDictionary = actual?.ToDictionary(p => p.Name, p => p.Value) ?? new Dictionary<string, JToken>();
            var expectedDictionary = expected?.ToDictionary(p => p.Name, p => p.Value) ?? new Dictionary<string, JToken>();

            foreach (KeyValuePair<string, JToken> expectedPair in expectedDictionary)
            {
                if (!actualDictionary.ContainsKey(expectedPair.Key))
                {
                    return new Difference(DifferenceKind.ActualMissesProperty, path.AddProperty(expectedPair.Key));
                }
            }

            foreach (KeyValuePair<string, JToken> actualPair in actualDictionary)
            {
                if (!expectedDictionary.ContainsKey(actualPair.Key))
                {
                    return new Difference(DifferenceKind.ExpectedMissesProperty, path.AddProperty(actualPair.Key));
                }
            }

            foreach (KeyValuePair<string, JToken> expectedPair in expectedDictionary)
            {
                JToken actualValue = actualDictionary[expectedPair.Key];

                Difference firstDifference = FindFirstDifference(actualValue, expectedPair.Value, 
                    path.AddProperty(expectedPair.Key));
                
                if (firstDifference != null)
                {
                    return firstDifference;
                }
            }

            return null;
        }

        private static Difference FindJPropertyDifference(JProperty actualProperty, JToken expected, JPath path)
        {
            if (!(expected is JProperty expectedProperty))
            {
                return new Difference(DifferenceKind.OtherType, path);
            }

            if (actualProperty.Name != expectedProperty.Name)
            {
                return new Difference(DifferenceKind.OtherName, path);
            }
            
            return FindFirstDifference(actualProperty.Value, expectedProperty.Value, path);
        }

        private static Difference FindValueDifference(JValue actualValue, JToken expected, JPath path)
        {
            if (!(expected is JValue expectedValue))
            {
                return new Difference(DifferenceKind.OtherType, path);
            }
            
            return CompareValues(actualValue, expectedValue, path);
        }

        private static Difference CompareValues(JValue actual, JValue expected, JPath path)
        {
            if (actual.Type != expected.Type)
            {
                return new Difference(DifferenceKind.OtherType, path);
            }
            
            if (!actual.Equals(expected))
            {
                return new Difference(DifferenceKind.OtherValue, path);
            }
            
            return null;
        }
    }

    internal class Difference
    {
        public Difference(DifferenceKind kind, JPath path)
        {
            Kind = kind;
            Path = path;
        }

        private DifferenceKind Kind { get; }

        private JPath Path { get; }

        public override string ToString()
        {
            switch (Kind)
            {
                case DifferenceKind.ActualIsNull:
                    return "is null";
                case DifferenceKind.ExpectedIsNull:
                    return "is not null";
                case DifferenceKind.OtherType:
                    return $"has a different type at {Path}";
                case DifferenceKind.OtherName:
                    return $"has a different name at {Path}";
                case DifferenceKind.OtherValue:
                    return $"has a different value at {Path}";
                case DifferenceKind.DifferentLength:
                    return $"has a different length at {Path}";
                case DifferenceKind.ActualMissesProperty:
                    return $"misses property {Path}";
                case DifferenceKind.ExpectedMissesProperty:
                    return $"has extra property {Path}";
                case DifferenceKind.OtherSet:
                    return $"has a mismatch in set at {Path}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    internal class JPath
    {
        private readonly List<string> nodes = new List<string>();
        
        public JPath()
        {
            nodes.Add("$");            
        }

        private JPath(JPath existingPath, string extraNode)
        {
            nodes.AddRange(existingPath.nodes);
            nodes.Add(extraNode);
        }

        public JPath AddProperty(string name)
        {
            return new JPath(this, $".{name}");
        }

        public JPath AddIndex(int index)
        {
            return new JPath(this, $"[{index}]");
        }

        public override string ToString()
        {
            return string.Join("", nodes);
        }
    }

    internal enum DifferenceKind
    {
        ActualIsNull,
        ExpectedIsNull,
        OtherType,
        OtherName,
        OtherValue,
        DifferentLength,
        ActualMissesProperty,
        ExpectedMissesProperty,
        OtherSet
    }
}