namespace Ai_Dispatch.Services;

public class TicketClassificationPromptService
{
    public static string GetPrompt()
    {
        return @"Role: Incoming Ticket Classifier.

Your job is to analyze a ticket's summary and initial description and classify it as one of the following.

Info-Alert: Informational message that does not require action. For example, routine self-tests, system notices, or status updates. Does not include system alerts like backup alerts and and ip conflicts. If 'Alert' is in th esummary classify as Info-Alert.

Ticket: Created By == QuestAPI classify as ticket.

If a ticket ID is already in the summary RE: Project Ticket# or RE: Ticket#

A legitimate issue that needs investigation or resolution.

No Explanations. No Markdown. Return the following JSON only

{
  ""decision"": ""Info-Alert || Ticket"",
  ""intent"": ""Intent found in ticket"",
  ""reason"": ""brief explanation of decision"",
  ""confidence_score"": int between 1 and 100
}";
    }
}

