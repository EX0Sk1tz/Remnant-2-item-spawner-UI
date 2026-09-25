namespace Remnant2UnlockerApp.Models;

public sealed class CategoryGroup
{
    public string Name { get; set; } = "";
    public List<string> Types { get; set; } = new();

    // What the sidebar binds to: one entry per type in Types, in the same order.
    public List<CategoryTypeEntry> Entries { get; set; } = new();
}
