using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dataportal.Services
{
    public interface ITabularFileImporter
    {
        Task ImportAsync(string tableName, IEnumerable<IFormFile> files);
    }
}