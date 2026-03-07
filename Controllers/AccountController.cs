using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioNew.Models;
using PortfolioNew.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PortfolioNew.Controllers
{
    public class AccountController : Controller
    {
        public AccountController()
        {
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel
            {
                ReturnUrl = returnUrl ?? ""
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            model.ReturnUrl = returnUrl ?? model.ReturnUrl ?? "";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            SheetUser? matchedUser;
            try
            {
                var users = GetUsersFromSheet();
                if (users.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "No users found in sheet 'User' (range A3:C).");
                    return View(model);
                }

                matchedUser = users.FirstOrDefault(u =>
                    string.Equals(u.Username, model.Username?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (matchedUser == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                if (!PasswordMatches(matchedUser.Password, model.Password))
                {
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Login failed: {ex.Message}");
                return View(model);
            }

            var displayName = string.IsNullOrWhiteSpace(matchedUser.DisplayName) ? matchedUser.Username : matchedUser.DisplayName;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, matchedUser.Username),
                new(ClaimTypes.Name, displayName),
                new("username", matchedUser.Username),
                new("display_name", displayName)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 24 : 8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
            HttpContext.Session.SetString("display_name", displayName);
            HttpContext.Session.SetString("username", matchedUser.Username);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        private List<SheetUser> GetUsersFromSheet()
        {
            var rows = GoogleSheetsService.GetDataFromSheet("User", "A3:C");
            return ParseUsers(rows);
        }

        private static List<SheetUser> ParseUsers(IList<IList<object>>? rows)
        {
            var users = new List<SheetUser>();
            if (rows == null || rows.Count == 0)
            {
                return users;
            }

            foreach (var row in rows)
            {
                if (row == null || row.Count < 3)
                {
                    continue;
                }

                string displayName = GetCell(row, 0); // Name
                string username = GetCell(row, 1); // UserID
                string password = GetCell(row, 2); // AES-256 encrypted password

                if (string.Equals(displayName, "Name", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(username, "UserID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    continue;
                }

                users.Add(new SheetUser
                {
                    Username = username,
                    Password = password,
                    DisplayName = displayName
                });
            }

            return users;
        }

        private bool PasswordMatches(string encryptedPassword, string inputPassword)
        {
            if (string.IsNullOrWhiteSpace(encryptedPassword) || string.IsNullOrEmpty(inputPassword))
            {
                return false;
            }

            var encryptedText = encryptedPassword.Trim();
            encryptedText = encryptedText.Trim('\'', '"');
            if (encryptedText.StartsWith("enc:", StringComparison.OrdinalIgnoreCase))
            {
                encryptedText = encryptedText[4..].Trim();
            }

            try
            {
                var allBytes = Convert.FromBase64String(encryptedText);
                if (allBytes.Length <= 16)
                {
                    return false;
                }

                var iv = allBytes.Take(16).ToArray();
                var cipher = allBytes.Skip(16).ToArray();
                var key = ResolveAes256Key();

                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                var plainText = Encoding.UTF8.GetString(plainBytes);

                return string.Equals(plainText, inputPassword, StringComparison.Ordinal);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private static string GetCell(IList<object> row, int index)
        {
            if (index < 0 || index >= row.Count)
            {
                return "";
            }

            return row[index]?.ToString()?.Trim() ?? "";
        }

        private byte[] ResolveAes256Key()
        {
            var configured = Environment.GetEnvironmentVariable("AUTH_USER_AES_KEY_BASE64");
            if (string.IsNullOrWhiteSpace(configured))
            {
                throw new InvalidOperationException("AES key is not configured. Set env var AUTH_USER_AES_KEY_BASE64.");
            }

            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(configured);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("AUTH_USER_AES_KEY_BASE64 must be a valid Base64 string.", ex);
            }

            if (keyBytes.Length != 32)
            {
                throw new InvalidOperationException("AUTH_USER_AES_KEY_BASE64 must decode to exactly 32 bytes (256-bit key).");
            }

            return keyBytes;
        }

        private sealed class SheetUser
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public string DisplayName { get; set; } = "";
        }
    }
}
