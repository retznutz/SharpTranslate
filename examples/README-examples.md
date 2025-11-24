# SharpTranslate Examples

This directory contains example JSON files to demonstrate SharpTranslate functionality.

## Files

- `en.json` - Basic example with common UI elements
- `complex.json` - More complex example with nested structures and various placeholder types

## Usage Examples

### Translate to Spanish
```bash
dotnet run -- --in examples/en.json --out examples/es-ES.json --lang es-ES
```

### Translate with Brand Protection
```bash
dotnet run -- --in examples/complex.json --out examples/fr-FR.json --lang fr-FR --protect "MyApp"
```

### Custom Tone Example
```bash
dotnet run -- --in examples/en.json --out examples/de-DE.json --lang de-DE --tone "Friendly, casual tone"
```

### Selective Key Updates
Update only specific keys in an existing translation:
```bash
dotnet run -- --in examples/en.json --out examples/es-ES.json --lang es-ES --keys "welcome,nav.home,buttons.save"
```

This will only translate the specified keys (`welcome`, `nav.home`, and `buttons.save`) and leave all other translations unchanged.

## Notes

- The examples show various placeholder formats that will be preserved
- HTML tags in the text will remain unchanged
- Brand terms specified with `--protect` will not be translated
- All JSON structure and formatting is preserved
- Use `--keys` to selectively update only specific keys in an existing translation file