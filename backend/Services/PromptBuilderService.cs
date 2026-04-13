// ============================================================
// KITSUNE – PromptBuilderService
// Builds prompts for local LLMs (SQLCoder format).
//
// SQLCoder prompt format — this exact structure is what
// defog/sqlcoder and similar models expect:
//
//   ### Instructions:
//   Generate a SQL query. Output only SQL.
//   ### Database Schema:
//   {schema in LLM format}
//   ### Question:
//   {user request}
//   ### SQL Query:
//
// The model continues from the "### SQL Query:" marker and
// produces only the SQL — no explanations, no JSON, no preamble.
// ============================================================
using System.Text;
using Kitsune.Backend.Models;

namespace Kitsune.Backend.Services
{
    public interface IPromptBuilderService
    {
        string BuildSqlGenerationPrompt(string naturalLanguage, string schemaLlmFormat, string databaseName);
        string BuildExplainPrompt(string sqlQuery);
        string BuildTableDetectionPrompt(string naturalLanguage, IEnumerable<string> tableNames);
    }

    public class PromptBuilderService : IPromptBuilderService
    {
        // ── SQL generation — SQLCoder format ─────────────────
        public string BuildSqlGenerationPrompt(
            string naturalLanguage,
            string schemaLlmFormat,
            string databaseName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### Instructions:");
            sb.AppendLine($"Your task is to convert a question into a SQL query, given a MS SQL Server database schema.");
            sb.AppendLine($"Adhere to these rules:");
            sb.AppendLine($"- Use ONLY tables and columns defined in the schema below.");
            sb.AppendLine($"- Do NOT guess column or table names.");
            sb.AppendLine($"- Use dbo.TableName prefix for all table references.");
            sb.AppendLine($"- For joins, use the foreign key relationships shown in the schema.");
            sb.AppendLine($"- Add TOP 1000 to SELECT queries unless a specific row count is requested.");
            sb.AppendLine($"- Output only the SQL query. No explanation. No JSON. No markdown.");
            sb.AppendLine();
            sb.AppendLine("### Database Schema:");

            if (!string.IsNullOrWhiteSpace(schemaLlmFormat))
            {
                sb.AppendLine($"-- Database: {databaseName}");
                sb.AppendLine(schemaLlmFormat);
            }
            else
            {
                sb.AppendLine($"-- Database: {databaseName} (schema not available)");
            }

            sb.AppendLine();
            sb.AppendLine("### Question:");
            sb.AppendLine(naturalLanguage);
            sb.AppendLine();
            sb.AppendLine("### SQL Query:");
            // Model continues from here — do NOT add anything after this line

            return sb.ToString();
        }

        // ── Explain query ─────────────────────────────────────
        public string BuildExplainPrompt(string sqlQuery)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Explain the following SQL query in simple business terms.");
            sb.AppendLine("Cover:");
            sb.AppendLine("1. What data this query returns (business purpose)");
            sb.AppendLine("2. Which tables are accessed and why");
            sb.AppendLine("3. Any JOIN conditions and what they do");
            sb.AppendLine("4. Any filters (WHERE) and what they mean");
            sb.AppendLine("5. Any aggregations (COUNT, SUM, GROUP BY)");
            sb.AppendLine("6. Performance notes (if any)");
            sb.AppendLine();
            sb.AppendLine("SQL Query:");
            sb.AppendLine(sqlQuery);
            sb.AppendLine();
            sb.AppendLine("Explanation:");
            return sb.ToString();
        }

        // ── Table detection ───────────────────────────────────
        public string BuildTableDetectionPrompt(string naturalLanguage, IEnumerable<string> tableNames)
        {
            var list = string.Join(", ", tableNames);
            return $"""
                Given the following database tables: {list}

                Which tables are needed to answer this question?
                Question: {naturalLanguage}

                Return ONLY a JSON array of table names needed. Example: ["Orders","Customers"]
                Do not include any explanation.
                """;
        }
    }
}
