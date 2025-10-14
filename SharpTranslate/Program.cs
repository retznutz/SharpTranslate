using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

/// <summary>
/// Main program class for SharpTranslate - a command-line tool for translating JSON localization files
/// using OpenAI's API while preserving HTML tags, placeholders, and protected brand terms.
/// </summary>
class Program
{
    // ---------- Config ----------
    /// <summary>Default OpenAI model to use for translations</summary>
    const string DefaultModel = "gpt-4o-mini";
    /// <summary>Number of strings to translate in each API batch call</summary>
    const int BatchSize = 15;
    /// <summary>Maximum number of retry attempts for failed API calls</summary>
    const int MaxRetries = 5;
    /// <summary>Sleep duration between API batch calls to respect rate limits</summary>
    const double SleepBetweenSeconds = 0.7;

    // Placeholder & HTML patterns to preserve exactly
    /// <summary>Regex pattern to match HTML tags that should be preserved during translation</summary>
    static readonly Regex HtmlTag = new Regex(@"</?([a-zA-Z][a-zA-Z0-9]*)\b[^>]*>", RegexOptions.Compiled);
    /// <summary>Regex pattern to match curly brace placeholders like {msg}, { email }, {0}</summary>
    static readonly Regex CurlyPlaceholder = new Regex(@"\{\s*[\w\.\-\[\]]+\s*\}", RegexOptions.Compiled); // {msg}, { email }, {0}
    /// <summary>Regex pattern to match percent placeholders like %s, %d</summary>
    static readonly Regex PercentPlaceholder = new Regex(@"%\w", RegexOptions.Compiled);                    // %s, %d
    /// <summary>Regex pattern to match mustache template placeholders like {{var}}</summary>
    static readonly Regex MustachePlaceholder = new Regex(@"\{\{\s*[\w\.\-]+\s*\}\}", RegexOptions.Compiled); // {{var}}
    /// <summary>Regex pattern to match colon placeholders like :name</summary>
    static readonly Regex ColonPlaceholder = new Regex(@":\w+", RegexOptions.Compiled);                    // :name
    /// <summary>Regex pattern to clean up whitespace followed by newlines</summary>
    static readonly Regex WhitespaceNewline = new Regex(@"\s+\n", RegexOptions.Compiled);

    /// <summary>
    /// Main entry point for the SharpTranslate application.
    /// Processes command-line arguments, loads and translates JSON files, and outputs the results.
    /// </summary>
    /// <param name="args">Command-line arguments for input/output files, language, and options</param>
    /// <returns>Exit code: 0 for success, 1 for errors, 2 for invalid arguments</returns>
    static int Main(string[] args)
    {
        try
        {
            var cli = CliOptions.Parse(args);
            if (cli == null) return 2;

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.Error.WriteLine("ERROR: OPENAI_API_KEY env var not set.");
                return 2;
            }

            // Load JSON (preserves property order with JToken)
            var inputText = File.ReadAllText(cli.InputPath, Encoding.UTF8);
            var root = JToken.Parse(inputText);

            // Collect all translatable strings
            var items = new List<(string path, string text)>();
            CollectStrings(root, "", items);

            if (items.Count == 0)
            {
                Console.WriteLine("No strings found to translate.");
                File.WriteAllText(cli.OutputPath, root.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
                return 0;
            }

            // Tokenize/placeholders/HTML/brands protection
            var tokenized = new List<string>(items.Count);
            var tokenMaps = new List<Dictionary<string, string>>(items.Count);

            foreach (var (_, text) in items)
            {
                var (tok, map) = Tokenize(text, cli.ProtectedTerms);
                tokenized.Add(tok);
                tokenMaps.Add(map);
            }

            // Translate in batches
            var translated = TranslateAllBatchesAsync(
                tokenized, cli, apiKey
            ).GetAwaiter().GetResult();

            if (translated.Count != tokenized.Count)
                throw new Exception("Translation count mismatch. Try lowering batch size.");

            // Detokenize & tidy
            var finalStrings = new List<string>(translated.Count);
            for (int i = 0; i < translated.Count; i++)
            {
                var restored = Detokenize(translated[i], tokenMaps[i]);
                restored = WhitespaceNewline.Replace(restored, "\n").Trim();
                finalStrings.Add(restored);
            }

            // Write back
            var output = root.DeepClone();
            for (int i = 0; i < items.Count; i++)
            {
                SetByPath(output, items[i].path, finalStrings[i]);
            }

            var outText = output.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(cli.OutputPath, outText, new UTF8Encoding(false));
            Console.WriteLine($"Done → {cli.OutputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAILED: " + ex.Message);
            return 1;
        }
    }

    // ---------- CLI Options ----------
    /// <summary>
    /// Configuration class that holds all command-line options and settings for the translation process.
    /// </summary>
    class CliOptions
    {
        /// <summary>Path to the input JSON file containing English text to translate</summary>
        public string InputPath = "";
        /// <summary>Path to the output JSON file where translated text will be written</summary>
        public string OutputPath = "";
        /// <summary>Target language code in BCP 47 format (e.g., es-ES, fr-FR)</summary>
        public string TargetLanguage = "es-ES";
        /// <summary>Tone and style instructions for the translation</summary>
        public string Tone = "Neutral, professional product UI tone";
        /// <summary>OpenAI model to use for translation</summary>
        public string Model = DefaultModel;
        /// <summary>List of brand terms and product names to protect from translation</summary>
        public List<string> ProtectedTerms = new();

        /// <summary>
        /// Parses command-line arguments and creates a CliOptions instance with the specified settings.
        /// </summary>
        /// <param name="args">Command-line arguments to parse</param>
        /// <returns>CliOptions instance if parsing succeeds, null if invalid arguments or help requested</returns>
        public static CliOptions? Parse(string[] args)
        {
            var o = new CliOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--in":
                        o.InputPath = args[++i];
                        break;
                    case "--out":
                        o.OutputPath = args[++i];
                        break;
                    case "--lang":
                        o.TargetLanguage = args[++i];
                        break;
                    case "--tone":
                        o.Tone = args[++i];
                        break;
                    case "--model":
                        o.Model = args[++i];
                        break;
                    case "--protect":
                        o.ProtectedTerms = args[++i]
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Distinct(StringComparer.Ordinal)
                            .ToList();
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown arg: {args[i]}");
                        return null;
                }
            }

            if (string.IsNullOrWhiteSpace(o.InputPath) || string.IsNullOrWhiteSpace(o.OutputPath))
            {
                Console.WriteLine(
                @"Usage:
                  --in <path>            Input JSON (English)
                  --out <path>           Output JSON (translated)
                  --lang <BCP47>         Target language (e.g., es-ES, fr-FR, de-DE). Default: es-ES
                  --tone <text>          Tone/style hint. Default: Neutral, professional product UI tone
                  --model <name>         OpenAI model. Default: gpt-4o-mini
                  --protect <CSV>        Brand terms to keep exactly (e.g., ""Guidestr,Stripe,Roku"")

                Example:
                  dotnet run -- --in en.json --out es-ES.json --lang es-ES --protect ""Guidestr,Stripe,Roku"""
                );
                return null;
            }

            return o;
        }
    }

    // ---------- JSON traversal ----------
    /// <summary>
    /// Recursively traverses a JSON structure and collects all string values along with their paths.
    /// Handles objects, arrays, and primitive values while maintaining path information.
    /// </summary>
    /// <param name="node">Current JSON token to process</param>
    /// <param name="path">Current path in dot notation (e.g., "user.profile.name")</param>
    /// <param name="acc">Accumulator list to collect (path, text) pairs</param>
    static void CollectStrings(JToken node, string path, List<(string path, string text)> acc)
    {
        switch (node.Type)
        {
            case JTokenType.Object:
                foreach (var prop in ((JObject)node).Properties())
                {
                    var p = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                    CollectStrings(prop.Value, p, acc);
                }
                break;
            case JTokenType.Array:
                int idx = 0;
                foreach (var item in (JArray)node)
                {
                    var p = $"{path}[{idx++}]";
                    CollectStrings(item, p, acc);
                }
                break;
            case JTokenType.String:
                acc.Add((path, node.Value<string>() ?? ""));
                break;
            default:
                break;
        }
    }


    /// <summary>
    /// Builds a compact JSON array string from a list of tokenized strings for API transmission.
    /// Minimizes token usage by creating a clean JSON array without extra formatting.
    /// </summary>
    /// <param name="tokenizedBatch">List of tokenized strings to include in the JSON array</param>
    /// <returns>Compact JSON array string</returns>
    static string BuildItemsJson(List<string> tokenizedBatch)
    {
        // Compact JSON array of strings to minimize tokens
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < tokenizedBatch.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(JsonConvert.SerializeObject(tokenizedBatch[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Sets a value at a specific path in a JSON structure, replacing the existing value.
    /// Handles both property values and direct token replacement.
    /// </summary>
    /// <param name="root">Root JSON token to modify</param>
    /// <param name="path">Path to the target location in dot notation</param>
    /// <param name="value">New string value to set</param>
    /// <exception cref="Exception">Thrown when the path is not found in the JSON structure</exception>
    static void SetByPath(JToken root, string path, string value)
    {
        var token = SelectTokenBySimplePath(root, path);
        if (token == null)
            throw new Exception($"Path not found: {path}");

        if (token.Type == JTokenType.Property)
        {
            ((JProperty)token).Value = value;
        }
        else
        {
            token.Replace(value);
        }
    }

    /// <summary>
    /// Simple path parser that navigates JSON structures using dot notation and array indexing.
    /// Supports paths like "a.b[2].c" to traverse objects and arrays.
    /// </summary>
    /// <param name="root">Root JSON token to start navigation from</param>
    /// <param name="path">Path string in dot notation with optional array indices</param>
    /// <returns>The JSON token at the specified path, or null if not found</returns>
    static JToken? SelectTokenBySimplePath(JToken root, string path)
    {
        var parts = new List<string>();
        var re = new Regex(@"([^\.\[\]]+)|\[(\d+)\]");
        foreach (Match m in re.Matches(path))
        {
            parts.Add(m.Groups[1].Success ? m.Groups[1].Value : $"[{m.Groups[2].Value}]");
        }

        JToken current = root;
        foreach (var part in parts)
        {
            if (part.StartsWith("["))
            {
                if (current.Type != JTokenType.Array) return null;
                int index = int.Parse(part.Trim('[', ']'));
                current = ((JArray)current)[index];
            }
            else
            {
                if (current.Type != JTokenType.Object) return null;
                current = ((JObject)current).Property(part) ?? ((JObject)current).Property(part, StringComparison.Ordinal);
                if (current == null) return null;
                current = ((JProperty)current).Value;
            }
        }
        return current;
    }

    // ---------- Tokenization / protection ----------
    /// <summary>
    /// Tokenizes a string by replacing HTML tags, placeholders, and protected terms with unique tokens.
    /// This protects these elements from being altered during translation while preserving their original values.
    /// </summary>
    /// <param name="text">Input text to tokenize</param>
    /// <param name="protectedTerms">List of brand terms and product names to protect</param>
    /// <returns>A tuple containing the tokenized text and a mapping of tokens to original values</returns>
    static (string tokenized, Dictionary<string, string> map) Tokenize(string text, List<string> protectedTerms)
    {
        var map = new Dictionary<string, string>();
        string Store(Match m)
        {
            var token = $"§T{map.Count}§";
            map[token] = m.Value;
            return token;
        }

        // HTML first
        var t = HtmlTag.Replace(text, Store);
        // Placeholders
        t = CurlyPlaceholder.Replace(t, Store);
        t = PercentPlaceholder.Replace(t, Store);
        t = MustachePlaceholder.Replace(t, Store);
        t = ColonPlaceholder.Replace(t, Store);
        // Protected brand terms (exact word)
        foreach (var term in protectedTerms.Distinct(StringComparer.Ordinal))
        {
            var termRe = new Regex(@"\b" + Regex.Escape(term) + @"\b");
            t = termRe.Replace(t, Store);
        }
        return (t, map);
    }

    /// <summary>
    /// Restores original values by replacing tokens with their corresponding original text.
    /// This reverses the tokenization process after translation is complete.
    /// </summary>
    /// <param name="text">Translated text containing tokens to replace</param>
    /// <param name="map">Dictionary mapping tokens to their original values</param>
    /// <returns>Text with all tokens replaced by their original values</returns>
    static string Detokenize(string text, Dictionary<string, string> map)
    {
        foreach (var kv in map.OrderByDescending(k => k.Key.Length))
        {
            text = text.Replace(kv.Key, kv.Value);
        }
        return text;
    }

    // ---------- OpenAI Translation ----------
    /// <summary>
    /// Orchestrates the translation of all strings by processing them in batches.
    /// Includes rate limiting between batches to respect API quotas.
    /// </summary>
    /// <param name="tokenized">List of tokenized strings to translate</param>
    /// <param name="cli">CLI options containing translation settings</param>
    /// <param name="apiKey">OpenAI API key for authentication</param>
    /// <returns>List of translated strings in the same order as input</returns>
    static async Task<List<string>> TranslateAllBatchesAsync(List<string> tokenized, CliOptions cli, string apiKey)
    {
        var results = new List<string>(tokenized.Count);
        for (int i = 0; i < tokenized.Count; i += BatchSize)
        {
            var batch = tokenized.Skip(i).Take(BatchSize).ToList();
            var outs = await TranslateBatchAsync(batch, cli, apiKey, attempt: 1);
            results.AddRange(outs);
            await Task.Delay(TimeSpan.FromSeconds(SleepBetweenSeconds));
        }
        return results;
    }

    /// <summary>
    /// Translates a single batch of strings using OpenAI's API with structured output and retry logic.
    /// Implements comprehensive error handling and validation of translation results.
    /// </summary>
    /// <param name="batch">Batch of tokenized strings to translate</param>
    /// <param name="cli">CLI options containing translation settings</param>
    /// <param name="apiKey">OpenAI API key for authentication</param>
    /// <param name="attempt">Current attempt number for retry logic</param>
    /// <returns>List of translated strings matching the input batch size and order</returns>
    /// <exception cref="Exception">Thrown when API calls fail after all retries or when response validation fails</exception>
    static async Task<List<string>> TranslateBatchAsync(List<string> batch, CliOptions cli, string apiKey, int attempt)
    {
        var system = $@"You are a professional localizer for product UIs.
Translate from English into {cli.TargetLanguage} in a {cli.Tone}.
STRICT RULES:
- Translate ONLY the user-visible text.
- Preserve placeholders exactly: {{msg}}, {{ email }}, {{0}}, %s, %d, {{var}}, :name, etc.
- Preserve ALL HTML tags & attributes unchanged (translate only visible text between tags).
- Keep brand/product names as-is (they may appear tokenized).
- Use proper accents and punctuation for the target language.
- Be concise and natural for UI strings.";

        // We pass the batch as a pure JSON array of strings (already tokenized) — NO delimiters.
        var itemsJson = BuildItemsJson(batch);

        var user = $@"You will receive a JSON array of input strings (tokenized). Translate EACH element from English to {cli.TargetLanguage}, following ALL rules above.
Return ONLY a JSON object with this exact shape and count:
{{
  ""translations"": [ ""<translated-1>"", ""<translated-2>"", ... ]   // exactly {batch.Count} items, same order
}}
- Do not add explanations or extra fields.
- If a string should remain unchanged, return it unchanged (NOT '---').";

        // JSON schema to force exact array length
        var schema = new
        {
            type = "object",
            properties = new
            {
                translations = new
                {
                    type = "array",
                    items = new { type = "string" },
                    minItems = batch.Count,
                    maxItems = batch.Count
                }
            },
            required = new[] { "translations" },
            additionalProperties = false
        };

        var payload = new
        {
            model = cli.Model, // e.g. "gpt-4o-mini"
            input = new object[]
            {
            new {
                role = "system",
                content = new object[] {
                    new { type = "input_text", text = system }
                }
            },
            new {
                role = "user",
                content = new object[] {
                    // First block: the JSON array of tokenized strings
                    new { type = "input_text", text = itemsJson },
                    // Second block: the instruction
                    new { type = "input_text", text = user }
                }
            }
            },
            temperature = 0.1,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "batch_translations",
                    schema
                }
            }
            // Optional: max_output_tokens = 4096
        };

        for (; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync("https://api.openai.com/v1/responses", content);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"OpenAI error ({resp.StatusCode}): {body}");

                var jo = JObject.Parse(body);

                // Preferred: structured parsed output
                var parsedToken = jo["output"]?[0]?["content"]?[0]?["parsed"]?["translations"];
                List<string>? list = null;

                if (parsedToken is JArray arr1)
                {
                    list = arr1.ToObject<List<string>>();
                }
                else
                {
                    // Fallback: JSON returned as text
                    string? contentText =
                        jo["output_text"]?.ToString() ??
                        jo["output"]?[0]?["content"]?[0]?["text"]?.ToString() ??
                        jo["choices"]?[0]?["message"]?["content"]?.ToString();

                    if (string.IsNullOrWhiteSpace(contentText))
                        throw new Exception("Could not locate JSON content in Responses payload.");

                    contentText = contentText.Trim().Trim('`'); // strip code fences
                    var parsedObj = JObject.Parse(contentText);
                    list = parsedObj["translations"]?.ToObject<List<string>>();
                }

                if (list == null || list.Count != batch.Count)
                    throw new Exception($"Count mismatch: expected {batch.Count}, got {list?.Count ?? 0}");

                // 🔒 Guard: if any string is empty or just '---', keep original tokenized input
                for (int i = 0; i < list.Count; i++)
                {
                    var s = list[i]?.Trim();
                    if (string.IsNullOrEmpty(s) || s == "---")
                    {
                        list[i] = batch[i]; // keep the tokenized original; will detokenize later
                    }
                }

                return list!;
            }
            catch
            {
                if (attempt == MaxRetries) throw;
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt));
            }
        }

        throw new Exception("Unexpected retry loop exit.");
    }



}
