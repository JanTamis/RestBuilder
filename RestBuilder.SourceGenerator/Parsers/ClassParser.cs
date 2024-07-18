using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RestBuilder.Core.Attributes;
using RestBuilder.SourceGenerator.Enumerators;
using RestBuilder.SourceGenerator.Helpers;
using RestBuilder.SourceGenerator.Interfaces;
using RestBuilder.SourceGenerator.Models;
using TypeShape.Roslyn;

namespace RestBuilder.SourceGenerator.Parsers;

public static class ClassParser
{
	public static ClassModel Parse(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol, ImmutableArray<AttributeData> attributes, Compilation compilation)
	{
		var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
		var className = classDeclaration.Identifier.Text;

		var baseAddress = GetBaseAddress(classSymbol, compilation);
		var clientName = GetClientName(classSymbol, compilation);
		var allowAnyStatusCode = GetAllowAnyStatusCode(classSymbol, compilation);

		var source = new ClassModel
		{
			Name = className,
			Namespace = namespaceName,
			IsStatic = classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword),
			IsDisposable = IsDisposable(classSymbol, compilation),
			ClientName = clientName!,
			NeedsClient = !GetNeedsClient(classSymbol, clientName, compilation),
			ResponseDeserializers = GetResponseDeserializers(classSymbol, compilation),
			RequestBodySerializers = GetRequestBodySerializers(classSymbol, compilation),
			RequestModifiers = GetRequestModifiers(classSymbol, compilation),
			RequestQueryParamSerializers = GetRequestQueryParamSerializerModels(classSymbol, compilation),
			HttpClientInitializer = GetHttpClientInitializer(classSymbol, compilation),
			BaseAddress = baseAddress,
			Attributes = GetLocationAttributes(classSymbol.GetAttributes(), HttpLocation.None)
				.ToImmutableEquatableArray(),
			Methods = GetMethods(classSymbol, compilation, allowAnyStatusCode),
			Properties = GetProperties(classSymbol),
		};

		return source;
	}

	public static ITypeSymbol? GetTaskTypeArgument(ITypeSymbol typeSymbol, Compilation compilation)
	{
		if (typeSymbol.IsTaskType(compilation))
		{
			if (typeSymbol is INamedTypeSymbol { TypeArguments.Length: > 0 } namedTypeSymbol)
			{
				return namedTypeSymbol.TypeArguments[0];
			}
		}

		return null;
	}

	public static bool IsValidHttpMethod(string? httpMethod)
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

	private static LocationAttributeModel GetLocationAttribute(ImmutableArray<AttributeData> attributes, HttpLocation defaultLocation)
	{
		return GetLocationAttributes(attributes, defaultLocation)
			.FirstOrDefault(new LocationAttributeModel
			{
				Location = HttpLocation.None,
			});
	}

	private static IEnumerable<LocationAttributeModel> GetLocationAttributes(ImmutableArray<AttributeData> attributes, HttpLocation defaultLocation)
	{
		return attributes
			.Where(w => w.AttributeClass?.ContainingNamespace?.ToString() == "RestBuilder.Core.Attributes")
			.Select(y => new LocationAttributeModel
			{
				Location = y.AttributeClass?.Name switch
				{
					nameof(QueryAttribute) => HttpLocation.Query,
					nameof(HeaderAttribute) => HttpLocation.Header,
					nameof(PathAttribute) => HttpLocation.Path,
					nameof(BodyAttribute) => HttpLocation.Body,
					nameof(QueryMapAttribute) => HttpLocation.QueryMap,
					nameof(RawQueryStringAttribute) => HttpLocation.Raw,
					_ => defaultLocation
				},
				Format = y.GetValue("Format", String.Empty),
				Name = y.GetValue<string?>(0, null) ?? y.GetValue<string?>("Name", null),
				Value = y.GetValue<string?>(1, null) ?? y.GetValue<string?>("Value", null),
				UrlEncode = y.GetValue("UrlEncode", true)
			});
	}

	private static TypeModel? GetCollectionItemType(ITypeSymbol type, Compilation compilation)
	{
		if (type.ContainingNamespace?.ToString() == "System" && type.Name == nameof(String))
		{
			return null;
		}

		if (type is IArrayTypeSymbol arrayTypeSymbol)
		{
			return GetTypeModel(arrayTypeSymbol.ElementType, compilation);
		}

		foreach (var @interface in type.AllInterfaces)
		{
			if (@interface.ContainingNamespace?.ToString() == "System.Collections.Generic" && @interface.Name == nameof(IEnumerable))
			{
				return GetTypeModel(@interface.TypeArguments[0], compilation);
			}
		}

		if (type.ContainingNamespace?.ToString() == "System.Collections.Generic" && type.Name == nameof(IEnumerable) && type is INamedTypeSymbol namedTypeSymbol)
		{
			return GetTypeModel(namedTypeSymbol.TypeArguments[0], compilation);
		}

		return null;
	}

	private static TypeModel? GetTypeModel(ITypeSymbol? type, Compilation compilation)
	{
		if (type is null)
		{
			return null;
		}

		if (type is IArrayTypeSymbol arrayType)
		{
			var ranks = String.Concat(Enumerable.Repeat("[]", arrayType.Rank));

			return new TypeModel
			{
				Name = $"{ToName(arrayType.ElementType)}{ranks}",
				IsNullable = arrayType.IsReferenceType,
				NullableAnnotation = type.NullableAnnotation,
				IsCollection = true,
				Namespace = "System",
				CollectionType = GetCollectionItemType(type, compilation),
				Type = $"{ToName(arrayType.ElementType)}{ranks}",
				IsGeneric = arrayType.ElementType is INamedTypeSymbol { IsGenericType: true }
			};
		}

		return new TypeModel
		{
			Name = type.Name,
			IsNullable = type.IsReferenceType,
			NullableAnnotation = type.NullableAnnotation,
			IsCollection = type.IsCollection(compilation),
			Namespace = type.ContainingNamespace?.ToString() ?? String.Empty,
			CollectionType = GetCollectionItemType(type, compilation),
			Type = ToName(type),
			IsGeneric = type is ITypeParameterSymbol or INamedTypeSymbol { IsGenericType: true },
		};
	}

	private static IEnumerable<TypeModel> GetGenericTypes(ITypeSymbol type, Compilation compilation)
	{
		foreach (var @interface in type.AllInterfaces)
		{
			if (@interface.ContainingNamespace?.ToString() == "System.Collections.Generic"
					&& @interface.Name == nameof(IDictionary))
			{
				return @interface.TypeArguments.Select(s => GetTypeModel(s, compilation));
			}
		}

		if (type is INamedTypeSymbol { TypeArguments.Length: > 0 } namedTypeSymbol)
		{
			return namedTypeSymbol.TypeArguments.Select(s => GetTypeModel(s, compilation));
		}

		return Enumerable.Empty<TypeModel>();
	}

	private static bool IsDisposable(ITypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol.AllInterfaces.Any(a => a.IsType<IDisposable>(compilation))
					 && !classSymbol
								.GetMembers()
								.OfType<IMethodSymbol>()
								.Any(a => !a.IsPartialDefinition && a is { ReturnsVoid: true, Parameters.IsEmpty: true, Name: nameof(IDisposable.Dispose) });
	}

	private static ImmutableEquatableArray<ResponseDeserializerModel> GetResponseDeserializers(INamedTypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Where(w => w.HasAttribute<ResponseDeserializerAttribute>(compilation)
									&& (w.HasParameters<HttpResponseMessage>(compilation)
										|| w.HasParameters<HttpResponseMessage, CancellationToken>(compilation)
										&& (!w.IsGenericMethod || w.TypeArguments.Length == 1)))
			.Select(s => new ResponseDeserializerModel
			{
				Name = s.Name,
				IsAsync = s.ReturnType.IsAwaitableType(),
				HasCancellation = s.HasParameters<HttpResponseMessage, CancellationToken>(compilation),
				Type = GetTypeModel(s.ReturnType.GetAwaitableReturnType() ?? s.ReturnType, compilation),
			})
			.ToImmutableEquatableArray();
	}

	private static ImmutableEquatableArray<RequestBodySerializerModel> GetRequestBodySerializers(INamedTypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Where(w => w.HasAttribute<RequestBodySerializerAttribute>(compilation)
									&& (w.ReturnType.IsType<HttpContent>(compilation)
										|| w.ReturnType.GetAwaitableReturnType()?.IsType<HttpContent>(compilation) == true)
										&& w.Parameters.Length is 1 or 2)
			.Select(s => new RequestBodySerializerModel
			{
				Name = s.Name,
				Type = GetTypeModel(s.Parameters[0].Type, compilation),
				IsAsync = s.ReturnType.IsAwaitableType(),
				HasCancellation = s.Parameters.Length > 1 && s.Parameters[1].Type.IsType<CancellationToken>(compilation),
			})
			.ToImmutableEquatableArray();
	}

	private static ImmutableEquatableArray<RequestModifierModel> GetRequestModifiers(INamedTypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Select(s => Tuple.Create(s, s.GetAttribute<RequestModifierAttribute>(compilation)))
			.Where(w => w.Item2 != null)
			.OrderBy(o => o.Item2.Order)
			.Select(s => new RequestModifierModel
			{
				Name = s.Item1.Name,
				IsAsync = s.Item1.ReturnType.IsAwaitableType(),
				HasCancellation = s.Item1.Parameters.Length > 1 && s.Item1.Parameters[1].Type.IsType<CancellationToken>(compilation),
			})
			.ToImmutableEquatableArray();
	}

	private static ImmutableEquatableArray<MethodModel> GetMethods(INamedTypeSymbol classSymbol, Compilation compilation, bool allowAnyStatusCode)
	{
		return classSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Where(w => w.IsPartialDefinition)
			.Select(s => new MethodModel
			{
				Name = s.Name,
				ReturnTypeName = ToName(s.ReturnType),
				ReturnType = s.ReturnType.IsAwaitableType()
					? GetTypeModel(s.ReturnType.GetAwaitableReturnType(), compilation)
					: GetTypeModel(s.ReturnType, compilation),
				IsAwaitable = s.ReturnType.IsAwaitableType(),
				ReturnNamespace = s.ReturnType?.ContainingNamespace?.ToString() ?? String.Empty,
				Method = GetHttpMethod(s),
				Path = s.GetAttributes()
					.Where(x => IsValidHttpMethod(x.AttributeClass?.Name?.Replace(nameof(Attribute), String.Empty)))
					.Select(x => x.GetValue(0, String.Empty))
					.FirstOrDefault(String.Empty)
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
						GenericTypes = GetGenericTypes(x.Type, compilation).ToImmutableEquatableArray(),
						IsCollection = x.Type.IsCollection(compilation),
						CollectionItemType = GetCollectionItemType(x.Type, compilation)
					})
					.ToImmutableEquatableArray(),
				AllowAnyStatusCode = s.GetAttribute<AllowAnyStatusCodeAttribute>(compilation)?.AllowAnyStatusCode ?? true,
			})
			.ToImmutableEquatableArray();
	}

	private static bool GetNeedsClient(INamedTypeSymbol classSymbol, string? clientName, Compilation compilation)
	{
		return classSymbol
			.GetMembers()
			.Any(w => w.Name == clientName && 
								(w is IFieldSymbol field 
									&& field.Type.IsType<HttpClient>(compilation) 
									|| w is IPropertySymbol propery && propery.Type.IsType<HttpClient>(compilation)));
	}

	private static ImmutableEquatableArray<PropertyModel> GetProperties(INamedTypeSymbol classSymbol)
	{
		return classSymbol
			.GetMembers()
			.OfType<IPropertySymbol>()
			.Select(s => new PropertyModel
			{
				IsNullable = s.Type.IsReferenceType,
				NullableAnnotation = s.NullableAnnotation,
				Name = s.Name,
				Namespace = s.Type.ContainingNamespace?.ToString() ?? String.Empty,
				Type = ToName(s.Type),
				Location = GetLocationAttribute(s.GetAttributes(), HttpLocation.None),
			})
			.Where(w => w.Location.Location != HttpLocation.None)
			.ToImmutableEquatableArray();
	}

	private static string GetBaseAddress(INamedTypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol
			.GetAttribute<BaseAddressAttribute>(compilation).BaseAddress
			.TrimEnd('?', '&');
	}

	private static string? GetClientName(INamedTypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol.GetAttribute<RestClientAttribute>(compilation).Name;
	}

	private static bool GetAllowAnyStatusCode(INamedTypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol.GetAttribute<AllowAnyStatusCodeAttribute>(compilation).AllowAnyStatusCode;
	}

	private static string? GetHttpClientInitializer(INamedTypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol
			.GetMethods()
			.Where(w => w.IsStatic 
									&& w.HasAttribute<HttpClientInitializerAttribute>(compilation) 
									&& w.Parameters.Length == 0 
									&& w.HasReturnType<HttpClient>(compilation))
			.Select(s => s.Name)
			.FirstOrDefault();
	}

	public static HttpMethod? GetHttpMethod(IMethodSymbol s)
	{
		return s.GetAttributes()
			.Select(x => x.AttributeClass?.Name?.Replace(nameof(Attribute), String.Empty))
			.Where(IsValidHttpMethod)
			.Select(x => new HttpMethod(x))
			.FirstOrDefault();
	}

	private static ImmutableEquatableArray<RequestQueryParamSerializerModel> GetRequestQueryParamSerializerModels(INamedTypeSymbol classSymbol, Compilation compilation)
	{
		return classSymbol
			.GetMethods()
			.Where(w => w.HasAttribute<RequestQueryParamSerializerAttribute>(compilation)
									&& w.ReturnType.IsType<IEnumerable<KeyValuePair<string, string>>>(compilation)
									&& w.Parameters.Length >= 2
									&& w.Parameters[0].Type.IsType<string>(compilation))
			.Select(s => new RequestQueryParamSerializerModel
			{
				ValueType = GetTypeModel(s.Parameters[1].Type, compilation),
				IsAsync = false, //s.ReturnType.IsType<IAsyncEnumerable<KeyValuePair<string, string>>>(compilation),
				Name = s.Name,
				IsCollection = s.Parameters[1].Type.IsCollection(compilation),
			})
			.ToImmutableEquatableArray();
	}

	public static bool TypeEquals(IType? x, IType? y)
	{
		if (x is null && y is null)
		{
			return true;
		}

		if (x is null || y is null)
		{
			return false;
		}

		return x.Type == y.Type &&
					 x.Namespace == y.Namespace;
		// x.NullableAnnotation == y.NullableAnnotation &&
		// x.IsNullable == y.IsNullable;
	}
}