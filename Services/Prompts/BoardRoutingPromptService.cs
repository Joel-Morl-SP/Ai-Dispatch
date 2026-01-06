using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ai_Dispatch.Models;

namespace Ai_Dispatch.Services;

public class BoardRoutingPromptService
{
    private static List<BoardRoutingData>? _boardRoutingCache;
    private const int CaduceusHealth = 47941;

    private static async Task<List<BoardRoutingData>> LoadBoardRoutingAsync()
    {
        if (_boardRoutingCache != null)
            return _boardRoutingCache;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ai_Dispatch.Data.board_routing.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        
        using var reader = new StreamReader(stream);
        var jsonContent = await reader.ReadToEndAsync();
        _boardRoutingCache = JsonSerializer.Deserialize<List<BoardRoutingData>>(jsonContent) ?? new List<BoardRoutingData>();
        return _boardRoutingCache;
    }

    public static async Task<string> GetPrompt(int? companyId, int? l1Board, int? l2Board, int? l3Board, int? caduceusBoard, int? securityBoard, int? nocBoard)
    {
        var boardRouting = await LoadBoardRoutingAsync();
        var sb = new StringBuilder();
        
        sb.AppendLine(@"Role: Dispatch Ticket Router

You job is to pick the correct board placement the ticket should be routed to.

The ticket intent (if provided) represents the core purpose of the ticket - use this to help understand the ticket's goal when matching to boards.

Use the SOP read-only schema and attempt to match rule first, then if no rule applies match the ticket content (and intent if provided) to a task. If no decision can be made from the schema output confidence score of -1.

Check notes ONLY for explicit instructions to route to a specific board. If notes contain explicit routing instructions, follow those instructions. Otherwise, use the SOP schema and ticket content as described above.

Confidence Score Guidelines:

Default to 90+ when SOP match is clear, only use 85 or less when genuinely uncertain.

- 100: Clear SOP match - use this for obvious classifications
- 95: Very strong - clear match with high confidence
- 90: Strong - Match found
- 85: Needs human review - good match but requires verification
- 75-84: Unclear - uncertain match, needs human review
- 75 or lower: Low confidence - poor match, needs human review
- -1: Not in SOP - no matching rule found

Note: Software installations do not have to be mentioned by name APP in the SOP explicitly.

SOP:");

        sb.AppendLine();
        
        List<BoardRoutingData> relevantBoards;
        
        if (companyId == CaduceusHealth)
        {
            sb.AppendLine("Caduceus board should be checked for matching criteria first. Then if no criteria matches what they handle then it can go to the best matching board.");
            sb.AppendLine();
            
            relevantBoards = boardRouting.Where(b => 
                b.BoardId == l1Board || 
                b.BoardId == l2Board || 
                b.BoardId == l3Board || 
                b.BoardId == caduceusBoard || 
                b.BoardId == securityBoard ||
                b.BoardId == nocBoard).ToList();
        }
        else
        {
            relevantBoards = boardRouting.Where(b => b.BoardId != caduceusBoard).ToList();
        }
        
        var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
        var boardsJson = JsonSerializer.Serialize(relevantBoards, jsonOptions);
        sb.AppendLine(boardsJson);
        
        sb.AppendLine("No Explanations. No Markdown. Return the following JSON only");
        sb.AppendLine();
        sb.AppendLine(@"CRITICAL: confidence_score MUST be an integer (not a string). Return 0-100 as an integer, or -1 as an integer if no determination can be made.");
        sb.AppendLine();
        sb.AppendLine(@"CRITICAL: board_id MUST be an integer (not a string). Return the board ID from the SOP as an integer.");
        sb.AppendLine();
        sb.AppendLine(@"{
  ""reason"": reason for decision,
  ""confidence_score"": 0-100 int or -1 int,
  ""board_name"": board_name from SOP,
  ""board_id"": id from SOP (integer)
}");
        
        return sb.ToString();
    }
}

