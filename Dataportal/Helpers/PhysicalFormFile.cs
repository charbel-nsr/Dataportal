using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Dataportal.Helpers
{
    public class PhysicalFormFile : IFormFile
    {
        private readonly string _filePath;
        private readonly IHeaderDictionary _headers;

        public PhysicalFormFile(string filePath, string originalFileName, string contentType)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            _filePath = filePath;
            var info = new FileInfo(filePath);
            if (!info.Exists)
            {
                throw new FileNotFoundException("The persisted upload could not be found.", filePath);
            }

            Length = info.Length;
            Name = info.Name;
            FileName = string.IsNullOrWhiteSpace(originalFileName) ? info.Name : originalFileName;
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            ContentDisposition = string.Empty;
            _headers = new HeaderDictionary
            {
                [HeaderNames.ContentType] = ContentType
            };
        }

        public Stream OpenReadStream()
        {
            return new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void CopyTo(Stream target)
        {
            using var stream = OpenReadStream();
            stream.CopyTo(target);
        }

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            await using var stream = OpenReadStream();
            await stream.CopyToAsync(target, cancellationToken);
        }

        public string ContentType { get; }

        public string ContentDisposition { get; }

        public IHeaderDictionary Headers => _headers;

        public long Length { get; }

        public string Name { get; }

        public string FileName { get; }
    }
}