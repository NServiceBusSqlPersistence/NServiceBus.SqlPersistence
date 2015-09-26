using System.IO;
using System.Text;
using ApprovalTests;
using NServiceBus.SqlPersistence;
using NUnit.Framework;

[TestFixture]
public class SubscriptionScriptBuilderTest
{
    [Test]
    public void BuildCreateScript()
    {
        var builder = new StringBuilder();
        using (var writer = new StringWriter(builder))
        {
            SubscriptionScriptBuilder.BuildCreateScript("theschema", "theendpointname", writer);
        }
        var script = builder.ToString();
        SqlValidator.Validate(script);
        Approvals.Verify(script);
    }

    [Test]
    public void BuildDropScript()
    {
        var builder = new StringBuilder();
        using (var writer = new StringWriter(builder))
        {
            SubscriptionScriptBuilder.BuildDropScript("theschema", "theendpointname", writer);
        }
        var script = builder.ToString();
        SqlValidator.Validate(script);
        Approvals.Verify(script);
    }
}