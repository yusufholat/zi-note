using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zinote.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private const string FirebaseAuthTokenKey = "FirebaseAuthToken";

    public UserInfo CurrentUser { get; private set; }

    public AuthService()
    {
        _httpClient = new HttpClient();
        // Do not block here. Config will be loaded when needed.
    }

    private string _firebaseApiKey;
    private bool _isConfigLoaded = false;

    private async Task EnsureConfigurationLoadedAsync()
    {
        if (_isConfigLoaded) return;

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("appsettings.json");
            using var reader = new StreamReader(stream);
            var contents = await reader.ReadToEndAsync();
            var doc = JsonDocument.Parse(contents);
            if (doc.RootElement.TryGetProperty("Firebase", out var firebaseSection))
            {
                if (firebaseSection.TryGetProperty("ApiKey", out var keyElement))
                {
                    _firebaseApiKey = keyElement.GetString();
                }
            }
            _isConfigLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
        }
    }

    public bool IsUserLoggedIn => CurrentUser != null;

    public async Task<string> LoginAsync(string email, string password)
    {
        try
        {
            await EnsureConfigurationLoadedAsync();

            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_firebaseApiKey}";
            var payload = new { email, password, returnSecureToken = true };
            
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<FirebaseAuthResponse>();
                await SaveTokenAsync(content);
                SetCurrentUser(content);
                return string.Empty;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return ParseError(errorContent);
            }
        }
        catch (Exception ex)
        {
            return $"An error occurred: {ex.Message}";
        }
    }

    public async Task<string> CreateUserAsync(string email, string password)
    {
        try
        {
            await EnsureConfigurationLoadedAsync();
            
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_firebaseApiKey}";
            var payload = new { email, password, returnSecureToken = true };

            var response = await _httpClient.PostAsJsonAsync(url, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<FirebaseAuthResponse>();
                await SaveTokenAsync(content);
                SetCurrentUser(content);
                return string.Empty;
            }
             else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return ParseError(errorContent);
            }
        }
        catch (Exception ex)
        {
            return $"An error occurred: {ex.Message}";
        }
    }

    public void LoginAsGuest()
    {
        CurrentUser = new UserInfo
        {
            Email = "Guest",
            LocalId = "guest",
            IsGuest = true
        };
        // We don't save token for guest, so it's a transient session.
        // Or we could save a dummy marker if we want auto-login for guest.
        // For now, let's treat it as session-only.
    }

    public void SignOut()
    {
        SecureStorage.Default.Remove(FirebaseAuthTokenKey);
        CurrentUser = null;
    }

    public async Task<bool> CheckLoginStatusAsync()
    {
        try
        {
            var tokenJson = await SecureStorage.Default.GetAsync(FirebaseAuthTokenKey);
            if (string.IsNullOrEmpty(tokenJson)) return false;

            var authSchema = JsonSerializer.Deserialize<FirebaseAuthResponse>(tokenJson);
            
            // Basic validity check (should check expiry in real app)
            if (authSchema != null && !string.IsNullOrEmpty(authSchema.IdToken))
            {
                SetCurrentUser(authSchema);
                return true;
            }
            return false;
        }
        catch
        {
            SignOut();
            return false;
        }
    }

    private async Task SaveTokenAsync(FirebaseAuthResponse authResponse)
    {
        var json = JsonSerializer.Serialize(authResponse);
        await SecureStorage.Default.SetAsync(FirebaseAuthTokenKey, json);
    }

    private void SetCurrentUser(FirebaseAuthResponse response)
    {
        CurrentUser = new UserInfo 
        { 
            Email = response.Email, 
            LocalId = response.LocalId,
            IsGuest = false
        };
    }

    private string ParseError(string jsonError)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonError);
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                if (errorElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString();
                }
            }
            return "Authentication Failed";
        }
        catch
        {
            return "Authentication Failed";
        }
    }
}

public class UserInfo
{
    public string Email { get; set; }
    public string LocalId { get; set; }
    public bool IsGuest { get; set; }
}

public class FirebaseAuthResponse
{
    [JsonPropertyName("idToken")]
    public string IdToken { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("expiresIn")]
    public string ExpiresIn { get; set; }

    [JsonPropertyName("localId")]
    public string LocalId { get; set; }
}
