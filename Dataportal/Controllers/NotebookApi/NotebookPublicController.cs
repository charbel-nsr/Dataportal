using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
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

            var tableCheck = ValidateTableName(table, out var normalizedTable);
            if (tableCheck != null)
            {
                return tableCheck;
            }

            var metadonnee = await FindMetadonneeForTableAsync(normalizedTable, cancellationToken);
            if (metadonnee == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Dataset not found",
                    Detail = $"No dataset metadata is associated with table '{normalizedTable}'."
                });
            }

            var tokenAuthResult = await HttpContext.AuthenticateAsync(NotebookTokenDefaults.AuthenticationScheme);
            if (tokenAuthResult.Succeeded && tokenAuthResult.Principal != null)
            {
                HttpContext.User = tokenAuthResult.Principal;
            }
            else if (tokenAuthResult.Failure != null)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Invalid token",
                    Detail = "The provided notebook token is invalid.",
                    Status = StatusCodes.Status401Unauthorized,
                    Type = "https://httpstatuses.com/401"
                });
            }

            if (!HttpContextUserHelper.CanCurrentUserAccessMetadonnee(HttpContext, Context, metadonnee, out var requiresAuthentication))
            {
                return requiresAuthentication
                    ? Challenge(NotebookTokenDefaults.AuthenticationScheme)
                    : Forbid();
            }

            var resolvedLimit = Math.Clamp(limit ?? 100, 1, MaxRows);
            var qualifiedTable = BuildQualifiedTable(schema, normalizedTable);

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