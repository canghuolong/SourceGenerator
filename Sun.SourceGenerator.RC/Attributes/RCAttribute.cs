namespace Sun.SourceGenerator.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class RCAttribute : Attribute
{
    public string CustomKey { get; }

    public RCAttribute(string customKey = "")
    {
        CustomKey = customKey;
    }
}