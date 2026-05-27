using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TranslateServer.Mcp;

namespace TranslateServer.Controllers
{
    /// <summary>
    /// REST surface for AI agents (MCP-friendly HTTP API).
    /// 
    /// Protected by a separate MCP token.
    /// Does not affect the main cookie-based human API.
    /// 
    /// For real MCP protocol support, see the MCP server configuration.
    /// </summary>
    [ApiController]
    [Route("api/mcp")]
    [ServiceFilter(typeof(McpAuthorizationFilter))]
    public class McpController : ControllerBase
    {
        private readonly McpAgentService _agent;

        public McpController(McpAgentService agent)
        {
            _agent = agent;
        }

        // ============================================
        // DISCOVERY
        // ============================================

        [HttpGet("status")]
        public async Task<ActionResult> GetStatus()
        {
            return Ok(await _agent.GetStatusAsync());
        }

        [HttpGet("projects")]
        public async Task<ActionResult> GetProjects()
        {
            return Ok(await _agent.GetProjectsAsync());
        }

        [HttpGet("projects/{project}/volumes")]
        public async Task<ActionResult> GetVolumes(string project)
        {
            if (!await _agent.ProjectExistsAsync(project)) return NotFound();
            return Ok(await _agent.GetVolumesAsync(project));
        }

        // ============================================
        // READING TEXTS + CONTEXT (most important for LLM)
        // ============================================

        [HttpGet("projects/{project}/volumes/{volume}/texts")]
        public async Task<ActionResult> GetTexts(
            string project,
            string volume,
            [FromQuery] string status = "all",
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0)
        {
            if (!await _agent.ProjectExistsAsync(project)) return NotFound();

            var result = await _agent.GetTextsAsync(project, volume, status, limit, offset);
            return Ok(result);
        }

        /// <summary>
        /// The most valuable endpoint for an LLM.
        /// Returns the source line + surrounding context + current translation + comments.
        /// </summary>
        [HttpGet("projects/{project}/volumes/{volume}/text/{number:int}/context")]
        public async Task<ActionResult> GetTextContext(
            string project,
            string volume,
            int number,
            [FromQuery] int contextLines = 3)
        {
            if (!await _agent.ProjectExistsAsync(project)) return NotFound();

            var result = await _agent.GetTextContextAsync(project, volume, number, contextLines);
            if (result == null) return NotFound();

            return Ok(result);
        }

        // ============================================
        // SEARCH (critical for consistency)
        // ============================================

        public class SearchRequest
        {
            public string Project { get; set; }
            public string Query { get; set; }
            public bool Source { get; set; } = true;
            public bool Translated { get; set; } = true;
            public int Size { get; set; } = 20;
        }

        [HttpPost("search")]
        public async Task<ActionResult> Search([FromBody] SearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Query))
                return BadRequest(new { error = "Query is required" });

            var result = await _agent.SearchAsync(request.Project, request.Query, request.Source, request.Translated, request.Size);
            return Ok(result);
        }

        // ============================================
        // WRITING (proposals only — safe for AI)
        // ============================================

        public class ProposeRequest
        {
            public string Project { get; set; }
            public string Volume { get; set; }
            public int Number { get; set; }
            public string Text { get; set; }
            public string Reason { get; set; }
            public double? Confidence { get; set; }
        }

        [HttpPost("propose")]
        public async Task<ActionResult> Propose([FromBody] ProposeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { error = "Text is required" });

            if (!await _agent.ProjectExistsAsync(request.Project))
                return NotFound(new { error = "Project not found" });

            if (_agent.IsReadOnly)
            {
                return Unauthorized(new
                {
                    error = "This agent is read-only and cannot propose translations.",
                    agentName = _agent.AgentName
                });
            }

            var result = await _agent.ProposeTranslationAsync(
                request.Project, request.Volume, request.Number,
                request.Text, request.Reason, request.Confidence);

            if (result == null)
                return NotFound(new { error = "Text line not found" });

            return Ok(result);
        }

        public class AddCommentRequest
        {
            public string TranslateId { get; set; }
            public string Text { get; set; }
        }

        [HttpPost("comments")]
        public async Task<ActionResult> AddComment([FromBody] AddCommentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.TranslateId) || string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { error = "TranslateId and Text are required" });

            if (_agent.IsReadOnly)
            {
                return Unauthorized(new
                {
                    error = "This agent is read-only and cannot add comments.",
                    agentName = _agent.AgentName
                });
            }

            var comment = await _agent.AddCommentAsync(request.TranslateId, request.Text);
            if (comment == null) return NotFound();

            return Ok(comment);
        }

        // ============================================
        // HISTORY
        // ============================================

        [HttpGet("translates/{id}/history")]
        public async Task<ActionResult> GetHistory(string id)
        {
            var result = await _agent.GetHistoryAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
