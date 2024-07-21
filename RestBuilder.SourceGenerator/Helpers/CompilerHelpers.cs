using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RestBuilder.SourceGenerator.Interfaces;
using RestBuilder.SourceGenerator.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RestBuilder.SourceGenerator.Helpers;

public static class CompilerHelpers
{
	public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, string attributeName)
	{
		return symbol.GetAttributes().Where(a => a.AttributeClass?.Name == attributeName);
	}

	public static AttributeData? GetAttribute(this ISymbol symbol, string attributeName)
	{
		return symbol.GetAttributes(attributeName).FirstOrDefault();
	}

	public static T GetAttribute<T>(this ISymbol symbol, Compilation compilation) where T : Attribute
	{
		T attribute;
		var attributeData = symbol.GetAttributes().FirstOrDefault(f => f.AttributeClass.IsType<T>(compilation));

		if (attributeData == null)
		{
			return default;
		}

		if (attributeData.AttributeConstructor != null && attributeData.ConstructorArguments.Length > 0)
		{
			attribute = (T)Activator.CreateInstance(typeof(T), attributeData.GetActualConstuctorParams().ToArray());
		}
		else
		{
			attribute = (T)Activator.CreateInstance(typeof(T));
		}
		foreach (var p in attributeData.NamedArguments)
		{
			typeof(T).GetField(p.Key).SetValue(attribute, p.Value.Value);
		}
		return attribute;
	}

	public static bool HasAttribute(this ISymbol symbol, string attributeName)
	{
		return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);
	}

	public static bool HasAttribute<T>(this ISymbol symbol, Compilation compilation)
	{
		return symbol.GetAttributes().Any(a => a.AttributeClass.InheritsFrom<T>(compilation));
	}

	public static bool HasAttribute(this ISymbol symbol, Func<AttributeData, bool> predicate)
	{
		return symbol.GetAttributes().Any(predicate);
	}

	public static bool HasParameters<T>(this IMethodSymbol method, Compilation compilation)
	{
		return method.Parameters.Length == 1 &&
		       method.Parameters[0].Type.IsType<T>(compilation);
	}

	public static bool HasParameters<T1, T2>(this IMethodSymbol method, Compilation compilation)
	{
		return method.Parameters.Length == 2 &&
		       method.Parameters[0].Type.IsType<T1>(compilation) &&
		       method.Parameters[1].Type.IsType<T2>(compilation);
	}

	public static bool HasParameters<T1, T2, T3>(this IMethodSymbol method, Compilation compilation)
	{
		return method.Parameters.Length == 3 &&
		       method.Parameters[0].Type.IsType<T1>(compilation) &&
		       method.Parameters[1].Type.IsType<T2>(compilation) &&
		       method.Parameters[2].Type.IsType<T3>(compilation);
	}

	public static bool HasParameters<T1, T2, T3, T4>(this IMethodSymbol method, Compilation compilation)
	{
		return method.Parameters.Length == 4 &&
		       method.Parameters[0].Type.IsType<T1>(compilation) &&
		       method.Parameters[1].Type.IsType<T2>(compilation) &&
		       method.Parameters[2].Type.IsType<T3>(compilation) &&
		       method.Parameters[3].Type.IsType<T4>(compilation);
	}

	public static Location GetParameterLocation<T>(this IMethodSymbol method, Compilation compilation)
	{
		return method.Parameters
			.Where(w => w.Type.IsType<T>(compilation))
			.Select(s => s.GetLocation())
			.DefaultIfEmpty(Location.None)
			.First()!;
	}

	public static bool HasReturnType<T>(this IMethodSymbol method, Compilation compilation)
	{
		return IsType<T>(method.ReturnType, compilation);
	}

	// public static bool IsType<T>(this ISymbol symbol)
	// {
	// 	var type = typeof(T);
	//
	// 	return symbol.ToString() == type.GetFriendlyName();
	// }

	public static bool IsType<T>(this ISymbol? symbol, Compilation compilation)
	{
		var type = typeof(T);
		var metadataSymbol = compilation.GetTypeByMetadataName(type.FullName!);

		if (metadataSymbol != null)
		{
			return SymbolEqualityComparer.Default.Equals(symbol, metadataSymbol);
		}

		return symbol?.ToString() == type.GetFriendlyName();
	}

	public static bool IsTypeNullable<T>(this ISymbol? symbol, Compilation compilation)
	{
		var type = typeof(T);
		var metadataSymbol = compilation.GetTypeByMetadataName(type.FullName!);

		if (metadataSymbol != null)
		{
			return SymbolEqualityComparer.IncludeNullability.Equals(symbol, metadataSymbol);
		}

		return symbol?.ToString() == type.GetFriendlyName();
	}

	public static bool IsType<T>(this IType symbol)
	{
		var type = typeof(T);
	
		if (type.Namespace == "System" && symbol.Namespace == "System")
		{
			switch (type.Name)
			{
				case "String":
					return symbol.Type is "String" or "string";
				case "Int32":
					return symbol.Type is "Int32" or "int";
				case "Int64":
					return symbol.Type is "Int64" or "long";
				case "Boolean":
					return symbol.Type is "Boolean" or "bool";
				case "Decimal":
					return symbol.Type is "Decimal" or "decimal";
				case "Double":
					return symbol.Type is "Double" or "double";
				case "Single":
					return symbol.Type is "Single" or "float";
				case "Byte":
					return symbol.Type is "Byte" or "byte";
				case "SByte":
					return symbol.Type is "SByte" or "sbyte";
				case "Char":
					return symbol.Type is "Char" or "char";
				case "UInt16":
					return symbol.Type is "UInt16" or "ushort";
				case "UInt32":
					return symbol.Type is "UInt32" or "uint";
				case "UInt64":
					return symbol.Type is "UInt64" or "ulong";
				case "Object":
					return symbol.Type is "Object" or "object";
			}
		}
	
		return symbol.Namespace == type.Namespace && symbol.Type == type.Name;
	}

	public static bool IsType<T>(this TypeModel symbol)
	{
		var type = typeof(T);

		if (type.Namespace == "System" && symbol.Namespace == "System")
		{
			switch (type.Name)
			{
				case "String":
					return symbol.Type is "String" or "string";
				case "Int32":
					return symbol.Type is "Int32" or "int";
				case "Int64":
					return symbol.Type is "Int64" or "long";
				case "Boolean":
					return symbol.Type is "Boolean" or "bool";
				case "Decimal":
					return symbol.Type is "Decimal" or "decimal";
				case "Double":
					return symbol.Type is "Double" or "double";
				case "Single":
					return symbol.Type is "Single" or "float";
				case "Byte":
					return symbol.Type is "Byte" or "byte";
				case "SByte":
					return symbol.Type is "SByte" or "sbyte";
				case "Char":
					return symbol.Type is "Char" or "char";
				case "UInt16":
					return symbol.Type is "UInt16" or "ushort";
				case "UInt32":
					return symbol.Type is "UInt32" or "uint";
				case "UInt64":
					return symbol.Type is "UInt64" or "ulong";
				case "Object":
					return symbol.Type is "Object" or "object";
			}
		}

		return symbol.Namespace == type.Namespace && symbol.Type == type.Name;
	}

	public static bool IsAwaitableType(this ITypeSymbol typeSymbol)
	{
		// Zoek de GetAwaiter methode zonder parameters
		var getAwaiterMethod = typeSymbol.GetMembers("GetAwaiter")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.IsEmpty && !m.ReturnsVoid);

		if (getAwaiterMethod == null)
		{
			return false;
		}

		var awaiterType = getAwaiterMethod.ReturnType;

		// Controleer de IsCompleted property
		var isCompletedProperty = awaiterType
			.GetMembers("IsCompleted")
			.OfType<IPropertySymbol>()
			.FirstOrDefault(p => p.Type.SpecialType == SpecialType.System_Boolean);

		if (isCompletedProperty == null)
		{
			return false;
		}

		// Controleer de GetResult methode
		var getResultMethod = awaiterType.GetMembers("GetResult")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.IsEmpty);

		if (getResultMethod == null)
		{
			return false;
		}

		// Controleer de OnCompleted methode
		var onCompletedMethod = awaiterType
			.GetMembers("OnCompleted")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.Length == 1 && m.Parameters[0].Type.ToDisplayString() == "System.Action");

		if (onCompletedMethod == null)
		{
			return false;
		}

		return true;
	}

	public static ITypeSymbol? GetAwaitableReturnType(this ITypeSymbol awaitableType)
	{
		// Verkrijg de GetAwaiter methode
		var getAwaiterMethod = awaitableType.GetMembers("GetAwaiter")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.IsEmpty && !m.ReturnsVoid);

		if (getAwaiterMethod == null)
		{
			return null; // Niet een geldig awaitable type
		}

		var awaiterType = getAwaiterMethod.ReturnType;

		// Zoek de GetResult methode binnen de awaiter
		var getResultMethod = awaiterType.GetMembers("GetResult")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.IsEmpty);

		// Het return type van GetResult is het type dat je zoekt
		return getResultMethod?.ReturnType;
	}

	public static bool IsCollection(this ITypeSymbol type, Compilation compilation)
	{
		if (type.IsType<String>(compilation))
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

	public static ITypeSymbol? GetCollectionType(this ITypeSymbol type, Compilation compilation)
	{
		if (type.IsType<String>(compilation))
		{
			return null;
		}

		if (type is IArrayTypeSymbol arraySymbol)
		{
			return arraySymbol.ElementType;
		}

		foreach (var @interface in type.AllInterfaces)
		{
			if (@interface.ContainingNamespace?.ToString() == "System.Collections.Generic" && @interface.Name == nameof(IEnumerable))
			{
				return @interface.TypeArguments[0];
			}
		}

		if (type is INamedTypeSymbol namedTypeSymbol && type.ContainingNamespace?.ToString() == "System.Collections.Generic" && type.Name == nameof(IEnumerable))
		{
			return namedTypeSymbol.TypeArguments[0];
		}

		return null;
	}

	public static bool IsTaskType(this ITypeSymbol? typeSymbol, Compilation compilation)
	{
		if (typeSymbol == null)
		{
			return false;
		}

		var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
		var taskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

		return SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, taskType) 
		       || SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, taskOfTType);
	}

	public static IEnumerable<IMethodSymbol> GetMethods(this ITypeSymbol type)
	{
		return type
			.GetBaseTypes()
			.SelectMany(s => s
				.GetMembers()
				.OfType<IMethodSymbol>());
	}

	public static IEnumerable<IFieldSymbol> GetFields(this ITypeSymbol type)
	{
		return type
			.GetBaseTypes()
			.SelectMany(s => s
				.GetMembers()
				.OfType<IFieldSymbol>());
	}

	public static IEnumerable<IPropertySymbol> GetProperties(this ITypeSymbol type)
	{
		return type
			.GetBaseTypes()
			.SelectMany(s => s
				.GetMembers()
				.OfType<IPropertySymbol>());
	}

	public static bool IsPartial(this IMethodSymbol method)
	{
		return method.DeclaringSyntaxReferences
			.Any(syntax => syntax.GetSyntax() is MethodDeclarationSyntax declaration && declaration.Modifiers
				.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
	}

	public static bool InheritsFrom<T>(this ITypeSymbol? type, Compilation compilation)
	{
		if (type.IsType<T>(compilation))
		{
			return true;
		}

		var baseType = type?.BaseType;

		while (baseType != null)
		{
			if (baseType.IsType<T>(compilation))
			{
				return true;
			}

			baseType = baseType.BaseType;
		}

		return false;
	}

	public static bool Implements<T>(this ITypeSymbol typeSymbol, Compilation compilation)
	{
		var attributes = typeSymbol.AllInterfaces;

		foreach (var attribute in attributes)
		{
			if (attribute.IsType<T>(compilation))
			{
				return true;
			}
		}

		return false;
	}

	public static bool Implements(this ITypeSymbol typeSymbol, string type)
	{
		if (typeSymbol is INamedTypeSymbol namedSymbol && namedSymbol.ConstructedFrom.ToString() == type)
		{
			return true;
		}

		var attributes = typeSymbol.AllInterfaces;

		foreach (var attribute in attributes)
		{
			if (attribute.ConstructedFrom.ToString() == type)
			{
				return true;
			}
		}

		return false;
	}

	public static IEnumerable<ITypeSymbol> GetBaseTypes(this ITypeSymbol typeSymbol)
	{
		yield return typeSymbol;

		var baseType = typeSymbol.BaseType;

		while (baseType is not null)
		{
			yield return baseType;

			baseType = baseType.BaseType;
		}
	}

	public static T GetValue<T>(this AttributeData attributes, int index, T defaultValue)
	{
		if (attributes.ConstructorArguments.Length <= index)
		{
			return defaultValue;
		}

		var result = attributes.ConstructorArguments[index].Value;

		if (result?.GetType() == typeof(T))
		{
			return (T) result;
		}

		return defaultValue;
	}

	public static T GetValue<T>(this AttributeData attributes, string name, T defaultValue)
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
				return (T) result;
			}
		}

		return defaultValue;
	}

	public static Location GetLocation(this ISymbol symbol)
	{
		return symbol.Locations
			.DefaultIfEmpty(Location.None)
			.First();
	}

	public static void ReportDiagnostic<T>(this SymbolAnalysisContext context, ISymbol symbol, Func<T, CSharpSyntaxNode?> selector, DiagnosticDescriptor descriptor, params object[] parameters) where T : CSharpSyntaxNode
	{
		foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
		{
			var syntax = syntaxReference.GetSyntax(context.CancellationToken);

			if (syntax is not T declaration)
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(descriptor, selector(declaration)?.GetLocation() ?? Location.None, [symbol.Name, ..parameters]));
		}
	}

	public static void ReportDiagnostic<T>(this SymbolAnalysisContext context, ISymbol symbol, Func<T, SyntaxToken> selector, DiagnosticDescriptor descriptor, params object[] parameters) where T : CSharpSyntaxNode
	{
		foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
		{
			var syntax = syntaxReference.GetSyntax(context.CancellationToken);

			if (syntax is not T declaration)
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(descriptor, selector(declaration).GetLocation(), [symbol.Name, .. parameters]));
		}
	}

	public static void ReportDiagnostic<T>(this SymbolAnalysisContext context, ISymbol symbol, Func<T, Location> selector, DiagnosticDescriptor descriptor, params object[] parameters) where T : CSharpSyntaxNode
	{
		foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
		{
			var syntax = syntaxReference.GetSyntax(context.CancellationToken);

			if (syntax is not T declaration)
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(descriptor, selector(declaration), [symbol.Name, .. parameters]));
		}
	}

	private static string GetFriendlyName(this Type type)
	{
		if (type == typeof(int))
			return "int";
		if (type == typeof(short))
			return "short";
		if (type == typeof(byte))
			return "byte";
		if (type == typeof(bool))
			return "bool";
		if (type == typeof(long))
			return "long";
		if (type == typeof(float))
			return "float";
		if (type == typeof(double))
			return "double";
		if (type == typeof(decimal))
			return "decimal";
		if (type == typeof(string))
			return "string";
		if (type.IsGenericType)
			return $"{type.Namespace}.{type.Name.Split('`')[0]}<{String.Join(", ", type.GetGenericArguments().Select(GetFriendlyName))}>";
		
		return $"{type.Namespace}.{type.Name}";
	}

	private static IEnumerable<object> GetActualConstuctorParams(this AttributeData attributeData)
	{
		foreach (var arg in attributeData.ConstructorArguments)
		{
			if (arg.Kind == TypedConstantKind.Array)
			{
				// Assume they are strings, but the array that we get from this
				// should actually be of type of the objects within it, be it strings or ints
				// This is definitely possible with reflection, I just don't know how exactly. 
				yield return arg.Values.Select(a => a.Value).OfType<string>().ToArray();
			}
			else
			{
				yield return arg.Value;
			}
		}
	}
}