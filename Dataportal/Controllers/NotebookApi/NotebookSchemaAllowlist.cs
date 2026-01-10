using System;
using System.Collections.Generic;

namespace Dataportal.Services
{
    public static class NotebookSchemaAllowlist
    {
        private static readonly HashSet<string> AllowedSchemas = new(StringComparer.OrdinalIgnoreCase)
        {
            "donnees",
            "metadonnee",
            "logdonnee"
        };

        public static bool IsAllowed(string? schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return false;
            }

            return AllowedSchemas.Contains(schema.Trim());
        }
    }
}