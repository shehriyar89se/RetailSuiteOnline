using RetailSuite.StoreAdmin.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Every storefront page view opens a Blazor circuit, and the storefront is
        // public — crawlers and uptime checks park circuits in memory just by
        // hitting it. The defaults (100 circuits kept for 3 minutes) are sized for
        // an app where disconnects are mostly real users on flaky connections, so
        // they're wasteful here. A minute still covers a genuine network blip and
        // lets a shopper resume mid-cart; it only gives up on tabs that are gone.
        options.DisconnectedCircuitMaxRetained      = 20;
        options.DisconnectedCircuitRetentionPeriod  = TimeSpan.FromMinutes(1);
    });

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<StorefrontContext>();
builder.Services.AddSingleton<ApiUrlService>();

builder.Services.AddScoped(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var apiBaseUrl = configuration["Api:BaseUrl"] ?? "https://localhost:7001/";

    return new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
