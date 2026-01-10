namespace Dataportal.Services
{
    public class NotebookApiOptions
    {
        public int MaxRowsPerPage { get; set; } = 100_000;
        public long MaxBytesPerResponse { get; set; } = 200L * 1024 * 1024;
        public int RowGroupSize { get; set; } = 10_000;
        public int CommandTimeoutSeconds { get; set; } = 60;
    }
}
