using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services;

public class TSIPromptService
{
    private static List<BoardSchema>? _boardSchemasCache;
    private static List<ItemDefinition>? _itemDefinitionsCache;
    private static List<PriorityDefinition>? _priorityDefinitionsCache;
    private static SocBoardTSIs? _socBoardTSIsCache;

    private static async Task<List<BoardSchema>> LoadBoardSchemasAsync()
    {
        if (_boardSchemasCache != null)
            return _boardSchemasCache;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ai_Dispatch.Data.service_board_TSIs.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        
        using var reader = new StreamReader(stream);
        var jsonContent = await reader.ReadToEndAsync();
        _boardSchemasCache = JsonSerializer.Deserialize<List<BoardSchema>>(jsonContent) ?? new List<BoardSchema>();
        return _boardSchemasCache;
    }

    private static async Task<List<ItemDefinition>> LoadItemDefinitionsAsync()
    {
        if (_itemDefinitionsCache != null)
            return _itemDefinitionsCache;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ai_Dispatch.Data.item_definitions.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        
        using var reader = new StreamReader(stream);
        var jsonContent = await reader.ReadToEndAsync();
        _itemDefinitionsCache = JsonSerializer.Deserialize<List<ItemDefinition>>(jsonContent) ?? new List<ItemDefinition>();
        return _itemDefinitionsCache;
    }

    private static async Task<List<PriorityDefinition>> LoadPriorityDefinitionsAsync()
    {
        if (_priorityDefinitionsCache != null)
            return _priorityDefinitionsCache;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ai_Dispatch.Data.priority_definitions.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        
        using var reader = new StreamReader(stream);
        var jsonContent = await reader.ReadToEndAsync();
        _priorityDefinitionsCache = JsonSerializer.Deserialize<List<PriorityDefinition>>(jsonContent) ?? new List<PriorityDefinition>();
        return _priorityDefinitionsCache;
    }

    private static async Task<SocBoardTSIs> LoadSocBoardTSIsAsync()
    {
        if (_socBoardTSIsCache != null)
            return _socBoardTSIsCache;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ai_Dispatch.Data.soc_board_TSIs.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        
        using var reader = new StreamReader(stream);
        var jsonContent = await reader.ReadToEndAsync();
        _socBoardTSIsCache = JsonSerializer.Deserialize<SocBoardTSIs>(jsonContent) ?? new SocBoardTSIs();
        return _socBoardTSIsCache;
    }

    public static async Task<string> GetPrompt(int? boardId, string? boardName)
    {
        if (boardName != null)
        {
            var boardNameLower = boardName.ToLower();
            
            if (boardNameLower.Contains("security") || boardNameLower.Contains("soc"))
            {
                return await GetSOCPrompt(boardId);
            }
            
            if (boardNameLower.Contains("noc") || boardNameLower.Contains("network operations"))
            {
                return await GetNOCPrompt();
            }
            
            if (boardNameLower.Contains("caduceus") || boardNameLower.Contains("caduseus"))
            {
                return await GetCaduceusPrompt(boardId);
            }
        }
        
        return await GetServicePrompt(boardId);
    }
    
    private static async Task<string> GetServicePrompt(int? boardId)
    {
        var boardSchemas = await LoadBoardSchemasAsync();
        var itemDefinitions = await LoadItemDefinitionsAsync();
        var priorityDefinitions = await LoadPriorityDefinitionsAsync();
        
        var boardSchema = boardSchemas.FirstOrDefault(b => b.BoardId == boardId);
        if (boardSchema == null)
            throw new InvalidOperationException($"Board schema not found for board ID: {boardId}");

        var sb = new StringBuilder();
        sb.AppendLine(@"Role: Dispatch - Type, Subtype, and Item Classifier

Use ONLY the provided SOPs to make your decision. Never use information outside the SOPs.

The ticket intent (if provided) represents the core purpose of the ticket - use this to help understand the ticket's goal when classifying type, subtype, and item.

If the provided type, subtype, and item in the ticket are already correct according to the SOPs, return them exactly as provided without reclassification.

If fields provided are like type and type is correct, but subtype and item are empty, still classify the missing fields.

If no type, subtype, or item is not provided classify like normal

Use the following 'Type' SOP to classify the correct Type for the ticket:");

        var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
        sb.AppendLine(JsonSerializer.Serialize(boardSchema.Types, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("After you have classified the type, classify the Subtype using the following 'SubType' SOP schema. Subtype typeAssociationIds must be available to use with the chosen the type id. IE the type ID must be in the subtypes typeAssociationIds:");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(boardSchema.SubTypes, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("Next classify an item IF one can be found in the 'Board Items' schema. Type and subtype name picked from above must match the item element picked.");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(itemDefinitions, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("After getting the correct item return the Item object from this schema:");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(boardSchema.BoardItems, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("Finally classify priority with the following priority object only:");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(priorityDefinitions, jsonOptions));
        sb.AppendLine();
        sb.AppendLine(@"## VALIDATION RULES

Before outputting JSON, verify:

1. Subtype typeAssociationIds contains the selected type ID

2. Item matches the type/subtype combination from examples

## CRITICAL WARNINGS

WARNING: DO NOT select ""Software"" (ID: 16051) as an item for Service Request tickets

WARNING: Always match the type/subtype/item combination from the examples above

CRITICAL: For ALL id fields (type.id, subtype.id, item.id, priority.id), if no value is available, return null (not empty string ""). Never return empty strings for id fields - always use null.

CRITICAL: confidence_score MUST be an integer (not a string). Return 0-100 as an integer, or -1 as an integer if no determination can be made.

Rule: If ""confidence_score"" is -1 (no determination can be made from the SOPs), set type.id, type.name, subtype.id, subtype.name, item.id, and item.name to null.

No Explanations. No Markdown. Return the following JSON only:

{
  ""reason"": ""reason for EACH decision (type, subtype, item, and priority)"",
  ""confidence_score"": 0-100 int (-1 if no determination can be made from the SOPs),
  ""type"": {
    ""id"": 7768 int | (type object from Type schema only),
    ""name"": ""type name""
  },
  ""subtype"": {
    ""id"": 12232 int | (subtype object from SubType schema only. Do not use ITEM IDS),
    ""name"": ""subtype name""
  },
  ""item"": {
    ""id"": 15730 int | (item object from Board Items schema only),
    ""name"": ""item name""
  },
  ""priority"": {
    ""id"": 4 int | (id from priority object),
    ""name"": ""name from priority object""
  },
  ""board_name"": """ + boardSchema.BoardName + @"""
}");

        return sb.ToString();
    }
    
    private static async Task<string> GetSOCPrompt(int? boardId)
    {
        var boardSchemas = await LoadBoardSchemasAsync();
        var priorityDefinitions = await LoadPriorityDefinitionsAsync();
        var socBoardTSIs = await LoadSocBoardTSIsAsync();
        
        var boardSchema = boardSchemas.FirstOrDefault(b => b.BoardId == boardId);
        if (boardSchema == null)
            throw new InvalidOperationException($"Board schema not found for board ID: {boardId}");

        var sb = new StringBuilder();
        sb.AppendLine(@"Role: Dispatch - Type, Subtype, priority Classifier

The ticket intent (if provided) represents the core purpose of the ticket - use this to help understand the ticket's goal when classifying.

Use the Keyword SOP first to classify the ticket. Match keywords from the ticket content (and intent if provided) to determine the appropriate Type and Subtype classification.");

        var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
        sb.AppendLine(JsonSerializer.Serialize(socBoardTSIs, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("Use the type and subtype schemas to get the correct IDs and correlating subtype. If no return match from the schema make proper classification.");
        sb.AppendLine();
        sb.AppendLine("Type:");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(boardSchema.Types, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("SubType:");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(boardSchema.SubTypes, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("Priority Classification:");
        sb.AppendLine();
        sb.AppendLine("- Use the priority specified in the Keyword SOP if available");
        sb.AppendLine();
        sb.AppendLine("- If no priority specified in SOP, use the following priority definitions as fallback:");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(priorityDefinitions, jsonOptions));
        sb.AppendLine();
        sb.AppendLine(@"## VALIDATION RULES

Before outputting JSON, verify:

1. Subtype typeAssociationIds contains the selected type ID

2. If no match found in schemas, classify based on ticket content

## CRITICAL WARNINGS

WARNING: Always match the type/subtype combination from the schemas above

CRITICAL: For ALL id fields (type.id, subtype.id, item.id, priority.id), if no value is available, return null (not empty string ""). Never return empty strings for id fields - always use null.

Rule: If ""confidence_score"" is -1 (no determination can be made from the SOPs), set type.id, type.name, subtype.id, subtype.name to null.

No Explanations. No Markdown. Return the following JSON only:

{
  ""reason"": ""reason for EACH decision (type, subtype, and priority)"",
  ""confidence_score"": 0-100 int (-1 if no determination can be made from the SOPs),
  ""type"": {
    ""id"": 7768 int | (type object from Type schema only),
    ""name"": ""type name""
  },
  ""subtype"": {
    ""id"": 12232 int | (subtype object from SubType schema only. Do not use ITEM IDS),
    ""name"": ""subtype name""
  },
  ""item"": {
    ""id"": null,
    ""name"": null
  },
  ""priority"": {
    ""id"": 4 int | (id from priority object),
    ""name"": ""name from priority object""
  },
  ""board_name"": """ + boardSchema.BoardName + @"""
}");

        return sb.ToString();
    }
    
    private static async Task<string> GetNOCPrompt()
    {
        var priorityDefinitions = await LoadPriorityDefinitionsAsync();

        var sb = new StringBuilder();
        sb.AppendLine(@"Role: Dispatch - Type, Subtype, priority Classifier

Use the Keyword SOP first to classify the ticket. Match keywords from the ticket content to determine the appropriate Type and Subtype classification.

Use the type and subtype schemas to get the correct IDs and correlating subtype. If no return match from the schema make proper classification.

Backup Tickets -

Key words: Backup, Restoration, Recovery, DR, Disaster Recovery, Backup validation, Missing file/document, Acronis, Datto,

Type: {""name"": ""Backups"", ""id"": 2799}

Subtype: {""name"": ""Restore"", ""id"": 10371}

In Queue/Backups/Failure

'Important: An Azure Backup failure alert has been activated'

General NOC-

Key Words: Script, RMM Access, Connectwise access, QP Access, ITGlue Access, offboard workstation, remove rmm, GDAP, Decommission or removal of Sourcepass tools, offboard, Remove from RMM, monitoring removed, Auvik, CyberQP, ITGlue, ScreenConnect

Type: {""name"": ""Incident"", ""id"": 7911}

Priority Classification:");

        sb.AppendLine();
        sb.AppendLine("- Use the priority specified in the Keyword SOP if available");
        sb.AppendLine();
        sb.AppendLine("- If no priority specified in SOP, use the following priority definitions as fallback:");
        sb.AppendLine();
        
        var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
        sb.AppendLine(JsonSerializer.Serialize(priorityDefinitions, jsonOptions));
        sb.AppendLine();
        sb.AppendLine(@"## CRITICAL WARNINGS

WARNING: Always match the type/subtype combination from the schemas above

CRITICAL: For ALL id fields (type.id, subtype.id, item.id, priority.id), if no value is available, return null (not empty string ""). Never return empty strings for id fields - always use null.

Rule: If ""confidence_score"" is -1 (no determination can be made from the SOPs), set type.id, type.name, subtype.id, subtype.name to null.

No Explanations. No Markdown. Return the following JSON only:

{
  ""reason"": ""reason for EACH decision (type, subtype, and priority)"",
  ""confidence_score"": 0-100 int (-1 if no determination can be made from the SOPs),
  ""type"": {
    ""id"": 7768 int | (type object from Type schema only),
    ""name"": ""type name""
  },
  ""subtype"": {
    ""id"": 12232 int | (subtype object from SubType schema only. Do not use ITEM IDS),
    ""name"": ""subtype name""
  },
  ""item"": {
    ""id"": null,
    ""name"": null
  },
  ""priority"": {
    ""id"": ""id from priority object"",
    ""name"": ""name from priority object""
  },
  ""board_name"": ""Network Operations Center""
}");

        return sb.ToString();
    }
    
    private static async Task<string> GetCaduceusPrompt(int? boardId)
    {
        var boardSchemas = await LoadBoardSchemasAsync();
        var priorityDefinitions = await LoadPriorityDefinitionsAsync();
        
        var boardSchema = boardSchemas.FirstOrDefault(b => b.BoardId == boardId);
        if (boardSchema == null)
            throw new InvalidOperationException($"Board schema not found for board ID: {boardId}");

        var sb = new StringBuilder();
        sb.AppendLine(@"Role: Dispatch - Type, Subtype, priority Classifier

Use the type and subtype schemas to get the correct IDs and correlating subtype. If no return match from the schema make proper classification.

Type:");

        var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(boardSchema.Types, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("SubType:");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(boardSchema.SubTypes, jsonOptions));
        sb.AppendLine();
        sb.AppendLine("Priority Classification:");
        sb.AppendLine();
        sb.AppendLine("- Use the priority specified in the Keyword SOP if available");
        sb.AppendLine();
        sb.AppendLine("- If no priority specified in SOP, use the following priority definitions as fallback:");
        sb.AppendLine();
        sb.AppendLine(JsonSerializer.Serialize(priorityDefinitions, jsonOptions));
        sb.AppendLine();
        sb.AppendLine(@"## VALIDATION RULES

Before outputting JSON, verify:

1. Subtype typeAssociationIds contains the selected type ID

2. If no match found in schemas, classify based on ticket content

## CRITICAL WARNINGS

WARNING: Always match the type/subtype combination from the schemas above

CRITICAL: For ALL id fields (type.id, subtype.id, item.id, priority.id), if no value is available, return null (not empty string ""). Never return empty strings for id fields - always use null.

Rule: If ""confidence_score"" is -1 (no determination can be made from the SOPs), set type.id, type.name, subtype.id, subtype.name to null.

No Explanations. No Markdown. Return the following JSON only:

{
  ""reason"": ""reason for EACH decision (type, subtype, and priority)"",
  ""confidence_score"": 0-100 int (-1 if no determination can be made from the SOPs),
  ""type"": {
    ""id"": 7768 int | (type object from Type schema only),
    ""name"": ""type name""
  },
  ""subtype"": {
    ""id"": 12232 int | (subtype object from SubType schema only. Do not use ITEM IDS),
    ""name"": ""subtype name""
  },
  ""item"": {
    ""id"": null,
    ""name"": null
  },
  ""priority"": {
    ""id"": 4 int | (id from priority object),
    ""name"": ""name from priority object""
  },
  ""board_name"": """ + boardSchema.BoardName + @"""
}");

        return sb.ToString();
    }
}

