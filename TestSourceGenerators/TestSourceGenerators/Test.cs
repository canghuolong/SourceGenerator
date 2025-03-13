using Sun.SourceGenerator.Attributes;
namespace TestSourceGenerators;

// [Cache(nameof(TestComp))]
public partial class Test
{
    [RC]
    public TestComp testComp;

    void A()
    {
        
    }
}

public class TestComp
{
    
}