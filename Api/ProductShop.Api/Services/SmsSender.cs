using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ProductShop.Api.Services;

// SMS pathano. appsettings er "Sms:Provider" onujayi:
//   "Log"     - SMS jay na, shudhu API er log e lekha hoy (test er jonno, free)
//   "SmsGate" - nijer Android phone theke SMS (sms-gate.app app), SIM er SMS rate e - API free
//   "BulkSmsBd" - bulksmsbd.net (account khulle kichu free test SMS dey)
public class SmsSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<SmsSender> _logger;

    public SmsSender(HttpClient http, IConfiguration config, ILogger<SmsSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    private string Provider => _config["Sms:Provider"] ?? "Log";

    public bool IsLogOnly => !Provider.Equals("SmsGate", StringComparison.OrdinalIgnoreCase)
                          && !Provider.Equals("BulkSmsBd", StringComparison.OrdinalIgnoreCase);

    // phone: "01711000000"; pathano gele true
    public async Task<bool> SendAsync(string phone, string text)
    {
        if (IsLogOnly)
        {
            _logger.LogWarning("[SMS test mode] {Phone}: {Text}", phone, text);
            return true;
        }

        return Provider.Equals("BulkSmsBd", StringComparison.OrdinalIgnoreCase)
            ? await SendBulkSmsBdAsync(phone, text)
            : await SendSmsGateAsync(phone, text);
    }

    // response e "response_code": 202 mane pathano hoyeche; onno code er mane BulkSMSBD er docs e
    private async Task<bool> SendBulkSmsBdAsync(string phone, string text)
    {
        var url = _config["Sms:BulkSmsBd:Url"] ?? "https://bulksmsbd.net/api/smsapi";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = _config["Sms:BulkSmsBd:ApiKey"] ?? string.Empty,
            ["senderid"] = _config["Sms:BulkSmsBd:SenderId"] ?? string.Empty,
            ["type"] = "text",
            ["number"] = "88" + phone,
            ["message"] = text
        });

        try
        {
            var response = await _http.PostAsync(url, form);
            var body = await response.Content.ReadAsStringAsync();
            try
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("response_code", out var code) && code.ToString() == "202") return true;
            }
            catch (JsonException) { }

            _logger.LogError("BulkSMSBD SMS pathano jay nai ({Status}): {Body}", (int)response.StatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "BulkSMSBD er sathe connect kora jay nai: {Url}", url);
        }
        return false;
    }

    private async Task<bool> SendSmsGateAsync(string phone, string text)
    {
        var url = _config["Sms:SmsGate:Url"] ?? "https://api.sms-gate.app/3rdparty/v1/message";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                textMessage = new { text },
                phoneNumbers = new[] { "+88" + phone }
            })
        };
        var credentials = $"{_config["Sms:SmsGate:Username"]}:{_config["Sms:SmsGate:Password"]}";
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

        try
        {
            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode) return true;

            _logger.LogError("SMS pathano jay nai ({Status}): {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "SMS gateway er sathe connect kora jay nai: {Url}", url);
        }
        return false;
    }
}
