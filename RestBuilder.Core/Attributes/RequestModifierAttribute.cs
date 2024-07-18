using System;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Marks a method which is invoked before a request is made
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class RequestModifierAttribute : Attribute
{
	/// <summary>
	/// In which order the RequestModifiers should be invoked, lower values means earlier
	/// </summary>
	public int Order { get; set; }
}
