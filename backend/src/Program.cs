using IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Web;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using WebAppBot.Config;
using WebAppBot.Extensions;
using WebAppBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var botConfig = builder.Services.AddSettings<BotConfig>(builder.Configuration, "BotConfig");
builder.Services.AddSettings<AuthConfig>(builder.Configuration, "AuthConfig");

if (botConfig == null || botConfig.NotConfigured)
{
    throw new Exception("Bot is not configured");
}

builder.Services.ConfigureTelegramBot<Microsoft.AspNetCore.Http.Json.JsonOptions>(opt => opt.SerializerOptions);
builder.Services.AddHttpClient("tgwebhook").RemoveAllLoggers().AddTypedClient(httpClient => new TelegramBotClient(botConfig?.Token ?? "", httpClient));
builder.Services.AddSingleton<AuthService>();
builder.Services.AddTransient<BotService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthorization();

app.MapGet("/bot/setWebhook", async (TelegramBotClient bot) =>
{
    await bot.SetWebhook(botConfig.PublicUrl + "bot"); return $"Webhook set to {botConfig.PublicUrl}bot";
});
app.MapPost("/bot", OnUpdate);
app.MapGet("/bot/auth", OnAuth);

app.Run();

async Task<IResult> OnAuth([FromQuery] string code, [FromQuery] string state, [FromQuery] string iss, AuthService authStorageService, TelegramBotClient bot)
{
    var result = await authStorageService.ChallengeAsync(code, state);
    if (result != null)
    {
        if (result.ChatId.HasValue)
        {
            await bot.SendMessage(result.ChatId.Value, "Авторизация прошла успешно");
        }
        return Results.Ok("Авторизация прошла успешно");
    }

    if (result?.ChatId.HasValue ?? false)
    {
        await bot.SendMessage(result.ChatId.Value, "Авторизация не удалась, поробуйте еще раз");
    }
    return Results.BadRequest("Авторизация завершилась с ошибкой");
}

async void OnUpdate(TelegramBotClient bot, Update update, ILogger<Program> logger, BotService botService)
{
    switch (update)
    {
        case { Message.Text: "/start" }:
            await bot.SendMessage(update.Message.Chat, "<b>Добрый день!</b>\n\nДавайте начнем с авторизации!",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: (InlineKeyboardMarkup)InlineKeyboardButton.WithWebApp("Запуск Mini-App", botConfig?.PublicUrl?.ToString() ?? ""));
            break;
        case { Message.Text: "/login" }:
            await botService.Login(bot, update);
            break;
        case { Message.Text: "/info" }:
            await botService.Info(bot, update);
            break;
        case { Message.WebAppData: { } wad }:
            logger.LogInformation("Received WebAppData: {Data} | button {ButtonText}", wad.Data, wad.ButtonText);
            break;
        default: break;
    }
}