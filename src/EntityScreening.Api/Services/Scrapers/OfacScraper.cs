using System.Net;
using System.Text.RegularExpressions;
using EntityScreening.Api.Models;
using HtmlAgilityPack;

namespace EntityScreening.Api.Services.Scrapers;

public class OfacScraper : IScraperService
{
    public const string BaseUrl = "https://sanctionssearch.ofac.treas.gov/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OfacScraper> _logger;

    public OfacScraper(IHttpClientFactory httpClientFactory, ILogger<OfacScraper> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string SourceKey => "ofac";

    public string DisplayName => "OFAC - Sanctions List Search (U.S. Treasury)";

    public async Task<SearchResponse> SearchAsync(string entityName, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(OfacScraper));

            // El sitio es ASP.NET WebForms: primero GET para obtener los tokens, luego POST.
            var initialHtml = await client.GetStringAsync(BaseUrl, cancellationToken);
            var (viewState, viewStateGen, eventValidation) = ExtractWebFormsTokens(initialHtml);

            var form = new Dictionary<string, string>
            {
                ["__EVENTTARGET"] = string.Empty,
                ["__EVENTARGUMENT"] = string.Empty,
                ["__VIEWSTATE"] = viewState,
                ["__VIEWSTATEGENERATOR"] = viewStateGen,
                ["__EVENTVALIDATION"] = eventValidation,
                ["ctl00$MainContent$txtLastName"] = entityName,
                ["ctl00$MainContent$ddlType"] = "All",
                ["ctl00$MainContent$ddlPrograms"] = "All",
                ["ctl00$MainContent$ddlList"] = "All",
                ["ctl00$MainContent$Slider1"] = "100",
                ["ctl00$MainContent$Slider1_Boundcontrol"] = "100",
                ["ctl00$MainContent$btnSearch"] = "Search"
            };

            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(BaseUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var resultHtml = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResults(entityName, resultHtml);
        }
        catch (ScrapingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al consultar OFAC para '{Entity}'", entityName);
            throw new ScrapingException(SourceKey,
                "No se pudo obtener información desde OFAC. La fuente puede estar temporalmente no disponible o haber cambiado su estructura.",
                ex);
        }
    }

    private static (string ViewState, string ViewStateGenerator, string EventValidation) ExtractWebFormsTokens(string html)
    {
        string GetHiddenValue(string id)
        {
            var match = Regex.Match(html, $"id=\"{id}\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
            return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : string.Empty;
        }

        return (GetHiddenValue("__VIEWSTATE"), GetHiddenValue("__VIEWSTATEGENERATOR"), GetHiddenValue("__EVENTVALIDATION"));
    }

    private SearchResponse ParseResults(string entityName, string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<EntityHit>();
        var table = doc.GetElementbyId("gvSearchResults");
        var rows = table?.SelectNodes(".//tr");

        if (rows != null)
        {
            foreach (var row in rows)
            {
                var cells = row.SelectNodes("./td");
                if (cells == null || cells.Count < 6)
                {
                    continue;
                }

                var nameCell = cells[0];
                var name = Clean(nameCell.InnerText);
                var detailsUrl = nameCell.SelectSingleNode(".//a")?.GetAttributeValue("href", string.Empty);
                if (!string.IsNullOrWhiteSpace(detailsUrl) && !detailsUrl.StartsWith("http"))
                {
                    detailsUrl = BaseUrl + detailsUrl.TrimStart('/');
                }

                results.Add(new EntityHit
                {
                    Name = name,
                    Attributes = new Dictionary<string, string?>
                    {
                        ["Name"] = name,
                        ["Address"] = Clean(cells[1].InnerText),
                        ["Type"] = Clean(cells[2].InnerText),
                        ["Program(s)"] = Clean(cells[3].InnerText),
                        ["List"] = Clean(cells[4].InnerText),
                        ["Score"] = Clean(cells[5].InnerText),
                        ["DetailsUrl"] = detailsUrl
                    }
                });
            }
        }

        return new SearchResponse
        {
            Source = SourceKey,
            Query = entityName,
            Hits = results.Count,
            Results = results
        };
    }

    private static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(raw).Replace('\u00A0', ' ');
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
