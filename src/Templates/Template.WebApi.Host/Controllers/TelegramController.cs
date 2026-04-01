using Microsoft.AspNetCore.Mvc;
using Minnaloushe.Core.ClientProviders.Telegram;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using Telegram.Bot;

namespace Template.WebApi.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TelegramController(
    IServiceProvider serviceProvider,
    IResolvedKeyedOptions<TelegramOptions> telegramOptions) : ControllerBase
{
    [HttpPost("TestKeyedBot/{botKey}")]
    public async Task<IActionResult> TestKeyedBot(
        string botKey,
        string message,
        CancellationToken cancellationToken)
    {
        var telegramClient = serviceProvider.GetRequiredKeyedService<ITelegramClientProvider>(botKey);
        var options = telegramOptions.Get(botKey);

        if (options == null)
        {
            return NotFound($"Bot with key '{botKey}' not found or not initialized.");
        }
        await telegramClient.Client.SendMessage(
            chatId: options.Value.ChatId,
            text: message,
            cancellationToken: cancellationToken);

        return Ok(new { BotKey = options.Value.Key, ChatId = options.Value.ChatId });
    }

    [HttpGet("BotInfo/{botKey}")]
    public IActionResult GetBotInfo(string botKey)
    {
        var options = telegramOptions.Get(botKey);

        return options == null
            ? NotFound($"Bot with key '{botKey}' not found or not initialized.")
            : Ok(new
            {
                BotKey = options.Value.Key,
                ChatId = options.Value.ChatId,
                HasToken = !string.IsNullOrWhiteSpace(options.Value.BotToken)
            });
    }

    [HttpPost("ReportedBot/TestMessage")]
    public async Task<IActionResult> TestMessage(
        [FromKeyedServices("ReporterBot")] ITelegramClientProvider telegramClient,
        [FromKeyedServices("ReporterBot")]
        string message)
    {
        await telegramClient.Client.SendMessage(
            chatId: telegramClient.ChatId,
            text: message);

        return Ok();
    }
}
