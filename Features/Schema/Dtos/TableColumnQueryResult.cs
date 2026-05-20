namespace OpsPilotAI.Features.Schema.Dtos;

/// <summary>
/// Dapper projection for the batched table+column query.
/// Using a single query eliminates the N+1 pattern in the original ExtractSchemaAsync
/// (which called GetColumnsAsync once per table, causing 50+ DB round-trips on a large schema).
/// </summary>
internal sealed class TableColumnQueryResult
{
    public string Table_Name { get; set; } = default!;
    public string? Column_Name { get; set; }
    public string? Data_Type { get; set; }
    public string? Is_Nullable { get; set; }
    public bool Is_Primary_Key { get; set; }
    public bool Is_Foreign_Key { get; set; }
}
