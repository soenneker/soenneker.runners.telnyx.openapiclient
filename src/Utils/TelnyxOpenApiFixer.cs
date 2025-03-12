using Microsoft.Extensions.Logging;
using Soenneker.Extensions.ValueTask;
using Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.FileSync;
using Soenneker.Utils.FileSync.Abstract;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Utils;

public class TelnyxOpenApiFixer : ITelnyxOpenApiFixer
{
    private readonly IFileUtil _fileUtil;
    private readonly ILogger<TelnyxOpenApiFixer> _logger;
    private readonly IFileUtilSync _fileUtilSync;

    public TelnyxOpenApiFixer(IFileUtil fileUtil, ILogger<TelnyxOpenApiFixer> logger, IFileUtilSync fileUtilSync)
    {
        _fileUtil = fileUtil;
        _logger = logger;
        _fileUtilSync = fileUtilSync;
    }

    public async ValueTask Fix(string sourceFilePath, string targetFilePath, CancellationToken cancellationToken = default)
    {
        string json = await _fileUtil.Read(sourceFilePath, cancellationToken).NoSync();

        JsonNode openApi = JsonNode.Parse(json);

        // Fix duplicate operationIds
        var seenOperationIds = new HashSet<string>();
        FixDuplicateOperationIds(openApi["paths"], seenOperationIds);

        // Remove requestBody from DELETE operations
        RemoveRequestBodyFromDelete(openApi["paths"]);

        // Fix invalid $ref values
        FixInvalidRef(openApi["paths"]);

        // Ensure missing path parameters are defined
        EnsurePathParameters(openApi["paths"]);

        // Fix invalid component names
        FixInvalidComponentNames(openApi["components"]);

        // Ensure default values exist in enums
        if (openApi["components"] is JsonObject components && components.TryGetPropertyValue("schemas", out JsonNode schemasNode))
        {
            EnsureDefaultInEnum(schemasNode);
        }

        // Fix ENUM NAMES FOR KIOTA
        if (openApi["components"] is JsonObject components2 && components2.TryGetPropertyValue("schemas", out JsonNode schemasNode2))
        {
            FixEnumNames(schemasNode2);
        }

        // Fix structural errors in `AuditEventChanges`
        FixAuditEventChanges(openApi);

        // Fix `HangupToolParams.required`
        FixHangupToolParams(openApi);

        FixActionsParticipantsRequestParticipants(openApi);

        _fileUtilSync.DeleteIfExists(targetFilePath);

        await _fileUtil.Write(targetFilePath, openApi.ToJsonString(new JsonSerializerOptions {WriteIndented = true}), cancellationToken);

        _logger.LogInformation($"OpenAPI spec fixed and saved as {targetFilePath}");
    }

    private static void FixActionsParticipantsRequestParticipants(JsonNode openApi)
    {
        if (openApi["components"] is JsonObject components && components["schemas"] is JsonObject schemas &&
            schemas.TryGetPropertyValue("ActionsParticipantsRequest", out JsonNode actionsParticipantsSchema) &&
            actionsParticipantsSchema is JsonObject actionsParticipants)
        {
            if (actionsParticipants.TryGetPropertyValue("properties", out JsonNode propertiesNode) && propertiesNode is JsonObject properties &&
                properties.TryGetPropertyValue("participants", out JsonNode participantsNode) && participantsNode is JsonObject participants)
            {
                // Remove any existing `oneOf` definition.
                if (participants.TryGetPropertyValue("oneOf", out JsonNode oneOfNode) && oneOfNode is JsonArray)
                {
                    participants.AsObject().Remove("oneOf");
                }

                // Force `participants` to always be an array of strings
                participants["type"] = "array";
                participants["items"] = new JsonObject
                {
                    ["type"] = "string"
                };
            }
        }
    }


    private static void FixDuplicateOperationIds(JsonNode node, HashSet<string> seen)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> prop in obj.ToList())
            {
                if (prop.Key == "operationId" && prop.Value != null)
                {
                    var id = prop.Value.ToString();
                    if (seen.Contains(id))
                    {
                        obj[prop.Key] = id + "_fixed";
                    }
                    else
                    {
                        seen.Add(id);
                    }
                }

                FixDuplicateOperationIds(prop.Value, seen);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (JsonNode? element in arr)
            {
                FixDuplicateOperationIds(element, seen);
            }
        }
    }

    private static void RemoveRequestBodyFromDelete(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> prop in obj.ToList())
            {
                if (prop.Key == "delete" && prop.Value is JsonObject deleteOp)
                {
                    if (deleteOp.AsObject().ContainsKey("requestBody"))
                    {
                        deleteOp.AsObject().Remove("requestBody");
                    }
                }

                RemoveRequestBodyFromDelete(prop.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (JsonNode? element in arr)
            {
                RemoveRequestBodyFromDelete(element);
            }
        }
    }

    private static void FixInvalidRef(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> prop in obj.ToList())
            {
                if (prop.Key == "$ref" && prop.Value != null)
                {
                    var refValue = prop.Value.ToString();
                    if (refValue.Contains(">") || refValue.Contains("<"))
                    {
                        string fixedRef = refValue.Replace(">", "").Replace("<", "");
                        obj[prop.Key] = fixedRef;
                    }
                }

                FixInvalidRef(prop.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (JsonNode? element in arr)
            {
                FixInvalidRef(element);
            }
        }
    }

    private static void EnsurePathParameters(JsonNode pathsNode)
    {
        if (pathsNode is JsonObject pathsObj)
        {
            foreach (KeyValuePair<string, JsonNode?> pathProp in pathsObj.ToList())
            {
                if (pathProp.Key.Contains("{siprec_sid}") && pathProp.Value is JsonObject pathObj)
                {
                    if (!pathObj.TryGetPropertyValue("parameters", out JsonNode parametersNode) || parametersNode is not JsonArray)
                    {
                        pathObj["parameters"] = new JsonArray();
                    }

                    JsonArray parametersArray = pathObj["parameters"].AsArray();
                    bool hasSiprec = parametersArray.Any(param =>
                    {
                        if (param is JsonObject paramObj && paramObj.TryGetPropertyValue("name", out JsonNode nameNode))
                        {
                            return nameNode.ToString() == "siprec_sid";
                        }

                        return false;
                    });
                    if (!hasSiprec)
                    {
                        var newParam = new JsonObject
                        {
                            ["name"] = "siprec_sid",
                            ["in"] = "path",
                            ["required"] = true,
                            ["schema"] = new JsonObject {["type"] = "string"}
                        };
                        parametersArray.Add(newParam);
                    }
                }
            }
        }
    }

    private static void FixInvalidComponentNames(JsonNode componentsNode)
    {
        if (componentsNode is JsonObject compObj)
        {
            if (compObj.TryGetPropertyValue("parameters", out JsonNode parametersNode) && parametersNode is JsonObject compParams)
            {
                foreach (KeyValuePair<string, JsonNode?> prop in compParams.ToList())
                {
                    if (prop.Key.Contains(">") || prop.Key.Contains("<"))
                    {
                        string fixedName = prop.Key.Replace(">", "").Replace("<", "");
                        JsonNode? clone = prop.Value?.DeepClone(); // Clone the node to avoid re-parenting errors.
                        compParams.Remove(prop.Key);
                        compParams[fixedName] = clone;
                    }
                }
            }
        }
    }


    private static void EnsureDefaultInEnum(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("default") && obj.ContainsKey("enum") && obj["enum"] is JsonArray enumArray)
            {
                var defaultVal = obj["default"]?.ToString();
                List<string> enumValues = enumArray.Select(x => x.ToString()).ToList();
                if (!enumValues.Contains(defaultVal) && enumValues.Any())
                {
                    obj["default"] = enumValues.First();
                }
            }

            foreach (KeyValuePair<string, JsonNode?> prop in obj)
            {
                EnsureDefaultInEnum(prop.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (JsonNode? element in arr)
            {
                EnsureDefaultInEnum(element);
            }
        }
    }

    private static void FixEnumNames(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> prop in obj.ToList())
            {
                if (prop.Key == "enum" && prop.Value is JsonArray enumArray)
                {
                    List<string> enumValues = enumArray.Select(x => x.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    if (enumValues.Count == 0)
                        continue;
                    List<string> enumVarNames = new List<string>();
                    List<string> fixedEnumValues = new List<string>();
                    foreach (string originalValue in enumValues)
                    {
                        string fixedName = Regex.Replace(originalValue, @"[^a-zA-Z0-9]", "");
                        if (string.IsNullOrEmpty(fixedName) || char.IsDigit(fixedName[0]))
                        {
                            fixedName = "_" + fixedName;
                        }

                        enumVarNames.Add(fixedName);
                        fixedEnumValues.Add(originalValue.Replace("\"", ""));
                    }

                    if (enumVarNames.Count > 0)
                    {
                        obj["enum"] = new JsonArray(fixedEnumValues.Select(v => (JsonNode) v).ToArray());
                        obj["x-enum-varnames"] = new JsonArray(enumVarNames.Select(n => (JsonNode) n).ToArray());
                    }
                }
                else
                {
                    FixEnumNames(prop.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (JsonNode? element in arr)
            {
                FixEnumNames(element);
            }
        }
    }

    private static void FixAuditEventChanges(JsonNode root)
    {
        if (root["components"] is JsonObject components && components["schemas"] is JsonObject schemas && schemas["AuditEventChanges"] is JsonObject audit &&
            audit["properties"] is JsonObject properties)
        {
            foreach (KeyValuePair<string, JsonNode?> prop in properties.ToList())
            {
                if (prop.Value is JsonObject propertyObj && propertyObj.TryGetPropertyValue("anyOf", out JsonNode anyOfNode) &&
                    anyOfNode is JsonArray anyOfArray)
                {
                    foreach (JsonNode? item in anyOfArray)
                    {
                        if (item is JsonObject itemObj && itemObj.TryGetPropertyValue("type", out JsonNode typeNode))
                        {
                            var typeStr = typeNode.ToString();
                            var allowed = new[] {"array", "boolean", "integer", "number", "object", "string"};
                            if (!allowed.Contains(typeStr))
                            {
                                itemObj["type"] = "string";
                            }
                        }
                    }
                }
            }
        }
    }

    private static void FixHangupToolParams(JsonNode root)
    {
        if (root["components"] is JsonObject components && components["schemas"] is JsonObject schemas && schemas["HangupToolParams"] is JsonObject hangup &&
            hangup["required"] is JsonArray requiredArray)
        {
            if (requiredArray.Count == 0)
            {
                requiredArray.Add("placeholderField");
            }
        }
    }
}