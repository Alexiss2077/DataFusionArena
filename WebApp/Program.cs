using DataFusionArena.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<DataStore>();

var app = builder.Build();

// Cargar datos iniciales (archivos SampleData) al arrancar
var store = app.Services.GetRequiredService<DataStore>();
store.CargarDatosIniciales(app.Environment.ContentRootPath);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
