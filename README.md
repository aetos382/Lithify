# Lithify

A static site generator written in C#. It takes Markdown and AsciiDoc as input and produces HTML.

## What makes it different

### Fragment-grained incremental builds

At the heart of Lithify is a **demand-driven memoized compute graph with early cutoff** (in the same family as Roslyn's `IncrementalValueProvider`, Salsa, and Adapton). A page is not assembled as one big string; it is expressed as a **composition of fragments**.

- The body fragment depends only on the article's source.
- Sidebar fragments (tag listings, monthly archives) depend only on the site-wide indexes.

So adding a new article re-evaluates the sidebar and nothing else — existing article bodies are never re-rendered. On top of that, if a recomputed value's fingerprint matches the previous one, downstream computation is cut off too. And because nothing is written when the output bytes are unchanged, **file modification times don't churn**.

The same machinery powers the development server's on-demand builds. Being demand-driven, it only has to ask for the page you're looking at — there is no need to build the whole site.

### Swappable pieces

Parsers, renderers, template engines, and syntax highlighters are all separate NuGet packages you combine as you like. Every parser emits the same language-neutral AST, so you can **mix Markdown and AsciiDoc within a single site** and still get consistent output.

Template engines include Handlebars and Liquid, plus **Blazor components** rendered statically.

### Site structure in C# code

Instead of learning a configuration dialect, you describe your site with the standard `HostApplicationBuilder`. Building the project gives you a CLI that emits HTML.

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.UseLithify()
    .UseMarkdig()
    .UseAdocNet()
    .UseHtmlRenderer()
    .UseHandlebarsNet("_templates")
    .AddBlog(blog => blog
        .Content("posts/**/*.{md,adoc}")
        .Permalink("/{year}/{month}/{slug}/")
        .WithTags()
        .WithMonthlyArchive())
    .AddStaticFiles("static/**");

return await builder.Build().RunLithifyAsync();
```

## Getting started

```console
$ dotnet new install Lithify.ProjectTemplates
$ dotnet new lithify-blog -n MyBlog
$ cd MyBlog
$ dotnet run -- serve
```

## Documentation

- [Architecture](docs/architecture.md) — the incremental compute graph and fragment composition
- [Setup](docs/setup.md) — setting up a development environment

## Status

**Early development.** The shape of the public API is settled, but the implementation is not finished.

## License

BSD-2-Clause-Patent
