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

## Notes

- The examples show various placeholder formats that will be preserved
- HTML tags in the text will remain unchanged
- Brand terms specified with `--protect` will not be translated
- All JSON structure and formatting is preserved