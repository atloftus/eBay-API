using eBay_API.Models.Config;
using eBay_API.Services;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);


builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);


builder.Services.Configure<Config>(builder.Configuration);
builder.Services.AddTransient<EbayService>(sp =>
{
    var c = sp.GetRequiredService<IOptions<Config>>().Value;
    return new EbayService(c.ebay);
});
builder.Services.AddTransient<GoogleDriveService>(sp =>
{
    var c = sp.GetRequiredService<IOptions<Config>>().Value;
    return new GoogleDriveService(c.googledrive);
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();