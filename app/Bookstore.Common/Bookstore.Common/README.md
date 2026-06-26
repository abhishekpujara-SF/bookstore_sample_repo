# Bookstore.Common

Common utilities for Bob's Used Bookstore application.

## Features

- String extension methods (ToLowerCase, ToCamelCase, ToPascalCase, Humanize)
- Common constants and utility functions

## Usage

```csharp
using Bookstore.Common;

// String extensions
string text = "hello_world";
string camelCase = text.ToCamelCase();  // "helloWorld"
string pascalCase = text.ToPascalCase(); // "HelloWorld"
string humanized = text.Humanize();      // "Hello world"
```

## Supported Frameworks

- .NET Framework 4.8
- .NET 8.0