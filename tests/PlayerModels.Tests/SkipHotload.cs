using System;

namespace Sandbox;

/// <summary>
/// s&box's own mark for a static field its hot reload builds afresh rather than carrying over (Core's code-derived tables
/// carry it); outside s&box it means nothing, so Core compiles here as it is.
/// </summary>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false )]
public sealed class SkipHotloadAttribute : Attribute { }
