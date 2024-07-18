using System;

namespace RestBuilder.Core.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class RequestQueryParamSerializerAttribute : Attribute
{
}
