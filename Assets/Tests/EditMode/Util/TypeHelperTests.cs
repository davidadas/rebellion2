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

        [Test]
        public void IsPrimitive_Int_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsPrimitive(typeof(int)));
        }

        [Test]
        public void IsPrimitive_String_ReturnsTrue()
        {
            Assert.IsTrue(TypeHelper.IsPrimitive(typeof(string)));
        }

        [Test]
        public void IsPrimitive_Class_ReturnsFalse()
        {
            Assert.IsFalse(TypeHelper.IsPrimitive(typeof(UntaggedClass)));
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
        public void IsStruct_Primitive_ReturnsFalse()
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

        [Test]
        public void ConvertToPrimitive_Int_ReturnsParsedValue()
        {
            Assert.AreEqual(42, TypeHelper.ConvertToPrimitive("42", typeof(int)));
        }

        [Test]
        public void ConvertToPrimitive_String_ReturnsSameValue()
        {
            Assert.AreEqual("hello", TypeHelper.ConvertToPrimitive("hello", typeof(string)));
        }

        [Test]
        public void ConvertToPrimitive_Bool_ReturnsParsedValue()
        {
            Assert.AreEqual(true, TypeHelper.ConvertToPrimitive("True", typeof(bool)));
        }

        [Test]
        public void ConvertToPrimitive_Float_ReturnsParsedValue()
        {
            Assert.AreEqual(1.5f, TypeHelper.ConvertToPrimitive("1.5", typeof(float)));
        }

        [Test]
        public void ConvertToPrimitive_Double_ReturnsParsedValue()
        {
            Assert.AreEqual(
                3.14,
                (double)TypeHelper.ConvertToPrimitive("3.14", typeof(double)),
                0.0001
            );
        }

        [Test]
        public void ConvertToPrimitive_Long_ReturnsParsedValue()
        {
            Assert.AreEqual(9999999999L, TypeHelper.ConvertToPrimitive("9999999999", typeof(long)));
        }

        [Test]
        public void ConvertToPrimitive_Enum_ReturnsParsedValue()
        {
            Assert.AreEqual(SampleEnum.B, TypeHelper.ConvertToPrimitive("B", typeof(SampleEnum)));
        }

        [Test]
        public void ConvertToPrimitive_UnsupportedType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                TypeHelper.ConvertToPrimitive("x", typeof(DateTime))
            );
        }

        [Test]
        public void ConvertToString_Null_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, TypeHelper.ConvertToString(null));
        }

        [Test]
        public void ConvertToString_Int_ReturnsStringRepresentation()
        {
            Assert.AreEqual("7", TypeHelper.ConvertToString(7));
        }

        [Test]
        public void ConvertToString_Enum_ReturnsName()
        {
            Assert.AreEqual("A", TypeHelper.ConvertToString(SampleEnum.A));
        }

        [Test]
        public void ConvertToString_DateTime_ReturnsIso8601()
        {
            DateTime dt = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            string result = TypeHelper.ConvertToString(dt);
            StringAssert.StartsWith("2024-01-15T12:00:00", result);
        }

        [Test]
        public void ConvertToString_DateTimeOffset_ReturnsIso8601()
        {
            DateTimeOffset dto = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
            string result = TypeHelper.ConvertToString(dto);
            StringAssert.StartsWith("2024-01-15T12:00:00", result);
        }

        [Test]
        public void ConvertToString_TimeSpan_ReturnsConstantFormat()
        {
            TimeSpan ts = new TimeSpan(1, 2, 3);
            Assert.AreEqual("01:02:03", TypeHelper.ConvertToString(ts));
        }
    }
}
