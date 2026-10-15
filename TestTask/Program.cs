using Microsoft.EntityFrameworkCore;
using Npgsql;
using RabbitMQ.Client;
using Serilog;

namespace TestTask;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = LogConfiguration.CreateLogger();
        Log.Information("Starting Tasks server");
        try
        {
            StartApp(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Unhandled exception");
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

      private static void StartApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog();
        
        builder.Services.AddOpenApi();
        builder.Services.AddValidation();

        // HACK: лучше эту информацию брать из файла (и сам файл прописать в .gitignore)
        var dataSourceBuilder = new NpgsqlDataSourceBuilder("Host=localhost;Database=learningdb;Username=work_account;Password=123");
        var dataSource = dataSourceBuilder.Build();

        builder.Services.AddDbContext<DatabaseContext>(options => options.UseNpgsql(dataSource));
        
        builder.Services.AddSingleton<IConnection>(options =>
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",     // HACK: и это тоже из файла
                UserName = "guest",
                Password = "guest",
                Port = 5672
            };
            return factory.CreateConnectionAsync().Result;
        });
        
        builder.Services.AddSingleton<RabbitMQPublisher>();    // HACK: тут бы нужен интерфейс

        var app = builder.Build();

        app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() =>
        {
            Log.Information("Closing Tasks server");
            
            var myService = app.Services.GetRequiredService<RabbitMQPublisher>();
            myService.CloseConnection().GetAwaiter().GetResult();
            
            // HACK: не трекаем состояние тасков, нет таймаута на недоопубликованные в момент завершения 
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.MapPost("/tasks", async (TaskItemPostDTO taskItemPostDto, DatabaseContext context, ILogger<Program> logger) =>
        {
            try
            {
                var taskItemToAdd = new TaskItem()
                {
                    CreatedAt = DateTime.UtcNow,
                    IsCompleted = false,
                    Priority =  taskItemPostDto.Priority ?? Priority.Medium,
                    Title = taskItemPostDto.Title
                };

                var addedTaskItem = context.TaskItems.Add(taskItemToAdd);
                await context.SaveChangesAsync();
                logger.LogInformation("POST /tasks: added ok, id: {id}", addedTaskItem.Entity.Id);
                return Results.Created($"/tasks/{addedTaskItem.Entity.Id}", addedTaskItem.Entity);  // HACK: лучше сразу завести отдельный DTO для response
            }
            catch (Exception e)
            {
                logger.LogError(e, "POST /tasks: failure");
                return Results.StatusCode(500);
            }
        });
        app.MapGet("/tasks", async (DatabaseContext context, ILogger<Program> logger) =>
        {
            try
            {
                var taskItems = await context.TaskItems.ToListAsync();
                return Results.Ok(taskItems);
            }
            catch (Exception e)
            {
                logger.LogError(e, "GET /tasks: failure");
                return Results.StatusCode(500);
            }
        });
        app.MapPut("/tasks/{id}/complete", async (Guid id, DatabaseContext context, ILogger<Program> logger, RabbitMQPublisher publisher) =>
        {
            try
            {
                var requestedTaskItem = await context.TaskItems.FindAsync(id);
                if (requestedTaskItem == null)
                    return Results.NotFound();
                if (requestedTaskItem.IsCompleted)      // HACK: race condition, и ещё возвращаем Conflict даже если не обрабатываем другой таск 
                    return Results.Conflict();
                
                requestedTaskItem.IsCompleted = true;
                requestedTaskItem.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                
                var publishTask = publisher.PublishAsync($$"""
                                                                  {
                                                                      "taskId": "{{requestedTaskItem.Id}}",
                                                                      "title": "{{requestedTaskItem.Title}}",
                                                                      "completedAt": "{{requestedTaskItem.CompletedAt}}",
                                                                      "priority": "{{requestedTaskItem.Priority.ToString()}}"
                                                                  }
                                                                  """, "task.events", "task.completed");
                var winner = await Task.WhenAny(
                    publishTask,
                    Task.Delay(TimeSpan.FromSeconds(5)));
                if (winner != publishTask)
                    logger.LogWarning("PUT /tasks/{id}/complete: completed ok, but publishing to RabbitMQ probably timed out", id);
                
                // HACK: успешное завершение тоже надо бы логировать, и у других эндпоинтов тоже
                return Results.NoContent();     // HACK: сущность тут тоже мб стоит возвращать
            }
            catch (Exception e)
            {
                logger.LogError(e, "PUT /tasks/{id}/complete: failure", id);
                return Results.StatusCode(500);
            }
        });
        app.MapDelete("/tasks/{id}", async (Guid id, DatabaseContext context, ILogger<Program> logger) =>
        {
            try
            {
                var requestedTaskItem = await context.TaskItems.FindAsync(id);
                if (requestedTaskItem == null)
                    return Results.NotFound();

                context.TaskItems.Remove(requestedTaskItem);
                await context.SaveChangesAsync();

                return Results.Ok();
            }
            catch (Exception e)
            {
                logger.LogError(e, "DELETE /tasks/{id}/: failure", id);
                return Results.StatusCode(500);
            }
        });
        
        app.Run();
    }
}