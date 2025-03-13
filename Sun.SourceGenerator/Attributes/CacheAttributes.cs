namespace Sun.SourceGenerator.Attributes;

using System;

[AttributeUsage(AttributeTargets.Class)]
public class CacheAttribute : Attribute
{
    public string[] Types { get; }
    public CacheAttribute(params string[] types)
    {
        Types = types;
    }
    
}

