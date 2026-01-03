using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dataportal.Services
{
    public interface ITabularFileImporter
    {
        Task<IReadOnlyList<TabularColumnDefinition>> InferColumnsAsync(IEnumerable<IFormFile> files, int sampleSize = 2000);

        Task<TabularImportResult> ImportAsync(TableImportTarget target, IEnumerable<IFormFile> files, IReadOnlyList<TabularColumnDefinition> columns, int batchSize = 5000);

        Task ImportAsync(TableImportTarget target, IEnumerable<IFormFile> files);

        Task DropTableAsync(TableImportTarget target);
    }
}