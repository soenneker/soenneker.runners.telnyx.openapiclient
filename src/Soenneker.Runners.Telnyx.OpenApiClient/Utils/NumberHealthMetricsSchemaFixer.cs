using System;
using System.Text.Json.Nodes;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Utils;

/// <summary>
/// Corrects the Telnyx number-health message count schema before client generation.
/// </summary>
public static class NumberHealthMetricsSchemaFixer
{
    /// <summary>
    /// Allows decimal-form JSON numbers for message counts instead of generating a strict integer parser.
    /// </summary>
    public static void Fix(JsonNode document)
    {
        JsonObject messageCount = document["components"]?["schemas"]?["NumberHealthMetrics"]?["properties"]?["message_count"] as JsonObject
            ?? throw new InvalidOperationException("Telnyx NumberHealthMetrics.message_count schema was not found; review the message-count override.");

        // Telnyx's response can use decimal-form counts, which Kiota's GetIntValue rejects.
        messageCount["type"] = "number";
        messageCount["format"] = "double";
    }
}
