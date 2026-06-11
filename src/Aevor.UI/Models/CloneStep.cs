namespace Aevor.UI.Models;

public class CloneStep
{
    public int    StepNumber   { get; set; }
    public string Title        { get; set; } = string.Empty;
    public bool   IsCompleted  { get; set; }
    public bool   IsActive     { get; set; }
}
