// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Won't be registered unless its a windows os")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "It doesn't make the field 'readonly' therefor can't be used")]
