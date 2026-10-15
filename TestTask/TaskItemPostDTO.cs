using System.ComponentModel.DataAnnotations;

namespace TestTask;

public class TaskItemPostDTO
{
    [MinLength(1), MaxLength(200)]
    public string Title { get; set; }
    public Priority? Priority { get; set; }
}