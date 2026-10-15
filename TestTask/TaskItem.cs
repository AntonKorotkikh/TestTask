using System.ComponentModel.DataAnnotations;

namespace TestTask;

public class TaskItem
{
    public Guid Id { get; set; }
    [MinLength(1), MaxLength(200)]
    public string Title { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Priority Priority { get; set; }
}