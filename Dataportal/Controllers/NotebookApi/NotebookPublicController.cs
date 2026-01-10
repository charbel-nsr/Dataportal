using Dataportal.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dataportal.Controllers.NotebookApi
{
    [Route("api/notebook/public")]
    public class NotebookPublicController : NotebookApiBaseController
    {
        private const int MaxRows = 1000;

        public NotebookPublicController(ApplicationDbContext context)
            : base(context)
        {
        }

        [HttpGet("query")]
        public async Task<IActionResult> QueryAsync([FromQuery] string schema, [FromQuery] string table, [FromQuery] int? limit, CancellationToken cancellationToken)
        {
            var schemaCheck = EnsureAllowedSchema(schema);
            if (schemaCheck != null)
            {
                return schemaCheck;
            }

            if (string.IsNullOrWhiteSpace(table))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Table name is required",
                    Detail = "A table name must be provided.",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://httpstatuses.com/400"
                });
            }

            var resolvedLimit = Math.Clamp(limit ?? 100, 1, MaxRows);
            var qualifiedTable = BuildQualifiedTable(schema, table);

            using var connection = new SqlConnection(Context.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            var query = $"SELECT TOP (@limit) * FROM {qualifiedTable}";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@limit", resolvedLimit);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<Dictionary<string, object?>>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(row);
            }

            return Ok(rows);
        }
    }
}