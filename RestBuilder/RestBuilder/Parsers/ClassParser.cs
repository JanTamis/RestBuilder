using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RestBuilder.Enumerators;
using RestBuilder.Models;
using TypeShape.Roslyn;

namespace RestBuilder.Parsers;

public static class ClassParser
{
	public static ClassModel Parse(InterfaceDeclarationSyntax interfaceDeclaration, INamedTypeSymbol interfaceSymbol, ImmutableArray<AttributeData> attributes, Compilation compilation)
	{
		var namespaceName = interfaceSymbol.ContainingNamespace.ToDisplayString();
		var className = interfaceDeclaration!.Identifier.Text;
		var baseAddress = attributes.First(f => f.AttributeClass.Name == "BaseAddressAttribute").ConstructorArguments[0].Value as string;

		var source = new ClassModel
		{
			Name = className,
			Namespace = namespaceName,
			IsStatic = interfaceDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword),
			BaseAddress = baseAddress,
			Methods = interfaceSymbol
				.GetMembers()
				.OfType<IMethodSymbol>()
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
						.Select(x => x.GetValue(String.Empty))
						.FirstOrDefault(),
					Parameters = s.Parameters
						.Select(x => new ParameterModel
						{
							Name = x.Name,
							Type = ToName(x.Type),
							IsNullable = x.Type.IsReferenceType,
							Namespace = x.Type.ContainingNamespace?.ToString(),
							Location = x.GetAttributes()
								.Where(w => w.AttributeClass.ContainingNamespace.ToString() == Literals.BaseNamespace)
								.Select(y =>
								{
									return y.AttributeClass.Name switch
									{
										nameof(Literals.QueryAttribute) => HttpLocation.Query,
										_                               => HttpLocation.Query
									};
								})
								.FirstOrDefault(),
							Format = x.GetAttributes()
								.Where(w => w.AttributeClass.ContainingNamespace.ToString() == Literals.BaseNamespace)
								.Select(y => y.GetValue("Format", String.Empty))
								.Where(w => !String.IsNullOrEmpty(w))
								.DefaultIfEmpty(String.Empty)
								.FirstOrDefault(), 
						})
						.ToImmutableEquatableArray(),
					AllowAnyStatusCode = s.GetAttributes()
						.GetByName("AllowAnyStatusCode")
						.Select(x => x.GetValue(true))
						.FirstOrDefault()
				})
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

	private static T GetValue<T>(this AttributeData attributes, T defaultValue)
	{
		return attributes.ConstructorArguments.Length > 0 
			? (T) attributes.ConstructorArguments[0].Value 
			: defaultValue;
	}

	private static T GetValue<T>(this AttributeData attributes, string name, T defaultValue)
	{
		return attributes.NamedArguments.Length > 0
			? (T) attributes.NamedArguments.FirstOrDefault(f => f.Key == name).Value.Value
			: defaultValue;
	}
}