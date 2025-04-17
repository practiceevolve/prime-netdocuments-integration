using System.Text.Json;

namespace PE.Mk2.Integrations.NetDocuments.Helpers
{
    public static class Utils
    {
        public static Task<string> SendJsonAsync(this HttpClient http, string url, HttpMethod method, object data = null, IDictionary<string, string> headers = null, CancellationToken? ct = null) =>
            http.SendJsonAsync<string>(url, method, data, null, ct);

        public static async Task<TOut?> SendJsonAsync<TOut>(this HttpClient http, string url, HttpMethod method, object data = null, IDictionary<string, string>? headers = null, CancellationToken? ct = null)
        {
            var requestMessage = new HttpRequestMessage(method, url);

            // Add custom headers if provided
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    requestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            if (data != null)
            {
                // Serialize the object data to JSON and set the content for POST/PUT requests
                requestMessage.Content = JsonContent.Create(data);
            }

            // Send the request and get the response
            var response = await http.SendAsync(requestMessage, ct ?? CancellationToken.None);

            var responseBody = await response.Content.ReadAsStringAsync();

            return response.IsSuccessStatusCode
                ? (TOut)(typeof(TOut) == typeof(string) ? (object)responseBody : JsonDocument.Parse(responseBody).Deserialize<TOut>())
                : throw new Exception($"{url} ({method}) failed: {(string.IsNullOrWhiteSpace(responseBody) ? response.StatusCode.ToString() : responseBody)}");
        }

    }
}


