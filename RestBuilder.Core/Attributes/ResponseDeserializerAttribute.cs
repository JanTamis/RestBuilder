using System;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Marks a method which is used to deserialize the response
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class ResponseDeserializerAttribute : Attribute
{
}
