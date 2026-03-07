using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace PortfolioNew.Services
{
    public class GoogleSheetsService
    {
        private static SheetsService? service;

        public static string[] Scopes = { SheetsService.Scope.Spreadsheets };
        public static string ApplicationName = "MyApp";
        public static string SpreadsheetId = "1nqB6yvhMOPcxeBBFMrATWPthH4T9W5JheoIJR_O7wBY";

        private static SheetsService CreateConnection()
        {
            var credentialsBase64 = Environment.GetEnvironmentVariable("GOOGLE_SHEETS_CLIENT_SECRET_JSON_BASE64");
            if (string.IsNullOrWhiteSpace(credentialsBase64))
            {
                throw new InvalidOperationException("Missing env var GOOGLE_SHEETS_CLIENT_SECRET_JSON_BASE64 for Google Sheets auth.");
            }

            byte[] credentialBytes;
            try
            {
                credentialBytes = Convert.FromBase64String(credentialsBase64);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("GOOGLE_SHEETS_CLIENT_SECRET_JSON_BASE64 is not valid Base64.", ex);
            }

            GoogleCredential credential;
#pragma warning disable CS0618 // Type or member is obsolete
            using (var stream = new MemoryStream(credentialBytes))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(Scopes);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            // Create Google Sheets API service.
            service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
            return service;
        }

        public static IList<IList<object>> GetDataFromSheet(string sheet, string sheetrange)
        {
            var service = CreateConnection();

            var range = $"{sheet}!{sheetrange}";
            SpreadsheetsResource.ValuesResource.GetRequest request =
                    service.Spreadsheets.Values.Get(SpreadsheetId, range);

            var response = request.Execute();
            IList<IList<object>> values = response.Values;

            return values;
        }
    }
}
