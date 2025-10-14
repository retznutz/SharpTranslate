# SharpTranslate

A powerful C# command-line tool for translating JSON localization files using OpenAI's API. SharpTranslate intelligently preserves HTML tags, placeholders, and protected brand terms while providing high-quality translations for user-visible text.

## Features

- **Smart Translation**: Uses OpenAI's GPT models for high-quality, contextually appropriate translations
- **Placeholder Preservation**: Automatically protects various placeholder formats:
  - Curly braces: `{msg}`, `{email}`, `{0}`
  - Percent placeholders: `%s`, `%d`
  - Mustache templates: `{{var}}`
  - Colon placeholders: `:name`
- **HTML Tag Protection**: Preserves all HTML tags and attributes while translating visible text
- **Brand Term Protection**: Keeps specified brand names and product terms unchanged
- **JSON Structure Preservation**: Maintains original JSON structure and property ordering
- **Batch Processing**: Efficiently processes translations in configurable batches
- **Retry Logic**: Built-in retry mechanism for API reliability
- **Flexible Configuration**: Customizable tone, model selection, and batch sizes

## Requirements

- .NET 8.0 or later
- OpenAI API key
- Internet connection for API calls

## Installation

### From Source

1. Clone the repository:
   ```bash
   git clone https://github.com/retznutz/SharpTranslate.git
   cd SharpTranslate
   ```

2. Build the project:
   ```bash
   dotnet build --configuration Release
   ```

3. Run the application:
   ```bash
   dotnet run --project SharpTranslate -- [options]
   ```

### Build Executable

```bash
dotnet publish --configuration Release --self-contained true --runtime win-x64
```

## Configuration

Set your OpenAI API key as an environment variable:

### Windows
```cmd
set OPENAI_API_KEY=your_api_key_here
```

### Linux/macOS
```bash
export OPENAI_API_KEY=your_api_key_here
```

## Usage

### Basic Usage

```bash
dotnet run -- --in en.json --out es-ES.json --lang es-ES
```

### Full Command Line Options

```
--in <path>            Input JSON file (English source)
--out <path>           Output JSON file (translated)
--lang <BCP47>         Target language code (e.g., es-ES, fr-FR, de-DE)
--tone <text>          Translation tone/style (default: "Neutral, professional product UI tone")
--model <name>         OpenAI model to use (default: "gpt-4o-mini")
--protect <CSV>        Comma-separated list of protected brand terms
```

### Examples

#### Basic Spanish Translation
```bash
dotnet run -- --in locales/en.json --out locales/es-ES.json --lang es-ES
```

#### French Translation with Brand Protection
```bash
dotnet run -- --in en.json --out fr-FR.json --lang fr-FR --protect "MyBrand,ProductName,CompanyInc"
```

#### Custom Tone and Model
```bash
dotnet run -- --in en.json --out de-DE.json --lang de-DE --tone "Casual, friendly tone" --model "gpt-4"
```

## Input/Output Format

### Input JSON Example
```json
{
  "welcome": "Welcome to {appName}!",
  "greeting": "Hello, {{username}}",
  "buttons": {
    "save": "Save Changes",
    "cancel": "Cancel"
  },
  "messages": [
    "Error: %s occurred",
    "Success! Your changes have been saved."
  ]
}
```

### Output JSON Example (Spanish)
```json
{
  "welcome": "¡Bienvenido a {appName}!",
  "greeting": "Hola, {{username}}",
  "buttons": {
    "save": "Guardar Cambios", 
    "cancel": "Cancelar"
  },
  "messages": [
    "Error: %s ocurrió",
    "¡Éxito! Tus cambios han sido guardados."
  ]
}
```

## Supported Languages

SharpTranslate supports all languages supported by OpenAI's models. Use standard BCP 47 language codes:

- `es-ES` - Spanish (Spain)
- `fr-FR` - French (France)
- `de-DE` - German (Germany)
- `it-IT` - Italian (Italy)
- `pt-BR` - Portuguese (Brazil)
- `ja-JP` - Japanese
- `ko-KR` - Korean
- `zh-CN` - Chinese (Simplified)
- And many more...

## Configuration Options

### Batch Size
Default batch size is 15 strings per API call. This balances API efficiency with token limits.

### Retry Logic
- Maximum retries: 5 attempts
- Exponential backoff: 400ms × attempt number
- Sleep between batches: 0.7 seconds

### Model Selection
Supported OpenAI models:
- `gpt-4o-mini` (default) - Fast and cost-effective
- `gpt-4` - Higher quality for complex translations
- `gpt-3.5-turbo` - Good balance of speed and quality

## Protected Elements

SharpTranslate automatically protects these elements from translation:

1. **HTML Tags**: `<div>`, `<span class="highlight">`, etc.
2. **Placeholders**: 
   - `{variable}`, `{ spaced }`
   - `%s`, `%d`, `%f`
   - `{{mustache}}`
   - `:parameter`
3. **Brand Terms**: Specified via `--protect` parameter
4. **Whitespace**: Preserves original formatting and newlines

## Error Handling

- **Missing API Key**: Clear error message with setup instructions
- **Invalid JSON**: Detailed parsing error information
- **API Failures**: Automatic retry with exponential backoff
- **Count Mismatches**: Validation ensures translation completeness

## Performance

- Processes translations in configurable batches
- Includes rate limiting to respect API quotas
- Optimized JSON structure preservation
- Memory-efficient string processing

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues, feature requests, or questions:
- Create an issue on GitHub
- Check existing documentation
- Review the source code for implementation details

## Acknowledgments

- Built with .NET 8.0
- Uses Newtonsoft.Json for JSON processing
- Powered by OpenAI's translation models