using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace TranslateServer.Mcp;

/// <summary>
/// MCP Tools for the translation system (Notabenoid-like).
/// These tools allow an LLM to explore projects, read rich context,
/// search for consistency, and propose translations.
/// </summary>
[McpServerToolType]
public class TranslateMcpTools
{
    private readonly McpAgentService _agent;

    public TranslateMcpTools(McpAgentService agent)
    {
        _agent = agent;
    }

    [McpServerTool(Name = "get_status")]
    [Description("Returns basic information about the MCP server and the configured AI agent name.")]
    public async Task<object> GetStatus()
    {
        return await _agent.GetStatusAsync();
    }

    [McpServerTool(Name = "list_projects")]
    [Description("Lists all available translation projects with their progress statistics (translated/approved letters and texts).")]
    public async Task<object> ListProjects()
    {
        return await _agent.GetProjectsAsync();
    }

    [McpServerTool(Name = "list_volumes")]
    [Description("Lists all volumes (chapters/parts) inside a specific project with progress stats.")]
    public async Task<object> ListVolumes(
        [Description("Project code (e.g. 'freddy_pharkas')")] string project)
    {
        return await _agent.GetVolumesAsync(project);
    }

    [McpServerTool(Name = "get_text_context")]
    [Description("The most important tool. Returns a text line with surrounding context (previous and next lines), current active translation, comments, and metadata. Use this before proposing any changes.")]
    public async Task<object> GetTextContext(
        [Description("Project code")] string project,
        [Description("Volume code (e.g. '300_scr')")] string volume,
        [Description("Line number inside the volume")] int number,
        [Description("How many lines of context before and after to include. Default is 3.")] int contextLines = 3)
    {
        var result = await _agent.GetTextContextAsync(project, volume, number, contextLines);
        if (result == null)
            return new { error = "Text line not found" };

        return result;
    }

    [McpServerTool(Name = "list_texts")]
    [Description("Lists text lines in a volume with filtering (untranslated, unapproved, approved, or all). Useful for finding work to do.")]
    public async Task<object> ListTexts(
        [Description("Project code")] string project,
        [Description("Volume code")] string volume,
        [Description("Filter: 'all' (default), 'untranslated', 'unapproved', or 'approved'")] string status = "all",
        [Description("Maximum number of items to return (max 500)")] int limit = 50,
        [Description("Offset for pagination")] int offset = 0)
    {
        return await _agent.GetTextsAsync(project, volume, status, limit, offset);
    }

    [McpServerTool(Name = "search")]
    [Description("Full-text search across source texts and/or existing translations in a project. Extremely useful for maintaining translation consistency.")]
    public async Task<object> Search(
        [Description("Project code (optional but recommended)")] string project,
        [Description("Search query")] string query,
        [Description("Search in original source texts")] bool source = true,
        [Description("Search in existing translations")] bool translated = true,
        [Description("Maximum results")] int size = 15)
    {
        return await _agent.SearchAsync(project, query, source, translated, size);
    }

    [McpServerTool(Name = "propose_translation")]
    [Description("Propose a new translation for a specific line. The proposal will be saved with the configured AI agent name as author. Always use get_text_context first and prefer search for consistency.")]
    public async Task<object> ProposeTranslation(
        [Description("Project code")] string project,
        [Description("Volume code")] string volume,
        [Description("Line number")] int number,
        [Description("The proposed translation text")] string text,
        [Description("Explanation / reasoning for this translation choice (highly recommended)")] string reason = "",
        [Description("Confidence score from 0.0 to 1.0")] double? confidence = null)
    {
        var result = await _agent.ProposeTranslationAsync(project, volume, number, text, reason, confidence);

        if (result == null)
            return new { error = "Failed to create proposal. Check that the line exists." };

        return result;
    }

    [McpServerTool(Name = "add_comment")]
    [Description("Add a review comment to an existing translation (by its ID). Use this to give feedback on human or previous AI translations.")]
    public async Task<object> AddComment(
        [Description("ID of the translation to comment on")] string translateId,
        [Description("Comment text")] string text)
    {
        var comment = await _agent.AddCommentAsync(translateId, text);

        if (comment == null)
            return new { error = "Translation not found" };

        return new
        {
            success = true,
            commentId = comment.Id,
            message = "Comment added successfully"
        };
    }

    [McpServerTool(Name = "get_translation_history")]
    [Description("Returns the full edit history for a specific translation line.")]
    public async Task<object> GetTranslationHistory(
        [Description("Translation ID")] string translateId)
    {
        var result = await _agent.GetHistoryAsync(translateId);
        if (result == null)
            return new { error = "Translation not found" };

        return result;
    }
}
