using System;

namespace Google.Cloud.Firestore
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class FirestoreDataAttribute : Attribute
    {
        public Type ConverterType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FirestorePropertyAttribute : Attribute
    {
        public FirestorePropertyAttribute() { }
        public FirestorePropertyAttribute(string name) { }
        public Type ConverterType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FirestoreDocumentIdAttribute : Attribute
    {
    }

    public class FirestoreEnumNameConverter<T> where T : struct, Enum
    {
    }
}
