using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Util
{
    [TestFixture]
    public class TypeHelperTests
    {
        private enum SampleEnum
        {
            A,
            B,
        }

        [AttributeUsage(AttributeTargets.Class)]
        private class SampleAttribute : Attribute { }

        [Sample]
        private class TaggedClass { }

        private class UntaggedClass { }

        private static IEnumerable<Type> ScalarTypes()
        {
            yield return typeof(string);
            yield return typeof(bool);
            yield return typeof(byte);
            yield return typeof(sbyte);
            yield return typeof(short);
            yield return typeof(ushort);
            yield return typeof(int);
            yield return typeof(uint);
            yield return typeof(long);
            yield return typeof(ulong);
            yield return typeof(float);
            yield return typeof(double);
            yield return typeof(decimal);
            yield return typeof(char);
            yield return typeof(SampleEnum);
            yield return typeof(DateTime);
            yield return typeof(DateTimeOffset);
            yield return typeof(TimeSpan);
            yield return typeof(Guid);
        }

        private static IEnumerable<TestCaseData> ScalarParseCases()
        {
            yield return new TestCaseData("hello", typeof(string), "hello");
            yield return new TestCaseData("True", typeof(bool), true);
            yield return new TestCaseData("8", typeof(byte), (byte)8);
            yield return new TestCaseData("-8", typeof(sbyte), (sbyte)-8);
            yield return new TestCaseData("-16", typeof(short), (short)-16);
            yield return new TestCaseData("16", typeof(ushort), (ushort)16);
            yield return new TestCaseData("-32", typeof(int), -32);
            yield return new TestCaseData("32", typeof(uint), 32u);
            yield return new TestCaseData("-64", typeof(long), -64L);
            yield return new TestCaseData("64", typeof(ulong), 64ul);
            yield return new TestCaseData("1.25", typeof(float), 1.25f);
            yield return new TestCaseData("2.5", typeof(double), 2.5d);
            yield return new TestCaseData("3.75", typeof(decimal), 3.75m);
            yield return new TestCaseData("Q", typeof(char), 'Q');
            yield return new TestCaseData("B", typeof(SampleEnum), SampleEnum.B);
            yield return new TestCaseData(
                "2024-01-15T12:00:00.0000000Z",
                typeof(DateTime),
                new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc)
            );
            yield return new TestCaseData(
                "2024-01-15T12:00:00.0000000+00:00",
                typeof(DateTimeOffset),
                new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero)
            );
            yield return new TestCaseData("01:02:03", typeof(TimeSpan), new TimeSpan(1, 2, 3));
            yield return new TestCaseData(
                "5f6a6ec4-655d-497b-8d10-bbd8dc155cfd",
                typeof(Guid),
                Guid.Parse("5f6a6ec4-655d-497b-8d10-bbd8dc155cfd")
            );
        }

        private static IEnumerable<TestCaseData> ScalarStringCases()
        {
            yield return new TestCaseData("hello", "hello");
            yield return new TestCaseData(true, "True");
            yield return new TestCaseData((byte)8, "8");
            yield return new TestCaseData((sbyte)-8, "-8");
            yield return new TestCaseData((short)-16, "-16");
            yield return new TestCaseData((ushort)16, "16");
            yield return new TestCaseData(-32, "-32");
            yield return new TestCaseData(32u, "32");
            yield return new TestCaseData(-64L, "-64");
            yield return new TestCaseData(64ul, "64");
            yield return new TestCaseData(1.25f, "1.25");
            yield return new TestCaseData(2.5d, "2.5");
            yield return new TestCaseData(3.75m, "3.75");
            yield return new TestCaseData('Q', "Q");
            yield return new TestCaseData(SampleEnum.B, "B");
            yield return new TestCaseData(
                new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                "2024-01-15T12:00:00.0000000Z"
            );
            yield return new TestCaseData(
                new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero),
                "2024-01-15T12:00:00.0000000+00:00"
            );
            yield return new TestCaseData(new TimeSpan(1, 2, 3), "01:02:03");
            yield return new TestCaseData(
                Guid.Parse("5f6a6ec4-655d-497b-8d10-bbd8dc155cfd"),
                "5f6a6ec4-655d-497b-8d10-bbd8dc155cfd"
            );
        }

        [TestCaseSource(nameof(ScalarTypes))]
        public void IsScalar_SupportedType_ReturnsTrue(Type type)
        {
            Assert.IsTrue(TypeHelper.IsScalar(type));
        }

        [Test]
        public void IsScalar_Class_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsScalar(typeof(UntaggedClass)));
        }

        [Test]
        public void IsValueType_Struct_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsValueType(typeof(int)));
        }

        [Test]
        public void IsValueType_String_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsValueType(typeof(string)));
        }

        [Test]
        public void IsValueType_Class_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsValueType(typeof(UntaggedClass)));
        }

        [Test]
        public void IsStruct_ValueTypeNonEnum_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsStruct(typeof(DateTime)));
        }

        [Test]
        public void IsStruct_Enum_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsStruct(typeof(SampleEnum)));
        }

        [Test]
        public void IsStruct_ClrPrimitive_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsStruct(typeof(int)));
        }

        [Test]
        public void IsStruct_Class_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsStruct(typeof(UntaggedClass)));
        }

        [Test]
        public void IsEnumerable_List_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsEnumerable(typeof(List<int>)));
        }

        [Test]
        public void IsEnumerable_String_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsEnumerable(typeof(string)));
        }

        [Test]
        public void IsEnumerable_Int_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsEnumerable(typeof(int)));
        }

        [Test]
        public void IsDictionary_GenericDictionary_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsDictionary(typeof(Dictionary<string, int>)));
        }

        [Test]
        public void IsDictionary_List_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsDictionary(typeof(List<int>)));
        }

        [Test]
        public void IsClass_Class_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsClass(typeof(UntaggedClass)));
        }

        [Test]
        public void IsClass_Struct_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsClass(typeof(DateTime)));
        }

        [Test]
        public void IsTuple_Tuple2_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsTuple(typeof(Tuple<int, string>)));
        }

        [Test]
        public void IsTuple_NonGenericType_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsTuple(typeof(int)));
        }

        [Test]
        public void IsTuple_List_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsTuple(typeof(List<int>)));
        }

        [Test]
        public void HasAttribute_Generic_TypeHasAttribute_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.HasAttribute<SampleAttribute>(typeof(TaggedClass)));
        }

        [Test]
        public void HasAttribute_Generic_TypeLacksAttribute_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.HasAttribute<SampleAttribute>(typeof(UntaggedClass)));
        }

        [Test]
        public void HasAttribute_NonGeneric_TypeHasAttribute_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.HasAttribute(typeof(TaggedClass), typeof(SampleAttribute)));
        }

        [Test]
        public void HasAttribute_NonGeneric_NonAttributeType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                TypeHelper.HasAttribute(typeof(TaggedClass), typeof(string))
            );
        }

        [TestCaseSource(nameof(ScalarParseCases))]
        public void ConvertToScalar_SupportedType_ReturnsParsedValue(
            string content,
            Type targetType,
            object expected
        )
        {
            Assert.AreEqual(expected, TypeHelper.ConvertToScalar(content, targetType));
        }

        [Test]
        public void ConvertToScalar_UnsupportedType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                TypeHelper.ConvertToScalar("x", typeof(UntaggedClass))
            );
        }

        [Test]
        public void ConvertScalarToString_Null_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, TypeHelper.ConvertScalarToString(null));
        }

        [TestCaseSource(nameof(ScalarStringCases))]
        public void ConvertScalarToString_SupportedValue_ReturnsSerializedValue(
            object value,
            string expected
        )
        {
            Assert.AreEqual(expected, TypeHelper.ConvertScalarToString(value));
        }

        [Test]
        public void ConvertScalarToString_UnsupportedType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                TypeHelper.ConvertScalarToString(new UntaggedClass())
            );
        }
    }
}
