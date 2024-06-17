using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RestBuilder.Enumerators;
using RestBuilder.Helpers;
using RestBuilder.Models;
using TypeShape.Roslyn;

namespace RestBuilder.Parsers;

public static class ClassParser
{
	public static ClassModel Parse(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol, ImmutableArray<AttributeData> attributes, Compilation compilation)
	{
		var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
		var className = classDeclaration!.Identifier.Text;
		
		var baseAddress = classSymbol
			.GetAttributes()
			.Where(w => w.AttributeClass.Name == nameof(Literals.BaseAddressAttribute))
			.Select(s => s.GetValue(0, String.Empty))
			.DefaultIfEmpty(String.Empty)
			.First()
			.TrimEnd('?', '&');

		var clientName = classSymbol
			.GetAttributes()
			.Where(w => w.AttributeClass.Name == nameof(Literals.RestClientAttribute))
			.Select(s => s.GetValue(0, String.Empty))
			.FirstOrDefault();

		var source = new ClassModel
		{
			Name = className,
			Namespace = namespaceName,
			IsStatic = classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword),
			IsDisposable = classSymbol.AllInterfaces.Any(a => a.IsType<IDisposable>()) &&
				!classSymbol
					.GetMembers()
					.OfType<IMethodSymbol>()
					.Any(a => !a.IsPartialDefinition && a.ReturnsVoid && a.Parameters.IsEmpty && a.Name == nameof(IDisposable.Dispose)),
			ClientName = clientName,
			NeedsClient = !classSymbol
				.GetMembers()
				.Any(w => w.Name == clientName && (w is IFieldSymbol field && field.Type.IsType<HttpClient>() ||
					w is IPropertySymbol propery && propery.Type.IsType<HttpClient>())),
			RequestModifiers = classSymbol
			.GetMembers()
				.OfType<IMethodSymbol>()
				.Where(w => w.Parameters.Length is > 0 and <= 2 && 
					w.Parameters[0].Type.IsType<HttpRequestMessage>() &&
					w.GetAttributes().Any(a => a.AttributeClass.Name == nameof(Literals.RequestModifierAttribute)))
				.OrderBy(o => o.GetAttributes()
					.Where(w => w.AttributeClass.Name == nameof(Literals.RequestModifierAttribute))
					.Select(s => s.GetValue("Order", 0))
					.FirstOrDefault())
				.Select(s => new RequestModifierModel
				{
					Name = s.Name,
					IsAsync = s.ReturnType.IsAwaitableType(),
					HasCancellation = s.Parameters.Length > 1 && s.Parameters[1].Type.IsType<CancellationToken>(),
				})
				.ToImmutableEquatableArray(),
			BaseAddress = baseAddress,
			Attributes = GetLocationAttributes(classSymbol.GetAttributes(), HttpLocation.None)
				.ToImmutableEquatableArray(),
			Methods = classSymbol
				.GetMembers()
				.OfType<IMethodSymbol>()
				.Where(w => w.IsPartialDefinition)
				.Select(s => new MethodModel
				{
					Name = s.Name,
					ReturnType = ToName(GetTaskTypeArgument(s.ReturnType, compilation)),
					ReturnNamespace = GetTaskTypeArgument(s.ReturnType, compilation)?.ContainingNamespace?.ToString(),
					Method = s.GetAttributes()
						.Select(x => x.AttributeClass.Name.Replace(nameof(Attribute), String.Empty))
						.Where(IsValidHttpMethod)
						.Select(x => new HttpMethod(x))
						.FirstOrDefault(),
					Path = s.GetAttributes()
						.Where(x => IsValidHttpMethod(x.AttributeClass.Name.Replace(nameof(Attribute), String.Empty)))
						.Select(x => x.GetValue(0, String.Empty))
						.FirstOrDefault()
						.TrimEnd('?', '&'),
					Locations = GetLocationAttributes(s.GetAttributes(), HttpLocation.None)
						.Where(w => w.Location != HttpLocation.None)
						.ToImmutableEquatableArray(),
					Parameters = s.Parameters
						.Select(x => new ParameterModel
						{
							Name = x.Name,
							Type = ToName(x.Type),
							IsNullable = x.Type.IsReferenceType,
							NullableAnnotation = x.NullableAnnotation,
							Namespace = x.Type.ContainingNamespace?.ToString() ?? String.Empty,
							Location = GetLocationAttribute(x.GetAttributes(), HttpLocation.Query),
							GenericTypes = GetGenericTypes(x.Type)
									.ToImmutableEquatableArray(),
							IsCollection = IsCollection(x.Type),
							CollectionItemType = GetCollectionItemType(x.Type)
						})
						.ToImmutableEquatableArray(),
					AllowAnyStatusCode = s.GetAttributes()
						.GetByName("AllowAnyStatusCode")
						.Select(x => x.GetValue(0, true))
						.FirstOrDefault()
				})
				.ToImmutableEquatableArray(),
			Properties = classSymbol
				.GetMembers()
				.OfType<IPropertySymbol>()
				.Select(s => new PropertyModel
				{
					IsNullable = s.Type.IsReferenceType,
					NullableAnnotation = s.NullableAnnotation,
					Name = s.Name,
					Namespace = s.Type.ContainingNamespace?.ToString(),
					Type = ToName(s.Type),
					Location = GetLocationAttribute(s.GetAttributes(), HttpLocation.None),
				})
				.Where(w => w.Location.Location != HttpLocation.None)
				.ToImmutableEquatableArray(),
		};

		return source;
	}

	public static ITypeSymbol? GetTaskTypeArgument(ITypeSymbol typeSymbol, Compilation compilation)
	{
		if (IsTaskType(typeSymbol, compilation))
		{
			if (typeSymbol is INamedTypeSymbol { TypeArguments.Length: > 0 } namedTypeSymbol)
			{
				return namedTypeSymbol.TypeArguments[0];
			}
		}

		return null;
	}

	public static bool IsTaskType(ITypeSymbol? typeSymbol, Compilation compilation)
	{
		if (typeSymbol == null)
		{
			return false;
		}

		var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
		var taskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

		if (typeSymbol.OriginalDefinition.Equals(taskType) || typeSymbol.OriginalDefinition.Equals(taskOfTType))
		{
			return true;
		}

		return false;
	}

	public static bool IsValidHttpMethod(string httpMethod)
	{
		var validMethods = new[]
		{
			HttpMethod.Get.Method,
			HttpMethod.Post.Method,
			HttpMethod.Put.Method,
			HttpMethod.Delete.Method,
			HttpMethod.Head.Method,
			HttpMethod.Options.Method,
			HttpMethod.Trace.Method,
		};

		return validMethods.Contains(httpMethod, StringComparer.InvariantCultureIgnoreCase);
	}

	private static string ToName(ITypeSymbol? type)
	{
		if (type is null)
		{
			return String.Empty;
		}

		return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
	}

	private static IEnumerable<AttributeData> GetByName(this ImmutableArray<AttributeData> attributes, string name)
	{
		return attributes.Where(w => w.AttributeClass.Name.Replace(nameof(Attribute), String.Empty) == name);
	}

	private static T GetValue<T>(this AttributeData attributes, int index, T defaultValue)
	{
		if (attributes.ConstructorArguments.Length <= index)
		{
			return defaultValue;
		}

		var result = attributes.ConstructorArguments[index].Value;

		if (result?.GetType() == typeof(T))
		{
			return (T)result;
		}

		return defaultValue;
	}

	private static T GetValue<T>(this AttributeData attributes, string name, T defaultValue)
	{
		if (attributes.NamedArguments.Length == 0)
		{
			return defaultValue;
		}

		foreach (var item in attributes.NamedArguments)
		{
			var result = item.Value.Value;

			if (item.Key == name && result != null && result.GetType() == typeof(T))
			{
				return (T)result;
			}
		}

		return defaultValue;
	}

	private static LocationAttributeModel GetLocationAttribute(ImmutableArray<AttributeData> attributes, HttpLocation defaultLocation)
	{
		return GetLocationAttributes(attributes, defaultLocation)
			.DefaultIfEmpty(new LocationAttributeModel
			{
				Location = HttpLocation.None,
			})
			.First();
	}

	private static IEnumerable<LocationAttributeModel> GetLocationAttributes(ImmutableArray<AttributeData> attributes, HttpLocation defaultLocation)
	{
		return attributes
			.Where(w => w.AttributeClass.ContainingNamespace.ToString() == Literals.BaseNamespace)
			.Select(y => new LocationAttributeModel
			{
				Location = y.AttributeClass.Name switch
				{
					nameof(Literals.QueryAttributes) => HttpLocation.Query,
					nameof(Literals.HeaderAttribute) => HttpLocation.Header,
					nameof(Literals.PathAttribute) => HttpLocation.Path,
					nameof(Literals.BodyAttribute) => HttpLocation.Body,
					nameof(Literals.QueryMapAttribute) => HttpLocation.QueryMap,
					nameof(Literals.RawQueryStringAttribute) => HttpLocation.Raw,
					_ => defaultLocation
				},
				Format = y.GetValue("Format", String.Empty),
				Name = y.GetValue<string?>(0, null) ?? y.GetValue<string?>("Name", null),
				Value = y.GetValue<string?>(1, null) ?? y.GetValue<string?>("Value", null),
				UrlEncode = y.GetValue("UrlEncode", true)
			});
	}

	private static bool IsCollection(ITypeSymbol type)
	{
		if (type.IsType<String>())
		{
			return false;
		}
		
		if (type is IArrayTypeSymbol)
		{
			return true;
		}

		foreach (var @interface in type.AllInterfaces)
		{
			if (@interface.ContainingNamespace?.ToString() == "System.Collections.Generic" && @interface.Name == nameof(IEnumerable))
			{
				return true;
			}
		}

		return type.ContainingNamespace?.ToString() == "System.Collections.Generic" && type.Name == nameof(IEnumerable);
	}

	private static TypeModel? GetCollectionItemType(ITypeSymbol type)
	{
		if (type.ContainingNamespace?.ToString() == "System" && type.Name == nameof(String))
		{
			return null;
		}

		if (type is IArrayTypeSymbol arrayTypeSymbol)
		{
			return GetTypeModel(arrayTypeSymbol.ElementType);
		}

		foreach (var @interface in type.AllInterfaces)
		{
			if (@interface.ContainingNamespace?.ToString() == "System.Collections.Generic" && @interface.Name == nameof(IEnumerable))
			{
				return GetTypeModel(@interface.TypeArguments[0]);
			}
		}

		if (type.ContainingNamespace?.ToString() == "System.Collections.Generic" && type.Name == nameof(IEnumerable) && type is INamedTypeSymbol namedTypeSymbol)
		{
			return GetTypeModel(namedTypeSymbol.TypeArguments[0]);
		}

		return null;
	}

	private static TypeModel GetTypeModel(ITypeSymbol type)
	{
		return new TypeModel
		{
			Name = type.Name,
			IsNullable = type.IsReferenceType,
			NullableAnnotation = type.NullableAnnotation,
			IsCollection = IsCollection(type),
			Namespace = type.ContainingNamespace?.ToString(),
			CollectionType = GetCollectionItemType(type),
			Type = ToName(type),
		};
	}

	private static IEnumerable<TypeModel> GetGenericTypes(ITypeSymbol type)
	{
		foreach (var @interface in type.AllInterfaces)
		{
			if (@interface.ContainingNamespace?.ToString() == "System.Collections.Generic" && @interface.Name == nameof(IDictionary))
			{
				return @interface.TypeArguments.Select(GetTypeModel);
			}
		}

		if (type is INamedTypeSymbol { TypeArguments.Length: > 0 } namedTypeSymbol)
		{
			return namedTypeSymbol.TypeArguments.Select(GetTypeModel);
		}

		return Enumerable.Empty<TypeModel>();
	}
}