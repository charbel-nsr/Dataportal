using Dataportal.Context;
using Dataportal.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Dataportal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Dataportal.Classes;
using Dataportal.Services.Email;
using IPortalEmailSender = Dataportal.Services.Email.IEmailSender;
using Microsoft.AspNetCore.CookiePolicy;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = UploadSizeLimits.StepUploadLimitBytes;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = UploadSizeLimits.StepUploadLimitBytes;
});

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
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.Always;
});

// Add cookie authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie(options =>
    {
        options.LoginPath = "/Compte/SeConnecter"; // Redirects to your login page if not authenticated.
        options.ReturnUrlParameter = "returnUrl";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, NotebookTokenAuthenticationHandler>(
        NotebookTokenDefaults.AuthenticationScheme,
        _ => { });

// Register the password hasher for Utilisateur class
builder.Services.AddScoped<IPasswordHasher<Utilisateur>, PasswordHasher<Utilisateur>>();

builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.SectionName));
builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection(PortalOptions.SectionName));
builder.Services.Configure<NotebookApiOptions>(builder.Configuration.GetSection("NotebookApi"));
builder.Services.AddOptions<JupyterSsoOptions>()
    .Bind(builder.Configuration.GetSection(JupyterSsoOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 32,
        "JupyterSso:SigningKey must be at least 32 characters.")
    .ValidateOnStart();
builder.Services.AddSingleton<JupyterSsoTokenService>();
builder.Services.AddScoped<IEmailTemplateRenderer, ThemedEmailTemplateRenderer>();
builder.Services.AddScoped<IPortalEmailSender, MailKitEmailSender>();
builder.Services.AddScoped<IAccountEmailService, AccountEmailService>();
builder.Services.AddHostedService<PendingRequestReminderHostedService>();
builder.Services.AddHostedService<UploadCleanupHostedService>();

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

app.UseCookiePolicy();

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
