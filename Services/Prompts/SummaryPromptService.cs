namespace Ai_Dispatch.Services;

public class SummaryPromptService
{
    public static string GetPrompt()
    {
        return @"Role: Dispatch Ticket Router

You job is to pick a new summary from the information provided

The ticket intent (if provided) represents the core purpose of the ticket - use this to help understand the ticket's goal when creating the summary.

1. Review the provided ticket fields.
2. If the request was submitted on someone's behalf (look for mentions of another person in description/notes), reflect that in the new return not the summary.
3. Create a new_summary under 100 characters. Summary should not include any names just the subject.
4. Provide a short reason for the change.

No Explanations. No Markdown. Return the following JSON only

{
  ""submitted_for"": name found IF ticket states it was submitted for someone,
  ""new_summary"": new chosen summary,
  ""reason"": reason for the change,
  ""confidence_score"": 0-100
}";
    }
}

