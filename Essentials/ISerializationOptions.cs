// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials;

using System.Collections.ObjectModel;

/// <summary>
/// Defines a contract for serialization options.
/// </summary>
public interface ISerializationOptions
{
	/// <summary>
	/// The policy for members when serializing.
	/// </summary>
	public MemberPolicy SerializationPolicy { get; set; }

	/// <summary>
	/// The policy for members when deserializing.
	/// </summary>
	public MemberPolicy DeserializationPolicy { get; set; }

	/// <summary>
	/// The policy for references.
	/// </summary>
	public ReferencePolicy ReferencePolicy { get; set; }

	/// <summary>
	/// The policy for boxing when serializing.
	/// </summary>
	public BoxingPolicy BoxingPolicy { get; set; }
}

/// <summary>
/// The policy for members.
/// </summary>
public class MemberPolicy
{
	/// <summary>
	/// The policy for naming the member. Use NamePolicy.None when deserializing to match case-insensitively.
	/// </summary>
	public NamePolicy NamePolicy { get; set; } = NamePolicy.PascalCase;

	/// <summary>
	/// The policy for naming the key of a dictionary.
	/// </summary>
	public NamePolicy KeyPolicy { get; set; } = NamePolicy.PascalCase;

	/// <summary>
	/// The policy for naming the enum.
	/// </summary>
	public NamePolicy EnumNamePolicy { get; set; } = NamePolicy.PascalCase;

	/// <summary>
	/// The policy for the value of an enum.
	/// </summary>
	public EnumValuePolicy EnumValuePolicy { get; set; } = EnumValuePolicy.Name;

	/// <summary>
	/// The policy for including the member when serializing or deserializing.
	/// </summary>
	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "User configuration")]
	public Collection<InclusionPolicy> InclusionPolicies { get; set; } = [InclusionPolicy.Public | InclusionPolicy.Instance | InclusionPolicy.Fields | InclusionPolicy.Properties];
}

/// <summary>
/// The policy for including a member when serializing or deserializing.
/// </summary>
[Flags]
public enum InclusionPolicy
{
	/// <summary>
	/// Do not include any members.
	/// </summary>
	None = 0,

	/// <summary>
	/// Include public members.
	/// </summary>
	Public = 1 << 0,

	/// <summary>
	/// Include non-public members.
	/// </summary>
	NonPublic = 1 << 1,

	/// <summary>
	/// Include instance members.
	/// </summary>
	Instance = 1 << 2,

	/// <summary>
	/// Include static members.
	/// </summary>
	Static = 1 << 3,

	/// <summary>
	/// Include fields.
	/// </summary>
	Fields = 1 << 4,

	/// <summary>
	/// Include properties.
	/// </summary>
	Properties = 1 << 5,

	/// <summary>
	/// Include members which are read-only.
	/// </summary>
	ReadOnly = 1 << 6,

	/// <summary>
	/// Include members which have default values.
	/// E.g. null for reference types, 0 for numeric types, etc.
	/// Does not mean values in initializers, values set in constructors, primary constructor parameters default values, etc.
	/// </summary>
	DefaultValues = 1 << 7,
}

/// <summary>
/// The policy for the casing of a name.
/// </summary>
public enum NamePolicy
{
	/// <summary>
	/// Do not change the name.
	/// </summary>
	None = 0,

	/// <summary>
	/// Convert to PascalCase.
	/// </summary>
	PascalCase,

	/// <summary>
	/// Convert to CamelCase.
	/// </summary>
	CamelCase,

	/// <summary>
	/// Convert to SnakeCase.
	/// </summary>
	SnakeCase,

	/// <summary>
	/// Convert to KebabCase.
	/// </summary>
	KebabCase,

	/// <summary>
	/// Convert to MacroCase.
	/// </summary>
	MacroCase,
}

/// <summary>
/// The policy for reference handling.
/// </summary>
public enum ReferencePolicy
{
	/// <summary>
	/// Ignore the reference.
	/// </summary>
	Ignore = 0,

	/// <summary>
	/// Make a copy of the referenced object, excluding circular references which will be ignored.
	/// </summary>
	Copy,

	/// <summary>
	/// Preserve the reference.
	/// </summary>
	Reference,
}

/// <summary>
/// The policy for the value of an enum.
/// </summary>
[Flags]
public enum EnumValuePolicy
{
	/// <summary>
	/// Use the name of the enum value.
	/// </summary>
	Name = 1 << 0,

	/// <summary>
	/// Use the number of the enum value.
	/// </summary>
	Number = 1 << 1,
}

/// <summary>
/// The policy for boxing.
/// </summary>
[Flags]
public enum BoxingPolicy
{
	/// <summary>
	/// Do not box.
	/// </summary>
	None = 0,

	/// <summary>
	/// Box numeric types.
	/// </summary>
	BoxNumeric = 1 << 0,

	/// <summary>
	/// Box derived types.
	/// </summary>
	BoxDerived = 1 << 1,

	/// <summary>
	/// Box all types.
	/// </summary>
	BoxAll = BoxNumeric | BoxDerived,
}
