using PeasyWare.WMS.App.Data;
using PeasyWare.WMS.Console.Models.DTOs;

    var builder = WebApplication.CreateBuilder(args);

    // Connection string (edit or load from config)
    builder.Services.AddScoped<DatabaseService>(provider =>
    {
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=WMS_DB;Trusted_Connection=True;";
        return new DatabaseService(connStr);
    });

    var app = builder.Build();

    app.MapPost("/api/inbound", async (InboundDeliveryDto inbound, DatabaseService db) =>
    {
        try
        {
            var (success, message) = await db.ImportInboundAsync(inbound);

            return success
                ? Results.Ok(new { message })
                : Results.BadRequest(new { error = message });
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {ex}");
            Console.ResetColor();

            return Results.Problem(
                detail: ex.ToString(),
                title: "Internal Server Error",
                statusCode: 500
            );
        }
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

app.Run();

