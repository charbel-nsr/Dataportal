using Dataportal.Context;
using Dataportal.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Dataportal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Dataportal.Classes;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Service for DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<ITabularFileImporter, TabularFileImportService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Dataportal.Session";
});

// Add cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Compte/SeConnecter"; // Redirects to your login page if not authenticated.
        options.ReturnUrlParameter = "returnUrl";
    });

// Register the password hasher for Utilisateur class
builder.Services.AddScoped<IPasswordHasher<Utilisateur>, PasswordHasher<Utilisateur>>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Accueil/Error");
    // TODO: change HSTS in production
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Accueil/Status/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.Use(async (context, next) =>
{
    var resumeId = context.Session.GetInt32(SessionKeys.CreationMetadonneeId);

    if (resumeId.HasValue && HttpMethods.IsGet(context.Request.Method))
    {
        var path = context.Request.Path;
        var isCreationFlow = path.StartsWithSegments("/Donnees/CreateStep3")
            || path.StartsWithSegments("/Donnees/CreateStep4")
            || path.StartsWithSegments("/Donnees/Details")
            || path.StartsWithSegments("/Donnees/Resume");
        var isLoginPage = path.StartsWithSegments("/Compte/SeConnecter");

        if (!isCreationFlow && !isLoginPage)
        {
            var resumeUrl = $"/Donnees/Details/{resumeId.Value}?creation=true";
            context.Response.Redirect(resumeUrl);
            return;
        }
    }

    await next();
});

// IMPORTANT: Add the authentication middleware before authorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Accueil}/{action=Index}/{id?}");

app.Run();
