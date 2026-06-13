using PrintCalc.Infrastructure.Services.Backup;

namespace PrintCalc.Api;

public static class MapBackupEndpoints
{
    public static void MapBackup(this WebApplication app)
    {
        app.MapGet("/api/backup/download", async (
            IBackupService backup,
            IWebHostEnvironment env,
            CancellationToken ct) =>
        {
            BackupCreateResult result;
            try
            {
                var appConfig = Path.Combine(env.ContentRootPath, "appsettings.json");
                result = await backup.CreateBackupZipAsync(
                    File.Exists(appConfig) ? appConfig : null,
                    ct);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, title: "Záloha selhala", statusCode: 500);
            }

            var fileName = Path.GetFileName(result.TempZipPath);
            return Results.File(
                result.TempZipPath,
                "application/zip",
                fileName,
                enableRangeProcessing: false);
        });

        app.MapPost("/api/backup/restore", async (
            HttpRequest request,
            IBackupService backup,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Očekáván multipart/form-data se souborem zálohy." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Vyberte soubor zálohy (.zip)." });

            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Záloha musí být ve formátu ZIP." });

            await using var stream = file.OpenReadStream();
            var result = await backup.RestoreFromZipAsync(stream, ct);

            if (!result.Success)
                return Results.BadRequest(new { error = result.Message, manifest = result.Manifest });

            return Results.Ok(new
            {
                message = result.Message,
                manifest = result.Manifest,
            });
        });
    }
}
