using DdtManual.Infrastructure;
using Dfe.Frontend.AspNetCore;
using GovUk.Frontend.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGovUkFrontend();
builder.Services
    .AddControllersWithViews()
    .AddApplicationPart(DfeFrontendAspNetCore.Assembly);
builder.Services.AddInfrastructure(builder.Configuration);

// Package static web assets load automatically in Development. For Test/Staging `dotnet run`, enable disk paths (same as Service Manual).
// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsProduction())
    builder.WebHost.UseStaticWebAssets();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseGovUkFrontend();
app.UseAuthorization();

// .NET 9+: maps NuGet RCL assets under /_content/ (DfE Frontend CSS/JS). UseStaticFiles alone does not serve these.
app.MapStaticAssets();

// Attribute routes ([Route("collection")], [Route("content")], etc.) — not covered by MapControllerRoute alone.
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
