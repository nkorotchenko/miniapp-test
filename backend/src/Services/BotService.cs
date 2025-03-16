using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using IdentityModel.Client;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using System;

namespace WebAppBot.Services
{
    public class BotService
    {
        private readonly AuthService _authService;

        public BotService(AuthService authStorageService)
        {
            _authService = authStorageService;
        }

        public async Task Login(TelegramBotClient bot, Update update)
        {
            if (update.Message != null && update.Message.Chat != null)
            {
                var url = _authService.CreateLoginUrl(new AuthUserInfo
                {
                    ChatId = update.Message.Chat?.Id,
                    UserId = update.Message.From?.Id,
                    UserName = update.Message.From?.Username,
                    FirstName = update.Message.From?.FirstName,
                    LastName = update.Message.From?.LastName,
                });

                await bot.SendMessage(update.Message.Chat, "Вход в систему",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    replyMarkup: (InlineKeyboardMarkup)InlineKeyboardButton.WithUrl("Войти", url));
            }
        }

        public async Task Info(TelegramBotClient bot, Update update)
        {
            if (update?.Message?.From?.Username != null)
            {
                var authInfo = await _authService.GetUserInfoAsync(update?.Message?.From?.Username ?? "");
                if (authInfo != null)
                {
                    await bot.SendMessage(update.Message.Chat.Id, $"ID: {authInfo.Id}\nNAME: {authInfo.FirstName} {authInfo.LastName}");
                }
                else
                {
                    await bot.SendMessage(update.Message.Chat.Id, "Информации нет, попробуйте авторизоваться.");
                }
            }
        }
    }
}
