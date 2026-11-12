using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Soenneker.Runners.Telnyx.OpenApiClient.Utils;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Tests;

public class NumberHealthMetricsSchemaFixerTests
{
    [Test]
    public async Task Fix_corrects_count_without_changing_other_metrics()
    {
        JsonNode document = JsonNode.Parse("""
            {"components":{"schemas":{"NumberHealthMetrics":{"properties":{
              "message_count":{"type":"integer","description":"Message count","nullable":true},
              "success_ratio":{"type":"number","format":"float"}
            }}}}}
            """)!;

        NumberHealthMetricsSchemaFixer.Fix(document);
        string firstPass = document.ToJsonString();
        NumberHealthMetricsSchemaFixer.Fix(document);

        JsonNode properties = document["components"]!["schemas"]!["NumberHealthMetrics"]!["properties"]!;
        await Assert.That(properties["message_count"]!["type"]!.GetValue<string>()).IsEqualTo("number");
        await Assert.That(properties["message_count"]!["format"]!.GetValue<string>()).IsEqualTo("double");
        await Assert.That(properties["message_count"]!["description"]!.GetValue<string>()).IsEqualTo("Message count");
        await Assert.That(properties["message_count"]!["nullable"]!.GetValue<bool>()).IsTrue();
        await Assert.That(properties["success_ratio"]!["format"]!.GetValue<string>()).IsEqualTo("float");
        await Assert.That(document.ToJsonString()).IsEqualTo(firstPass);
    }

    [Test]
    public async Task Fix_requires_review_if_upstream_schema_moves()
    {
        await Assert.That(() => NumberHealthMetricsSchemaFixer.Fix(JsonNode.Parse("{}")!))
            .Throws<InvalidOperationException>();
    }
}
