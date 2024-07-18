using System;

namespace RestBuilder.Core.Attributes
{
	/// <summary>
	/// Marks a method which is used to process the request body
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public class RequestBodySerializerAttribute : Attribute
	{
	}
}
